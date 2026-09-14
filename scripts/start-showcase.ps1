param(
    [int]$Port = 8082,
    [switch]$NoBrowser
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
Push-Location $repoRoot
try {
    $env:PMGM_SHOWCASE_HTTP_PORT = [string]$Port
    Write-Host "Construyendo Proyecto Milenio Showcase..." -ForegroundColor Cyan
    docker compose -f infrastructure/docker-compose.showcase.yml up -d --build
    if ($LASTEXITCODE -ne 0) { throw "Docker Compose terminó con código $LASTEXITCODE." }

    $url = "http://127.0.0.1:$Port"
    Write-Host "Showcase disponible en $url" -ForegroundColor Green
    Write-Host "Salud: $url/health/web"
    if (-not $NoBrowser) { Start-Process $url }
}
finally {
    Pop-Location
}
