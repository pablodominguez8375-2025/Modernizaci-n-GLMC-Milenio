# QA525 G6 v2 — Captor físico (biblioteca INACTIVA, no se ejecuta aquí)

`scripts/qa/qa525-vnext-g6-v2-captor.py` construye el `captor(stage)` que invoca
`qa525-vnext-g6-v2-producer.py::backup`. No es un GO, no autoriza respaldo ni restore, y
**no se ejecutó contra glmdch**. Todo comando de PostgreSQL es un argv FIJO construido en el
módulo: sin shell, sin texto de comando del llamador, entorno mínimo (`PATH`, `LANG`).

## Qué deja en el stage
Exactamente `pmgm.dump`, `pmgm_keycloak.dump`, `globals.sql` y un `silo/` no vacío, todo 0600/0700.
Antes: el stage debe ser un directorio vacío, 0700, del `uid` del llamador. Después de cada dump:
`pg_restore --list` debe declarar la base correcta, formato CUSTOM, y al menos 10 entradas de TOC.
Devuelve solo los conteos de TOC.

## Comandos (golden en las pruebas)
- Identidad: `docker --host unix:///var/run/docker.sock inspect --format <labels+Running> centenario-qa-lab2-postgres-1`
  debe dar `centenario-qa-lab2|postgres|true`.
- `docker exec --user postgres <c> pg_dump --format=custom --no-owner --no-acl --dbname <db>` (×2).
- `docker exec -i --user postgres <c> pg_restore --list` (stdin = el dump, salida acotada a 8 MiB).
- `docker exec --user postgres <c> pg_dumpall --globals-only --no-role-passwords`.

## Decisiones que ChatGPT/Sponsor deben confirmar
1. `--no-role-passwords`: el archivo que va a Drive no lleva hashes de contraseñas de roles. Tras un restore real
   los roles se recrean con las claves del entorno. Si se prefiere incluirlas, es un cambio de una línea y una decisión de seguridad.
2. `--no-owner --no-acl` (igual que el operador legado `qa525-dual-db-age-backup.py`): los permisos los reaplica `db-grants`.
3. El **Silo** es un puerto inyectado (`copy_silo(dest)`) más el copiador `copy_tree_nofollow` (solo archivos regulares,
   sin enlaces/dispositivos/FIFO, topes de 200 000 archivos y 8 GiB). **Falta la ruta física del volumen del Silo**; sin ese dato
   no hay captor real. Se pedirá al Sponsor un dato de solo lectura cuando ChatGPT lo apruebe.

## Límites (no autenticado aquí)
- El binario `docker`, el host y la ruta del Silo los fija y autentica el bootstrap root (#936/#822).
- Las dos bases y el Silo **no** se capturan en un instante atómico; cada dump es consistente por base.
- `pg_restore --list` valida el archivo, no que los datos sean los de producción; el restore aislado con Postgres real
  es el **verificador**, aún por escribir (siguiente pieza).
- Un fallo deja archivos parciales: el productor borra siempre el stage.
- El timeout (30 min) y los topes de salida matan el GRUPO de procesos.
