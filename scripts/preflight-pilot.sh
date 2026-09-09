#!/usr/bin/env bash
set -euo pipefail

ROOT="$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)"
COMPOSE_FILE="$ROOT/infrastructure/docker-compose.pilot.yml"
ENV_FILE="${PMGM_PILOT_ENV_FILE:-$ROOT/infrastructure/.env.pilot.local}"
REALM_FILE="$ROOT/infrastructure/keycloak/pmgm-pilot-realm.json"

command -v docker >/dev/null 2>&1 || { echo "Docker es obligatorio." >&2; exit 1; }
command -v python3 >/dev/null 2>&1 || { echo "Python 3 es obligatorio." >&2; exit 1; }
docker compose version >/dev/null 2>&1 || { echo "Docker Compose es obligatorio." >&2; exit 1; }

[ -f "$ENV_FILE" ] || {
  echo "No existe $ENV_FILE" >&2
  echo "Copie infrastructure/pilot.env.example a infrastructure/.env.pilot.local y complete sus valores." >&2
  exit 1
}

set -a
# Archivo local administrado por el operador del piloto.
# shellcheck disable=SC1090
. "$ENV_FILE"
set +a

required=(
  PMGM_PILOT_HOSTNAME PMGM_PILOT_PUBLIC_URL
  PMGM_PILOT_DB_PASSWORD PMGM_PILOT_MINIO_USER PMGM_PILOT_MINIO_PASSWORD
  PMGM_PILOT_KEYCLOAK_ADMIN_USERNAME PMGM_PILOT_KEYCLOAK_ADMIN_PASSWORD
  PMGM_PILOT_ADMIN_USERNAME PMGM_PILOT_ADMIN_EMAIL PMGM_PILOT_ADMIN_PASSWORD
)
for name in "${required[@]}"; do
  if [ -z "${!name:-}" ]; then
    echo "$name es obligatorio." >&2
    exit 1
  fi
done

ENV_FILE="$ENV_FILE" REALM_FILE="$REALM_FILE" python3 - <<'PY'
import json
import os
import re
from pathlib import Path
from urllib.parse import urlparse


def fail(message: str) -> None:
    raise SystemExit(message)

hostname = os.environ["PMGM_PILOT_HOSTNAME"].strip().lower()
public_url = os.environ["PMGM_PILOT_PUBLIC_URL"].strip()
allow_local = os.environ.get("PMGM_PILOT_ALLOW_LOCALHOST", "false").lower() == "true"
insecure_tls = os.environ.get("PMGM_PILOT_INSECURE_TLS", "false").lower() == "true"
parsed = urlparse(public_url)

if parsed.scheme != "https":
    fail("PMGM_PILOT_PUBLIC_URL debe usar https://")
if parsed.path not in ("", "/") or parsed.query or parsed.fragment:
    fail("PMGM_PILOT_PUBLIC_URL debe ser sólo el origen HTTPS, sin ruta, query ni fragmento.")
if public_url.endswith("/"):
    fail("PMGM_PILOT_PUBLIC_URL no debe terminar en '/'.")
if not parsed.hostname or parsed.hostname.lower() != hostname:
    fail("PMGM_PILOT_HOSTNAME debe coincidir con el hostname de PMGM_PILOT_PUBLIC_URL.")

local_names = {"localhost", "127.0.0.1", "::1"}
if hostname in local_names and not allow_local:
    fail("localhost sólo está permitido con PMGM_PILOT_ALLOW_LOCALHOST=true para pruebas controladas.")
if insecure_tls and not allow_local:
    fail("PMGM_PILOT_INSECURE_TLS=true sólo se admite junto a PMGM_PILOT_ALLOW_LOCALHOST=true.")

https_port = int(os.environ.get("PMGM_PILOT_HTTPS_PORT", "443"))
http_port = int(os.environ.get("PMGM_PILOT_HTTP_PORT", "80"))
if not (1 <= https_port <= 65535 and 1 <= http_port <= 65535):
    fail("Los puertos del piloto deben estar entre 1 y 65535.")
