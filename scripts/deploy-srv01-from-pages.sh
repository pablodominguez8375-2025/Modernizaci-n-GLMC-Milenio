#!/usr/bin/env bash
set -euo pipefail

ROOT="$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)"
ENV_FILE="${PMGM_QA_ENV_FILE:-/etc/pmgm/srv01.env}"
DEPLOY_ROOT="${PMGM_QA_DEPLOY_ROOT:-/opt/centenario}"
PAGES_BASE="${PMGM_QA_PAGES_BASE:-https://pablodominguez8375-2025.github.io/Modernizaci-n-GLMC-Milenio}"
EXPECTED_SHA=""
PREPARE_ENV=false
NO_BACKUP=false

usage(){
  cat <<'EOF'
Uso: bash scripts/deploy-srv01-from-pages.sh [opciones]

--sha SHA40       exige desplegar exactamente ese SHA.
--env RUTA        archivo de entorno; defecto /etc/pmgm/srv01.env.
--deploy-root R   raíz de releases; defecto /opt/centenario.
--prepare-env     prepara /etc/pmgm/srv01.env usando el paquete descargado y termina.
--no-backup       omite respaldo previo sólo por decisión explícita.
EOF
}

while [ "$#" -gt 0 ]; do
  case "$1" in
    --sha) EXPECTED_SHA="${2:?Falta SHA}"; shift 2 ;;
    --env) ENV_FILE="${2:?Falta ruta}"; shift 2 ;;
    --deploy-root) DEPLOY_ROOT="${2:?Falta ruta}"; shift 2 ;;
    --prepare-env) PREPARE_ENV=true; shift ;;
    --no-backup) NO_BACKUP=true; shift ;;
    -h|--help) usage; exit 0 ;;
    *) echo "Argumento no reconocido: $1" >&2; usage >&2; exit 2 ;;
  esac
done

for cmd in curl python3 sha256sum unzip; do
  command -v "$cmd" >/dev/null 2>&1 || { echo "Falta comando: $cmd" >&2; exit 1; }
done

if [ -z "$EXPECTED_SHA" ] && command -v git >/dev/null 2>&1 && git -C "$ROOT" rev-parse --is-inside-work-tree >/dev/null 2>&1; then
  git -C "$ROOT" fetch -q origin dev || true
  EXPECTED_SHA="$(git -C "$ROOT" rev-parse origin/dev 2>/dev/null || true)"
fi
if [ -n "$EXPECTED_SHA" ] && ! [[ "$EXPECTED_SHA" =~ ^[0-9a-fA-F]{40}$ ]]; then
  echo "SHA esperado inválido: $EXPECTED_SHA" >&2
  exit 2
fi

tmp="$(mktemp -d)"
cleanup(){ rm -rf "$tmp"; }
trap cleanup EXIT

manifest_url="$PAGES_BASE/downloads/qa-current.json?ts=$(date +%s)"
curl -fsSL --retry 3 --connect-timeout 10 "$manifest_url" -o "$tmp/qa-current.json"

readarray -t meta < <(MANIFEST="$tmp/qa-current.json" python3 - <<'PY'
import json, os, re
from pathlib import Path
data=json.loads(Path(os.environ["MANIFEST"]).read_text(encoding="utf-8"))
sha=data.get("sourceSha")
filename=data.get("filename")
digest=data.get("sha256")
if not isinstance(sha,str) or not re.fullmatch(r"[0-9a-f]{40}",sha):
    raise SystemExit("qa-current.json: sourceSha inválido")
if not isinstance(filename,str) or "/" in filename or not filename.endswith(".zip"):
    raise SystemExit("qa-current.json: filename inválido")
if not isinstance(digest,str) or not re.fullmatch(r"[0-9a-f]{64}",digest):
    raise SystemExit("qa-current.json: sha256 inválido")
print(sha)
print(filename)
print(digest)
PY
)
SOURCE_SHA="${meta[0]}"
FILENAME="${meta[1]}"
EXPECTED_DIGEST="${meta[2]}"

if [ -n "$EXPECTED_SHA" ] && [ "$SOURCE_SHA" != "$EXPECTED_SHA" ]; then
  echo "Pages publica $SOURCE_SHA pero se esperaba $EXPECTED_SHA. No se despliega." >&2
  exit 1
fi

package_url="$PAGES_BASE/downloads/$FILENAME?ts=$(date +%s)"
curl -fsSL --retry 3 --connect-timeout 15 "$package_url" -o "$tmp/$FILENAME"

actual_digest="$(sha256sum "$tmp/$FILENAME" | awk '{print $1}')"
[ "$actual_digest" = "$EXPECTED_DIGEST" ] || {
  echo "SHA-256 del ZIP no coincide. esperado=$EXPECTED_DIGEST actual=$actual_digest" >&2
  exit 1
}

