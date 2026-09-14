#!/usr/bin/env bash
set -euo pipefail

ROOT="$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)"
BACKUP_DIR="${1:-}"

if [ -z "$BACKUP_DIR" ] || [ "$BACKUP_DIR" = "-h" ] || [ "$BACKUP_DIR" = "--help" ]; then
  echo "Uso: bash scripts/verify-first-backup.sh RUTA_RESPALDO"
  exit $([ -n "$BACKUP_DIR" ] && [ "$BACKUP_DIR" != "-h" ] && [ "$BACKUP_DIR" != "--help" ] && echo 2 || echo 0)
fi

command -v python3 >/dev/null 2>&1 || { echo "Python 3 es obligatorio." >&2; exit 1; }
BACKUP_DIR="$(python3 -c 'import os,sys; print(os.path.abspath(sys.argv[1]))' "$BACKUP_DIR")"

BACKUP_DIR="$BACKUP_DIR" python3 - <<'PY'
import hashlib
import json
import os
from pathlib import Path, PurePosixPath

root = Path(os.environ["BACKUP_DIR"])
manifest_path = root / "manifest.json"
if not root.is_dir():
    raise SystemExit(f"No existe el directorio de respaldo: {root}")
if not manifest_path.is_file():
    raise SystemExit("Falta manifest.json")

manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
if manifest.get("formatVersion") != 1:
    raise SystemExit(f"Versión de respaldo no soportada: {manifest.get('formatVersion')}")
if manifest.get("project") != "PMGM" or manifest.get("stack") != "first-implementation":
    raise SystemExit("El manifiesto no corresponde a PMGM first-implementation.")

entries = manifest.get("files")
if not isinstance(entries, list) or not entries:
    raise SystemExit("El manifiesto no contiene archivos.")

expected = set()
calculated_total = 0
for entry in entries:
    rel_text = entry.get("path", "")
    rel = PurePosixPath(rel_text)
    if not rel_text or rel.is_absolute() or ".." in rel.parts:
        raise SystemExit(f"Ruta insegura en manifiesto: {rel_text!r}")
    if rel_text in expected:
        raise SystemExit(f"Ruta duplicada en manifiesto: {rel_text}")
    expected.add(rel_text)

    path = root.joinpath(*rel.parts)
    if not path.is_file() or path.is_symlink():
        raise SystemExit(f"Archivo faltante o no válido: {rel_text}")

    digest = hashlib.sha256()
    size = 0
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
            size += len(chunk)

    if size != entry.get("sizeBytes"):
        raise SystemExit(f"Tamaño inválido: {rel_text}")
    if digest.hexdigest() != entry.get("sha256"):
        raise SystemExit(f"SHA-256 inválido: {rel_text}")
    calculated_total += size

actual = {
    path.relative_to(root).as_posix()
    for path in root.rglob("*")
    if path.is_file() and path.name != "manifest.json"
}
if actual != expected:
    missing = sorted(expected - actual)
    unexpected = sorted(actual - expected)
    raise SystemExit(f"Contenido distinto al manifiesto. faltantes={missing}, inesperados={unexpected}")

if manifest.get("fileCount") != len(expected):
    raise SystemExit("fileCount no coincide con el manifiesto.")
if manifest.get("totalBytes") != calculated_total:
    raise SystemExit("totalBytes no coincide con el contenido.")
if "database/pmgm.dump" not in expected:
    raise SystemExit("Falta database/pmgm.dump en el manifiesto.")

print(f"RESPALDO OK: {len(expected)} archivos, {calculated_total} bytes")
print(f"Creado: {manifest.get('createdAtUtc', 'desconocido')}")
print(f"Commit fuente: {manifest.get('sourceCommit', 'desconocido')}")
PY
