#!/usr/bin/env bash
set -euo pipefail

ROOT="$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)"
ENV_FILE="${PMGM_QA_ENV_FILE:-/etc/pmgm/srv01.env}"
COMPOSE_FILE="$ROOT/infrastructure/docker-compose.srv01.yml"

failures=0
warnings=0
ok(){ printf 'OK   %s\n' "$*"; }
warn(){ printf 'WARN %s\n' "$*" >&2; warnings=$((warnings+1)); }
fail(){ printf 'FAIL %s\n' "$*" >&2; failures=$((failures+1)); }

printf 'Proyecto Centenario — preflight QA srv01\n\n'

for cmd in docker curl python3 sha256sum; do
  command -v "$cmd" >/dev/null 2>&1 && ok "Comando disponible: $cmd" || fail "Falta comando obligatorio: $cmd"
done
if command -v docker >/dev/null 2>&1; then
  docker compose version >/dev/null 2>&1 && ok "Docker Compose plugin disponible" || fail "Docker Compose plugin no disponible"
  docker info >/dev/null 2>&1 && ok "Docker daemon accesible" || fail "El usuario no puede acceder al Docker daemon"
fi

cpu_count="$(getconf _NPROCESSORS_ONLN 2>/dev/null || nproc 2>/dev/null || echo 0)"
if [ "$cpu_count" -ge 8 ]; then ok "vCPU disponibles: $cpu_count"
elif [ "$cpu_count" -ge 4 ]; then warn "vCPU disponibles: $cpu_count; válido para QA, bajo el objetivo de 8"
else fail "vCPU disponibles: $cpu_count; mínimo QA: 4"; fi

if [ -r /proc/meminfo ]; then
  mem_kib="$(awk '/MemTotal:/ {print $2}' /proc/meminfo)"
  mem_gib=$((mem_kib / 1024 / 1024))
  if [ "$mem_gib" -ge 7 ]; then ok "RAM disponible ~${mem_gib} GiB para QA"
  else fail "RAM ~${mem_gib} GiB; srv01 QA requiere al menos ~7 GiB"; fi
fi

free_kib="$(df -Pk "$ROOT" | awk 'NR==2 {print $4}' || echo 0)"
free_gib=$((free_kib / 1024 / 1024))
if [ "$free_gib" -ge 30 ]; then ok "Espacio libre ~${free_gib} GiB"
elif [ "$free_gib" -ge 15 ]; then warn "Espacio libre ~${free_gib} GiB; vigilar crecimiento de imágenes, BD y objetos"
else fail "Espacio libre ~${free_gib} GiB; mínimo QA recomendado: 15 GiB"; fi

[ -f "$ENV_FILE" ] || fail "No existe archivo de entorno: $ENV_FILE"
if [ -f "$ENV_FILE" ]; then
  perms="$(stat -c '%a' "$ENV_FILE" 2>/dev/null || true)"
  case "$perms" in 600|640) ok "Permisos de entorno: $perms" ;; *) fail "Permisos de entorno inseguros; se requiere 600/640" ;; esac
  set -a
  # shellcheck disable=SC1090
  . "$ENV_FILE"
  set +a

  # Fail closed before attempting signed-model generation or any deployment.
  if [ "${PMGM_QA_HTTPS:-false}" = true ] && [ "${PMGM_QA_DB2:-false}" != true ]; then
    fail "Ingreso HTTPS QA requiere segregación PostgreSQL G4 BD2"
  fi
  if [ "${PMGM_QA_HTTPS:-false}" = true ] && [ "${PMGM_QA_G8_ENABLED:-false}" != true ]; then
    fail "Ingreso HTTPS QA requiere G8: cuentas ficticias con issuer+sub y grados"
  fi
  if [ "${PMGM_QA_DB2:-false}" = true ]; then
    python3 - <<'PY'
import os
names = (
    'PMGM_QA_DB_PASSWORD', 'PMGM_QA_KC_DB_PASSWORD',
    'PMGM_QA_RUNTIME_DB_PASSWORD', 'PMGM_QA_MIGRATOR_DB_PASSWORD',
    'PMGM_QA_REPORT_DB_PASSWORD', 'PMGM_QA_STORAGE_ADMIN_PASSWORD',
    'PMGM_QA_DOCUMENT_SECRET_KEY', 'PMGM_QA_BACKUP_SECRET_KEY',
    'PMGM_QA_KEYCLOAK_ADMIN_PASSWORD', 'PMGM_QA_USER_PASSWORD'
)
values = [os.environ.get(k, '') for k in names]
if any(len(v) < 20 or 'REEMPLAZAR' in v or 'CHANGE-ME' in v.upper() for v in values):
    raise SystemExit('QA BD2: se requieren 10 contraseñas distintas >=20 caracteres, no placeholders')
