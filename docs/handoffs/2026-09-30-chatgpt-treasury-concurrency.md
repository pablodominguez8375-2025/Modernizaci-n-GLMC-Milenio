# Handoff — concurrencia de ajustes de recibos — 2026-09-30

Agente: ChatGPT. Issue #191. Base dev e097298641cdd959b2e47c9354fffc748ab97e4d; main 6dfb9546a4873baff15955cf86abfd7d47e3d111. Retoma autorizada por el PO («Continúa con pendientes»).

## Reserva

backend/tests/PMGM.Api.Tests/Integration/LodgeReceiptAdjustmentPostgreSqlTests.cs; LodgeReceiptConcurrencyPostgreSqlTests.cs (nuevo); docs/qa/PMGM-QA-RECIBOS-AJUSTES-191.md; changelog/2026-09-30-treasury-receipt-concurrency.md; este handoff nuevo. Anti-conflicto: sin cruces con PR de implementación abiertos; #1 es comparador dev→main y permanece fuera de alcance. PR draft antes de adaptar el commit local 430ee44. START-HERE y handoff histórico de #191 no se trasladan de ese commit; continuidad conforme GOV-003, una sola línea START-HERE en PR documental posterior.

## Objetivo y estado

Añadir pruebas PostgreSQL de dos ajustes serializables simultáneos (doble anulación, clave idempotente compartida, correcciones que excederían origen), auditoría única, reintento seguro, originales/saldos e inmutabilidad DELETE. Sin cambios de producto, permisos, contabilidad, migraciones o UI. Implementación/CI exact-head pendientes; resultados, publicación y paquete se registrarán antes del cierre.

## Pendientes que se conservan

#191 sigue abierto por presentación contable institucional y validación operacional. Devolución efectiva fuera de este alcance. Main sin promoción; srv01 pausado por #97, despliegue QA pendiente, sin instalación, QA física ni UAT. El commit local original 430ee44 se preserva.
