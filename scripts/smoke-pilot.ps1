param()

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$envFile = if ($env:PMGM_PILOT_ENV_FILE) { $env:PMGM_PILOT_ENV_FILE } else { Join-Path $repoRoot 'infrastructure/.env.pilot.local' }

function Import-PmgmEnv([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { throw "No existe $Path" }
    Get-Content -LiteralPath $Path | ForEach-Object {
        if ($_ -match '^([^#=]+)=(.*)$') {
            [Environment]::SetEnvironmentVariable($matches[1].Trim(), $matches[2], 'Process')
        }
    }
}

Import-PmgmEnv $envFile
if ([string]::IsNullOrWhiteSpace($env:PMGM_PILOT_PUBLIC_URL)) { throw 'PMGM_PILOT_PUBLIC_URL es obligatorio.' }

$skipCert = $env:PMGM_PILOT_INSECURE_TLS -eq 'true'

function Invoke-PilotWeb([string]$Uri, [string]$Method = 'Get', $Body = $null, [string]$ContentType = '') {
    $params = @{ Uri = $Uri; Method = $Method; TimeoutSec = 15; UseBasicParsing = $true }
    if ($skipCert) { $params.SkipCertificateCheck = $true }
    if ($null -ne $Body) { $params.Body = $Body }
    if ($ContentType) { $params.ContentType = $ContentType }
    return Invoke-WebRequest @params
}

function Wait-PilotUrl([string]$Uri, [string]$Name, [int]$Attempts = 120) {
    for ($i = 1; $i -le $Attempts; $i++) {
        try {
            $response = Invoke-PilotWeb $Uri
            if ([int]$response.StatusCode -ge 200 -and [int]$response.StatusCode -lt 400) {
                Write-Host "✓ $Name" -ForegroundColor Green
                return
            }
        } catch { }
        Start-Sleep -Seconds 2
    }
    throw "$Name no respondió: $Uri"
}

$base = $env:PMGM_PILOT_PUBLIC_URL
Wait-PilotUrl "$base/identity/realms/pmgm/.well-known/openid-configuration" 'Keycloak discovery HTTPS'
Wait-PilotUrl "$base/health/live" 'API live'
Wait-PilotUrl "$base/health/ready" 'API ready/PostgreSQL'
Wait-PilotUrl "$base/health/web" 'frontend Nginx'
Wait-PilotUrl "$base/" 'frontend HTTPS'

$callback = Invoke-PilotWeb "$base/auth/callback"
if ([int]$callback.StatusCode -ne 200) { throw "El callback SPA devolvió HTTP $($callback.StatusCode)." }
Write-Host '✓ separación /auth (SPA) y /identity (Keycloak)' -ForegroundColor Green

$discovery = (Invoke-PilotWeb "$base/identity/realms/pmgm/.well-known/openid-configuration").Content | ConvertFrom-Json
$expectedIssuer = "$base/identity/realms/pmgm"
if ($discovery.issuer -ne $expectedIssuer) { throw "Issuer inesperado: $($discovery.issuer) != $expectedIssuer" }
Write-Host '✓ issuer OIDC público consistente' -ForegroundColor Green

$systemInfo = (Invoke-PilotWeb "$base/api/system/info").Content | ConvertFrom-Json
if ($systemInfo.version -ne '0.32.0') { throw "Versión API inesperada: $($systemInfo.version)" }
Write-Host '✓ API v0.32.0 detrás de HTTPS' -ForegroundColor Green

$sessionStatus = 0
try {
    $response = Invoke-PilotWeb "$base/api/session/me"
    $sessionStatus = [int]$response.StatusCode
} catch {
    if ($_.Exception.Response) { $sessionStatus = [int]$_.Exception.Response.StatusCode }
    else { throw }
}
if ($sessionStatus -ne 401) { throw "Se esperaba 401 sin token en /api/session/me y se obtuvo $sessionStatus." }
Write-Host '✓ endpoint protegido exige autenticación' -ForegroundColor Green

$tokenBody = @{
    client_id = 'pmgm-web'
    grant_type = 'password'
    username = $env:PMGM_PILOT_ADMIN_USERNAME
    password = $env:PMGM_PILOT_ADMIN_PASSWORD
}
$tokenStatus = 0
try {
    $response = Invoke-PilotWeb "$base/identity/realms/pmgm/protocol/openid-connect/token" 'Post' $tokenBody 'application/x-www-form-urlencoded'
    $tokenStatus = [int]$response.StatusCode
} catch {
    if ($_.Exception.Response) { $tokenStatus = [int]$_.Exception.Response.StatusCode }
    else { throw }
}
if ($tokenStatus -eq 200) { throw 'FALLO DE SEGURIDAD: pmgm-web aceptó password grant.' }
Write-Host "✓ password grant deshabilitado para pmgm-web (HTTP $tokenStatus)" -ForegroundColor Green

Write-Host ''
Write-Host 'SMOKE PILOTO OK' -ForegroundColor Green
