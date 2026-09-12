param()

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$composeFile = Join-Path $repoRoot 'infrastructure/docker-compose.pilot.yml'
$realmFile = Join-Path $repoRoot 'infrastructure/keycloak/pmgm-pilot-realm.json'
$envFile = if ($env:PMGM_PILOT_ENV_FILE) { $env:PMGM_PILOT_ENV_FILE } else { Join-Path $repoRoot 'infrastructure/.env.pilot.local' }

function Import-PmgmEnv([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) {
        throw "No existe $Path. Copie infrastructure/pilot.env.example a infrastructure/.env.pilot.local y complete sus valores."
    }
    Get-Content -LiteralPath $Path | ForEach-Object {
        if ($_ -match '^([^#=]+)=(.*)$') {
            [Environment]::SetEnvironmentVariable($matches[1].Trim(), $matches[2], 'Process')
        }
    }
}

Import-PmgmEnv $envFile

& docker compose version *> $null
if ($LASTEXITCODE -ne 0) { throw 'Docker Compose es obligatorio.' }

$required = @(
    'PMGM_PILOT_HOSTNAME', 'PMGM_PILOT_PUBLIC_URL',
    'PMGM_PILOT_DB_PASSWORD', 'PMGM_PILOT_MINIO_USER', 'PMGM_PILOT_MINIO_PASSWORD',
    'PMGM_PILOT_KEYCLOAK_ADMIN_USERNAME', 'PMGM_PILOT_KEYCLOAK_ADMIN_PASSWORD',
    'PMGM_PILOT_ADMIN_USERNAME', 'PMGM_PILOT_ADMIN_EMAIL', 'PMGM_PILOT_ADMIN_PASSWORD'
)
foreach ($name in $required) {
    if ([string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($name, 'Process'))) {
        throw "$name es obligatorio."
    }
}

$hostname = $env:PMGM_PILOT_HOSTNAME.Trim().ToLowerInvariant()
try { $publicUri = [Uri]$env:PMGM_PILOT_PUBLIC_URL.Trim() } catch { throw 'PMGM_PILOT_PUBLIC_URL no es una URL válida.' }
if ($publicUri.Scheme -ne 'https') { throw 'PMGM_PILOT_PUBLIC_URL debe usar https://.' }
if ($publicUri.AbsolutePath -ne '/' -or $publicUri.Query -or $publicUri.Fragment) {
    throw 'PMGM_PILOT_PUBLIC_URL debe ser sólo el origen HTTPS, sin ruta, query ni fragmento.'
}
if ($env:PMGM_PILOT_PUBLIC_URL.EndsWith('/')) { throw "PMGM_PILOT_PUBLIC_URL no debe terminar en '/'." }
if ($publicUri.Host.ToLowerInvariant() -ne $hostname) { throw 'PMGM_PILOT_HOSTNAME debe coincidir con PMGM_PILOT_PUBLIC_URL.' }

$allowLocal = $env:PMGM_PILOT_ALLOW_LOCALHOST -eq 'true'
$insecureTls = $env:PMGM_PILOT_INSECURE_TLS -eq 'true'
if (@('localhost', '127.0.0.1', '::1') -contains $hostname) {
    if (-not $allowLocal) { throw 'localhost sólo está permitido con PMGM_PILOT_ALLOW_LOCALHOST=true para pruebas controladas.' }
}
if ($insecureTls -and -not $allowLocal) { throw 'PMGM_PILOT_INSECURE_TLS=true sólo se admite con PMGM_PILOT_ALLOW_LOCALHOST=true.' }

$httpsPort = if ($env:PMGM_PILOT_HTTPS_PORT) { [int]$env:PMGM_PILOT_HTTPS_PORT } else { 443 }
$httpPort = if ($env:PMGM_PILOT_HTTP_PORT) { [int]$env:PMGM_PILOT_HTTP_PORT } else { 80 }
if ($httpsPort -lt 1 -or $httpsPort -gt 65535 -or $httpPort -lt 1 -or $httpPort -gt 65535) {
    throw 'Los puertos del piloto deben estar entre 1 y 65535.'
}
$urlPort = if ($publicUri.IsDefaultPort) { 443 } else { $publicUri.Port }
if ($urlPort -ne $httpsPort) { throw "El puerto HTTPS de PMGM_PILOT_PUBLIC_URL ($urlPort) no coincide con PMGM_PILOT_HTTPS_PORT ($httpsPort)." }

