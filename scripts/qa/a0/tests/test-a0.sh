#!/usr/bin/env bash
# Pruebas HERMÉTICAS de A0 v3 (no tocan ningún host real): sudo/docker/hostname/id simulados, BD local `a0test`. Debe correr como root con PostgreSQL local en /tmp.
# Uso: bash test-a0.sh
set -u
HERE="$(cd "$(dirname "$0")/.." && pwd)"
PGBIN=/usr/lib/postgresql/16/bin
MOCK="$(mktemp -d)"; chmod 755 "$MOCK"; fails=0; total=0
P() { su postgres -c "$PGBIN/psql -h /tmp -X -q -v ON_ERROR_STOP=1 $*"; }
chk() { total=$((total+1)); if [ "$1" = ok ]; then echo "  PASS: $2"; else echo "  FAIL: $2"; fails=$((fails+1)); [ -n "${3:-}" ] && echo "$3" | tail -6; fi; }
sql() { P -d a0test -c "\"$1\"" >/dev/null; }

P -c "'drop database if exists a0test with (force)'" -c "'create database a0test'" >/dev/null
P -c "'drop role if exists a0app'" -c "'drop role if exists a0owner'" >/dev/null 2>&1
P -c "'create role a0owner'" -c "'create role a0app login'" >/dev/null
sed -n '/^expected_ids() { cat <<.IDS.$/,/^IDS$/p' "$HERE/qa-a0-diag.sh" | sed '1d;$d' > "$MOCK/ids.txt"
[ "$(wc -l < "$MOCK/ids.txt")" -eq 100 ] || { echo "no se pudo extraer la lista de ids"; exit 9; }
{ echo 'create schema core; create table public."__EFMigrationsHistory"("MigrationId" text primary key, "ProductVersion" text);'
  echo 'create table core.organizations("Id" uuid primary key, "Type" text, "Name" text);'
  echo "insert into core.organizations values (gen_random_uuid(),'workshop','T1'),(gen_random_uuid(),'support','S0');"
  echo 'alter table core.organizations owner to a0owner; grant usage on schema core to a0app; grant select,insert,update,delete on core.organizations to a0app;'
  while read -r id; do echo "insert into public.\"__EFMigrationsHistory\" values ('$id','10');"; done < "$MOCK/ids.txt"; } > "$MOCK/seed.sql"
su postgres -c "$PGBIN/psql -h /tmp -X -q -v ON_ERROR_STOP=1 -d a0test -f $MOCK/seed.sql" >/dev/null || { echo "no se pudo sembrar a0test"; exit 9; }
su postgres -c "grep -q a0app /var/tmp/pgdata/pg_hba.conf" || { echo 'local all a0app trust' | cat - /var/tmp/pgdata/pg_hba.conf > "$MOCK/hba" && cp "$MOCK/hba" /var/tmp/pgdata/pg_hba.conf && chown postgres: /var/tmp/pgdata/pg_hba.conf && su postgres -c "$PGBIN/pg_ctl -D /var/tmp/pgdata reload" >/dev/null; }

