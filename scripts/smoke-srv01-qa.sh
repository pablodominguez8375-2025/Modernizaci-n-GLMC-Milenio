#!/usr/bin/env bash
set -euo pipefail

ROOT="$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)"
ENV_FILE="${PMGM_QA_ENV_FILE:-/etc/pmgm/srv01.env}"
LODGE23="23232323-2323-2323-2323-232323232323"

[ -f "$ENV_FILE" ] || { echo "No existe $ENV_FILE" >&2; exit 1; }
set -a
# shellcheck disable=SC1090
. "$ENV_FILE"
set +a

: "${PMGM_QA_WEB_PUBLIC_URL:?PMGM_QA_WEB_PUBLIC_URL es obligatorio}"
: "${PMGM_QA_OIDC_PUBLIC_URL:?PMGM_QA_OIDC_PUBLIC_URL es obligatorio}"
: "${PMGM_QA_USER_PASSWORD:?PMGM_QA_USER_PASSWORD es obligatorio}"

wait_url(){
  local url="$1" name="$2"
  for _ in $(seq 1 120); do
    if curl -fsS --max-time 5 "$url" >/dev/null 2>&1; then
      printf '✓ %s\n' "$name"
      return 0
    fi
    sleep 2
  done
  echo "✗ No respondió: $name ($url)" >&2
  return 1
}

get_token(){
  local username="$1"
  local payload
  payload="$(curl -fsS --max-time 15 \
    -H 'Content-Type: application/x-www-form-urlencoded' \
    --data-urlencode 'client_id=pmgm-web' \
    --data-urlencode 'grant_type=password' \
    --data-urlencode "username=$username" \
    --data-urlencode "password=$PMGM_QA_USER_PASSWORD" \
    "$PMGM_QA_OIDC_PUBLIC_URL/realms/pmgm/protocol/openid-connect/token")"
  TOKEN_JSON="$payload" python3 - <<'PY'
import json, os
token=json.loads(os.environ["TOKEN_JSON"]).get("access_token")
if not token:
    raise SystemExit("Keycloak no devolvió access_token")
print(token)
PY
}

json_get(){
  local token="$1" path="$2"
  curl -fsS --max-time 20 -H "Authorization: Bearer $token" -H 'Accept: application/json' "$PMGM_QA_WEB_PUBLIC_URL$path"
}

wait_url "$PMGM_QA_OIDC_PUBLIC_URL/realms/pmgm/.well-known/openid-configuration" "Keycloak QA"
wait_url "$PMGM_QA_WEB_PUBLIC_URL/health/live" "API live"
wait_url "$PMGM_QA_WEB_PUBLIC_URL/health/ready" "API ready"
wait_url "$PMGM_QA_WEB_PUBLIC_URL/health/web" "Frontend"

admin_token="$(get_token qa.admin)"
venerable_token="$(get_token qa.venerable23)"
printf '✓ autenticación QA real\n'

session="$(json_get "$admin_token" /api/session/me)"
BODY="$session" python3 - <<'PY'
import json, os
payload=json.loads(os.environ["BODY"])
if payload.get("accessScope") != "order":
    raise SystemExit("qa.admin no obtuvo scope order")
PY
printf '✓ sesión Gran Logia\n'

council="$(json_get "$venerable_token" "/api/gestion-logial/talleres/$LODGE23/consejo/sesiones")"
BODY="$council" python3 - <<'PY'
import json, os
payload=json.loads(os.environ["BODY"])
if "items" not in payload:
    raise SystemExit("respuesta Consejo sin items")
PY
printf '✓ Consejo de Administración accesible con Venerable QA\n'

printf '\nSMOKE QA SRV01 OK\n'
