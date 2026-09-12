#!/usr/bin/env bash
set -euo pipefail

ROOT="$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)"
ENV_FILE="${PMGM_PILOT_ENV_FILE:-/etc/pmgm/pmgm.env}"

failures=0
warnings=0

ok() { printf 'OK   %s\n' "$*"; }
warn() { printf 'WARN %s\n' "$*" >&2; warnings=$((warnings+1)); }
fail() { printf 'FAIL %s\n' "$*" >&2; failures=$((failures+1)); }

printf 'Proyecto Milenio — preflight VM institucional definitiva\n\n'

for cmd in docker git curl openssl python3 sha256sum tar; do
  if command -v "$cmd" >/dev/null 2>&1; then ok "Comando disponible: $cmd"; else fail "Falta comando obligatorio: $cmd"; fi
done

if command -v docker >/dev/null 2>&1; then
  if docker compose version >/dev/null 2>&1; then ok "Docker Compose v2 disponible"; else fail "Docker Compose v2 no disponible"; fi
  if docker info >/dev/null 2>&1; then ok "Docker daemon accesible"; else fail "El usuario actual no puede acceder al Docker daemon"; fi
fi

arch="$(uname -m 2>/dev/null || true)"
case "$arch" in
  x86_64|amd64) ok "Arquitectura x86_64" ;;
  *) warn "Arquitectura detectada: ${arch:-desconocida}. El paquete recomendado es x86_64." ;;
esac

if [ -r /proc/meminfo ]; then
  mem_kib="$(awk '/MemTotal:/ {print $2}' /proc/meminfo)"
  mem_gib=$((mem_kib / 1024 / 1024))
  if [ "$mem_gib" -ge 30 ]; then ok "RAM instalada ~${mem_gib} GiB (recomendado cumplido)"
  elif [ "$mem_gib" -ge 15 ]; then warn "RAM instalada ~${mem_gib} GiB: válida para UAT, bajo el recomendado definitivo de 32 GiB"
  else fail "RAM instalada ~${mem_gib} GiB: menor al mínimo UAT de 16 GiB"
  fi
fi

cpu_count="$(getconf _NPROCESSORS_ONLN 2>/dev/null || nproc 2>/dev/null || echo 0)"
if [ "$cpu_count" -ge 8 ]; then ok "vCPU disponibles: $cpu_count (recomendado cumplido)"
elif [ "$cpu_count" -ge 4 ]; then warn "vCPU disponibles: $cpu_count: válido para UAT, bajo el recomendado definitivo de 8"
else fail "vCPU disponibles: $cpu_count: menor al mínimo UAT de 4"
fi

root_free_kib="$(df -Pk "$ROOT" 2>/dev/null | awk 'NR==2 {print $4}' || echo 0)"
root_free_gib=$((root_free_kib / 1024 / 1024))
if [ "$root_free_gib" -ge 300 ]; then ok "Espacio libre en filesystem de aplicación ~${root_free_gib} GiB"
elif [ "$root_free_gib" -ge 100 ]; then warn "Espacio libre ~${root_free_gib} GiB: suficiente para arranque/UAT, menor al margen definitivo recomendado"
else fail "Espacio libre ~${root_free_gib} GiB: insuficiente para operación segura"
fi

if [ -f "$ENV_FILE" ]; then
  ok "Archivo de entorno encontrado: $ENV_FILE"
  perms="$(stat -c '%a' "$ENV_FILE" 2>/dev/null || true)"
  if [ "$perms" = "600" ] || [ "$perms" = "640" ]; then ok "Permisos restrictivos del archivo de entorno: $perms"
  else warn "Permisos de $ENV_FILE = ${perms:-desconocidos}; se recomienda 600 (o 640 con grupo operacional controlado)"
  fi
else
  fail "No existe el archivo de entorno esperado: $ENV_FILE"
fi

if [ -f "$ENV_FILE" ]; then
  set -a
  # shellcheck disable=SC1090
  . "$ENV_FILE"
  set +a

  public_url="${PMGM_PILOT_PUBLIC_URL:-}"
  hostname="${PMGM_PILOT_HOSTNAME:-}"
  if [[ "$public_url" == https://* ]]; then ok "URL pública usa HTTPS: $public_url"; else fail "PMGM_PILOT_PUBLIC_URL debe usar HTTPS"; fi
  if [ -n "$hostname" ]; then
    if getent ahosts "$hostname" >/dev/null 2>&1; then ok "DNS resuelve: $hostname"; else warn "DNS aún no resuelve desde este host: $hostname"; fi
  else
    fail "PMGM_PILOT_HOSTNAME no está configurado"
  fi
  if [ "${PMGM_PILOT_ALLOW_LOCALHOST:-false}" = "false" ]; then ok "Localhost deshabilitado"; else fail "PMGM_PILOT_ALLOW_LOCALHOST debe ser false en VM institucional"; fi
  if [ "${PMGM_PILOT_INSECURE_TLS:-false}" = "false" ]; then ok "TLS inseguro deshabilitado"; else fail "PMGM_PILOT_INSECURE_TLS debe ser false"; fi
fi

if command -v ss >/dev/null 2>&1; then
  for port in 5432 9000 9001 3310 8080; do
    if ss -lntH | awk '{print $4}' | grep -Eq "(^|:)$port$"; then warn "Puerto interno $port aparece escuchando en el host; verificar que no esté publicado externamente"
    else ok "Puerto interno $port no aparece publicado en host"
    fi
  done
fi

if [ -d "$ROOT/.git" ]; then
  current_sha="$(git -C "$ROOT" rev-parse HEAD 2>/dev/null || true)"
  ok "Repositorio Git detectado; HEAD=${current_sha:-desconocido}"
else
  warn "No se detecta .git en $ROOT; la actualización por referencia Git no estará disponible"
fi

printf '\nResumen: %d fallo(s), %d advertencia(s).\n' "$failures" "$warnings"
if [ "$failures" -gt 0 ]; then
  exit 1
fi

printf 'PREFLIGHT HOST DEFINITIVO: PASS\n'
