# Cierre técnico — consulta de crédito propio en Mi ficha

Issue #254; remanente acotado de #58/#36/#191. PR funcional #255; base viva 86945d34278f874313873f56f7f0a3f9dc417a6c (Claude #253/UI QA v0.66 preservados). Main permanece 6dfb9546a4873baff15955cf86abfd7d47e3d111.

## Resultado

La API ya entregaba unappliedCredits; contrato, demo sintética y cartola ahora muestran recibo, fecha, moneda y disponible. CLP/USD separados, sin conversión ni compensación automática de deuda. IDs internos y referencias bancarias no se muestran. Campo opcional admite respuestas anteriores. No se modifica backend productivo, fórmula contable, tarifa ni migración.

Reserva #254 y draft #255 anteriores a programación. Revisión de ramas/reservas/comentarios/Actions y cruces antes del primer commit y del merge. Cruce no caliente #58 comunicado; ramas ajenas y archivos calientes intactos. Este PR documental agrega exactamente una línea a START-HERE, después del squash funcional.

## Evidencia técnica

Head funcional 741c3ba5319ba803e26bffba4a94e987d5bf6359:
- CI 36936902421, Showcase 36936902379 y QA Installable 36936902381: SUCCESS antes de squash.
- Backend 384/384 PASS, 0 fallos/omisiones. TRX artifact 11198209095, digest 9133379504f903988e0bb61c4dab3bfc39dc4c9daeef7c5fa6ebdbc9f13122da comprobado, dos nuevos escenarios HTTP PostgreSQL CLP/USD PASS.
- Cada escenario comprueba corrección que libera crédito, anulación, recibo agotado, crédito sin cargos en su moneda/Taller anterior, aislamiento por identidad aunque se suministre otro MemberId, private/no-store, ausencia de ID documental y deuda/originales intactos. PostgreSQL real habilitado en CI.
- Frontend 264/264 PASS en runner y local; container 263 PASS/1 skip de paridad ambiental. Lint/tsc/build PASS. Privacy 12, classification 99, migration safety 58 PASS.
- CI inicial: 382 PASS/2 FAIL por fixture sin grado y ReceiptNumber duplicado; corregidos sin cambiar producción ni relajar assertions. Evidencia fallida conservada en #255.

Squash funcional: 358bed71e40a49a17e25f467f03d9ffba8708853. El SHA documental final y su publicación se registrarán en recibos finales #254/#255 y PR documental; no reutilizar evidencias de cortes anteriores. Se comprobarán downloads/qa-current.json, ZIP SHA-256, cada MANIFEST, SOURCE_SHA y BUILD_RUN_ID, más demo del mismo SHA. Handoff de implementación y matriz: docs/handoffs/2026-10-01-chatgpt-member-treasury-credits.md; docs/qa/PMGM-QA-MEMBER-CREDITS.md.

## Pendientes y límites

Cierre sólo técnico de #254. #58/#191 permanecen abiertos: cargos genéricos/vencimientos, comprobante/documento/foto según requisitos y alcance de presentación financiera institucional/operación requieren sus intervenciones propias. Revisión visual detallada de cartola móvil y aceptación institucional siguen pendientes salvo evidencia explícita registrada posteriormente. Capturas responsive automatizadas no equivalen a UAT.

#131 sustituido por #246/#247; no rehacer. #116 mantiene NO FUSIONAR TODAVÍA; #60 conserva brechas institucionales, no promover su rama antigua; #1 dev→main requiere autorización/UAT. #44/#235, Perú USD6 #190 y publicación/evidencia #237/#239/#240 preservados (legacy_not_recorded sin inventar antecedentes).

Claude #253 integrado/P3 técnico v0.66. Adenda Drive aún PROPUESTA: conciliación avisada en #253, sin inventar aprobación ni reclamar #252. Auditoría de exportaciones en servidor sigue delegada y pendiente de alcance/reserva propios.

srv01 pausado; despliegue QA pendiente. Sin instalación, QA física ni UAT. Línea Base de Drive y GitHub recibirán el resultado final y lecturas de retorno antes de cerrar la intervención.
