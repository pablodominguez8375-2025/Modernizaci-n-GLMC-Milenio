#!/usr/bin/env python3
"""Set a NEW secret for one existing synthetic QA administrator; never log the password.

Sponsor must first generate/save password in Bitwarden, then enter it in a
hidden terminal prompt. Operates only on qa.admin and never on production.
Never supply passwords as command line flags, Docker environment, or GitHub.
"""
import getpass
import json
import os
import re
import socket
import subprocess
import sys

TARGET_USERNAME = "qa.admin"
TARGET_ID = "aa820001-0000-4000-8000-000000000001"
CONTAINER_SERVICE = "keycloak"
BANNER = "QA_PASSWORD_RESET_ADMIN"
# Executed INSIDE existing Keycloak container, never from the public interface.
# No mutable target supplied by the user.
CONTAINER_COMMAND = r'''
set -eu
umask 077
CLI=/opt/keycloak/bin/kcadm.sh
[ -x "$CLI" ] || { echo "PRECHECK_KCADM"; exit 10; }
[ "$KC_BOOTSTRAP_ADMIN_USERNAME" = "qa-keycloak-admin" ] &&
[ -n "$KC_BOOTSTRAP_ADMIN_PASSWORD" ] ||
  { echo "PRECHECK_ADMIN_MISSING"; exit 11; }
cfg="$(mktemp /tmp/pmgm-qa-kcadm.XXXXXXXX)"
trap 'rm -f "$cfg"' EXIT
KC_CLI_PASSWORD="$KC_BOOTSTRAP_ADMIN_PASSWORD" \
  "$CLI" config credentials \
  --server http://127.0.0.1:8080/identity \
  --realm master --user "$KC_BOOTSTRAP_ADMIN_USERNAME" \
  --config "$cfg" >/dev/null ||
  { echo "PRECHECK_AUTH_DENIED"; exit 12; }
target="$("$CLI" get users/aa820001-0000-4000-8000-000000000001 \
  -r pmgm --fields username,enabled --config "$cfg")" ||
  { echo "PRECHECK_USER_MISSING"; exit 13; }
printf '%s\n' "$target" |
  grep -Eq '"username"[[:space:]]*:[[:space:]]*"qa[.]admin"' ||
  { echo "PRECHECK_WRONG_USER"; exit 14; }
printf '%s\n' "$target" |
  grep -Eq '"enabled"[[:space:]]*:[[:space:]]*true' ||
  { echo "PRECHECK_USER_DISABLED"; exit 15; }
# Solo el admin sintético preexistente, con rol del sistema.
roles="$("$CLI" get users/aa820001-0000-4000-8000-000000000001/role-mappings/realm \
  -r pmgm --fields name --config "$cfg")" ||
  { echo "PRECHECK_ADMIN_ROLE"; exit 17; }
printf '%s\n' "$roles" |
  grep -Eq '"name"[[:space:]]*:[[:space:]]*"platform_superadmin"' ||
  { echo "PRECHECK_ADMIN_ROLE"; exit 17; }
# The new secret travels ONLY over docker-exec stdin, as JSON to the REST API.
# -n prevents a GET against the write-only reset-password endpoint.
"$CLI" update users/aa820001-0000-4000-8000-000000000001/reset-password \
  -r pmgm -f - -n --config "$cfg" >/dev/null ||
  { echo "RESET_DENIED"; exit 16; }
echo "RESET_QA_ADMIN_OK"
'''


def fail(message):
    print(BANNER + ": " + message, file=sys.stderr)
    return 1


def main():
    if os.geteuid() != 0:
        return fail("BLOQUEADO: ejecutar con sudo python3 (solo QA).")
    if len(sys.argv) != 1:
        return fail("BLOQUEADO: no se admiten argumentos.")
    if socket.gethostname().split(".")[0] != "glmdch":
        return fail("BLOQUEADO: solo VM glmdch QA.")
    try:
        found = subprocess.run(
            ["docker", "ps", "--filter", "label=com.docker.compose.service=" + CONTAINER_SERVICE,
             "--format", "{{.ID}}"],
            capture_output=True, text=True, timeout=12, check=True)
    except (OSError, subprocess.SubprocessError):
        return fail("BLOQUEADO: no se pudo inspeccionar Docker.")
    ids = [line.strip() for line in found.stdout.splitlines() if line.strip()]
    if len(ids) != 1 or not re.fullmatch(r"[a-f0-9]{12,64}", ids[0]):
        return fail("BLOQUEADO: se esperaba exactamente un Keycloak QA activo.")
    container_id = ids[0]
    try:
        pw = getpass.getpass(
            "Pega la clave NUEVA que guardaste en Bitwarden para qa.admin (oculta): ")
    except (EOFError, KeyboardInterrupt, OSError):
        return fail("CANCELADO: no se leyó contraseña; sin cambios.")
    if (len(pw) < 20
            or not re.search(r"[A-Z]", pw)
            or not re.search(r"[a-z]", pw)
            or not re.search(r"[0-9]", pw)
            or not re.search(r"[^a-zA-Z0-9]", pw)
            or TARGET_USERNAME.lower() in pw.lower()):
        return fail("BLOQUEADO: exige 20+ caracteres, mayúscula, minúscula, número y símbolo.")
    payload = json.dumps(
        {"type": "password", "temporary": False, "value": pw},
        ensure_ascii=True, separators=(",", ":")).encode("ascii")
    del pw
    try:
        result = subprocess.run(
            ["docker", "exec", "-i", container_id, "/bin/sh", "-c", CONTAINER_COMMAND],
            input=payload, capture_output=True, timeout=150, check=False)
    except (OSError, subprocess.SubprocessError):
        return fail("BLOQUEADO: Keycloak no confirmó el cambio.")
    # Never print CLI stdout/stderr unfiltered: may contain sensitive diagnostics.
    if result.returncode != 0 or b"RESET_QA_ADMIN_OK" not in result.stdout:
        # Omit raw errors intentionally; code and known stage are safe to report.
        safe_stage = "ERROR"
        for stage in ("PRECHECK_KCADM", "PRECHECK_ADMIN_MISSING",
                      "PRECHECK_AUTH_DENIED", "PRECHECK_USER_MISSING",
                      "PRECHECK_WRONG_USER", "PRECHECK_USER_DISABLED",
                      "PRECHECK_ADMIN_ROLE",
                      "RESET_DENIED"):
            if stage.encode() in result.stdout:
                safe_stage = stage
                break
        return fail("BLOQUEADO: " + safe_stage + " (sin mostrar secretos).")
    print(BANNER + ": RESET_QA_ADMIN_OK")
    print("Inicia sesión con qa.admin y tu clave guardada en Bitwarden.")
    print("No pegues ni compartas esa clave en chats, GitHub o Drive.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
