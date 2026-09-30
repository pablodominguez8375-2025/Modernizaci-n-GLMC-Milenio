# Handoff — concurrencia de ajustes de recibos — 2026-09-30

Agente: ChatGPT. Issue #191. Base dev e097298641cdd959b2e47c9354fffc748ab97e4d; main 6dfb9546a4873baff15955cf86abfd7d47e3d111. Retoma autorizada por el PO («Continúa con pendientes»).

## Reserva

backend/tests/PMGM.Api.Tests/Integration/LodgeReceiptAdjustmentPostgreSqlTests.cs; LodgeReceiptConcurrencyPostgreSqlTests.cs (nuevo); docs/qa/PMGM-QA-RECIBOS-AJUSTES-191.md; changelog/2026-09-30-treasury-receipt-concurrency.md; este handoff nuevo. Anti-conflicto: sin cruces con PR de implementación abiertos; #1 es comparador dev→main y permanece fuera de alcance. PR draft antes de adaptar el commit local 430ee44. START-HERE y handoff histórico de #191 no se trasladan de ese commit; continuidad conforme GOV-003, una sola línea START-HERE en PR documental posterior.

## Objetivo y estado

Añadir pruebas PostgreSQL de dos ajustes serializables simultáneos (doble anulación, clave idempotente compartida, correcciones que excederían origen), auditoría única, reintento seguro, originales/saldos e inmutabilidad DELETE. Sin cambios de producto, permisos, contabilidad, migraciones o UI. PR #228; pruebas adaptadas del commit local430ee44, matriz QA actualizada y changelog nuevo. Revisión del endpoint confirma transacciones Serializable y conflicto40001/23505 sin reintento automático; la barrera fuerza las dos lecturas antes de escribir. git diff --check correcto. No hay SDK .NET/PostgreSQL local: la ejecución de backend se verificará en CI con PMGM_TEST_POSTGRES configurado y evidencia TRX/logs; no se acredita ejecución PostgreSQL desde compilación ni retorno sin variable. Gates exact-head/publicación y paquete pendientes.

## Pendientes que se conservan

#191 sigue abierto por presentación contable institucional y validación operacional. Devolución efectiva fuera de este alcance. Main sin promoción; srv01 pausado por #97, despliegue QA pendiente, sin instalación, QA física ni UAT. El commit local original 430ee44 se preserva.

## Hallazgo de CI y reparación

CI 36714474824 ejecutó 351 pruebas: 349 correctas y 2 fallos reproducidos de doble anulación (misma clave y claves distintas): 201+500. Corrección concurrente y DELETE pasaron. Npgsql envuelve 40001 de SaveChanges en InvalidOperationException → DbUpdateException → PostgresException. Se amplía reserva a backend/src/PMGM.Api/Modules/Treasury/LodgeReceiptAdjustments.cs; anti-conflicto revisado y registrado antes de editar. Se reconoce exclusivamente la envoltura de 40001/23505 para retornar 409 y conservar reintento explícito, sin modificar reglas, permisos ni migraciones. Se mantienen las assertions originales; requiere nuevos gates exact-head. La declaración inicial sin cambio de producto queda sustituida por esta reparación acotada.
