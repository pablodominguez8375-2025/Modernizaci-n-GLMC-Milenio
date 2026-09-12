param(
    [string]$OutputPath = ''
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$composeFile = Join-Path $repoRoot 'infrastructure/docker-compose.first.yml'
$envFile = if ($env:PMGM_FIRST_ENV_FILE) { $env:PMGM_FIRST_ENV_FILE } else { Join-Path $repoRoot 'infrastructure/.env.first.local' }

function Import-PmgmEnv([string]$Path) {
    if (-not (Test-Path $Path)) { return }
    Get-Content $Path | ForEach-Object {
        if ($_ -match '^([^#=]+)=(.*)$') {
            [Environment]::SetEnvironmentVariable($matches[1].Trim(), $matches[2], 'Process')
        }
    }
}

function Invoke-Compose {
    param([Parameter(Mandatory = $true)][string[]]$Arguments)
    & docker @script:composeArgs @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "docker compose falló con código ${LASTEXITCODE}: $($Arguments -join ' ')"
    }
}

Import-PmgmEnv $envFile
$required = @(
    'PMGM_FIRST_DB_PASSWORD',
    'PMGM_FIRST_MINIO_USER',
    'PMGM_FIRST_MINIO_PASSWORD',
    'PMGM_FIRST_KEYCLOAK_ADMIN_PASSWORD',
    'PMGM_QA_USER_PASSWORD'
)
foreach ($name in $required) {
    if ([string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($name, 'Process'))) {
        throw "$name es obligatorio. Ejecute primero .\scripts\start-first-implementation.ps1 o defina el entorno."
    }
}

& docker compose version *> $null
if ($LASTEXITCODE -ne 0) { throw 'Docker Compose es obligatorio.' }

$script:composeArgs = @('compose')
if (Test-Path $envFile) {
    $script:composeArgs += @('--env-file', $envFile)
}
$script:composeArgs += @('-f', $composeFile)

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $stamp = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssZ')
    $OutputPath = Join-Path $repoRoot "backups/pmgm-first-$stamp"
}
$OutputPath = [IO.Path]::GetFullPath($OutputPath)
if (Test-Path $OutputPath) { throw "La ruta de respaldo ya existe: $OutputPath" }

$running = @(& docker @script:composeArgs ps --status running --services 2>$null)
if ($LASTEXITCODE -ne 0) { $running = @() }
foreach ($service in @('postgres', 'minio')) {
    if ($running -notcontains $service) {
        throw "El servicio $service no está en ejecución. Inicie primero la primera implementación."
    }
}
$resume = @('api', 'web') | Where-Object { $running -contains $_ }

New-Item -ItemType Directory -Path (Join-Path $OutputPath 'database') -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $OutputPath 'objects/pmgm-documents') -Force | Out-Null

try {
    if ($resume.Count -gt 0) {
        Write-Host 'Pausando escrituras (api/web)...'
        Invoke-Compose -Arguments (@('stop') + @($resume)) | Out-Null
    }

    Write-Host 'Respaldando PostgreSQL...'
    $tmpDump = '/tmp/pmgm-first-backup.dump'
    Invoke-Compose -Arguments @('exec', '-T', 'postgres', 'rm', '-f', $tmpDump) | Out-Null
    Invoke-Compose -Arguments @(
        'exec', '-T', 'postgres', 'pg_dump',
        '-U', 'pmgm_app', '-d', 'pmgm',
        '--format=custom', '--no-owner', '--no-privileges', "--file=$tmpDump"
    ) | Out-Null
    Invoke-Compose -Arguments @('cp', "postgres:$tmpDump", (Join-Path $OutputPath 'database/pmgm.dump')) | Out-Null
    Invoke-Compose -Arguments @('exec', '-T', 'postgres', 'rm', '-f', $tmpDump) | Out-Null

    Write-Host 'Respaldando documentos MinIO por API S3...'
    $objectsPath = Join-Path $OutputPath 'objects'
    $minioBackupScript = 'set -eu; mc alias set pmgm http://minio:9000 "$MINIO_ACCESS_KEY" "$MINIO_SECRET_KEY" >/dev/null; mkdir -p /backup/pmgm-documents; mc mirror --overwrite pmgm/pmgm-documents /backup/pmgm-documents'
    Invoke-Compose -Arguments @(
        'run', '--rm', '-T', '--no-deps',
        '-v', "${objectsPath}:/backup",
        '--entrypoint', '/bin/sh',
        'minio-init', '-c', $minioBackupScript
    ) | Out-Null

    $sourceCommit = 'unknown'
    try {
        $candidate = (& git -C $repoRoot rev-parse HEAD 2>$null | Select-Object -First 1).Trim()
        if ($candidate) { $sourceCommit = $candidate }
    } catch { }

    $files = @()
    $totalBytes = [int64]0
    Get-ChildItem -Path $OutputPath -Recurse -File | Where-Object { $_.Name -ne 'manifest.json' } | Sort-Object FullName | ForEach-Object {
        $relative = [IO.Path]::GetRelativePath($OutputPath, $_.FullName).Replace('\', '/')
        $hash = (Get-FileHash -Algorithm SHA256 -Path $_.FullName).Hash.ToLowerInvariant()
        $size = [int64]$_.Length
        $files += [ordered]@{ path = $relative; sha256 = $hash; sizeBytes = $size }
        $totalBytes += $size
    }
    if (-not ($files | Where-Object { $_.path -eq 'database/pmgm.dump' })) {
        throw 'El dump PostgreSQL no fue generado.'
    }

    $manifest = [ordered]@{
        formatVersion = 1
        project = 'PMGM'
        stack = 'first-implementation'
        createdAtUtc = [DateTimeOffset]::UtcNow.ToString('o')
        sourceCommit = $sourceCommit
        components = [ordered]@{
            postgres = [ordered]@{ database = 'pmgm'; format = 'pg_dump-custom' }
            minio = [ordered]@{ bucket = 'pmgm-documents'; format = 's3-object-mirror' }
        }
        fileCount = $files.Count
        totalBytes = $totalBytes
        files = $files
    }
    $manifest | ConvertTo-Json -Depth 8 | Set-Content -Path (Join-Path $OutputPath 'manifest.json') -Encoding utf8
}
finally {
    if ($resume.Count -gt 0) {
        Write-Host 'Reanudando servicios...'
        try { Invoke-Compose -Arguments (@('up', '-d') + @($resume)) | Out-Null } catch { Write-Warning $_ }
    }
}

Write-Host "Respaldo creado: $OutputPath" -ForegroundColor Green
Write-Host "Verifique antes de restaurar con: .\scripts\verify-first-backup.ps1 -BackupPath `"$OutputPath`""
