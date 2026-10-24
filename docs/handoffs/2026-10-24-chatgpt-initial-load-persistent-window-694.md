# PMGM #694 — fase F: estado durable de ventana (sin rutas de escritura)

Fecha: 24-10-2026 · Agente ChatGPT · dev de origen `7007142045dedbe310470ecc4e2dff607353916e`.

## Objetivo
Persistir el estado singleton de la ventana de carga inicial, **sin admitir ni importar registros**. Se usa el `BootstrapDbContext` institucional ya existente (migraciones propias y auditoría `core.audit_events`), no se crea un segundo sistema paralelo. Las reglas de autorización de la fase E (#704) ya están integradas pero no conectadas a endpoints de mutación.

## Diseño conservador
- Tabla `core.initial_load_windows` con una fila fija `Id=1`; nace en `not_opened`, versión 0, sin aprobación.
- Estados permitidos `not_opened → open → closed`, sin reabrir `closed` ni permitir saltos o ediciones sin transición. Una función/trigger PostgreSQL bloquea UPDATE no autorizado, DELETE/TRUNCATE y cambios posteriores al cierre.
- Al abrir: versión creciente, fecha UTC, referencia obligatoria a `audit_events` y digest hexadecimal SHA-256 de la evidencia jurídica verificada por un servicio del servidor, sin conservar nombres ni cuerpos de la nómina. La existencia de un digest **no prueba la validez legal**: ésta requiere revisión del documento jurídico original en futuro servicio autenticado.
- Al cerrar: fecha de cierre y segunda referencia de auditoría, con versión monotónica. No se expone una API de abrir/cerrar/importar. Requiere inserciones de auditoría y cambio de estado en la misma transacción en fase posterior.
- Restricciones de esquema SQL CHECK, FKs al ledger inmutable de auditoría y prueba de mapping EF. Se **bloquea** en vez de admitir un estado inconsistente. El rollback de migración revierte sólo en entornos de laboratorio; despliegues institucionales requieren plan de respaldo y aprobación por separado.

## Archivos propios
- `backend/src/PMGM.Api/Modules/Bootstrap/Entities/InitialLoadWindow.cs` nuevo.
- `backend/src/PMGM.Api/Modules/Bootstrap/BootstrapDbContext.cs` ampliación mínima.
- `backend/src/PMGM.Api/Migrations/20261024183000_AddInitialLoadWindow.cs` nueva migración del BootstrapDbContext (reservar exclusividad de migraciones y revisar otras ramas antes de merge).
- `backend/tests/PMGM.Api.Tests/InitialLoadWindowModelTests.cs` nuevo test de modelo EF/DDL.
- `backend/tests/PMGM.Api.Tests/Integration/InstitutionalBootstrapPostgreSqlTests.cs` ampliación de test PostgreSQL ya registrado en shard: migración y denegación real de UPDATE directo a cerrado, DELETE y TRUNCATE con rollback.
- Este handoff.

## Política de ejecución y límites
No hay endpoints, servicio de cambios, aprobación Jurídico, carga de CSV, transacciones reales, identidad de cuenta Máster ni posibilidad Apply. QA físico glmdch y main NO se tocan. Antes de cualquier uso real, se deben implantar: comprobación server-side de cuenta única `admin` y aprobación Jurídico Ley 21.719; auditoría en la misma transacción; hashes no derivados de PII, mecanismos de reversión/idempotencia, UAT, validación de migraciones y respaldo/restauración, y autorización explícita del PO del despliegue.

PR draft obligatorio y gates CI/Showcase/QA Installable **3/3 SUCCESS exact-head** antes de integrar solo a dev. Registro de PR/issue en GitHub y adenda en Drive.
