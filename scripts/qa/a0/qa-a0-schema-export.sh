#!/usr/bin/env bash
# A0b (Talleres #1035): exporta SOLO el esquema (sin datos) a un archivo root-only 0600 en el host. NO sube nada a Drive.
# v2 (corrige NO-GO 7009939051): pipefail, entorno limpio para sudo (env -i), captura comprobada, trap de limpieza ante error,
# archivo 0600 desde su creación, retención explícita (borrado con CLEANUP_DIR). No imprime contenido ni errores del volcado.
#   Exportar:  CONFIRM_EXPORT=EXPORT_SCHEMA_ONLY ./qa-a0-schema-export.sh
#   Borrar:    CLEANUP_DIR=/var/tmp/qa525-a0-schema.XXXXXX ./qa-a0-schema-export.sh
set -u
set -o pipefail
umask 077
stop() { echo "STOP: $*" >&2; exit 2; }
PATH=/usr/sbin:/usr/bin:/sbin:/bin; export PATH

[ "$(hostname)" = "glmdch" ] || stop "hostname distinto de glmdch"
[ "$(id -un)" = "soporte" ] || stop "usuario distinto de soporte"
sudo -n true >/dev/null 2>&1 || stop "sudo -n pide clave"

if [ -n "${CLEANUP_DIR:-}" ]; then
  printf '%s' "$CLEANUP_DIR" | grep -Eq '^/var/tmp/qa525-a0-schema\.[A-Za-z0-9]{6}$' || stop "CLEANUP_DIR no es un directorio de esta exportación"
  sudo -n env -i PATH="$PATH" D="$CLEANUP_DIR" bash -c 'set -eu; [ -d "$D" ] && [ ! -L "$D" ]; rm -r -- "$D"' || stop "no se pudo borrar $CLEANUP_DIR"
  echo "A0_CLEANUP=DONE"; exit 0
fi

[ "${CONFIRM_EXPORT:-}" = "EXPORT_SCHEMA_ONLY" ] || stop "falta CONFIRM_EXPORT=EXPORT_SCHEMA_ONLY"

PS="$(sudo -n docker ps --format '{{.Names}} {{.Image}}' 2>/dev/null)" || stop "docker ps falló"
DBLIST="$(printf '%s\n' "$PS" | awk '$2 ~ /postgres/ {print $1}')"
[ -n "$DBLIST" ] || stop "no hay contenedor postgres"
[ "$(printf '%s\n' "$DBLIST" | wc -l)" -eq 1 ] || stop "hay más de un contenedor postgres"
DB="$DBLIST"
printf '%s' "$DB" | grep -Eq '^[A-Za-z0-9_.-]+$' || stop "nombre de contenedor de BD no válido"

sudo -n env -i PATH="$PATH" DB="$DB" bash -c '
  set -eu; set -o pipefail; umask 077
  d="$(mktemp -d /var/tmp/qa525-a0-schema.XXXXXX)"
  trap '"'"'rc=$?; if [ "$rc" -ne 0 ]; then rm -rf -- "$d"; echo "STOP: exportación fallida, directorio temporal borrado" >&2; fi'"'"' EXIT
  chmod 0700 "$d"
  f="$d/qa-schema-only.sql"
  install -m 0600 /dev/null "$f"
  docker exec -i -e PGOPTIONS="-c default_transaction_read_only=on -c statement_timeout=120000" "$DB" \
    sh -c '"'"'pg_dump -U "$POSTGRES_USER" -d "${POSTGRES_DB:-$POSTGRES_USER}" --schema-only --no-owner --no-privileges'"'"' >"$f" 2>/dev/null
  [ -s "$f" ] || exit 2
  [ "$(stat -c %a "$f")" = "600" ] || exit 2
  echo "DIRECTORIO=$d"
  stat -c "modo=%a dueno=%U tamano_bytes=%s" "$f"
  echo "lineas=$(wc -l < "$f")"
  sha256sum "$f"
' || stop "la exportación falló"
echo "A0_EXPORT=DONE (solo en el host; sin copia a Drive; retención máx. 24 h). Borrar: CLEANUP_DIR=<DIRECTORIO mostrado> + este script"
