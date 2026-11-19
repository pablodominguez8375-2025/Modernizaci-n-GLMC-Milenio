#!/usr/bin/env bash
# A0b (Talleres #1035): exporta SOLO el esquema (sin datos) a un directorio root-only del host. NO sube nada a Drive.
# v3 (corrige NO-GO 7009939051 y 7010295218). Modos (variables de entorno):
#   Exportar:   CONFIRM_EXPORT=EXPORT_SCHEMA_ONLY ./qa-a0-schema-export.sh
#   Estado:     STATUS=1 ./qa-a0-schema-export.sh                   (lista exportaciones existentes con su marcador; no borra)
#   Borrar:     CLEANUP_DIR=/var/tmp/qa525-a0-schema.XXXXXX ./qa-a0-schema-export.sh
# RETENCIÓN: MANUAL. No hay borrado automático ni plazo garantizado: el Sponsor debe ejecutar el modo Borrar apenas termine el uso autorizado.
# Cada exportación lleva un MARCADOR root-only (.qa525-a0-marker) con identidad, fecha y hash; el borrado solo procede si el marcador, el
# propietario, los modos, el contenido exacto del directorio y el hash del volcado coinciden. Sin `rm -r`: se borran SOLO los dos archivos por nombre y rmdir.
# Modo de errores: `set -eEu -o pipefail` + `trap ERR`; además cada paso crítico lleva `|| stop` y tiene prueba negativa en tests/test-a0.sh.
set -eEu -o pipefail
umask 077
stop() { echo "STOP: $*" >&2; exit 2; }
trap 'stop "error inesperado en la línea $LINENO"' ERR
PATH=/usr/sbin:/usr/bin:/sbin:/bin; export PATH

[ "$(hostname)" = "glmdch" ] || stop "hostname distinto de glmdch"
[ "$(id -un)" = "soporte" ] || stop "usuario distinto de soporte"
sudo -n true >/dev/null 2>&1 || stop "sudo -n pide clave"

# ---------- Modo Estado ----------
if [ "${STATUS:-}" = "1" ]; then
  sudo -n env -i PATH="$PATH" bash -c '
    set -eu
    n=0
    for d in /var/tmp/qa525-a0-schema.??????; do
      [ -e "$d" ] || continue; n=$((n+1))
      if [ -L "$d" ]; then echo "$d: ENLACE SIMBOLICO (no tocar)"; continue; fi
      echo "$d: $(stat -c "dueno=%U modo=%a" "$d")"
      if [ -f "$d/.qa525-a0-marker" ] && [ ! -L "$d/.qa525-a0-marker" ]; then grep -E "^(created_utc|dump_sha256|dump_bytes)=" "$d/.qa525-a0-marker" | sed "s/^/   /"; else echo "   SIN MARCADOR (no es una exportación verificable)"; fi
    done
    echo "exportaciones_encontradas=$n"
  ' || stop "no se pudo listar"
  exit 0
fi

# ---------- Modo Borrar ----------
if [ -n "${CLEANUP_DIR:-}" ]; then
  printf '%s' "$CLEANUP_DIR" | grep -Eq '^/var/tmp/qa525-a0-schema\.[A-Za-z0-9]{6}$' || stop "CLEANUP_DIR no es un directorio de esta exportación"
  sudo -n env -i PATH="$PATH" D="$CLEANUP_DIR" bash -c '
    set -eu
    bad() { echo "STOP: $*" >&2; exit 2; }
    [ -d "$D" ] || bad "no existe el directorio"
    [ ! -L "$D" ] || bad "es un enlace simbólico"
    [ "$(realpath -e -- "$D")" = "$D" ] || bad "la ruta real difiere (posible sustitución)"
    [ "$(stat -c "%U:%G:%a" "$D")" = "root:root:700" ] || bad "propietario/modo del directorio no es root:root:700"
    cd -P -- "$D"
    [ "$(pwd -P)" = "$D" ] || bad "cwd real difiere"
    [ "$(ls -A | LC_ALL=C sort | tr "\n" " ")" = ".qa525-a0-marker qa-schema-only.sql " ] || bad "el contenido del directorio no es exactamente marcador + volcado"
    for f in .qa525-a0-marker qa-schema-only.sql; do
      [ -f "$f" ] && [ ! -L "$f" ] || bad "$f no es un archivo regular"
      [ "$(stat -c "%U:%G:%a:%h" "$f")" = "root:root:600:1" ] || bad "$f: propietario/modo/enlaces no son root:root:600:1"
    done
    grep -qx "schema=qa525-a0-export-v1" .qa525-a0-marker || bad "marcador sin schema esperado"
    grep -qx "host=glmdch" .qa525-a0-marker || bad "marcador de otro host"
    grep -qx "dir=$D" .qa525-a0-marker || bad "el marcador pertenece a otro directorio"
    want="$(sed -n "s/^dump_sha256=//p" .qa525-a0-marker)"
    printf "%s" "$want" | grep -Eq "^[0-9a-f]{64}$" || bad "marcador sin hash válido"
    have="$(sha256sum qa-schema-only.sql | cut -d" " -f1)"
    [ "$want" = "$have" ] || bad "el hash del volcado no coincide con el marcador"
    rm -f -- qa-schema-only.sql .qa525-a0-marker
    cd /
    rmdir -- "$D"
    [ ! -e "$D" ] || bad "no se pudo verificar el borrado"
  ' || stop "no se borró $CLEANUP_DIR"
  echo "A0_CLEANUP=DONE (verificado: el directorio ya no existe)"; exit 0
