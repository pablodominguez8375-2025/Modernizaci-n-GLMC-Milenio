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
  docker compose version >/dev/null 2>&1 && ok "Docker Compose v2 disponible" || fail "Docker Compose v2 no disponible"
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
  case "$perms" in 600|640) ok "Permisos de entorno: $perms" ;; *) warn "Permisos $perms; se recomienda 600/640" ;; esac
  set -a
  # shellcheck disable=SC1090
  . "$ENV_FILE"
  set +a

  required=(PMGM_QA_WEB_PUBLIC_URL PMGM_QA_OIDC_PUBLIC_URL PMGM_QA_DB_PASSWORD PMGM_QA_MINIO_USER PMGM_QA_MINIO_PASSWORD PMGM_QA_KEYCLOAK_ADMIN_PASSWORD PMGM_QA_USER_PASSWORD)
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

  python3 - <<'PY'
import os
names = [
    "PMGM_QA_DB_PASSWORD",
    "PMGM_QA_MINIO_PASSWORD",
    "PMGM_QA_KEYCLOAK_ADMIN_PASSWORD",
    "PMGM_QA_USER_PASSWORD",
]
values = [os.environ.get(name, "") for name in names]
for name, value in zip(names, values):
    if len(value) < 16:
        raise SystemExit(f"{name} debe tener al menos 16 caracteres")
if len(set(values)) != len(values):
    raise SystemExit("Las contraseñas QA deben ser distintas entre sí")
PY

  docker compose --env-file "$ENV_FILE" -f "$COMPOSE_FILE" config >/dev/null && ok "Docker Compose srv01 válido" || fail "Docker Compose srv01 inválido"
fi

printf '\nResumen: %d fallo(s), %d advertencia(s).\n' "$failures" "$warnings"
[ "$failures" -eq 0 ] || exit 1
printf 'PREFLIGHT QA SRV01: PASS\n'
