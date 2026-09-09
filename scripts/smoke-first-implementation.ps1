param(
    [string]$BaseUrl = 'http://127.0.0.1:8081',
    [string]$OidcUrl = 'http://127.0.0.1:8180/realms/pmgm'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$envFile = Join-Path $repoRoot 'infrastructure/.env.first.local'
$lodge23 = '23232323-2323-2323-2323-232323232323'

if (-not (Test-Path $envFile)) {
    throw "No existe $envFile. Ejecute primero .\scripts\start-first-implementation.ps1"
}

Get-Content $envFile | ForEach-Object {
    if ($_ -match '^([^#=]+)=(.*)$') {
        [Environment]::SetEnvironmentVariable($matches[1].Trim(), $matches[2], 'Process')
    }
}
if ([string]::IsNullOrWhiteSpace($env:PMGM_QA_USER_PASSWORD)) {
    throw 'PMGM_QA_USER_PASSWORD no está definido.'
}

function Wait-Endpoint([string]$Url, [string]$Name, [int]$Attempts = 120) {
    for ($i = 1; $i -le $Attempts; $i++) {
        try {
            $response = Invoke-WebRequest -UseBasicParsing -Uri $Url -TimeoutSec 5
            if ($response.StatusCode -ge 200 -and $response.StatusCode -lt 400) {
                Write-Host "✓ $Name" -ForegroundColor Green
                return
            }
        } catch { }
        Start-Sleep -Seconds 2
    }
    throw "$Name no respondió: $Url"
}

function Get-QaToken([string]$Username) {
    $body = @{
        client_id = 'pmgm-web'
        grant_type = 'password'
        username = $Username
        password = $env:PMGM_QA_USER_PASSWORD
    }
    $response = Invoke-RestMethod -Method Post -Uri "$OidcUrl/protocol/openid-connect/token" -ContentType 'application/x-www-form-urlencoded' -Body $body -TimeoutSec 15
    if ([string]::IsNullOrWhiteSpace($response.access_token)) { throw "Keycloak no devolvió access_token para $Username" }
    return $response.access_token
}

function Get-Json([string]$Token, [string]$Path, [string]$Name) {
    $headers = @{ Authorization = "Bearer $Token"; Accept = 'application/json' }
    $result = Invoke-RestMethod -Method Get -Uri "$BaseUrl$Path" -Headers $headers -TimeoutSec 20
    Write-Host "✓ $Name" -ForegroundColor Green
    return $result
}

function Assert-TransferredDegree($Payload, [string]$Actor) {
    $row = $Payload.items | Where-Object { $_.institutionalNumber -eq 'GLM-QA-0230' } | Select-Object -First 1
    if (-not $row) { throw 'No se encontró al miembro trasladado GLM-QA-0230 en Taller 23.' }
    if ($row.membershipStatus -ne 'active') { throw "Membresía trasladada no está activa: $($row.membershipStatus)" }
    if ($row.currentDegree -ne 'master') { throw "El grado maestro no acompañó el traslado: $($row.currentDegree)" }
    Write-Host "✓ grado maestro preservado tras traslado ($Actor)" -ForegroundColor Green
}

Wait-Endpoint "$OidcUrl/.well-known/openid-configuration" 'Keycloak discovery'
Wait-Endpoint "$BaseUrl/health/live" 'API live'
Wait-Endpoint "$BaseUrl/health/ready" 'API ready/PostgreSQL'
Wait-Endpoint "$BaseUrl/health/web" 'frontend Nginx'

$adminToken = Get-QaToken 'qa.admin'
$workshopToken = Get-QaToken 'qa.taller23'
Write-Host '✓ autenticación QA mediante Keycloak' -ForegroundColor Green

$null = Get-Json $adminToken '/api/session/me' 'sesión institucional de Gran Logia'
$adminMembers = Get-Json $adminToken "/api/members?organizationId=$lodge23&limit=20" 'Membresía / Ficha de Taller'
Assert-TransferredDegree $adminMembers 'Gran Logia'
$null = Get-Json $adminToken '/api/candidate-publications/active' 'Portal de insinuados'
# Usar la ruta canónica evita que el smoke dependa del seguimiento automático de 307.
$null = Get-Json $adminToken '/api/biblioteca/' 'Biblioteca Virtual'
$null = Get-Json $adminToken '/api/grand-archive/?status=active&limit=20' 'Gran Archivero'

$headers = @{ Authorization = "Bearer $workshopToken"; Accept = 'application/json' }
$statusCode = 0
try {
    $response = Invoke-WebRequest -UseBasicParsing -Method Get -Uri "$BaseUrl/api/grand-archive/?status=active&limit=20" -Headers $headers -TimeoutSec 20
    $statusCode = [int]$response.StatusCode
} catch {
    if ($_.Exception.Response) { $statusCode = [int]$_.Exception.Response.StatusCode }
    else { throw }
}
if ($statusCode -ne 403) { throw "Se esperaba 403 para Gran Archivero desde Taller y se obtuvo $statusCode." }
Write-Host '✓ segregación de permisos: Taller no accede a Gran Archivero' -ForegroundColor Green

$workshopSession = Get-Json $workshopToken '/api/session/me' 'sesión institucional de Taller'
if ($workshopSession.accessScope -ne 'organization') { throw "Scope inesperado para Taller: $($workshopSession.accessScope)" }
if ($workshopSession.capabilities.canManageLodgeOperations -ne $true) { throw 'El perfil Taller no obtuvo capacidad de Gestión Logial.' }
Write-Host '✓ perfil Taller limitado a organization con Gestión Logial' -ForegroundColor Green

$workshopMembers = Get-Json $workshopToken "/api/members?organizationId=$lodge23&limit=20" 'Membresía desde Taller destino'
Assert-TransferredDegree $workshopMembers 'Taller destino'

Write-Host ''
Write-Host 'SMOKE FIRST IMPLEMENTATION OK' -ForegroundColor Green
