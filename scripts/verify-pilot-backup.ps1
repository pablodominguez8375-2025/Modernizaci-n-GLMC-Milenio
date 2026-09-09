param(
    [Parameter(Mandatory = $true)]
    [string]$BackupPath
)

$ErrorActionPreference = 'Stop'
$BackupPath = [IO.Path]::GetFullPath($BackupPath)
if (-not (Test-Path -LiteralPath $BackupPath -PathType Container)) { throw "No existe el directorio: $BackupPath" }
$manifestPath = Join-Path $BackupPath 'manifest.json'
if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) { throw 'Falta manifest.json.' }
$manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding utf8 | ConvertFrom-Json
if ([int]$manifest.formatVersion -ne 1) { throw "Versión no soportada: $($manifest.formatVersion)" }
if ($manifest.project -ne 'PMGM' -or $manifest.stack -ne 'pilot-operational') { throw 'El manifiesto no corresponde al piloto operacional PMGM.' }

$expected = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$total = [int64]0
foreach ($entry in @($manifest.files)) {
    $relative = ([string]$entry.path).Replace('\','/')
    if ([string]::IsNullOrWhiteSpace($relative) -or [IO.Path]::IsPathRooted($relative)) { throw "Ruta insegura: $relative" }
    $segments = $relative.Split('/', [StringSplitOptions]::RemoveEmptyEntries)
    if ($segments -contains '..') { throw "Ruta insegura: $relative" }
    if (-not $expected.Add($relative)) { throw "Ruta duplicada: $relative" }
    $full = $BackupPath
    foreach ($segment in $segments) { $full = Join-Path $full $segment }
    if (-not (Test-Path -LiteralPath $full -PathType Leaf)) { throw "Archivo faltante: $relative" }
    $item = Get-Item -LiteralPath $full -Force
    if ($item.LinkType) { throw "No se permiten enlaces: $relative" }
    if ([int64]$item.Length -ne [int64]$entry.sizeBytes) { throw "Tamaño inválido: $relative" }
    $hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $full).Hash.ToLowerInvariant()
    if ($hash -ne ([string]$entry.sha256).ToLowerInvariant()) { throw "SHA-256 inválido: $relative" }
    $total += [int64]$item.Length
}

$actual = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
Get-ChildItem -LiteralPath $BackupPath -Recurse -File | Where-Object Name -ne 'manifest.json' | ForEach-Object {
    [void]$actual.Add([IO.Path]::GetRelativePath($BackupPath, $_.FullName).Replace('\','/'))
}
if (-not $actual.SetEquals($expected)) { throw 'El contenido del respaldo no coincide exactamente con el manifiesto.' }
if ([int]$manifest.fileCount -ne $expected.Count -or [int64]$manifest.totalBytes -ne $total) { throw 'Totales del manifiesto inconsistentes.' }
foreach ($required in @('database/pmgm.dump','database/pmgm_keycloak.dump')) {
    if (-not $expected.Contains($required)) { throw "Falta $required" }
}
$dbNames = @($manifest.components.postgres.databases)
if ($dbNames.Count -ne 2 -or $dbNames[0] -ne 'pmgm' -or $dbNames[1] -ne 'pmgm_keycloak') { throw 'El manifiesto no declara ambas bases PostgreSQL del piloto.' }
Write-Host "RESPALDO PILOTO OK: $($expected.Count) archivos, $total bytes" -ForegroundColor Green
Write-Host "Creado: $($manifest.createdAtUtc)"
Write-Host "Commit fuente: $($manifest.sourceCommit)"
