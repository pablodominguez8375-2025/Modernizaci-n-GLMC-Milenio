#!/usr/bin/env bash
# A0b: exporta SOLO el esquema (sin datos) a un archivo root-only 0600 en el host. NO sube nada a Drive. Requiere confirmación explícita.
# Sensible: el esquema puede traer defaults/comentarios/definiciones. No se imprime su contenido.
set -u
umask 077
stop() { echo "STOP: $*" >&2; exit 2; }
[ "${CONFIRM_EXPORT:-}" = "EXPORT_SCHEMA_ONLY" ] || stop "falta CONFIRM_EXPORT=EXPORT_SCHEMA_ONLY"
[ "$(hostname)" = "glmdch" ] || stop "hostname=$(hostname), se esperaba glmdch"
[ "$(id -un)" = "soporte" ] || stop "usuario=$(id -un), se esperaba soporte"
sudo -n true 2>/dev/null || stop "sudo -n pide clave"
mapfile -t DBS < <(sudo -n docker ps --format '{{.Names}} {{.Image}}' | awk '$2 ~ /postgres/ {print $1}')
[ "${#DBS[@]}" -eq 1 ] || stop "contenedores postgres encontrados: ${#DBS[@]} (se esperaba 1)"
export DB="${DBS[0]}"
sudo -n -E bash -c '
  set -eu; umask 077
  d="$(mktemp -d /var/tmp/qa525-a0-schema.XXXXXX)"; chmod 0700 "$d"
  f="$d/qa-schema-only.sql"
  docker exec -i -e PGOPTIONS="-c default_transaction_read_only=on" "$DB" \
    sh -c '"'"'pg_dump -U "$POSTGRES_USER" -d "${POSTGRES_DB:-$POSTGRES_USER}" --schema-only --no-owner --no-privileges'"'"' > "$f"
  chmod 0600 "$f"
  [ -s "$f" ] || { echo "STOP: volcado vacío" >&2; exit 2; }
  echo "ARCHIVO=$f"
  stat -c "modo=%a dueno=%U tamano_bytes=%s" "$f"
  echo "lineas=$(wc -l < "$f")"
  sha256sum "$f"
' || stop "la exportación falló"
echo "A0_EXPORT=DONE (solo en el host; sin copia a Drive). Para borrar después: sudo -n rm -r <directorio mostrado en ARCHIVO>"