# --- stubs controlados por archivos ($MOCK/fail, $MOCK/user, $MOCK/sudofail, $MOCK/count) ---
printf '#!/bin/sh\n[ -n "${MOCK_HOST:-}" ] && echo "$MOCK_HOST" || echo glmdch\n' > "$MOCK/hostname"
cat > "$MOCK/id" <<'XEOF'
#!/bin/sh
cat @@M@@/user 2>/dev/null || echo soporte
XEOF
cat > "$MOCK/sudo" <<'XEOF'
#!/bin/bash
[ "$1" = "-n" ] && shift
[ -e @@M@@/sudofail ] && [ "$1" = "true" ] && exit 1
if [ "$1" = "env" ] && [ "$2" = "-i" ]; then shift 2; args=(); for a in "$@"; do case "$a" in PATH=*) args+=("PATH=$MOCKPATH");; *) args+=("$a");; esac; done; exec env -i "${args[@]}"; fi
exec "$@"
XEOF
cat > "$MOCK/docker" <<'XEOF'
#!/bin/bash
F="$(cat @@M@@/fail 2>/dev/null)"
case "$1" in
 ps) [ "$F" = ps ] && exit 1
     if [ "$F" = nodb ]; then echo "pmgm-api pmgm-api:x Up 2 days"; elif [ "$F" = twodb ]; then echo "pmgm-db postgres:16 Up"; echo "otro-db postgres:16 Up"; else echo "pmgm-db postgres:16 Up 2 days"; echo "pmgm-api pmgm-api:x Up 2 days"; fi;;
 image) [ "$F" = image ] && exit 1; echo "pmgm-api:x sha256:abc <none>";;
 exec) shift; while [ "$1" != "sh" ] && [ $# -gt 0 ]; do shift; done; shift; shift; cmd="$1"
   if printf '%s' "$cmd" | grep -q pg_dump; then [ "$F" = pgdump ] && { echo "secreto-que-no-debe-salir" >&2; exit 1; }; fi
   if printf '%s' "$cmd" | grep -q psql; then n=$(( $(cat @@M@@/count 2>/dev/null || echo 0) + 1 )); echo $n > @@M@@/count; [ "$F" = "psql:$n" ] && { echo "secreto-que-no-debe-salir" >&2; exit 1; }; fi
   export POSTGRES_USER=postgres POSTGRES_DB=a0test PGHOST=/tmp; exec sh -c "$cmd";;
esac
XEOF
sed -i "s#@@M@@#$MOCK#g" "$MOCK/id" "$MOCK/sudo" "$MOCK/docker"; chmod +x "$MOCK"/*
export MOCKPATH="$MOCK:$PGBIN:/usr/sbin:/usr/bin:/sbin:/bin"; export PATH="$MOCKPATH"
for f in qa-a0-diag.sh qa-a0-schema-export.sh; do sed 's#^PATH=/usr/sbin:/usr/bin:/sbin:/bin; export PATH$#PATH="$MOCKPATH"; export PATH#' "$HERE/$f" > "$MOCK/$f"; grep -q 'MOCKPATH' "$MOCK/$f" || { echo "no se pudo sustituir PATH en $f"; exit 9; }; done
DIAG="$MOCK/qa-a0-diag.sh"; EXP="$MOCK/qa-a0-schema-export.sh"
reset() { rm -f "$MOCK/fail" "$MOCK/sudofail" "$MOCK/user"; echo 0 > "$MOCK/count"; unset MOCK_HOST; }
run() { echo 0 > "$MOCK/count"; out="$("$@" 2>&1)"; rc=$?; }
SESS=""; start_session() { su postgres -c "$PGBIN/psql -h /tmp -U a0app -d a0test -X -q -c 'select pg_sleep(300)'" >/dev/null 2>&1 & SESS=$!; sleep 1; }
stop_session() { [ -n "$SESS" ] && kill "$SESS" 2>/dev/null; su postgres -c "$PGBIN/psql -h /tmp -X -q -d a0test -c \"select pg_terminate_backend(pid) from pg_stat_activity where usename='a0app'\"" >/dev/null 2>&1; SESS=""; sleep 1; }
no_partial() { ! echo "$1" | grep -q "A0_DIAG=DONE"; }
nosecret() { ! echo "$1" | grep -q "secreto-que-no-debe-salir"; }
leftovers() { ls -d /var/tmp/qa525-a0-schema.* 2>/dev/null | wc -l; }
rm -rf /var/tmp/qa525-a0-schema.*

echo "== DIAGNÓSTICO: caminos felices"; reset; start_session
run env APP_ROLE=a0app bash "$DIAG"; [ $rc -eq 0 ] && echo "$out" | grep -q "A0_DIAG=DONE" && echo "$out" | grep -q "ROLE_APP=a0app (declarado por el Sponsor; coincide con una sesión de cliente: si)" && echo "$out" | grep -q "BASELINE=OK" && chk ok "DONE con APP_ROLE declarado y sesión coincidente" || chk bad "camino feliz rc=$rc" "$out"
stop_session
run env APP_ROLE=a0app bash "$DIAG"; [ $rc -eq 0 ] && echo "$out" | grep -q "coincide con una sesión de cliente: no" && chk ok "DONE con APP_ROLE y sin sesión (se informa que no coincide)" || chk bad "sin sesión rc=$rc" "$out"

echo "== DIAGNÓSTICO: rol de la app"
start_session; run bash "$DIAG"; [ $rc -eq 3 ] && echo "$out" | grep -q "ROLE_APP=UNKNOWN" && echo "$out" | grep -q "candidato:a0app" && no_partial "$out" && chk ok "sin APP_ROLE: UNKNOWN (el candidato por sesión solo es informativo), exit 3" || chk bad "UNKNOWN rc=$rc" "$out"
stop_session; run bash "$DIAG"; [ $rc -eq 3 ] && echo "$out" | grep -q "ROLE_APP=UNKNOWN" && chk ok "sin APP_ROLE y sin sesiones: UNKNOWN" || chk bad "sin nada rc=$rc" "$out"
for r in 'a;b' 'a b' '$(id)'; do run env APP_ROLE="$r" bash "$DIAG"; [ $rc -eq 2 ] && echo "$out" | grep -q "STOP: APP_ROLE tiene caracteres no válidos" && chk ok "APP_ROLE inválido rechazado: $r" || chk bad "APP_ROLE inválido '$r' rc=$rc" "$out"; done
run env APP_ROLE=a0owner bash "$DIAG"; [ $rc -eq 2 ] && echo "$out" | grep -q "no puede ser el dueño" && chk ok "APP_ROLE = dueño rechazado" || chk bad "APP_ROLE dueño rc=$rc" "$out"
run env APP_ROLE=postgres bash "$DIAG"; [ $rc -eq 2 ] && echo "$out" | grep -q "superusuario" && chk ok "APP_ROLE superusuario rechazado" || chk bad "APP_ROLE superusuario rc=$rc" "$out"
run env APP_ROLE=nadie bash "$DIAG"; [ $rc -eq 2 ] && no_partial "$out" && chk ok "APP_ROLE inexistente: STOP" || chk bad "APP_ROLE inexistente rc=$rc" "$out"

echo "== DIAGNÓSTICO: cada comando/pipeline que puede fallar"
reset; echo ps > "$MOCK/fail"; run env APP_ROLE=a0app bash "$DIAG"; [ $rc -eq 2 ] && echo "$out" | grep -q "STOP: docker ps" && ! echo "$out" | grep -q "CONTENEDORES" && chk ok "docker ps falla: STOP sin parciales" || chk bad "docker ps rc=$rc" "$out"
reset; echo image > "$MOCK/fail"; run env APP_ROLE=a0app bash "$DIAG"; [ $rc -eq 2 ] && echo "$out" | grep -q "STOP: docker image ls" && no_partial "$out" && chk ok "docker image ls falla: STOP" || chk bad "docker image rc=$rc" "$out"
reset; echo nodb > "$MOCK/fail"; run env APP_ROLE=a0app bash "$DIAG"; [ $rc -eq 2 ] && echo "$out" | grep -q "no hay contenedor postgres" && chk ok "0 contenedores postgres: STOP" || chk bad "nodb rc=$rc" "$out"
reset; echo twodb > "$MOCK/fail"; run env APP_ROLE=a0app bash "$DIAG"; [ $rc -eq 2 ] && echo "$out" | grep -q "más de un contenedor postgres" && chk ok "2 contenedores postgres: STOP" || chk bad "twodb rc=$rc" "$out"
for n in 1 2 3 4 5 6 7 8; do reset; echo "psql:$n" > "$MOCK/fail"; run env APP_ROLE=a0app bash "$DIAG"; [ $rc -eq 2 ] && no_partial "$out" && nosecret "$out" && chk ok "consulta SQL #$n falla: STOP, sin DONE, error no impreso" || chk bad "consulta SQL #$n rc=$rc" "$out"; done
reset; export MOCK_HOST=otro; run env APP_ROLE=a0app bash "$DIAG"; [ $rc -eq 2 ] && echo "$out" | grep -q "hostname" && chk ok "hostname distinto: STOP" || chk bad "hostname rc=$rc" "$out"; unset MOCK_HOST
reset; echo otrousuario > "$MOCK/user"; run env APP_ROLE=a0app bash "$DIAG"; [ $rc -eq 2 ] && echo "$out" | grep -q "usuario distinto" && chk ok "usuario distinto de soporte: STOP" || chk bad "usuario rc=$rc" "$out"
reset; touch "$MOCK/sudofail"; run env APP_ROLE=a0app bash "$DIAG"; [ $rc -eq 2 ] && echo "$out" | grep -q "sudo -n pide clave" && chk ok "sudo -n falla: STOP" || chk bad "sudo rc=$rc" "$out"
reset

echo "== DIAGNÓSTICO: línea base y esquema"
sql "delete from public.\\\"__EFMigrationsHistory\\\" where \\\"MigrationId\\\"='20261030223000_SeedSupportWorkshop00'"
run env APP_ROLE=a0app bash "$DIAG"; [ $rc -eq 2 ] && echo "$out" | grep -q "BASELINE=UNKNOWN" && chk ok "falta una migración: BASELINE=UNKNOWN" || chk bad "baseline faltante rc=$rc" "$out"
sql "insert into public.\\\"__EFMigrationsHistory\\\" values ('20261030223000_SeedSupportWorkshop00','10')"
sql "insert into public.\\\"__EFMigrationsHistory\\\" values ('99999999999999_Inesperada','10')"
run env APP_ROLE=a0app bash "$DIAG"; [ $rc -eq 2 ] && echo "$out" | grep -q "BASELINE=UNKNOWN" && echo "$out" | grep -q "99999999999999_Inesperada" && chk ok "sobra una migración: BASELINE=UNKNOWN" || chk bad "baseline sobrante rc=$rc" "$out"
sql "delete from public.\\\"__EFMigrationsHistory\\\" where \\\"MigrationId\\\"='99999999999999_Inesperada'"
sql "insert into public.\\\"__EFMigrationsHistory\\\" values ('20261118150000_AddWorkshopRegistryState','10')"
run env APP_ROLE=a0app bash "$DIAG"; [ $rc -eq 2 ] && chk ok "delta ya aplicado: STOP" || chk bad "delta rc=$rc" "$out"
sql "delete from public.\\\"__EFMigrationsHistory\\\" where \\\"MigrationId\\\"='20261118150000_AddWorkshopRegistryState'"
sql "alter table core.organizations add column \\\"Status\\\" text"
run env APP_ROLE=a0app bash "$DIAG"; [ $rc -eq 2 ] && echo "$out" | grep -q "ya trae Status" && chk ok "columna Status ya existe: STOP" || chk bad "status rc=$rc" "$out"
sql "alter table core.organizations drop column \\\"Status\\\""
sql "create table core.workshop_status_transitions(x int)"
run env APP_ROLE=a0app bash "$DIAG"; [ $rc -eq 2 ] && echo "$out" | grep -q "ya trae Status o tabla" && chk ok "tabla de transiciones ya existe: STOP" || chk bad "tabla rc=$rc" "$out"
sql "drop table core.workshop_status_transitions"
sed '0,/^20[0-9]\{12\}_/s//20000000000000_/' "$DIAG" > "$MOCK/tampered.sh"
run env APP_ROLE=a0app bash "$MOCK/tampered.sh"; [ $rc -eq 2 ] && echo "$out" | grep -q "script alterado" && chk ok "lista embebida alterada: STOP por hash propio" || chk bad "tampered rc=$rc" "$out"

echo "== EXPORTACIÓN"
reset; run bash "$EXP"; [ $rc -eq 2 ] && echo "$out" | grep -q "falta CONFIRM_EXPORT" && chk ok "sin confirmación: STOP" || chk bad "sin confirmación rc=$rc" "$out"
reset; echo pgdump > "$MOCK/fail"; before="$(leftovers)"; CONFIRM_EXPORT=EXPORT_SCHEMA_ONLY run bash "$EXP"; after="$(leftovers)"
[ $rc -eq 2 ] && [ "$before" = "$after" ] && nosecret "$out" && chk ok "pg_dump falla: STOP, sin restos, error no impreso" || chk bad "pg_dump rc=$rc antes=$before despues=$after" "$out"
reset; echo ps > "$MOCK/fail"; CONFIRM_EXPORT=EXPORT_SCHEMA_ONLY run bash "$EXP"; [ $rc -eq 2 ] && echo "$out" | grep -q "docker ps" && chk ok "export: docker ps falla" || chk bad "export docker rc=$rc" "$out"
reset; CONFIRM_EXPORT=EXPORT_SCHEMA_ONLY run bash "$EXP"
d="$(echo "$out" | sed -n 's/^DIRECTORIO=//p')"
[ $rc -eq 0 ] && echo "$out" | grep -q "modo=600 dueno=root" && [ -n "$d" ] && [ "$(stat -c %a "$d/.qa525-a0-marker")" = "600" ] && grep -q "^dump_sha256=" "$d/.qa525-a0-marker" && chk ok "exportación OK: 0600 root, con marcador" || chk bad "export rc=$rc" "$out"
! echo "$out" | grep -q "CREATE TABLE" && chk ok "no imprime el contenido del esquema" || chk bad "imprimió contenido"
echo "$out" | grep -q "retención MANUAL, sin plazo garantizado" && ! echo "$out" | grep -q "24 h" && chk ok "retención declarada MANUAL, sin prometer plazo" || chk bad "texto de retención"
reset; STATUS=1 run bash "$EXP"; [ $rc -eq 0 ] && echo "$out" | grep -q "$d" && echo "$out" | grep -q "exportaciones_encontradas=1" && chk ok "STATUS lista la exportación" || chk bad "status rc=$rc" "$out"

echo "== BORRADO: identidad y defensas"
CLEANUP_DIR=/etc run bash "$EXP"; [ $rc -eq 2 ] && chk ok "ruta ajena rechazada" || chk bad "cleanup /etc rc=$rc"
CLEANUP_DIR="/var/tmp/qa525-a0-schema.ZZZZZZ" run bash "$EXP"; [ $rc -eq 2 ] && chk ok "directorio inexistente: STOP" || chk bad "cleanup inexistente rc=$rc"
ln -s /tmp /var/tmp/qa525-a0-schema.LnkAbc; CLEANUP_DIR="/var/tmp/qa525-a0-schema.LnkAbc" run bash "$EXP"; [ $rc -eq 2 ] && [ -d /tmp ] && echo "$out" | grep -q "enlace simbólico" && chk ok "enlace simbólico rechazado (no se sigue)" || chk bad "symlink rc=$rc" "$out"; rm -f /var/tmp/qa525-a0-schema.LnkAbc
mkdir -m 700 /var/tmp/qa525-a0-schema.NoMark; touch /var/tmp/qa525-a0-schema.NoMark/x; CLEANUP_DIR="/var/tmp/qa525-a0-schema.NoMark" run bash "$EXP"; [ $rc -eq 2 ] && [ -e /var/tmp/qa525-a0-schema.NoMark/x ] && chk ok "directorio sin marcador: STOP, no se borra" || chk bad "sin marcador rc=$rc" "$out"; rm -rf /var/tmp/qa525-a0-schema.NoMark
touch "$d/extra"; CLEANUP_DIR="$d" run bash "$EXP"; [ $rc -eq 2 ] && [ -d "$d" ] && chk ok "archivo extra: STOP, no se borra" || chk bad "extra rc=$rc" "$out"; rm -f "$d/extra"
chmod 644 "$d/qa-schema-only.sql"; CLEANUP_DIR="$d" run bash "$EXP"; [ $rc -eq 2 ] && [ -d "$d" ] && chk ok "modo del volcado alterado: STOP" || chk bad "modo rc=$rc" "$out"; chmod 600 "$d/qa-schema-only.sql"
echo "-- alterado" >> "$d/qa-schema-only.sql"; CLEANUP_DIR="$d" run bash "$EXP"; [ $rc -eq 2 ] && [ -d "$d" ] && echo "$out" | grep -q "hash del volcado no coincide" && chk ok "volcado alterado: STOP por hash, no se borra" || chk bad "hash rc=$rc" "$out"
sed -i '/-- alterado/d' "$d/qa-schema-only.sql"
cp "$d/.qa525-a0-marker" "$MOCK/marker.bak"; sed -i "s#^dir=.*#dir=/var/tmp/qa525-a0-schema.Otro00#" "$d/.qa525-a0-marker"; CLEANUP_DIR="$d" run bash "$EXP"; [ $rc -eq 2 ] && [ -d "$d" ] && echo "$out" | grep -q "pertenece a otro directorio" && chk ok "marcador de otro directorio: STOP" || chk bad "marcador dir rc=$rc" "$out"
cp "$MOCK/marker.bak" "$d/.qa525-a0-marker"; chmod 600 "$d/.qa525-a0-marker"
CLEANUP_DIR="$d" run bash "$EXP"; [ $rc -eq 0 ] && [ ! -e "$d" ] && echo "$out" | grep -q "A0_CLEANUP=DONE" && chk ok "borrado verificado de una exportación íntegra" || chk bad "cleanup final rc=$rc" "$out"
[ "$(leftovers)" = "0" ] && chk ok "sin restos en /var/tmp" || chk bad "restos en /var/tmp"
stop_session; rm -rf "$MOCK" /var/tmp/qa525-a0-schema.*
echo; echo "casos=$total fallos=$fails"; [ $fails -eq 0 ] && echo "TODAS LAS PRUEBAS A0 PASARON" || { echo "FALLARON: $fails"; exit 1; }
