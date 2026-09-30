# Cierre — concurrencia de ajustes de recibos — 2026-09-30

Agente ChatGPT; Issue #191. PR funcional #228 integrado por squash: dev 49b3d1b72d6cba3014d3d1ffa253f321f7a0fc14; inicio dev e097298641cdd959b2e47c9354fffc748ab97e4d; main 6dfb9546a4873baff15955cf86abfd7d47e3d111.

## Reserva del PR documental

Este handoff nuevo y una sola línea en START-HERE.md conforme GOV-003. Ningún otro cambio. Draft previo a editar START-HERE. Integración autorizada por el PO cuando los tres gates del HEAD exacto estén SUCCESS. Se conserva #191 abierto y la pausa #97. Publicación postmerge funcional verificada antes de completar este PR documental. PR documental #229.

## Cambio y archivos del PR #228

LodgeReceiptAdjustments.cs reconoce exclusivamente InvalidOperationException que envuelve PostgresException o DbUpdateException→PostgresException con SqlState40001/23505, y responde409. El conflicto se revierte; no reintenta automáticamente. Pruebas preservan recibo/imputación originales, un solo ajuste/auditoría, caja/cuotas/crédito y reintento idempotente. Archivos: backend/src/PMGM.Api/Modules/Treasury/LodgeReceiptAdjustments.cs; backend/tests/PMGM.Api.Tests/Integration/LodgeReceiptAdjustmentPostgreSqlTests.cs; LodgeReceiptConcurrencyPostgreSqlTests.cs; docs/qa/PMGM-QA-RECIBOS-AJUSTES-191.md; changelog/2026-09-30-treasury-receipt-concurrency.md; docs/handoffs/2026-09-30-chatgpt-treasury-concurrency.md. Sin migraciones, cambio de reglas contables, permisos o UI.

## Pruebas verificadas

HEAD funcional b164ac7b595e5f6b6b913293ead4deac242cd9ef: CI36715785265, Showcase36715785249 y QA installable36715785388 SUCCESS. TRX autenticado artefacto11096830483 (sha2567e24d0e667a8707b7b7bebf9e28b3468b1e3000e5899f705195358ce823906a3):351 ejecutadas/351 correctas/0 fallos. Logs confirman PMGM_TEST_POSTGRES y PostgreSQL. Doble anulación claves distintas1.809s; misma clave1.613s; correcciones concurrentes1.695s; historia/DELETE2.026s, todos Passed. Corrigen regresión demostrada en CI36714474824 (349/351,201+500), sin debilitar assertions.

Postmerge funcional: CI36716880689 y QA installable36716880651 SUCCESS; Pre-UAT installable36716880823 SUCCESS. Lint/build/tests frontend incluidos en CI. Sin SDK .NET/PostgreSQL local; no se acredita ejecución local backend. git diff --check correcto. Gates del PR documental se registrarán por SHA exacto antes del merge en su conversación; el registro final de dev/Pages/ZIP y Drive se confirma mediante comentarios de cierre y lectura de retorno.

## Pendientes y límites

#191 sigue abierto: presentación contable institucional y validación operacional. Las carreras ajuste/cierre o imputación permanecen como criterios QA adicionales; este incremento prueba carreras entre ajustes. No crear reglas contables ni devolución efectiva desde esta entrega. Main permanece6dfb9546a4873baff15955cf86abfd7d47e3d111; no promover. Srv01 pausado#97, despliegue QA pendiente, sin QA física/UAT. No tomar PR/archivos de Claude. Commit local preparado430ee44 preservado. Drive: Línea Base Maestra será actualizada con resultado final y verificada con lectura de retorno.

## Pages e instalable verificados del funcional

Showcase postmerge36716880758 SUCCESS y publicación descargada: https://pablodominguez8375-2025.github.io/Modernizaci-n-GLMC-Milenio/; downloads/qa-current.json sourceSha49b3d1b72d6cba3014d3d1ffa253f321f7a0fc14. Incluye #228 sobre #224/#225/#226/#227 y #221/#222. ZIP público Proyecto-Centenario-QA-srv01-49b3d1b72d6c.zip: SHA-2565d7505cb505a2556ae26a548daef3bfa00cc4e9fa4cbc8b751816c95aaa64c96,808checksums internos y BUILD-INFO SOURCE_SHA/BUILD_RUN_ID36716880758 verificados. Artefacto Actions del runQA36716880651 mismo sourceSHA tiene digest de ZIP de9cf48d491dc162a52cc1b0b60e3de995a0ea5deb2bb7c846e72f7e5d29659c (808checksums); no confundir digest entre ejecuciones. El cierre documental actualizará el SHA publicado; su registro exacto final y Drive quedan en comentarios #229/#191 con lectura de retorno. Despliegue QA pendiente.