if len(values) != len(set(values)):
    raise SystemExit('QA BD2: secretos reutilizados')
PY
  fi

  # Select only a same-source, cryptographically verified local Silo archive when supplied.
  source "$ROOT/scripts/qa/silo-compose.sh"
  qa_silo_prepare

  required=(PMGM_QA_WEB_PUBLIC_URL PMGM_QA_OIDC_PUBLIC_URL PMGM_QA_DB_PASSWORD PMGM_QA_STORAGE_ADMIN_USER PMGM_QA_STORAGE_ADMIN_PASSWORD PMGM_QA_DOCUMENT_ACCESS_KEY PMGM_QA_DOCUMENT_SECRET_KEY PMGM_QA_BACKUP_ACCESS_KEY PMGM_QA_BACKUP_SECRET_KEY PMGM_QA_KEYCLOAK_ADMIN_PASSWORD PMGM_QA_USER_PASSWORD)
  for name in "${required[@]}"; do
    value="${!name:-}"
    [ -n "$value" ] || fail "$name es obligatorio"
    [[ "$value" != *REEMPLAZAR* ]] || fail "$name conserva placeholder"
  done

  for url_var in PMGM_QA_WEB_PUBLIC_URL PMGM_QA_OIDC_PUBLIC_URL; do
    url="${!url_var:-}"
    [[ "$url" == http://* || "$url" == https://* ]] || fail "$url_var debe ser un origen http(s)"
    [[ "$url" != */ ]] || fail "$url_var no debe terminar en /"
  done

  # Guard captures the expanded model privately; never log credentials or raw errors.
  if python3 "$ROOT/scripts/check-qa-storage-config.py" --compose-file "$COMPOSE_FILE" --env-file "$ENV_FILE"; then
    qa_silo_require_bundle
    ok "Configuración S3 sin imágenes mutables ni credenciales administrativas reutilizadas; IAM/proveedor pendientes de aceptación"
  else
    fail "Configuración de almacenamiento bloqueada; revisar contrato #470/#479 antes de instalar"
  fi

  python3 - <<'PY'
import os, ipaddress
from ipaddress import ip_network
bind = os.environ.get("PMGM_QA_BIND_ADDRESS", "127.0.0.1")
try:
    address = ipaddress.ip_address(bind)
except ValueError:
    raise SystemExit("QA requiere IP concreta de enlace privado")
private_ranges = [ip_network(v) for v in ("10.0.0.0/8", "172.16.0.0/12", "192.168.0.0/16")]
if not (address.is_loopback or any(address in n for n in private_ranges)):
    raise SystemExit("QA HTTP debe enlazar loopback o IPv4 privada RFC1918, no wildcard ni IP pública")
names = [
    "PMGM_QA_DB_PASSWORD",
    "PMGM_QA_STORAGE_ADMIN_PASSWORD",
    "PMGM_QA_DOCUMENT_SECRET_KEY",
    "PMGM_QA_BACKUP_SECRET_KEY",
    "PMGM_QA_KEYCLOAK_ADMIN_PASSWORD",
    "PMGM_QA_USER_PASSWORD",
]
values = [os.environ.get(name, "") for name in names]
for name, value in zip(names, values):
    if len(value) < 16:
        raise SystemExit(f"{name} debe tener al menos 16 caracteres")
if len(set(values)) != len(values):
    raise SystemExit("Las contraseñas QA deben ser distintas entre sí")
identities = [os.environ.get(k, "") for k in ("PMGM_QA_STORAGE_ADMIN_USER", "PMGM_QA_DOCUMENT_ACCESS_KEY", "PMGM_QA_BACKUP_ACCESS_KEY")]
if len(set(identities)) != len(identities):
    raise SystemExit("La identidad documental no puede ser root/admin")
if any(identity in values for identity in identities):
    raise SystemExit("Identidad de cuenta no debe repetirse como contraseña")
if any(len(i) < 8 for i in identities):
    raise SystemExit("Identidades QA de almacenamiento deben tener mínimo 8 caracteres")
PY

fi

printf '\nResumen: %d fallo(s), %d advertencia(s).\n' "$failures" "$warnings"
[ "$failures" -eq 0 ] || exit 1
printf 'PREFLIGHT QA SRV01: PASS\n'
