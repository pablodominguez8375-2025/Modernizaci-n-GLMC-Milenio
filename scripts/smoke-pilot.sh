#!/usr/bin/env bash
set -euo pipefail

ROOT="$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)"
ENV_FILE="${PMGM_PILOT_ENV_FILE:-$ROOT/infrastructure/.env.pilot.local}"

command -v curl >/dev/null 2>&1 || { echo "curl es obligatorio." >&2; exit 1; }
command -v python3 >/dev/null 2>&1 || { echo "Python 3 es obligatorio." >&2; exit 1; }
[ -f "$ENV_FILE" ] || { echo "No existe $ENV_FILE" >&2; exit 1; }

set -a
# shellcheck disable=SC1090
. "$ENV_FILE"
set +a

: "${PMGM_PILOT_PUBLIC_URL:?PMGM_PILOT_PUBLIC_URL es obligatorio}"
: "${PMGM_PILOT_ADMIN_USERNAME:?PMGM_PILOT_ADMIN_USERNAME es obligatorio}"
: "${PMGM_PILOT_ADMIN_PASSWORD:?PMGM_PILOT_ADMIN_PASSWORD es obligatorio}"

curl_common=(--max-time 15 --silent --show-error)
if [ "${PMGM_PILOT_INSECURE_TLS:-false}" = "true" ]; then
  curl_common+=(--insecure)
fi

wait_url() {
  local url="$1" name="$2" attempts="${3:-120}"
  local i=0
  while [ "$i" -lt "$attempts" ]; do
    if curl "${curl_common[@]}" --fail "$url" >/dev/null 2>&1; then
      printf '✓ %s\n' "$name"
      return 0
    fi
    i=$((i + 1))
    sleep 2
  done
  printf '✗ %s no respondió: %s\n' "$name" "$url" >&2
  return 1
}

wait_url "$PMGM_PILOT_PUBLIC_URL/identity/realms/pmgm/.well-known/openid-configuration" "Keycloak discovery HTTPS"
wait_url "$PMGM_PILOT_PUBLIC_URL/health/live" "API live"
wait_url "$PMGM_PILOT_PUBLIC_URL/health/ready" "API ready/PostgreSQL"
wait_url "$PMGM_PILOT_PUBLIC_URL/health/web" "frontend Nginx"
wait_url "$PMGM_PILOT_PUBLIC_URL/" "frontend HTTPS"

# El callback pertenece al SPA; /identity pertenece a Keycloak. Este control evita regresiones de routing.
callback_status="$(curl "${curl_common[@]}" -o /tmp/pmgm-pilot-callback.html -w '%{http_code}' "$PMGM_PILOT_PUBLIC_URL/auth/callback")"
[ "$callback_status" = "200" ] || { echo "El callback SPA devolvió HTTP $callback_status." >&2; exit 1; }
printf '✓ separación /auth (SPA) y /identity (Keycloak)\n'

discovery="$(curl "${curl_common[@]}" --fail -H 'Accept: application/json' "$PMGM_PILOT_PUBLIC_URL/identity/realms/pmgm/.well-known/openid-configuration")"
DISCOVERY="$discovery" EXPECTED="$PMGM_PILOT_PUBLIC_URL/identity/realms/pmgm" python3 - <<'PY'
import json, os
payload = json.loads(os.environ['DISCOVERY'])
expected = os.environ['EXPECTED']
if payload.get('issuer') != expected:
    raise SystemExit(f"issuer OIDC inesperado: {payload.get('issuer')} != {expected}")
if not str(payload.get('authorization_endpoint', '')).startswith(expected + '/'):
    raise SystemExit('authorization_endpoint no pertenece al origen del piloto')
PY
printf '✓ issuer OIDC público consistente\n'

system_info="$(curl "${curl_common[@]}" --fail -H 'Accept: application/json' "$PMGM_PILOT_PUBLIC_URL/api/system/info")"
SYSTEM_INFO="$system_info" python3 - <<'PY'
import json, os
payload = json.loads(os.environ['SYSTEM_INFO'])
if payload.get('version') != '0.32.0':
    raise SystemExit(f"versión API inesperada: {payload.get('version')}")
PY
printf '✓ API v0.32.0 expuesta sólo detrás de HTTPS\n'

session_status="$(curl "${curl_common[@]}" -o /tmp/pmgm-pilot-session.json -w '%{http_code}' -H 'Accept: application/json' "$PMGM_PILOT_PUBLIC_URL/api/session/me")"
[ "$session_status" = "401" ] || { echo "Se esperaba 401 sin token en /api/session/me y se obtuvo $session_status." >&2; exit 1; }
printf '✓ endpoint protegido exige autenticación\n'

# El cliente web del piloto no puede usar Resource Owner Password Credentials.
token_status="$(curl "${curl_common[@]}" -o /tmp/pmgm-pilot-token.json -w '%{http_code}' \
  -H 'Content-Type: application/x-www-form-urlencoded' \
  --data-urlencode 'client_id=pmgm-web' \
  --data-urlencode 'grant_type=password' \
  --data-urlencode "username=$PMGM_PILOT_ADMIN_USERNAME" \
  --data-urlencode "password=$PMGM_PILOT_ADMIN_PASSWORD" \
  "$PMGM_PILOT_PUBLIC_URL/identity/realms/pmgm/protocol/openid-connect/token")"
if [ "$token_status" = "200" ]; then
  echo "FALLO DE SEGURIDAD: pmgm-web aceptó password grant en el piloto." >&2
  exit 1
fi
printf '✓ password grant deshabilitado para pmgm-web (HTTP %s)\n' "$token_status"

printf '\nSMOKE PILOTO OK\n'
