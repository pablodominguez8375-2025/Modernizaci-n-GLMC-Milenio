# Issue #694 fase K — bloquear escrituras bootstrap institucional después de abrir carga

24-10-2026 · ChatGPT · base `dev@fdceada9572b34f06f98bdeda4568916efdd0485`.

## Problema
El antiguo `POST /api/platform/bootstrap/apply` permite crear organizaciones/Talleres y catálogo institucional con rol plataforma, aunque se haya iniciado o cerrado la nueva ventana de carga inicial `core.initial_load_windows`. Esto puede eludir el cierre institucional aprobado por el PO. No es el endpoint de CSV ni permite importar Hermano.

## Corrección mínima y retrocompatible
En `InstitutionalBootstrapService.ApplyAsync`, dentro de su transacción PostgreSQL **antes de cualquier mutación de tablas**, bloquear la única fila de ventana `Id=1` mediante SELECT FOR UPDATE y validar que esté exactamente `not_opened` (versión 0 y resto de invariantes de forma). Si no existe, fue alterada o está `open`/`closed`, rollback y rechazo con mensaje fijo; no crear organizaciones/catálogos ni emitir auditoría de éxito. El bloqueo mantiene serializable la decisión respecto de una transición concurrente de ventana que actualice la misma fila y requiera el candado correspondiente.

Mantener permitida la configuración institucional original antes de abrir la ventana; operación `plan` y `catalog` siguen read-only, no se alteran. No interceptar/autorizar aquí cargas de CSV, que permanecen prohibidas hasta aprobación Jurídico Ley 21.719.

## Cambios propios
- `backend/src/PMGM.Api/Modules/Bootstrap/InstitutionalBootstrapService.cs` guard real de escritura bajo transacción.
- `backend/src/PMGM.Api/Modules/Bootstrap/InitialLoadLegacyBootstrapWriteGuard.cs` política pura conservadora.
- `backend/tests/PMGM.Api.Tests/InitialLoadLegacyBootstrapWriteGuardTests.cs` regresiones sintéticas estados y versión.
- `backend/tests/PMGM.Api.Tests/Integration/InstitutionalBootstrapPostgreSqlTests.cs` test actual seguirá validando que bootstrap antes de abrir funciona; verificar el SQL SELECT FOR UPDATE sobre PostgreSQL.
- Este handoff.

## Limitaciones / controles
Sin nuevas migraciones ni endpoints. No bloquear las rutas previas de visualización/read-only; no introducir credenciales Master para el flujo heredado previo a apertura sin decisión PO y UAT. No afirmar que la política garantiza seguridad ante escritores SQL directos o servicios que no respeten el lock; los triggers de ventana sí mantienen transiciones monotónicas. Para transición futura, usar también la misma fila y auditoría atómica del lado servidor. No modificar `main` ni desplegar en QA físico sin autorización y verificación. PR DRAFT + 3/3 gates exact-head + behind0 antes de squash a `dev`; registrar en GitHub Issue #694 y Drive.