fi

# ---------- Modo Exportar ----------
[ "${CONFIRM_EXPORT:-}" = "EXPORT_SCHEMA_ONLY" ] || stop "falta CONFIRM_EXPORT=EXPORT_SCHEMA_ONLY"
PS="$(sudo -n docker ps --format '{{.Names}} {{.Image}}' 2>/dev/null)" || stop "docker ps falló"
DBLIST="$(printf '%s\n' "$PS" | awk '$2 ~ /postgres/ {print $1}')" || stop "no se pudo analizar docker ps"
[ -n "$DBLIST" ] || stop "no hay contenedor postgres"
[ "$(printf '%s\n' "$DBLIST" | wc -l)" -eq 1 ] || stop "hay más de un contenedor postgres"
DB="$DBLIST"
printf '%s' "$DB" | grep -Eq '^[A-Za-z0-9_.-]+$' || stop "nombre de contenedor de BD no válido"

sudo -n env -i PATH="$PATH" DB="$DB" CREATED_BY="$(id -un)" bash -c '
  set -eu -o pipefail; umask 077
  d="$(mktemp -d /var/tmp/qa525-a0-schema.XXXXXX)"
  trap '"'"'rc=$?; if [ "$rc" -ne 0 ]; then rm -f -- "$d/qa-schema-only.sql" "$d/.qa525-a0-marker"; rmdir -- "$d" 2>/dev/null || true; echo "STOP: exportación fallida, directorio temporal borrado" >&2; fi'"'"' EXIT
  chmod 0700 "$d"
  f="$d/qa-schema-only.sql"; m="$d/.qa525-a0-marker"
  install -m 0600 /dev/null "$f"
  docker exec -i -e PGOPTIONS="-c default_transaction_read_only=on -c statement_timeout=120000" "$DB" \
    sh -c '"'"'pg_dump -U "$POSTGRES_USER" -d "${POSTGRES_DB:-$POSTGRES_USER}" --schema-only --no-owner --no-privileges'"'"' >"$f" 2>/dev/null
  [ -s "$f" ] || exit 2
  [ "$(stat -c %a "$f")" = "600" ] || exit 2
  sum="$(sha256sum "$f" | cut -d" " -f1)"
  install -m 0600 /dev/null "$m"
  {
    echo "schema=qa525-a0-export-v1"; echo "host=glmdch"; echo "dir=$d"; echo "created_utc=$(date -u +%FT%TZ)"
    echo "created_by=$CREATED_BY"; echo "dump_name=qa-schema-only.sql"; echo "dump_sha256=$sum"; echo "dump_bytes=$(stat -c %s "$f")"
    echo "nonce=$(head -c 16 /dev/urandom | od -An -tx1 | tr -d " \n")"
  } > "$m"
  echo "DIRECTORIO=$d"
  stat -c "modo=%a dueno=%U tamano_bytes=%s" "$f"
  echo "lineas=$(wc -l < "$f")"
  echo "sha256=$sum"
' || stop "la exportación falló"
echo "A0_EXPORT=DONE (solo en el host; sin copia a Drive; retención MANUAL, sin plazo garantizado). Estado: STATUS=1; borrar: CLEANUP_DIR=<DIRECTORIO mostrado>"
