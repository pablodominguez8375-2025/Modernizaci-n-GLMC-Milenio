#!/usr/bin/env bash
set -euo pipefail

ROOT="$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)"
ENV_FILE="${PMGM_QA_ENV_FILE:-/etc/pmgm/srv01.env}"
BUILD_INFO="${PMGM_BUILD_INFO:-$ROOT/BUILD-INFO.txt}"
TEMPLATE="$ROOT/release/PMGM-QA-SRV01-REGRESSION.template.json"
OUTPUT=""

while [ "$#" -gt 0 ]; do
  case "$1" in
    --env) ENV_FILE="${2:?Falta ruta para --env}"; shift 2 ;;
    --build-info) BUILD_INFO="${2:?Falta ruta para --build-info}"; shift 2 ;;
    --output) OUTPUT="${2:?Falta ruta para --output}"; shift 2 ;;
    -h|--help)
      echo "Uso: bash scripts/prepare-srv01-regression.sh [--env RUTA] [--build-info RUTA] [--output RUTA]"
      exit 0 ;;
    *) echo "Argumento no reconocido: $1" >&2; exit 2 ;;
  esac
done

[ -f "$ENV_FILE" ] || { echo "No existe $ENV_FILE" >&2; exit 1; }
[ -f "$BUILD_INFO" ] || { echo "No existe $BUILD_INFO" >&2; exit 1; }
[ -f "$TEMPLATE" ] || { echo "No existe $TEMPLATE" >&2; exit 1; }

set -a
# shellcheck disable=SC1090
. "$ENV_FILE"
set +a
: "${PMGM_QA_WEB_PUBLIC_URL:?PMGM_QA_WEB_PUBLIC_URL es obligatorio}"

SOURCE_SHA="$(awk -F= '$1=="SOURCE_SHA"{print $2}' "$BUILD_INFO" | tail -n1)"
[ "${#SOURCE_SHA}" -eq 40 ] || { echo "BUILD-INFO no contiene SOURCE_SHA válido" >&2; exit 1; }

short="${SOURCE_SHA:0:12}"
if [ -z "$OUTPUT" ]; then
  mkdir -p "$ROOT/evidence"
  OUTPUT="$ROOT/evidence/PMGM-QA-srv01-${short}.json"
fi

SOURCE_SHA="$SOURCE_SHA" BASE_URL="$PMGM_QA_WEB_PUBLIC_URL" TEMPLATE="$TEMPLATE" OUTPUT="$OUTPUT" python3 - <<'PY'
import datetime as dt
import json
import os
from pathlib import Path

src=Path(os.environ["TEMPLATE"])
out=Path(os.environ["OUTPUT"])
data=json.loads(src.read_text(encoding="utf-8"))
sha=os.environ["SOURCE_SHA"]
data["sourceSha"]=sha
data["executionId"]=f"qa-srv01-{sha[:12]}-{dt.datetime.now(dt.timezone.utc).strftime('%Y%m%dT%H%M%SZ')}"
data["environment"]["baseUrl"]=os.environ["BASE_URL"]
data["environment"]["executedAtUtc"]=dt.datetime.now(dt.timezone.utc).isoformat().replace("+00:00","Z")
out.parent.mkdir(parents=True, exist_ok=True)
out.write_text(json.dumps(data,indent=2,ensure_ascii=False)+"\n",encoding="utf-8")
print(out)
PY

python3 "$ROOT/tests/qa_srv01_regression_gate.py" "$OUTPUT" --allow-pending
echo "Ejecución QA preparada: $OUTPUT"
echo "Este archivo contiene sólo referencias de evidencia; no incorpore secretos ni datos personales reales."
