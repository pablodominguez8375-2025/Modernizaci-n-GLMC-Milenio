#!/usr/bin/env bash
# Pruebas HERMÉTICAS de A0 (no tocan ningún host real): sudo/docker/hostname/id simulados, BD local `a0test`.
# Requiere PostgreSQL local en /tmp (usuario postgres). Uso: bash test-a0.sh
set -u
HERE="$(cd "$(dirname "$0")/.." && pwd)"
PGBIN=/usr/lib/postgresql/16/bin
MOCK="$(mktemp -d)"; chmod 755 "$MOCK"; fails=0
P() { su postgres -c "$PGBIN/psql -h /tmp -X -q -v ON_ERROR_STOP=1 $*"; }
ok() { echo "  PASS: $1"; }
bad() { echo "  FAIL: $1"; fails=$((fails+1)); }

# --- BD sintética: historial = lista esperada, core.organizations sin Status, roles dueño/app ---
P -c "'drop database if exists a0test with (force)'" -c "'create database a0test'" >/dev/null
P -c "'drop role if exists a0app'" -c "'drop role if exists a0owner'" >/dev/null 2>&1
P -c "'create role a0owner'" -c "'create role a0app login'" >/dev/null
sed -n '/^expected_ids() { cat <<.IDS.$/,/^IDS$/p' "$HERE/qa-a0-diag.sh" | sed '1d;$d' > "$MOCK/ids.txt"
{ echo 'create schema core; create table public."__EFMigrationsHistory"("MigrationId" text primary key, "ProductVersion" text);'
  echo 'create table core.organizations("Id" uuid primary key, "Type" text, "Name" text);'
  echo "insert into core.organizations values (gen_random_uuid(),'workshop','T1'),(gen_random_uuid(),'support','S0');"
  echo 'alter table core.organizations owner to a0owner; grant usage on schema core to a0app; grant select,insert,update,delete on core.organizations to a0app;'
  while read -r id; do echo "insert into public.\"__EFMigrationsHistory\" values ('$id','10');"; done < "$MOCK/ids.txt"; } > "$MOCK/seed.sql"
su postgres -c "$PGBIN/psql -h /tmp -X -q -v ON_ERROR_STOP=1 -d a0test -f $MOCK/seed.sql" >/dev/null || { echo "no se pudo sembrar a0test"; exit 9; }


