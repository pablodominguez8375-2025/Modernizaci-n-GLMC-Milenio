# QA525 G6 v2 — Verificador de restore aislado (biblioteca INACTIVA, no se ejecutó)

`scripts/qa/qa525-vnext-g6-v2-verifier.py` construye el `verifier(data_dir)` que el RESTORE del productor
G6 v2 invoca sobre los datos extraídos. No es un GO, no autoriza nada y **no se ejecutó contra glmdch**.

## Qué hace
Levanta un contenedor PostgreSQL **desechable** (imagen fijada por digest, Debian/uid 999, versión ≥ 17), restaura
`pmgm.dump` y `pmgm_keycloak.dump` con `pg_restore --exit-on-error --no-owner --no-acl`, cuenta las tablas de usuario de cada
base (cada una debe ser > 0) y cuenta los objetos regulares no vacíos de `silo/` (> 0, sin enlaces ni FIFO). Devuelve
`({"pmgm": n, "pmgm_keycloak": m}, silo)`; el productor rechaza cualquier cero.

## Aislamiento
`--network none`, `--read-only`, `--cap-drop ALL`, `no-new-privileges`, `--pids-limit 256`, `--memory 2g`, `--user 999:999`,
`tmpfs` para datos/run/tmp y **ningún montaje del host**: cada dump entra por stdin de `pg_restore`. Autenticación `trust`
dentro de un contenedor sin red (sin secretos). El contenedor se elimina SIEMPRE (`rm -f -v`); si queda vivo, `G6VER_CLEANUP_FAILED`
(nunca un PASS silencioso). Un contenedor con el mismo nombre previo aborta (`G6VER_NAME_IN_USE`).
El runner es el mismo puerto de argv fijo del captor (#952): solo ejecuta argv cuyo primer elemento es el docker fijado.

## No probado aquí (declarado)
- Que los datos sean los de producción, ni la restauración de **roles y contraseñas** (`globals.sql` NO se aplica; aceptado por
  ChatGPT que el restore real debe recrearlos desde secretos propios y probar Keycloak y API).
- Propietarios/ACL tras `--no-owner --no-acl` (requiere una corrida de `db-grants`, prueba aparte).
- Que MinIO lea el Silo restaurado; consistencia funcional entre bases y Silo (no se capturan en un instante atómico).
- La imagen: digest y versión de Postgres a fijar con un dato de solo lectura de glmdch (aún no pedido).
- Pruebas con Docker real: aquí solo con un docker simulado.
