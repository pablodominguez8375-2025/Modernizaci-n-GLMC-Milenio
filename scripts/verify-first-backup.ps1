param(
    [Parameter(Mandatory = $true)]
    [string]$BackupPath
)

$ErrorActionPreference = 'Stop'
$BackupPath = [IO.Path]::GetFullPath($BackupPath)
if (-not (Test-Path -LiteralPath $BackupPath -PathType Container)) {
    throw "No existe el directorio de respaldo: $BackupPath"
}

$manifestPath = Join-Path $BackupPath 'manifest.json'
if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) { throw 'Falta manifest.json.' }
$manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding utf8 | ConvertFrom-Json

if ([int]$manifest.formatVersion -ne 1) { throw "Versión de respaldo no soportada: $($manifest.formatVersion)" }
if ($manifest.project -ne 'PMGM' -or $manifest.stack -ne 'first-implementation') {
    throw 'El manifiesto no corresponde a PMGM first-implementation.'
}
if (-not $manifest.files -or $manifest.files.Count -eq 0) { throw 'El manifiesto no contiene archivos.' }

$expected = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$totalBytes = [int64]0
foreach ($entry in $manifest.files) {
    $relative = [string]$entry.path
    if ([string]::IsNullOrWhiteSpace($relative) -or [IO.Path]::IsPathRooted($relative)) {
        throw "Ruta insegura en manifiesto: '$relative'"
    }
    $segments = $relative.Replace('\', '/').Split('/', [StringSplitOptions]::RemoveEmptyEntries)
    if ($segments -contains '..') { throw "Ruta insegura en manifiesto: '$relative'" }
    if (-not $expected.Add($relative.Replace('\', '/'))) { throw "Ruta duplicada en manifiesto: $relative" }

    $fullPath = $BackupPath
    foreach ($segment in $segments) { $fullPath = Join-Path $fullPath $segment }
    if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) { throw "Archivo faltante: $relative" }

    $item = Get-Item -LiteralPath $fullPath -Force
    if ($item.LinkType) { throw "No se permiten enlaces en el respaldo: $relative" }
    $size = [int64]$item.Length
    if ($size -ne [int64]$entry.sizeBytes) { throw "Tamaño inválido: $relative" }
    $hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $fullPath).Hash.ToLowerInvariant()
    if ($hash -ne ([string]$entry.sha256).ToLowerInvariant()) { throw "SHA-256 inválido: $relative" }
    $totalBytes += $size
}

$actual = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
Get-ChildItem -LiteralPath $BackupPath -Recurse -File | Where-Object { $_.Name -ne 'manifest.json' } | ForEach-Object {
    $relative = [IO.Path]::GetRelativePath($BackupPath, $_.FullName).Replace('\', '/')
    [void]$actual.Add($relative)
}

if (-not $actual.SetEquals($expected)) {
    $missing = @($expected | Where-Object { -not $actual.Contains($_) })
    $unexpected = @($actual | Where-Object { -not $expected.Contains($_) })
    throw "Contenido distinto al manifiesto. faltantes=$($missing -join ','), inesperados=$($unexpected -join ',')"
}
if ([int]$manifest.fileCount -ne $expected.Count) { throw 'fileCount no coincide con el manifiesto.' }
if ([int64]$manifest.totalBytes -ne $totalBytes) { throw 'totalBytes no coincide con el contenido.' }
if (-not $expected.Contains('database/pmgm.dump')) { throw 'Falta database/pmgm.dump en el manifiesto.' }

Write-Host "RESPALDO OK: $($expected.Count) archivos, $totalBytes bytes" -ForegroundColor Green
Write-Host "Creado: $($manifest.createdAtUtc)"
Write-Host "Commit fuente: $($manifest.sourceCommit)"
