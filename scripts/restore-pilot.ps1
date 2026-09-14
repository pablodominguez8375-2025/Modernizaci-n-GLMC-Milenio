param(
    [Parameter(Mandatory = $true)][string]$BackupPath,
    [switch]$Yes
)

$ErrorActionPreference = 'Stop'
if (-not $Yes) { throw 'Restauración cancelada. Vuelva a ejecutar con -Yes.' }
$repoRoot = Split-Path -Parent $PSScriptRoot
$composeFile = Join-Path $repoRoot 'infrastructure/docker-compose.pilot.yml'
$envFile = if ($env:PMGM_PILOT_ENV_FILE) { $env:PMGM_PILOT_ENV_FILE } else { Join-Path $repoRoot 'infrastructure/.env.pilot.local' }
$BackupPath = [IO.Path]::GetFullPath($BackupPath)

& (Join-Path $PSScriptRoot 'verify-pilot-backup.ps1') -BackupPath $BackupPath

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
function Invoke-ComposeCapture([string[]]$Arguments) {
    $out = @(& docker @script:composeArgs @Arguments)
    if ($LASTEXITCODE -ne 0) { throw "docker compose falló con código ${LASTEXITCODE}: $($Arguments -join ' ')" }
    return $out
}

Import-PmgmEnv $envFile
& docker compose version *> $null
if ($LASTEXITCODE -ne 0) { throw 'Docker Compose es obligatorio.' }
$script:composeArgs = @('compose','--env-file',$envFile,'-f',$composeFile)
$running = @(& docker @script:composeArgs ps --status running --services 2>$null)
$resume = @('gateway','web','api','keycloak') | Where-Object { $running -contains $_ }

if ($resume.Count -gt 0) {
    Write-Host 'Deteniendo acceso al piloto...'
    Invoke-Compose (@('stop') + @($resume)) | Out-Null
}

try {
    Invoke-Compose @('up','-d','postgres','minio') | Out-Null
    $ready = $false
    for ($i=1; $i -le 30; $i++) {
        & docker @script:composeArgs exec -T postgres pg_isready -U pmgm_app -d pmgm *> $null
        if ($LASTEXITCODE -eq 0) { $ready=$true; break }
        Start-Sleep -Seconds 2
    }
    if (-not $ready) { throw 'PostgreSQL no quedó disponible.' }

    foreach ($database in @('pmgm','pmgm_keycloak')) {
        Write-Host "Restaurando PostgreSQL: $database..."
        $tmp = "/tmp/${database}-pilot-restore.dump"
        Invoke-Compose @('exec','-T','postgres','rm','-f',$tmp) | Out-Null
        Invoke-Compose @('cp',(Join-Path $BackupPath "database/$database.dump"),"postgres:$tmp") | Out-Null
        Invoke-Compose @('exec','-T','postgres','pg_restore','--clean','--if-exists','--no-owner','--no-privileges','--exit-on-error','-U','pmgm_app','-d',$database,$tmp) | Out-Null
        Invoke-Compose @('exec','-T','postgres','rm','-f',$tmp) | Out-Null
    }

    Write-Host 'Restaurando MinIO...'
    $objectsPath = Join-Path $BackupPath 'objects'
    $restoreScript = 'set -eu; until mc alias set pmgm http://minio:9000 "$MINIO_ACCESS_KEY" "$MINIO_SECRET_KEY" >/dev/null 2>&1; do sleep 2; done; mc mb --ignore-existing pmgm/pmgm-documents >/dev/null; mc anonymous set none pmgm/pmgm-documents >/dev/null; mc mirror --overwrite --remove /backup/pmgm-documents pmgm/pmgm-documents'
    Invoke-Compose @('run','--rm','-T','--no-deps','-v',"${objectsPath}:/backup:ro",'--entrypoint','/bin/sh','minio-init','-c',$restoreScript) | Out-Null

    $localCount = @(Get-ChildItem -LiteralPath (Join-Path $BackupPath 'objects/pmgm-documents') -Recurse -File).Count
    $countScript = 'set -eu; mc alias set pmgm http://minio:9000 "$MINIO_ACCESS_KEY" "$MINIO_SECRET_KEY" >/dev/null; mc ls --recursive pmgm/pmgm-documents | wc -l'
    $countOutput = Invoke-ComposeCapture @('run','--rm','-T','--no-deps','--entrypoint','/bin/sh','minio-init','-c',$countScript)
    $numeric = @($countOutput | ForEach-Object { ([string]$_).Trim() } | Where-Object { $_ -match '^\d+$' } | Select-Object -Last 1)
    if ($numeric.Count -ne 1) { throw 'No fue posible determinar el conteo remoto de MinIO.' }
    $remoteCount = [int]$numeric[0]
    if ($localCount -ne $remoteCount) { throw "Conteo MinIO inconsistente: local=$localCount remoto=$remoteCount" }
}
catch {
    Write-Error "Falló la restauración del piloto. gateway/web/api/keycloak permanecen detenidos. $($_.Exception.Message)"
    throw
}

if ($resume.Count -gt 0) {
    Write-Host 'Reanudando servicios...'
    Invoke-Compose (@('up','-d') + @($resume)) | Out-Null
}
Write-Host 'RESTAURACIÓN PILOTO OK' -ForegroundColor Green
Write-Host "Objetos MinIO restaurados: $remoteCount"
Write-Host 'Ejecute .\scripts\smoke-pilot.ps1 para validación funcional.'
