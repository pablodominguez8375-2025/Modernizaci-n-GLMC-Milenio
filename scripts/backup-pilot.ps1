param(
    [string]$OutputPath = ''
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$composeFile = Join-Path $repoRoot 'infrastructure/docker-compose.pilot.yml'
$envFile = if ($env:PMGM_PILOT_ENV_FILE) { $env:PMGM_PILOT_ENV_FILE } else { Join-Path $repoRoot 'infrastructure/.env.pilot.local' }

function Import-PmgmEnv([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { throw "No existe $Path" }
    Get-Content -LiteralPath $Path | ForEach-Object {
        if ($_ -match '^([^#=]+)=(.*)$') { [Environment]::SetEnvironmentVariable($matches[1].Trim(), $matches[2], 'Process') }
    }
}
function Invoke-Compose([string[]]$Arguments) {
    & docker @script:composeArgs @Arguments
    if ($LASTEXITCODE -ne 0) { throw "docker compose falló con código ${LASTEXITCODE}: $($Arguments -join ' ')" }
}

Import-PmgmEnv $envFile
& docker compose version *> $null
if ($LASTEXITCODE -ne 0) { throw 'Docker Compose es obligatorio.' }
$script:composeArgs = @('compose','--env-file',$envFile,'-f',$composeFile)

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $repoRoot ("backups/pmgm-pilot-" + [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssZ'))
}
$OutputPath = [IO.Path]::GetFullPath($OutputPath)
if (Test-Path -LiteralPath $OutputPath) { throw "La ruta ya existe: $OutputPath" }

$running = @(& docker @script:composeArgs ps --status running --services 2>$null)
foreach ($required in @('postgres','minio')) {
    if ($running -notcontains $required) { throw "$required no está en ejecución." }
}
$resume = @('gateway','web','api','keycloak') | Where-Object { $running -contains $_ }
New-Item -ItemType Directory -Path (Join-Path $OutputPath 'database') -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $OutputPath 'objects/pmgm-documents') -Force | Out-Null

try {
    if ($resume.Count -gt 0) {
        Write-Host 'Pausando acceso y escrituras (gateway/web/api/keycloak)...'
        Invoke-Compose (@('stop') + @($resume)) | Out-Null
    }

    foreach ($database in @('pmgm','pmgm_keycloak')) {
        Write-Host "Respaldando PostgreSQL: $database..."
        $tmp = "/tmp/${database}-pilot-backup.dump"
        Invoke-Compose @('exec','-T','postgres','rm','-f',$tmp) | Out-Null
        Invoke-Compose @('exec','-T','postgres','pg_dump','-U','pmgm_app','-d',$database,'--format=custom','--no-owner','--no-privileges',"--file=$tmp") | Out-Null
        Invoke-Compose @('cp',"postgres:$tmp",(Join-Path $OutputPath "database/$database.dump")) | Out-Null
        Invoke-Compose @('exec','-T','postgres','rm','-f',$tmp) | Out-Null
    }

    Write-Host 'Respaldando MinIO pmgm-documents...'
    $objectsPath = Join-Path $OutputPath 'objects'
    $minioScript = 'set -eu; mc alias set pmgm http://minio:9000 "$MINIO_ACCESS_KEY" "$MINIO_SECRET_KEY" >/dev/null; mkdir -p /backup/pmgm-documents; mc mirror --overwrite pmgm/pmgm-documents /backup/pmgm-documents'
    Invoke-Compose @('run','--rm','-T','--no-deps','-v',"${objectsPath}:/backup",'--entrypoint','/bin/sh','minio-init','-c',$minioScript) | Out-Null

    $sourceCommit = 'unknown'
    try {
        $candidate = (& git -C $repoRoot rev-parse HEAD 2>$null | Select-Object -First 1).Trim()
        if ($candidate) { $sourceCommit = $candidate }
    } catch { }

    $files = @(); $total = [int64]0
    Get-ChildItem -LiteralPath $OutputPath -Recurse -File | Where-Object Name -ne 'manifest.json' | Sort-Object FullName | ForEach-Object {
        $relative = [IO.Path]::GetRelativePath($OutputPath,$_.FullName).Replace('\','/')
        $size = [int64]$_.Length
        $files += [ordered]@{ path=$relative; sha256=(Get-FileHash -Algorithm SHA256 -LiteralPath $_.FullName).Hash.ToLowerInvariant(); sizeBytes=$size }
        $total += $size
    }
    foreach ($required in @('database/pmgm.dump','database/pmgm_keycloak.dump')) {
        if (-not ($files | Where-Object path -eq $required)) { throw "Falta $required" }
    }
    $manifest = [ordered]@{
        formatVersion=1; project='PMGM'; stack='pilot-operational'; createdAtUtc=[DateTimeOffset]::UtcNow.ToString('o'); sourceCommit=$sourceCommit
        components=[ordered]@{ postgres=[ordered]@{ databases=@('pmgm','pmgm_keycloak'); format='pg_dump-custom' }; minio=[ordered]@{ bucket='pmgm-documents'; format='s3-object-mirror' } }
        fileCount=$files.Count; totalBytes=$total; files=$files
    }
    $manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $OutputPath 'manifest.json') -Encoding utf8
    & (Join-Path $PSScriptRoot 'verify-pilot-backup.ps1') -BackupPath $OutputPath
}
finally {
    if ($resume.Count -gt 0) {
        Write-Host 'Reanudando servicios...'
        try { Invoke-Compose (@('up','-d') + @($resume)) | Out-Null } catch { Write-Warning $_ }
    }
}

Write-Host "RESPALDO PILOTO CREADO: $OutputPath" -ForegroundColor Green
