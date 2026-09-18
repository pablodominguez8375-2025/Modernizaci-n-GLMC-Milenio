#!/usr/bin/env bash
set -euo pipefail

ENV_FILE="${PMGM_QA_ENV_FILE:-/etc/pmgm/srv01.env}"
DEPLOY_ROOT="${PMGM_QA_DEPLOY_ROOT:-/opt/centenario}"
RELEASE_SHA=""
BACKUP_DIR=""
CONFIRMED=false

while [ "$#" -gt 0 ]; do
  case "$1" in
    --release-sha) RELEASE_SHA="${2:?Falta SHA}"; shift 2 ;;
    --backup) BACKUP_DIR="${2:?Falta ruta}"; shift 2 ;;
    --env) ENV_FILE="${2:?Falta ruta}"; shift 2 ;;
    --yes) CONFIRMED=true; shift ;;
    -h|--help)
      echo "Uso: bash scripts/rollback-srv01-qa.sh --release-sha SHA40 --backup RUTA --yes [--env RUTA]"
      exit 0 ;;
    *) echo "Argumento no reconocido: $1" >&2; exit 2 ;;
  esac
done

[[ "$RELEASE_SHA" =~ ^[0-9a-fA-F]{40}$ ]] || { echo "SHA inválido" >&2; exit 2; }
[ -n "$BACKUP_DIR" ] || { echo "Debe indicar --backup" >&2; exit 2; }
[ "$CONFIRMED" = true ] || { echo "Agregue --yes para confirmar rollback" >&2; exit 2; }

release_dir="$DEPLOY_ROOT/releases/$RELEASE_SHA"
[ -d "$release_dir" ] || { echo "No existe release: $release_dir" >&2; exit 1; }
[ -f "$ENV_FILE" ] || { echo "No existe $ENV_FILE" >&2; exit 1; }

(cd "$release_dir" && sha256sum -c MANIFEST.sha256 >/dev/null)
PMGM_QA_ENV_FILE="$ENV_FILE" bash "$release_dir/INSTALAR.sh" --env "$ENV_FILE" --skip-smoke
PMGM_QA_ENV_FILE="$ENV_FILE" bash "$release_dir/scripts/restore-srv01-qa.sh" --env "$ENV_FILE" --backup "$BACKUP_DIR" --yes
ln -sfn "$release_dir" "$DEPLOY_ROOT/current"

echo "ROLLBACK QA SRV01 OK"
echo "Release activa: $release_dir"