$placeholders = @('reemplazar', 'change-me', 'cambiar', 'example.cl')
Get-ChildItem Env:PMGM_PILOT_* | ForEach-Object {
    $value = [string]$_.Value
    foreach ($token in $placeholders) {
        if ($value.ToLowerInvariant().Contains($token)) { throw "$($_.Name) todavía contiene un valor de ejemplo/place-holder." }
    }
}

$secretNames = @(
    'PMGM_PILOT_DB_PASSWORD',
    'PMGM_PILOT_MINIO_PASSWORD',
    'PMGM_PILOT_KEYCLOAK_ADMIN_PASSWORD',
    'PMGM_PILOT_ADMIN_PASSWORD'
)
$secretValues = @()
foreach ($name in $secretNames) {
    $value = [Environment]::GetEnvironmentVariable($name, 'Process')
    if ($value.Length -lt 20) { throw "$name debe tener al menos 20 caracteres." }
    $secretValues += $value
}
if (($secretValues | Sort-Object -Unique).Count -ne $secretValues.Count) { throw 'Las contraseñas del piloto deben ser diferentes entre sí.' }

foreach ($name in @('PMGM_PILOT_KEYCLOAK_ADMIN_USERNAME', 'PMGM_PILOT_ADMIN_USERNAME')) {
    $username = [Environment]::GetEnvironmentVariable($name, 'Process').ToLowerInvariant()
    if ($username.StartsWith('qa.') -or $username.StartsWith('qa-')) { throw "$name no puede usar una identidad QA en el piloto." }
}
if ($env:PMGM_PILOT_ADMIN_EMAIL -notmatch '^[^@\s]+@[^@\s]+\.[^@\s]+$') { throw 'PMGM_PILOT_ADMIN_EMAIL no tiene formato válido.' }
if ($env:PMGM_PILOT_ADMIN_EMAIL.ToLowerInvariant().EndsWith('.invalid')) { throw 'PMGM_PILOT_ADMIN_EMAIL debe ser operativo, no un dominio .invalid.' }

$realm = Get-Content -LiteralPath $realmFile -Raw | ConvertFrom-Json
if ($realm.realm -ne 'pmgm' -or $realm.sslRequired -ne 'external') { throw 'El realm piloto debe ser pmgm y exigir SSL externo.' }
$client = @($realm.clients | Where-Object { $_.clientId -eq 'pmgm-web' })
if ($client.Count -ne 1 -or $client[0].directAccessGrantsEnabled -ne $false) { throw 'pmgm-web debe deshabilitar Direct Access Grants.' }
$users = @($realm.users)
if ($users.Count -ne 1 -or $users[0].username -ne '${PMGM_PILOT_ADMIN_USERNAME}') { throw 'El realm piloto sólo debe contener el bootstrap institucional parametrizado.' }
if (@($users | Where-Object { ([string]$_.username).ToLowerInvariant().StartsWith('qa') }).Count -gt 0) { throw 'El realm piloto no puede contener usuarios QA.' }

$caddyFile = if ($env:PMGM_PILOT_CADDYFILE) { $env:PMGM_PILOT_CADDYFILE } else { './caddy/Caddyfile.pilot' }
if ([IO.Path]::IsPathRooted($caddyFile)) {
    $resolvedCaddy = $caddyFile
} else {
    $resolvedCaddy = Join-Path (Join-Path $repoRoot 'infrastructure') ($caddyFile -replace '^\./', '')
}
if (-not (Test-Path -LiteralPath $resolvedCaddy)) { throw "No existe Caddyfile de piloto: $resolvedCaddy" }

& docker compose --env-file $envFile -f $composeFile config *> $null
if ($LASTEXITCODE -ne 0) { throw 'La configuración Docker Compose del piloto no es válida.' }

Write-Host 'PREFLIGHT PILOTO OK' -ForegroundColor Green
Write-Host "Origen público: $($env:PMGM_PILOT_PUBLIC_URL)"
Write-Host 'DemoData: desactivado'
Write-Host 'Servicios de datos: sin puertos públicos'