# --- stubs ---
printf '#!/bin/sh\n[ -n "${MOCK_HOST:-}" ] && echo "$MOCK_HOST" || echo glmdch\n' > "$MOCK/hostname"
printf '#!/bin/sh\necho soporte\n' > "$MOCK/id"
cat > "$MOCK/sudo" <<'XEOF'
#!/bin/bash
[ "$1" = "-n" ] && shift
if [ "$1" = "env" ] && [ "$2" = "-i" ]; then shift 2; args=(); for a in "$@"; do case "$a" in PATH=*) args+=("PATH=$MOCKPATH");; *) args+=("$a");; esac; done; exec env -i "${args[@]}"; fi
exec "$@"
XEOF
cat > "$MOCK/docker" <<'XEOF'
#!/bin/bash
case "$1" in
 ps) [ "$(cat "$MOCKPATH_DIR/fail" 2>/dev/null)" = ps ] && exit 1; echo "pmgm-db postgres:16 Up 2 days"; echo "pmgm-api pmgm-api:x Up 2 days";;
 image) echo "pmgm-api:x sha256:abc <none>";;
 exec) shift; while [ "$1" != "sh" ] && [ $# -gt 0 ]; do shift; done; shift; shift; cmd="$1"
   if [ "$(cat "$MOCKPATH_DIR/fail" 2>/dev/null)" = pgdump ] && printf '%s' "$cmd" | grep -q pg_dump; then echo "secreto-que-no-debe-salir" >&2; exit 1; fi
   export POSTGRES_USER=postgres POSTGRES_DB=a0test PGHOST=/tmp; exec sh -c "$cmd";;
esac
XEOF
sed -i "s#\$MOCKPATH_DIR#$MOCK#g" "$MOCK/docker"; chmod +x "$MOCK"/*
export MOCKPATH="$MOCK:$PGBIN:/usr/sbin:/usr/bin:/sbin:/bin"; export PATH="$MOCKPATH"
# Copias de los scripts con SOLO la línea de PATH sustituida por el PATH simulado (el resto es idéntico al script real).
for f in qa-a0-diag.sh qa-a0-schema-export.sh; do sed 's#^PATH=/usr/sbin:/usr/bin:/sbin:/bin; export PATH$#PATH="$MOCKPATH"; export PATH#' "$HERE/$f" > "$MOCK/$f"; grep -q 'MOCKPATH' "$MOCK/$f" || { echo "no se pudo sustituir PATH en $f"; exit 9; }; done
run() { out="$("$@" 2>&1)"; rc=$?; }
SESS=""; start_session() { su postgres -c "PGPASSWORD= $PGBIN/psql -h /tmp -U a0app -d a0test -X -q -c 'select pg_sleep(120)'" >/dev/null 2>&1 & SESS=$!; sleep 1; }
stop_session() { [ -n "$SESS" ] && kill "$SESS" 2>/dev/null; su postgres -c "$PGBIN/psql -h /tmp -X -q -d a0test -c \"select pg_terminate_backend(pid) from pg_stat_activity where usename='a0app'\"" >/dev/null 2>&1; SESS=""; }
# peer/trust: permitir login de a0app por socket
su postgres -c "grep -q a0app /var/tmp/pgdata/pg_hba.conf" || { echo 'local all a0app trust' | cat - /var/tmp/pgdata/pg_hba.conf > "$MOCK/hba" && cp "$MOCK/hba" /var/tmp/pgdata/pg_hba.conf && chown postgres: /var/tmp/pgdata/pg_hba.conf && su postgres -c "$PGBIN/pg_ctl -D /var/tmp/pgdata reload" >/dev/null; }

echo "1) diagnóstico OK con sesión de la app"; start_session
run bash "$MOCK/qa-a0-diag.sh"; [ $rc -eq 0 ] && echo "$out" | grep -q "A0_DIAG=DONE" && echo "$out" | grep -q "^app|a0app" && ok "DONE y rol app descubierto" || { bad "esperado DONE (rc=$rc)"; echo "$out" | tail -5; }
echo "2) docker ps falla"; echo ps > "$MOCK/fail"; run bash "$MOCK/qa-a0-diag.sh"; rm -f "$MOCK/fail"; [ $rc -eq 2 ] && echo "$out" | grep -q "STOP: docker ps" && ! echo "$out" | grep -q "BASELINE" && ok "STOP sin resultados parciales" || bad "docker fallido rc=$rc"
echo "3) hostname distinto"; MOCK_HOST=otro run bash "$MOCK/qa-a0-diag.sh"; [ $rc -eq 2 ] && ok "STOP por hostname" || bad "hostname rc=$rc"
echo "4) rol de la app sin sesiones"; stop_session; sleep 1
run bash "$MOCK/qa-a0-diag.sh"; [ $rc -eq 3 ] && echo "$out" | grep -q "ROLE_APP=UNKNOWN" && ok "ROLE_APP=UNKNOWN, INCOMPLETE" || { bad "rol sin sesiones rc=$rc"; echo "$out" | tail -4; }
echo "5) línea base desconocida (falta una migración)"
P -d a0test -c "\"delete from public.\\\"__EFMigrationsHistory\\\" where \\\"MigrationId\\\"='20261030223000_SeedSupportWorkshop00'\"" >/dev/null
run bash "$MOCK/qa-a0-diag.sh"; [ $rc -eq 2 ] && echo "$out" | grep -q "BASELINE=UNKNOWN" && ok "BASELINE=UNKNOWN y STOP" || bad "baseline rc=$rc"
P -d a0test -c "\"insert into public.\\\"__EFMigrationsHistory\\\" values ('20261030223000_SeedSupportWorkshop00','10')\"" >/dev/null
echo "6) delta ya aplicado"
P -d a0test -c "\"insert into public.\\\"__EFMigrationsHistory\\\" values ('20261118150000_AddWorkshopRegistryState','10')\"" >/dev/null
run bash "$MOCK/qa-a0-diag.sh"; [ $rc -eq 2 ] && ok "STOP con delta aplicado" || bad "delta rc=$rc"
P -d a0test -c "\"delete from public.\\\"__EFMigrationsHistory\\\" where \\\"MigrationId\\\"='20261118150000_AddWorkshopRegistryState'\"" >/dev/null
echo "7) exportación sin confirmación"; run bash "$MOCK/qa-a0-schema-export.sh"; [ $rc -eq 2 ] && echo "$out" | grep -q "falta CONFIRM_EXPORT" && ok "STOP sin confirmación" || bad "sin confirmación rc=$rc"
echo "8) pg_dump falla: sin restos ni mensajes sensibles"
before="$(ls -d /var/tmp/qa525-a0-schema.* 2>/dev/null | wc -l)"
echo pgdump > "$MOCK/fail"; CONFIRM_EXPORT=EXPORT_SCHEMA_ONLY run bash "$MOCK/qa-a0-schema-export.sh"; after="$(ls -d /var/tmp/qa525-a0-schema.* 2>/dev/null | wc -l)"; rm -f "$MOCK/fail"
[ $rc -eq 2 ] && [ "$before" = "$after" ] && ! echo "$out" | grep -q "secreto-que-no-debe-salir" && ok "STOP, directorio borrado, error no impreso" || { bad "pg_dump fallido rc=$rc antes=$before despues=$after"; echo "$out"; }
echo "9) exportación OK y limpieza"
CONFIRM_EXPORT=EXPORT_SCHEMA_ONLY run bash "$MOCK/qa-a0-schema-export.sh"
d="$(echo "$out" | sed -n 's/^DIRECTORIO=//p')"
[ $rc -eq 0 ] && echo "$out" | grep -q "modo=600 dueno=root" && [ -n "$d" ] && ok "archivo 0600 root en $d" || { bad "exportación rc=$rc"; echo "$out"; }
! echo "$out" | grep -q "CREATE TABLE" && ok "no imprime el contenido del esquema" || bad "imprimió contenido"
CLEANUP_DIR="$d" run bash "$MOCK/qa-a0-schema-export.sh"; [ $rc -eq 0 ] && [ ! -e "$d" ] && ok "CLEANUP borró el directorio" || bad "cleanup rc=$rc"
CLEANUP_DIR="/etc" run bash "$MOCK/qa-a0-schema-export.sh"; [ $rc -eq 2 ] && ok "CLEANUP rechaza rutas ajenas" || bad "cleanup /etc rc=$rc"
stop_session; rm -rf "$MOCK"
echo; [ $fails -eq 0 ] && echo "TODAS LAS PRUEBAS A0 PASARON" || { echo "FALLARON: $fails"; exit 1; }
