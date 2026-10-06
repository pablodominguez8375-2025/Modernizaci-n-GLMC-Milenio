# Cierre técnico #191 — caja y cuotas no recuperadas

## Alcance y fuente
Decisión PO registrada en Issue #191 y Línea Base Maestra: no plan de cuentas por ahora; dinero efectivamente recibido por mes/año de recepción; cuotas/regularidad como control paralelo del requisito financiero para derechos masónicos. Reconocimiento de deuda no recuperada por expulsión por no pago como pérdida separada del informe, sin egreso ni descuento de caja.

Drive revisado antes de programar: solo Línea Base presentó actualización pertinente desde consulta anterior. Instrucciones de seguridad delegadas y propuesta Claude pendientes fuera de este corte. No se modifica identidad gráfica ni reglas institucionales ni archivos reclamados de PR #327/#116.

## Implementación PR #329
Base dev 4a026bdf62adb8579ad3faab08d57975383c0185; main 6dfb9546a4873baff15955cf86abfd7d47e3d111. HEAD exacto 58103ca36c761eec72fdfcc7fb72c9c118a8bf1d.
Tabla inmutable core.lodge_unrecovered_dues, migración 20261006002500. Reconocimiento explícito por retiro forzoso aprobado, confirmación causa no pago, respaldo, monto servidor, crédito recibido revisado antes, unicidad por cargo/idempotencia y auditoría. Reporte separado por fecha/moneda; UI Tesorería → Reportes y adaptador sintético. Conserva obligaciones, regularidad, caja y abonos. Recuperaciones posteriores son ingresos reales con fecha efectiva; no reescriben el reconocimiento histórico.
DB-010, cambio GOV-004 y GL-023 incluidos. 15 archivos modificados en funcionalidad; sin App.tsx/estilos ni migraciones históricas de #116. PmgmDbContext/pmgmApi son cruces no calientes registrados.

## Evidencia exact-head
PMGM CI 37394550399 SUCCESS. TRX:489 ejecutadas,489 aprobadas,0 fallos; CLP y USD de LodgeUnrecoveredDuesPostgreSqlTests aprobados. Frontend422/422, lint/build PASS; privacidad12, clasificación100, migraciones62 PASS. Smokes autenticado/piloto y backup/recovery PASS. Backend no ejecutado localmente por ausencia SDK/PostgreSQL; evidencia es CI/TRX.
QA 37394550907 SUCCESS; artifact11382451635 SHA2565baf2f20f284d839ee51bb1b09db602c5957f1452d4021cae6df9125eadd0319. ZIP CRC PASS, MANIFEST1.038 entradas PASS, BUILD-INFO SOURCE_SHA58103ca36c761eec72fdfcc7fb72c9c118a8bf1d.
La revisión6767294 contenía expectativa201 errónea para imputación existente200; corregida antes de integrar y validada en HEAD exacto. Contrato existente conservado.

## Pendientes operacionales
Issue #191 se mantiene abierto hasta aceptación operacional. Sin cuentas contables nuevas; decisión PO reemplaza antiguos pendientes de diseño de cuentas en fuentes históricas.
Despliegue QA pendiente: srv01/UAT pausados por #97; no instalación ni prueba física ni promoción main. Demo e instalable no equivalen a operación institucional.

## Integración y publicación
PR #329 integrado por squash en dev@a2da261f6b8bd918a438f24299ddd6827e1d4a38; main conserva su SHA. Gates exact-head CI, Showcase y QA SUCCESS. Showcase incluye la matriz responsive; no sustituye aceptación institucional.
Publicación y gates postmerge en curso al escribir este handoff. Ver recibo final en Issue #191 y PR documental de este handoff para SHA publicado, QA-current/manifest y validación de demo. No declarar operacional mientras srv01/UAT están pausados.
Este PR documental sólo añade este handoff y una línea de enlace en START-HERE; no modifica comportamiento ni requisitos.
