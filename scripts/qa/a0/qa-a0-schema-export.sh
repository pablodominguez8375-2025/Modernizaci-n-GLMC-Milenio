#!/usr/bin/env bash
# A0b (Talleres #1035): exporta SOLO el esquema (sin datos) a un directorio root-only del host. NO sube nada a Drive.
# v3 (corrige NO-GO 7009939051 y 7010295218). Modos (variables de entorno):
#   Exportar:   CONFIRM_EXPORT=EXPORT_SCHEMA_ONLY ./qa-a0-schema-export.sh
#   Estado:     STATUS=1 ./qa-a0-schema-export.sh                   (lista exportaciones existentes con su marcador; no borra)
#   Borrar:     CLEANUP_DIR=/var/tmp/qa525-a0-schema.XXXXXX ./qa-a0-schema-export.sh
# RETENCIÓN: MANUAL. No hay borrado automático ni plazo garantizado: el Sponsor debe ejecutar el modo Borrar apenas termine el uso autorizado.
# Cada exportación lleva un MARCADOR root-only (.qa525-a0-marker) con identidad, fecha y hash; el borrado solo procede si el marcador, el
# propietario, los modos, el contenido exacto del directorio y el hash del volcado coinciden. Sin `rm -r`. Borrado por descriptores (dirfd) con Python 3 (anti-TOCTOU).
# EL EXPORT A0b SIGUE NO AUTORIZADO hasta dictamen exact-hash de ChatGPT y autorización literal del Sponsor.
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
# TOCTOU: el borrado NO opera por ruta. Python abre el directorio con O_NOFOLLOW|O_DIRECTORY y TODO lo demás (fstat, listdir, apertura de archivos con
# O_NOFOLLOW, lectura/hash, unlink, rmdir) es relativo a esos descriptores (dirfd). Un cambio de la ruta después de abrir no redirige nada; ante cualquier
# discrepancia: STOP sin borrar (fail-closed). Amenaza fuera de alcance: otro proceso root modificando el directorio (el directorio es root:root 0700).
if [ -n "${CLEANUP_DIR:-}" ]; then
  printf '%s' "$CLEANUP_DIR" | grep -Eq '^/var/tmp/qa525-a0-schema\.[A-Za-z0-9]{6}$' || stop "CLEANUP_DIR no es un directorio de esta exportación"
  PYCODE="$(cat <<'PY'
import os, sys, stat, hashlib
D = os.environ["D"]
def bad(msg):
    sys.stderr.write("STOP: " + msg + "\n"); sys.exit(2)
parent, name = os.path.split(D)
if parent != "/var/tmp" or not name.startswith("qa525-a0-schema."): bad("ruta fuera de lo permitido")
try:
    pfd = os.open(parent, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW)
    dfd = os.open(name, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW, dir_fd=pfd)
except OSError:
    bad("el directorio no existe, no es directorio o es un enlace simbólico")
st = os.fstat(dfd)
if (st.st_uid, st.st_gid, stat.S_IMODE(st.st_mode)) != (0, 0, 0o700): bad("propietario/modo del directorio no es root:root:700")
names = sorted(os.listdir(dfd))
if names != [".qa525-a0-marker", "qa-schema-only.sql"]: bad("el contenido no es exactamente marcador + volcado")
fds = {}
for n in names:
    try: fd = os.open(n, os.O_RDONLY | os.O_NOFOLLOW, dir_fd=dfd)
    except OSError: bad(n + ": no se pudo abrir sin seguir enlaces")
    s = os.fstat(fd)
    if not stat.S_ISREG(s.st_mode) or (s.st_uid, s.st_gid, stat.S_IMODE(s.st_mode), s.st_nlink) != (0, 0, 0o600, 1): bad(n + ": no es archivo regular root:root:600 con 1 enlace")
    fds[n] = (fd, s)
def read_all(fd, limit=1 << 20):
    out = b""
    while True:
        chunk = os.read(fd, 65536)
        if not chunk: return out
        out += chunk
        if len(out) > limit: bad("marcador demasiado grande")
marker = {}
for line in read_all(fds[".qa525-a0-marker"][0]).decode("utf-8", "strict").splitlines():
    if "=" in line:
        k, v = line.split("=", 1); marker[k] = v
if marker.get("schema") != "qa525-a0-export-v1": bad("marcador sin schema esperado")
if marker.get("host") != "glmdch": bad("marcador de otro host")
if marker.get("dir") != D: bad("el marcador pertenece a otro directorio")
if marker.get("dump_name") != "qa-schema-only.sql": bad("marcador con nombre de volcado inesperado")
want = marker.get("dump_sha256", "")
if len(want) != 64 or any(c not in "0123456789abcdef" for c in want): bad("marcador sin hash válido")
h = hashlib.sha256(); size = 0
vfd = fds["qa-schema-only.sql"][0]
while True:
    chunk = os.read(vfd, 1 << 20)
    if not chunk: break
    h.update(chunk); size += len(chunk)
if h.hexdigest() != want: bad("el hash del volcado no coincide con el marcador")
if str(size) != marker.get("dump_bytes"): bad("el tamaño del volcado no coincide con el marcador")
for n in names: os.unlink(n, dir_fd=dfd)
for n in names: os.close(fds[n][0])
cur = os.stat(name, dir_fd=pfd, follow_symlinks=False)
if (cur.st_dev, cur.st_ino) != (st.st_dev, st.st_ino): bad("el directorio fue reemplazado durante el borrado; no se elimina")
os.rmdir(name, dir_fd=pfd)
try:
    os.stat(name, dir_fd=pfd, follow_symlinks=False); bad("no se pudo verificar el borrado")
except FileNotFoundError:
    pass
print("verificado: directorio borrado")
PY
)" || stop "no se pudo preparar el borrado"
  sudo -n env -i PATH="$PATH" D="$CLEANUP_DIR" python3 -I -c "$PYCODE" || stop "no se borró $CLEANUP_DIR"
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
