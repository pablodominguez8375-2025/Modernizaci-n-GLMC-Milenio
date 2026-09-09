param(
    [Parameter(Mandatory = $true)]
    [string]$BackupPath,
    [switch]$Yes
)

$ErrorActionPreference = 'Stop'
if (-not $Yes) {
    throw 'Restauración cancelada. Vuelva a ejecutar con -Yes para confirmar que reemplazará PostgreSQL y pmgm-documents.'
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$composeFile = Join-Path $repoRoot 'infrastructure/docker-compose.first.yml'
$envFile = if ($env:PMGM_FIRST_ENV_FILE) { $env:PMGM_FIRST_ENV_FILE } else { Join-Path $repoRoot 'infrastructure/.env.first.local' }
$BackupPath = [IO.Path]::GetFullPath($BackupPath)

& (Join-Path $PSScriptRoot 'verify-first-backup.ps1') -BackupPath $BackupPath

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
        throw "docker compose falló con código $LASTEXITCODE: $($Arguments -join ' ')"
    }
}

function Invoke-ComposeCapture {
    param([Parameter(Mandatory = $true)][string[]]$Arguments)
    $output = @(& docker @script:composeArgs @Arguments)
    if ($LASTEXITCODE -ne 0) {
        throw "docker compose falló con código $LASTEXITCODE: $($Arguments -join ' ')"
    }
    return $output
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
if (Test-Path $envFile) { $script:composeArgs += @('--env-file', $envFile) }
$script:composeArgs += @('-f', $composeFile)

$runningBefore = @(& docker @script:composeArgs ps --status running --services 2>$null)
if ($LASTEXITCODE -ne 0) { $runningBefore = @() }
$resume = @('api', 'web') | Where-Object { $runningBefore -contains $_ }

if ($resume.Count -gt 0) {
    Write-Host 'Deteniendo acceso a la aplicación...'
    Invoke-Compose -Arguments (@('stop') + @($resume)) | Out-Null
}

try {
    Write-Host 'Preparando PostgreSQL y MinIO...'
    Invoke-Compose -Arguments @('up', '-d', 'postgres', 'minio') | Out-Null

    $postgresReady = $false
    for ($attempt = 1; $attempt -le 30; $attempt++) {
        & docker @script:composeArgs exec -T postgres pg_isready -U pmgm_app -d pmgm *> $null
        if ($LASTEXITCODE -eq 0) { $postgresReady = $true; break }
        Start-Sleep -Seconds 2
    }
    if (-not $postgresReady) { throw 'PostgreSQL no quedó disponible.' }

    Write-Host 'Restaurando PostgreSQL...'
    $tmpDump = '/tmp/pmgm-first-restore.dump'
    Invoke-Compose -Arguments @('exec', '-T', 'postgres', 'rm', '-f', $tmpDump) | Out-Null
    Invoke-Compose -Arguments @('cp', (Join-Path $BackupPath 'database/pmgm.dump'), "postgres:$tmpDump") | Out-Null
    Invoke-Compose -Arguments @(
        'exec', '-T', 'postgres', 'pg_restore',
        '--clean', '--if-exists', '--no-owner', '--no-privileges', '--exit-on-error',
        '-U', 'pmgm_app', '-d', 'pmgm', $tmpDump
    ) | Out-Null
    Invoke-Compose -Arguments @('exec', '-T', 'postgres', 'rm', '-f', $tmpDump) | Out-Null

    Write-Host 'Restaurando MinIO por API S3...'
    $objectsPath = Join-Path $BackupPath 'objects'
    $minioRestoreScript = 'set -eu; until mc alias set pmgm http://minio:9000 "$MINIO_ACCESS_KEY" "$MINIO_SECRET_KEY" >/dev/null 2>&1; do sleep 2; done; mc mb --ignore-existing pmgm/pmgm-documents >/dev/null; mc anonymous set none pmgm/pmgm-documents >/dev/null; mc mirror --overwrite --remove /backup/pmgm-documents pmgm/pmgm-documents'
    Invoke-Compose -Arguments @(
        'run', '--rm', '-T', '--no-deps',
        '-v', "${objectsPath}:/backup:ro",
        '--entrypoint', '/bin/sh',
        'minio-init', '-c', $minioRestoreScript
    ) | Out-Null

    $localCount = @(Get-ChildItem -LiteralPath (Join-Path $BackupPath 'objects/pmgm-documents') -Recurse -File).Count
    $minioCountScript = 'set -eu; mc alias set pmgm http://minio:9000 "$MINIO_ACCESS_KEY" "$MINIO_SECRET_KEY" >/dev/null; mc find pmgm/pmgm-documents --type f | wc -l'
    $countOutput = Invoke-ComposeCapture -Arguments @(
        'run', '--rm', '-T', '--no-deps',
        '--entrypoint', '/bin/sh',
        'minio-init', '-c', $minioCountScript
    )
    $numericLine = @($countOutput | ForEach-Object { ([string]$_).Trim() } | Where-Object { $_ -match '^\d+$' } | Select-Object -Last 1)
    if ($numericLine.Count -ne 1) { throw "No fue posible determinar el conteo remoto de MinIO: $($countOutput -join ' ')" }
    $remoteCount = [int]$numericLine[0]
    if ($localCount -ne $remoteCount) {
        throw "Conteo MinIO inconsistente tras restaurar: local=$localCount remoto=$remoteCount"
    }
}
catch {
    Write-Error "Falló la restauración. API/web permanecen detenidos para no exponer un estado parcial. $($_.Exception.Message)"
    throw
}

if ($resume.Count -gt 0) {
    Write-Host 'Reanudando servicios previamente activos...'
    Invoke-Compose -Arguments (@('up', '-d') + @($resume)) | Out-Null
}

Write-Host 'RESTAURACIÓN OK' -ForegroundColor Green
Write-Host "Objetos MinIO restaurados: $remoteCount"
Write-Host 'Ejecute la validación funcional final:'
Write-Host '  .\scripts\smoke-first-implementation.ps1'
