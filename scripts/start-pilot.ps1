param()

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$envFile = if ($env:PMGM_PILOT_ENV_FILE) { $env:PMGM_PILOT_ENV_FILE } else { Join-Path $repoRoot 'infrastructure/.env.pilot.local' }
$composeFile = Join-Path $repoRoot 'infrastructure/docker-compose.pilot.yml'

& (Join-Path $PSScriptRoot 'preflight-pilot.ps1')

Get-Content -LiteralPath $envFile | ForEach-Object {
    if ($_ -match '^([^#=]+)=(.*)$') {
        [Environment]::SetEnvironmentVariable($matches[1].Trim(), $matches[2], 'Process')
    }
}

Write-Host 'Levantando Proyecto Milenio — piloto operacional...'
& docker compose --env-file $envFile -f $composeFile up -d --build
if ($LASTEXITCODE -ne 0) { throw "No fue posible levantar el stack piloto (código $LASTEXITCODE)." }

try {
    & (Join-Path $PSScriptRoot 'smoke-pilot.ps1')
} catch {
    Write-Warning 'El stack arrancó, pero el smoke del piloto falló.'
    Write-Host "Revise: docker compose --env-file `"$envFile`" -f `"$composeFile`" logs --tail=300"
    throw
}

Write-Host ''
Write-Host "PILOTO OPERACIONAL DISPONIBLE: $($env:PMGM_PILOT_PUBLIC_URL)" -ForegroundColor Green
Write-Host 'El primer usuario institucional debe cambiar su contraseña temporal al iniciar sesión.'