unzip -q "$tmp/$FILENAME" -d "$tmp/extracted"
package_root="$(find "$tmp/extracted" -mindepth 1 -maxdepth 1 -type d -print -quit)"
[ -n "$package_root" ] || { echo "ZIP sin directorio de paquete" >&2; exit 1; }

grep -qx "SOURCE_SHA=$SOURCE_SHA" "$package_root/BUILD-INFO.txt"
(cd "$package_root" && sha256sum -c MANIFEST.sha256 >/dev/null)

release_dir="$DEPLOY_ROOT/releases/$SOURCE_SHA"
mkdir -p "$DEPLOY_ROOT/releases" "$DEPLOY_ROOT/backups" "$DEPLOY_ROOT/evidence"
if [ -e "$release_dir" ]; then
  echo "Release ya existe, verificando: $release_dir"
  (cd "$release_dir" && sha256sum -c MANIFEST.sha256 >/dev/null)
else
  mv "$package_root" "$release_dir"
fi

if [ "$PREPARE_ENV" = true ]; then
  PMGM_QA_ENV_FILE="$ENV_FILE" bash "$release_dir/scripts/install-srv01-qa.sh" --env "$ENV_FILE" --prepare-env
  echo "Entorno preparado. Complete $ENV_FILE y ejecute nuevamente sin --prepare-env."
  exit 0
fi

[ -f "$ENV_FILE" ] || {
  echo "Falta $ENV_FILE. Ejecute primero: $0 --sha $SOURCE_SHA --prepare-env" >&2
  exit 1
}

previous_release=""
if [ -L "$DEPLOY_ROOT/current" ]; then
  previous_release="$(readlink -f "$DEPLOY_ROOT/current" || true)"
fi

backup_dir=""
if [ "$NO_BACKUP" = false ] && [ -n "$previous_release" ] && [ -f "$previous_release/scripts/backup-srv01-qa.sh" ]; then
  if docker compose -p pmgm-srv01 --env-file "$ENV_FILE" -f "$previous_release/infrastructure/docker-compose.srv01.yml" ps --status running --services 2>/dev/null | grep -qx postgres; then
    backup_dir="$DEPLOY_ROOT/backups/predeploy-$(date -u +%Y%m%dT%H%M%SZ)-$(basename "$previous_release")"
    PMGM_QA_ENV_FILE="$ENV_FILE" bash "$previous_release/scripts/backup-srv01-qa.sh" --env "$ENV_FILE" --output "$backup_dir"
  fi
fi

echo "Desplegando SHA $SOURCE_SHA..."
if ! PMGM_QA_ENV_FILE="$ENV_FILE" bash "$release_dir/INSTALAR.sh" --env "$ENV_FILE"; then
  echo "Falló el despliegue de $SOURCE_SHA." >&2
  [ -n "$previous_release" ] && echo "Release anterior: $previous_release" >&2
  [ -n "$backup_dir" ] && echo "Backup previo: $backup_dir" >&2
  exit 1
fi

ln -sfn "$release_dir" "$DEPLOY_ROOT/current"

PMGM_QA_ENV_FILE="$ENV_FILE" PMGM_BUILD_INFO="$release_dir/BUILD-INFO.txt" \
  bash "$release_dir/scripts/prepare-srv01-regression.sh" \
  --env "$ENV_FILE" \
  --build-info "$release_dir/BUILD-INFO.txt" \
  --output "$DEPLOY_ROOT/evidence/PMGM-QA-srv01-${SOURCE_SHA:0:12}.json"

DEPLOY_ROOT="$DEPLOY_ROOT" SOURCE_SHA="$SOURCE_SHA" ZIP_SHA256="$EXPECTED_DIGEST" BACKUP_DIR="$backup_dir" PREVIOUS_RELEASE="$previous_release" python3 - <<'PY'
import datetime as dt, json, os
from pathlib import Path
root=Path(os.environ["DEPLOY_ROOT"])
report={
 "project":"Proyecto Centenario",
 "environment":"qa-srv01",
 "sourceSha":os.environ["SOURCE_SHA"],
 "packageSha256":os.environ["ZIP_SHA256"],
 "deployedAtUtc":dt.datetime.now(dt.timezone.utc).isoformat().replace("+00:00","Z"),
 "previousRelease":os.environ.get("PREVIOUS_RELEASE") or None,
 "preDeployBackup":os.environ.get("BACKUP_DIR") or None,
 "status":"smoke-pass-regression-pending"
}
path=root/"evidence"/f"deploy-{os.environ['SOURCE_SHA']}.json"
path.write_text(json.dumps(report,indent=2,ensure_ascii=False)+"\n",encoding="utf-8")
print(path)
PY

echo
echo "DEPLOY QA SRV01 OK"
echo "SHA: $SOURCE_SHA"
echo "Release: $release_dir"
echo "Current: $DEPLOY_ROOT/current"
[ -n "$backup_dir" ] && echo "Backup previo: $backup_dir"
echo "Regresión pendiente: $DEPLOY_ROOT/evidence/PMGM-QA-srv01-${SOURCE_SHA:0:12}.json"
