#!/usr/bin/env bash
set -euo pipefail

ROOT="$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)"
ENV_FILE="${PMGM_FIRST_ENV_FILE:-$ROOT/infrastructure/.env.first.local}"
BASE_URL="${PMGM_FIRST_BASE_URL:-http://127.0.0.1:8081}"
OIDC_URL="${PMGM_FIRST_OIDC_URL:-http://127.0.0.1:8180/realms/pmgm}"
LODGE23="23232323-2323-2323-2323-232323232323"

command -v curl >/dev/null 2>&1 || { echo "curl es obligatorio para el smoke test." >&2; exit 1; }
command -v python3 >/dev/null 2>&1 || { echo "python3 es obligatorio para el smoke test." >&2; exit 1; }

if [ -f "$ENV_FILE" ]; then
  set -a
  # El archivo es generado por el launcher oficial y contiene sólo pares CLAVE=VALOR.
  # shellcheck disable=SC1090
  . "$ENV_FILE"
  set +a
fi

: "${PMGM_QA_USER_PASSWORD:?PMGM_QA_USER_PASSWORD es obligatorio para el smoke autenticado}"

wait_url() {
  local url="$1" name="$2" attempts="${3:-120}"
  local i=0
  while [ "$i" -lt "$attempts" ]; do
    if curl -fsS --max-time 5 "$url" >/dev/null 2>&1; then
      printf '✓ %s\n' "$name"
      return 0
    fi
    i=$((i + 1))
    sleep 2
  done
  printf '✗ %s no respondió: %s\n' "$name" "$url" >&2
  return 1
}

get_token() {
  local username="$1"
  local payload
  payload=$(curl -fsS --max-time 15 \
    -H 'Content-Type: application/x-www-form-urlencoded' \
    --data-urlencode 'client_id=pmgm-web' \
    --data-urlencode 'grant_type=password' \
    --data-urlencode "username=$username" \
    --data-urlencode "password=$PMGM_QA_USER_PASSWORD" \
    "$OIDC_URL/protocol/openid-connect/token")
  TOKEN_JSON="$payload" python3 - <<'PY'
import json, os
payload = json.loads(os.environ['TOKEN_JSON'])
token = payload.get('access_token')
if not token:
    raise SystemExit('Keycloak no devolvió access_token')
print(token)
PY
}

json_get() {
  local token="$1" path="$2"
  curl -fsS --max-time 20 -H "Authorization: Bearer $token" -H 'Accept: application/json' "$BASE_URL$path"
}

assert_json_get() {
  local token="$1" path="$2" name="$3"
  local payload
  payload=$(json_get "$token" "$path")
  BODY="$payload" python3 - <<'PY'
import json, os
json.loads(os.environ['BODY'])
PY
  printf '✓ %s\n' "$name"
}

assert_transferred_degree() {
  local payload="$1" actor="$2"
  BODY="$payload" python3 - <<'PY'
import json, os
payload = json.loads(os.environ['BODY'])
rows = payload.get('items', [])
row = next((item for item in rows if item.get('institutionalNumber') == 'GLM-QA-0230'), None)
if row is None:
    raise SystemExit('no se encontró al miembro trasladado GLM-QA-0230 en Taller 23')
if row.get('membershipStatus') != 'active':
    raise SystemExit(f"membresía trasladada no está activa: {row.get('membershipStatus')}")
if row.get('currentDegree') != 'master':
    raise SystemExit(f"el grado maestro no acompañó el traslado: {row.get('currentDegree')}")
PY
  printf '✓ grado maestro preservado tras traslado (%s)\n' "$actor"
}

wait_url "$OIDC_URL/.well-known/openid-configuration" "Keycloak discovery"
wait_url "$BASE_URL/health/live" "API live"
wait_url "$BASE_URL/health/ready" "API ready/PostgreSQL"
wait_url "$BASE_URL/health/web" "frontend Nginx"

admin_token=$(get_token 'qa.admin')
workshop_token=$(get_token 'qa.taller23')
printf '✓ autenticación QA mediante Keycloak\n'

assert_json_get "$admin_token" '/api/session/me' 'sesión institucional de Gran Logia'
admin_members=$(json_get "$admin_token" "/api/members?organizationId=$LODGE23&limit=20")
assert_transferred_degree "$admin_members" 'Gran Logia'
printf '✓ Membresía / Ficha de Taller\n'
assert_json_get "$admin_token" '/api/candidate-publications/active' 'Portal de insinuados'
# La raíz de Biblioteca redirige deliberadamente a /buscar. El smoke llama al
# endpoint JSON real para validar el catálogo sin depender de seguimiento de 307.
assert_json_get "$admin_token" '/api/biblioteca/buscar' 'Biblioteca Virtual'
assert_json_get "$admin_token" '/api/grand-archive/?status=active&limit=20' 'Gran Archivero'

workshop_archive_status=$(curl -sS --max-time 20 -o /tmp/pmgm-workshop-archive.json -w '%{http_code}' \
  -H "Authorization: Bearer $workshop_token" -H 'Accept: application/json' \
  "$BASE_URL/api/grand-archive/?status=active&limit=20")
if [ "$workshop_archive_status" != '403' ]; then
  echo "Se esperaba 403 para Gran Archivero desde el perfil Taller y se obtuvo $workshop_archive_status." >&2
  cat /tmp/pmgm-workshop-archive.json >&2 || true
  exit 1
fi
printf '✓ segregación de permisos: Taller no accede a Gran Archivero\n'

workshop_session=$(json_get "$workshop_token" '/api/session/me')
BODY="$workshop_session" python3 - <<'PY'
import json, os
payload = json.loads(os.environ['BODY'])
if payload.get('accessScope') != 'organization':
    raise SystemExit(f"scope inesperado: {payload.get('accessScope')}")
if payload.get('capabilities', {}).get('canManageLodgeOperations') is not True:
    raise SystemExit('el perfil Taller no obtuvo capacidad de gestión logial')
PY
printf '✓ perfil Taller limitado a organization con Gestión Logial\n'
workshop_members=$(json_get "$workshop_token" "/api/members?organizationId=$LODGE23&limit=20")
assert_transferred_degree "$workshop_members" 'Taller destino'

printf '\nSMOKE FIRST IMPLEMENTATION OK\n'