url_port = parsed.port or 443
if url_port != https_port:
    fail(f"El puerto HTTPS de PMGM_PILOT_PUBLIC_URL ({url_port}) no coincide con PMGM_PILOT_HTTPS_PORT ({https_port}).")

placeholders = ("REEMPLAZAR", "CHANGE-ME", "CAMBIAR", "example.cl")
for key, value in os.environ.items():
    if key.startswith("PMGM_PILOT_") and any(token.lower() in value.lower() for token in placeholders):
        fail(f"{key} todavía contiene un valor de ejemplo/place-holder.")

secret_names = [
    "PMGM_PILOT_DB_PASSWORD",
    "PMGM_PILOT_MINIO_PASSWORD",
    "PMGM_PILOT_KEYCLOAK_ADMIN_PASSWORD",
    "PMGM_PILOT_ADMIN_PASSWORD",
]
secrets = [os.environ[name] for name in secret_names]
for name, value in zip(secret_names, secrets):
    if len(value) < 20:
        fail(f"{name} debe tener al menos 20 caracteres.")
if len(set(secrets)) != len(secrets):
    fail("Las contraseñas del piloto deben ser diferentes entre sí.")

for user_key in ("PMGM_PILOT_KEYCLOAK_ADMIN_USERNAME", "PMGM_PILOT_ADMIN_USERNAME"):
    username = os.environ[user_key].strip()
    if username.lower().startswith("qa.") or username.lower().startswith("qa-"):
        fail(f"{user_key} no puede usar una identidad QA en el piloto.")

email = os.environ["PMGM_PILOT_ADMIN_EMAIL"].strip()
if not re.fullmatch(r"[^@\s]+@[^@\s]+\.[^@\s]+", email):
    fail("PMGM_PILOT_ADMIN_EMAIL no tiene formato válido.")
if email.lower().endswith(".invalid"):
    fail("PMGM_PILOT_ADMIN_EMAIL debe ser un correo operativo, no un dominio .invalid.")

realm = json.loads(Path(os.environ["REALM_FILE"]).read_text(encoding="utf-8"))
if realm.get("realm") != "pmgm" or realm.get("sslRequired") != "external":
    fail("El realm piloto debe ser pmgm y exigir SSL externo.")
clients = [c for c in realm.get("clients", []) if c.get("clientId") == "pmgm-web"]
if len(clients) != 1 or clients[0].get("directAccessGrantsEnabled") is not False:
    fail("El cliente pmgm-web del piloto debe deshabilitar Direct Access Grants.")
users = realm.get("users", [])
if len(users) != 1 or users[0].get("username") != "${PMGM_PILOT_ADMIN_USERNAME}":
    fail("El realm piloto sólo debe incluir el bootstrap institucional parametrizado.")
if any(str(u.get("username", "")).lower().startswith("qa") for u in users):
    fail("El realm piloto no puede contener usuarios QA.")
PY

# La ruta del Caddyfile se interpreta respecto de infrastructure/ por Docker Compose.
caddy_file="${PMGM_PILOT_CADDYFILE:-./caddy/Caddyfile.pilot}"
if [[ "$caddy_file" = /* ]]; then
  resolved_caddy="$caddy_file"
else
  resolved_caddy="$ROOT/infrastructure/${caddy_file#./}"
fi
[ -f "$resolved_caddy" ] || { echo "No existe Caddyfile de piloto: $resolved_caddy" >&2; exit 1; }

# Validación estructural final con las mismas variables que usará el arranque.
docker compose --env-file "$ENV_FILE" -f "$COMPOSE_FILE" config >/dev/null

echo "PREFLIGHT PILOTO OK"
echo "Origen público: $PMGM_PILOT_PUBLIC_URL"
echo "DemoData: desactivado"
echo "Servicios de datos: sin puertos públicos"
