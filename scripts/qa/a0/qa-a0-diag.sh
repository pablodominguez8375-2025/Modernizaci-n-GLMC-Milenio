#!/usr/bin/env bash
# A0 SOLO LECTURA (Talleres #1035). Corre como `soporte` en glmdch. No modifica nada, no escribe archivos, no imprime secretos.
set -u
umask 077
stop() { echo "STOP: $*" >&2; exit 2; }

# 1) Preflight: host, usuario y sudo sin clave. Cualquier desajuste = STOP.
[ "$(hostname)" = "glmdch" ] || stop "hostname=$(hostname), se esperaba glmdch"
[ "$(id -un)" = "soporte" ] || stop "usuario=$(id -un), se esperaba soporte"
sudo -n true 2>/dev/null || stop "sudo -n pide clave"
echo "PREFLIGHT=OK host=$(hostname) user=$(id -un) fecha_utc=$(date -u +%FT%TZ)"

# 2) Contenedores e imágenes: solo nombres, estado e identificadores/digests (sin inspect general ni variables de entorno).
echo "== CONTENEDORES (nombre imagen estado)"
sudo -n docker ps --format '{{.Names}} {{.Image}} {{.Status}}' | sort || stop "docker ps falló"
echo "== IMAGENES (repo:tag id digest)"
sudo -n docker image ls --digests --no-trunc --format '{{.Repository}}:{{.Tag}} {{.ID}} {{.Digest}}' | sort || stop "docker image ls falló"

# 3) Contenedor de BD: se descubre, no se asume. Debe haber exactamente uno con imagen postgres.
mapfile -t DBS < <(sudo -n docker ps --format '{{.Names}} {{.Image}}' | awk '$2 ~ /postgres/ {print $1}')
[ "${#DBS[@]}" -eq 1 ] || stop "contenedores postgres encontrados: ${#DBS[@]} (se esperaba 1): ${DBS[*]:-ninguno}"
DB="${DBS[0]}"
echo "CONTENEDOR_BD=$DB"

# 4) Consultas de solo lectura (transacción de solo lectura forzada). Credenciales: las del propio contenedor, sin imprimirlas.
q() { sudo -n docker exec -i -e PGOPTIONS='-c default_transaction_read_only=on -c statement_timeout=15000' "$DB" \
      sh -c 'psql -X -A -t -F "|" -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "${POSTGRES_DB:-$POSTGRES_USER}"'; }

echo "== SERVIDOR"
q <<'SQL' || stop "no se pudo consultar la BD"
select 'server_version=' || current_setting('server_version');
select 'database=' || current_database();
select 'sesion_usuario=' || current_user;
SQL

echo "== HISTORIAL EF (ids exactos)"
q <<'SQL' || stop "no se pudo leer __EFMigrationsHistory"
select "MigrationId" from public."__EFMigrationsHistory" order by 1;
SQL

echo "== CHEQUEOS DE LINEA BASE (esperado: base=1 delta=0 status_col=0 tabla_transiciones=0)"
q <<'SQL' || stop "no se pudieron hacer los chequeos de línea base"
select 'base_20261030223000=' || count(*) from public."__EFMigrationsHistory" where "MigrationId"='20261030223000_SeedSupportWorkshop00';
select 'delta_aplicado=' || count(*) from public."__EFMigrationsHistory" where "MigrationId" in
 ('20261118150000_AddWorkshopRegistryState','20261118170000_AddWorkshopRetiredGuard','20261119010000_AddWorkshopStatusTransitions','20261119010100_AddWorkshopStatusTransitionsAppendOnly');
select 'status_col=' || count(*) from information_schema.columns where table_schema='core' and table_name='organizations' and column_name='Status';
select 'tabla_transiciones=' || (to_regclass('core.workshop_status_transitions') is not null)::int;
SQL

BL="$(q <<'SQL'
select (select count(*) from public."__EFMigrationsHistory" where "MigrationId"='20261030223000_SeedSupportWorkshop00')
 || ',' || (select count(*) from public."__EFMigrationsHistory" where "MigrationId" in ('20261118150000_AddWorkshopRegistryState','20261118170000_AddWorkshopRetiredGuard','20261119010000_AddWorkshopStatusTransitions','20261119010100_AddWorkshopStatusTransitionsAppendOnly'))
 || ',' || (select count(*) from information_schema.columns where table_schema='core' and table_name='organizations' and column_name='Status')
 || ',' || (to_regclass('core.workshop_status_transitions') is not null)::int;
SQL
)"
[ "$BL" = "1,0,0,0" ] || stop "la línea base no coincide (base,delta,status_col,tabla = $BL; esperado 1,0,0,0). No adaptar a ciegas: reportar."
echo "LINEA_BASE=OK"

echo "== CONTEOS AGREGADOS (sin datos de personas)"
q <<'SQL' || stop "no se pudieron contar organizaciones"
select 'organizaciones_total=' || count(*) from core.organizations;
select 'organizaciones_tipo:' || "Type" || '=' || count(*) from core.organizations group by "Type" order by "Type";
SQL

echo "== ROLES RELEVANTES (solo el dueño de core.organizations y los roles con sesiones de la app; sin secretos)"
q <<'SQL' || stop "no se pudieron consultar roles/privilegios"
with c as (
  select usename::text as r from pg_stat_activity where datname=current_database() and backend_type='client backend' and usename is not null
  union select tableowner::text from pg_tables where schemaname='core' and tablename='organizations')
select r || '|super=' || coalesce((select rolsuper::text from pg_roles where rolname=r),'?')
 || '|insert=' || has_table_privilege(r,'core.organizations','INSERT')
 || '|update=' || has_table_privilege(r,'core.organizations','UPDATE')
 || '|delete=' || has_table_privilege(r,'core.organizations','DELETE')
 || '|truncate=' || has_table_privilege(r,'core.organizations','TRUNCATE')
 || '|create_en_core=' || has_schema_privilege(r,'core','CREATE')
from c order by r;
SQL
echo "A0_DIAG=DONE (solo lectura; no se escribió ningún archivo)"
