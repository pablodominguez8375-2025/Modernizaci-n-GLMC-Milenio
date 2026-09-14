param(
    [switch]$NoBrowser,
    [switch]$Reset,
    [switch]$ShowCredentials
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$compose = Join-Path $repoRoot 'infrastructure/docker-compose.first.yml'
$envFile = Join-Path $repoRoot 'infrastructure/.env.first.local'

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    throw 'Docker no está disponible. Instale Docker Desktop y vuelva a ejecutar este script.'
}

function New-LocalSecret {
    $bytes = New-Object byte[] 32
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try { $rng.GetBytes($bytes) } finally { $rng.Dispose() }
    return ([System.BitConverter]::ToString($bytes)).Replace('-', '').ToLowerInvariant()
}

if (-not (Test-Path $envFile)) {
    $lines = @(
        "PMGM_FIRST_DB_PASSWORD=$(New-LocalSecret)",
        'PMGM_FIRST_MINIO_USER=pmgmqa',
        "PMGM_FIRST_MINIO_PASSWORD=$(New-LocalSecret)",
        "PMGM_FIRST_KEYCLOAK_ADMIN_PASSWORD=$(New-LocalSecret)",
        "PMGM_QA_USER_PASSWORD=$(New-LocalSecret)"
    )
    Set-Content -Path $envFile -Value $lines -Encoding Ascii
    Write-Host "Secretos QA generados en $envFile" -ForegroundColor Green
}

Push-Location $repoRoot
try {
    if ($Reset) {
        Write-Host 'Eliminando datos locales de la primera implementación...' -ForegroundColor Yellow
        docker compose --env-file $envFile -f $compose down -v --remove-orphans
        if ($LASTEXITCODE -ne 0) { throw 'No fue posible limpiar el entorno Docker.' }
    }

    Write-Host 'Construyendo y levantando Proyecto Milenio v0.30...' -ForegroundColor Cyan
    docker compose --env-file $envFile -f $compose up -d --build
    if ($LASTEXITCODE -ne 0) { throw 'Docker Compose no pudo iniciar la plataforma.' }

    function Wait-Endpoint([string]$Url, [string]$Name, [int]$Attempts = 120) {
        for ($i = 1; $i -le $Attempts; $i++) {
            try {
                $response = Invoke-WebRequest -UseBasicParsing -Uri $Url -TimeoutSec 4
                if ($response.StatusCode -ge 200 -and $response.StatusCode -lt 400) {
                    Write-Host "✓ $Name disponible" -ForegroundColor Green
                    return
                }
            } catch { }
            Start-Sleep -Seconds 2
        }
        throw "$Name no quedó disponible. Revise: docker compose --env-file infrastructure/.env.first.local -f infrastructure/docker-compose.first.yml logs"
    }

    Wait-Endpoint 'http://127.0.0.1:8180/realms/pmgm/.well-known/openid-configuration' 'Identidad Keycloak'
    Wait-Endpoint 'http://127.0.0.1:8081/health/live' 'API PMGM'
    Wait-Endpoint 'http://127.0.0.1:8081/health/ready' 'Base de datos PMGM'
    Wait-Endpoint 'http://127.0.0.1:8081/health/web' 'Interfaz web'

    Write-Host ''
    Write-Host 'Proyecto Milenio — primera implementación lista.' -ForegroundColor Green
    Write-Host 'Aplicación: http://127.0.0.1:8081'
    Write-Host 'Usuarios QA: qa.admin y qa.taller23'
    Write-Host "Contraseña QA: almacenada localmente en $envFile"
    Write-Host 'Entorno: sólo local/QA; no exponer a Internet.' -ForegroundColor Yellow
    Write-Host 'Para reiniciar datos: .\scripts\start-first-implementation.ps1 -Reset'
    Write-Host 'Para mostrar la contraseña QA: .\scripts\start-first-implementation.ps1 -NoBrowser -ShowCredentials'

    if ($ShowCredentials) {
        $passwordLine = Get-Content $envFile | Where-Object { $_ -like 'PMGM_QA_USER_PASSWORD=*' } | Select-Object -First 1
        if ($passwordLine) { Write-Host $passwordLine -ForegroundColor Yellow }
    }

    if (-not $NoBrowser) { Start-Process 'http://127.0.0.1:8081' }
}
finally {
    Pop-Location
}
