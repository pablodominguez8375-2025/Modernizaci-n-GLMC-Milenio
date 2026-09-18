#!/usr/bin/env bash
set -euo pipefail

BACKUP_DIR="${1:-}"
if [ -z "$BACKUP_DIR" ] || [ "$BACKUP_DIR" = "-h" ] || [ "$BACKUP_DIR" = "--help" ]; then
  echo "Uso: bash scripts/verify-srv01-backup.sh RUTA_RESPALDO"
  exit 0
fi

command -v python3 >/dev/null 2>&1 || { echo "Python 3 es obligatorio." >&2; exit 1; }
BACKUP_DIR="$(python3 -c 'import os,sys; print(os.path.abspath(sys.argv[1]))' "$BACKUP_DIR")"

BACKUP_DIR="$BACKUP_DIR" python3 - <<'PY'
import hashlib, json, os
from pathlib import Path, PurePosixPath

root=Path(os.environ["BACKUP_DIR"])
manifest_path=root/"manifest.json"
if not root.is_dir():
    raise SystemExit(f"No existe: {root}")
if not manifest_path.is_file():
    raise SystemExit("Falta manifest.json")
manifest=json.loads(manifest_path.read_text(encoding="utf-8"))
if manifest.get("formatVersion") != 1:
    raise SystemExit("Versión de respaldo no soportada")
if manifest.get("project") != "PMGM" or manifest.get("stack") != "qa-srv01":
    raise SystemExit("El respaldo no corresponde a QA srv01")

entries=manifest.get("files")
if not isinstance(entries,list) or not entries:
    raise SystemExit("El manifiesto no contiene archivos")

expected=set()
total=0
for entry in entries:
    rel_text=entry.get("path","")
    rel=PurePosixPath(rel_text)
    if not rel_text or rel.is_absolute() or ".." in rel.parts:
        raise SystemExit(f"Ruta insegura: {rel_text!r}")
    if rel_text in expected:
        raise SystemExit(f"Ruta duplicada: {rel_text}")
    expected.add(rel_text)
    path=root.joinpath(*rel.parts)
    if not path.is_file() or path.is_symlink():
        raise SystemExit(f"Archivo faltante/no válido: {rel_text}")
    h=hashlib.sha256()
    size=0
    with path.open("rb") as fh:
        for chunk in iter(lambda: fh.read(1024*1024), b""):
            h.update(chunk); size += len(chunk)
    if h.hexdigest() != entry.get("sha256"):
        raise SystemExit(f"SHA-256 inválido: {rel_text}")
    if size != entry.get("sizeBytes"):
        raise SystemExit(f"Tamaño inválido: {rel_text}")
    total += size

required={"database/pmgm.dump","database/pmgm_keycloak.dump"}
if not required.issubset(expected):
    raise SystemExit(f"Faltan dumps obligatorios: {sorted(required-expected)}")

actual={
    p.relative_to(root).as_posix()
    for p in root.rglob("*")
    if p.is_file() and p.name != "manifest.json"
}
if actual != expected:
    raise SystemExit(f"Contenido distinto al manifiesto; faltan={sorted(expected-actual)} extra={sorted(actual-expected)}")
if manifest.get("fileCount") != len(expected) or manifest.get("totalBytes") != total:
    raise SystemExit("Totales inconsistentes")

print(f"RESPALDO QA SRV01 OK: {len(expected)} archivos, {total} bytes")
print(f"Creado: {manifest.get('createdAtUtc','desconocido')}")
print(f"SHA desplegado: {manifest.get('sourceSha','desconocido')}")
PY
