#!/usr/bin/env bash
# G8: read-only attest of real QA OIDC subjects, member links, grades and offices.
# Run only after the signed Compose starts; do not print identities or credentials.
set -euo pipefail
ROOT="$(CDPATH= cd -- "$(dirname -- "$0")/../.." && pwd)"
ENV_FILE="${PMGM_QA_ENV_FILE:-/etc/pmgm/srv01.env}"
[ -f "$ENV_FILE" ] || { echo "G8: entorno no configurado" >&2; exit 1; }
set -a
# shellcheck disable=SC1090
. "$ENV_FILE"
set +a
[ "${PMGM_QA_HTTPS:-false}" = true ] &&
[ "${PMGM_QA_DB2:-false}" = true ] &&
[ "${PMGM_QA_G8_ENABLED:-false}" = true ] || {
  echo "G8: solo habilitado para QA HTTPS + BD2 + fixtures sintéticas" >&2; exit 1;
}
source "$ROOT/scripts/qa/silo-compose.sh"
qa_silo_prepare
qa_silo_require_bundle
compose=(docker compose -p "${PMGM_QA_COMPOSE_PROJECT:-pmgm-srv01}" --env-file "$ENV_FILE" -f "$COMPOSE_FILE")
query(){
  "${compose[@]}" exec -T postgres psql -X -U pmgm_app -d "$1" -v ON_ERROR_STOP=1 -tA -c "$2" 2>/dev/null
}
# No hard-coded PII: only namespaced synthetic UUIDs. The admin account is
# intentionally not tied to a member (platform administration exception).
read -r -d '' member_sql <<'SQL' || true
WITH expected AS (
  SELECT n,
         'aa820001-0000-4000-8000-' || lpad(n::text,12,'0') AS subject,
         CASE WHEN n=11 THEN 'apprentice'
              WHEN n=12 THEN 'fellowcraft' ELSE 'master' END AS degree
  FROM generate_series(2,19) AS n
),
valid AS (
  SELECT DISTINCT e.n
  FROM expected e
  JOIN core.member_identity_links l
    ON l."Subject"=e.subject
   AND l."Issuer"='https://sistema.granlogiamixta.cl/identity/realms/pmgm'
   AND l."RevokedAtUtc" IS NULL
  JOIN core.members m ON m."Id"=l."MemberId"
  JOIN core.memberships ms
    ON ms."MemberId"=m."Id" AND ms."Status"='active'
   AND ms."OrganizationId"='23232323-2323-2323-2323-232323232323'
  JOIN core.degree_events d
    ON d."MemberId"=m."Id" AND d."Degree"=e.degree
   AND d."EffectiveDate" <= CURRENT_DATE
)
SELECT count(*) FROM valid;
SQL
read -r -d '' kc_sql <<'SQL' || true
SELECT count(*) FROM public.user_entity
WHERE id LIKE 'aa820001-0000-4000-8000-%'
  AND enabled=true;
SQL
read -r -d '' office_sql <<'SQL' || true
SELECT (
  (SELECT count(*) FROM core.office_assignments a
   WHERE a."MemberId"='bb820002-0000-4000-8000-000000000003'
     AND a."OfficeType"='venerable_master'
     AND a."StartDate"<=CURRENT_DATE
     AND a."EndDate">=CURRENT_DATE) = 1
  AND
  (SELECT count(*) FROM core.office_assignments a
   WHERE a."MemberId"='bb820002-0000-4000-8000-000000000014'
     AND a."EndDate"<CURRENT_DATE) = 1
)::text;
SQL
ready=false
for _ in $(seq 1 45); do
  members="$(query pmgm "$member_sql" || true)"
  kc_users="$(query pmgm_keycloak "$kc_sql" || true)"
  offices="$(query pmgm "$office_sql" || true)"
  if [ "$members" = 18 ] && [ "$kc_users" = 19 ] && [ "$offices" = true ]; then
    ready=true
    break
  fi
  sleep 2
done
[ "$ready" = true ] || {
  echo "G8 FAIL: las cuentas Keycloak, enlaces OIDC, grados u oficinas sintéticas no concuerdan" >&2
  exit 1
}
echo "G8 PASS: 19 cuentas Keycloak, 18 enlaces por iss+sub y grados, cargos vigentes/vencidos. Datos exclusivamente sintéticos."
