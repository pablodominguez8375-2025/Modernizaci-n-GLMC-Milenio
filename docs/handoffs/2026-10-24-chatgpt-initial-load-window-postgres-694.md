# Issue #694 — fase N — validación PostgreSQL atómica de ventana y auditoría
Corte 24-10-2026 CL. Agente ChatGPT. Base `dev@c3dda9ab58e09c16e48f78c96a41b061dd53354f`.

## Objetivo
Añadir **prueba real PostgreSQL** para fase M (PR #715), aislada en transacción revertida, verificando `not_opened → open → closed`, claves foráneas de auditoría, trigger de irreversibilidad e invariantes de versiones. Sin usar credenciales reales, cuentas, nóminas ni abrir la ventana operativa de QA.

## Archivos reservados
- `backend/tests/PMGM.Api.Tests/Integration/InitialLoadWindowAtomicPostgreSqlTests.cs` nuevo.
- `.github/backend-integration-shards.json`: registro explícito en shard `a` (el CI exige cobertura de todas las clases nuevas).
- Este handoff `docs/handoffs/2026-10-24-chatgpt-initial-load-window-postgres-694.md`.

## Pruebas y límites
Contextos nuevos EF Core con PostgreSQL `PMGM_TEST_POSTGRES` en CI; seed singleton al estado no abierto, lock de fila con timeout, auditoría sintética, dos actualizaciones en transacción, rollback explícito y lectura posterior asegurando ventana 0/auditorías ausentes. No modifica API ni migraciones ni frontend. Es prueba de persistencia y revertibilidad de DB, **no acredita aprobación jurídica, endpoint operacional, funcionamiento del servicio completo ni importador**.
Primer CI HEAD `6b1c27a9` falló ANTES de correr la prueba: clase nueva ausente de `.github/backend-integration-shards.json` (`missing=['InitialLoadWindowAtomicPostgreSqlTests']`); se corrigió la asignación al shard `a` sin tocar los workflows. Reejecutar los 3 gates exact-head después de actualizar este documento. Si falla la prueba, corregir según evidencia CI; no certificarla por compilar.
PR DRAFT, tres gates exact-head antes de merge, descripción de PR como handoff. No `main`, no glmdch. Continuidad GitHub #694 y adenda Drive.

## 24-10-2026 — conciliación con Claude y regresión de auditoría
Dev avanzó de `c3dda9ab` a `cceaa5ad` por PR #717 (Biblioteca, Claude). Se reconciliaron **sus** cambios de `.github/backend-integration-shards.json` (nuevo test `WorkPaperReviewHttpTests`) con esta fase N (`InitialLoadWindowAtomicPostgreSqlTests` en shard `a`), sin perder cobertura.
Primer CI con shards corregidos (run 40148760887) reveló una falla de un test EXISTENTE: `PostgreSqlMigrationTests.Audit_events_allow_insert_but_reject_update_delete_and_truncate` línea 282; operación `TRUNCATE core.audit_events`, esperado SQLSTATE `55000` de trigger append-only, PostgreSQL devolvió `0A000` porque un FK de `core.initial_load_windows` referencia audit_events e impide truncarla antes del trigger. El dato quedó preservado; por eso el test acepta **sólo** esos dos motivos explícitos de denegación y continúa comprobando lectura intacta. No se cambia autorización, triggers, esquema ni se permite TRUNCATE.
Archivos adicionales: `.github/backend-integration-shards.json` (configuración compartida ya fusionada de Claude preservada), `backend/tests/PMGM.Api.Tests/Integration/PostgreSqlMigrationTests.cs` (única aserción y comentario). Nueva validación 3/3 obligatoria para el HEAD rebasado.
