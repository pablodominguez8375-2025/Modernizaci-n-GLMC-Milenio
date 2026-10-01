# Cierre de concurrencia de imputaciones — 01-10-2026

Issue #191; PR funcional #248 y documental #249. Continúa [handoff técnico](2026-10-01-chatgpt-receipt-allocation-concurrency.md).

## Integración y reparación

Inicio dev `ae1e5497f50317f53d3c6612e8b15c94acec3077`; main `6dfb9546a4873baff15955cf86abfd7d47e3d111`. PR #248 integrada por squash en dev `2ee13ed570a8d5d28e7af110f373ad16c9646355`, desde head `92fb5e9f4912aeaf54ddb0dc83890fa542faf27f`. Gates exact-head SUCCESS: CI `36898425941`, Showcase `36898425796`, QA Installable `36898425780`. Dev/base/head vivos y los cuatro PR históricos/archivos comprobados antes de squash; ningún cruce funcional salvo #1, comparador de promoción que no se toca.

CI inicial `36897829717`, head `a3cc57baa4cdc855a8618e2d17dd6d7b1ae4d55f`: 374 ejecutadas, 371 PASS, 3 FAIL, 0 omitidas. Doble imputación CLP/USD dio 500+200 y anulación/imputación CLP 201+500; PostgreSQL rechazó la operación competidora con 40001, envuelto por Npgsql. TRX `11179932959`, SHA-256 descargado/verificado `d6d9255fc8247d141428386b10142dd13aad319b789267c24742e505895f987e`.

LodgeTreasuryEndpoints añade únicamente un wrapper al endpoint de imputación: reconoce 40001/23505 y sus envolturas conocidas y responde 409 después de rollback. Core y reglas previas conservados; no hay reintento automático ni clave idempotente nueva de imputación. La operación rechazada debe reintentarse explícitamente desde saldos actualizados.

Seis casos HTTP PostgreSQL con barrera dentro de ambas lecturas serializables: anulación/imputación, corrección a destino/imputación e imputación/imputación, en CLP y USD sintéticos. Exigen una operación confirmada y otra 409, originales intactos, destino no sobreabonado, caja/crédito conciliados y auditoría sólo del commit. El reintento incompatible mantiene 409; si la imputación ganó, la anulación posterior puede confirmar y revierte también ese abono. La clave del ajuste evita duplicarlo al repetirlo. No se relajaron assertions tras el fallo.

CI reparado: backend 374 ejecutadas/374 PASS, 0 fallos/omitidas; los seis casos nuevos se verificaron en TRX `11180991678`, SHA-256 `0164f5b84d204f56fd446e46f9d432d8e516deac2f3c8ca8f8e67aae14221f14`. Frontend checkout completo 254/254 PASS; contenedor con sólo frontend 253 PASS + 1 omisión condicional de paridad. Lint/build PASS; privacidad 12, clasificación 99 y migraciones 58 PASS. Sin SDK .NET local: evidencia backend procede de CI real con PMGM_TEST_POSTGRES. Autenticación sintética de CI usando permisos productivos; esto no acredita operación OIDC/SMTP/S3 ni UAT física. No hay migraciones ni cambios de UI/roles/normas/tarifas.

Cinco archivos funcionales/documentales: LodgeTreasuryEndpoints.cs, LodgeReceiptAllocationConcurrencyPostgreSqlTests.cs (nuevo), PMGM-QA-RECIBOS-AJUSTES-191.md, changelog nuevo y handoff técnico nuevo. Blobs remotos comparados con archivos locales, sin truncación. Demo conserva adaptadores y contratos vigentes; la carrera se valida en PostgreSQL, no en el mock público.

## Publicación y cierre persistente

#249 agrega exactamente una línea a START-HERE y este handoff nuevo; la base completa se conserva. Cruce documental no caliente con #116 informado, sin editar su rama. La Línea Base registra reserva, reproducción y reparación con lectura de retorno.

Este archivo no anticipa su propio squash ni el paquete final. El recibo persistente de [Issue #191](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/issues/191), PR #248 y #249 registra el dev final, gates postmerge, sourceSha de qa-current.json, ZIP/SHA-256, checksums internos, SOURCE_SHA y BUILD_RUN_ID real con Build/Deploy SUCCESS, y readbacks GitHub/Drive. Consultar ese recibo para la publicación y cierre confirmados; no usar el paquete anterior como evidencia del nuevo corte.

Inicio Pages ae1e549: paquete SHA-256 `e27d788f43264ee7c9fd694d45f0aa19318af69eff268eb1a695a75674319283`, MANIFEST 836/836, SOURCE_SHA y BUILD_RUN_ID `36895699866` verificados. Es evidencia del inicio, no de #248/#249. Demo: https://pablodominguez8375-2025.github.io/Modernizaci-n-GLMC-Milenio/ .

## Pendientes preservados

#191 permanece abierto por presentación financiera institucional de anticipos/recuperaciones, carrera ajuste/cierre anual y validación operacional. No inventar cuentas contables, devoluciones efectivas ni nuevas tarifas. #116 sigue draft/NO FUSIONAR TODAVÍA; #60/#58 conservan remanentes según GAP-001 sección 6; no introducir un segundo ledger de #58. #131 ya sustituida y cerrada sin merge; no rehacerla. #237/#44/#235 y Perú USD 6 preservados. UI v0.65 de Claude intacta, P3 pendiente de aprobación.

Main congelado; **srv01 pausado, despliegue QA pendiente**, sin instalación, QA física ni UAT. Retomar desde HEAD vivo, fuentes y recibos, con reclamo/draft y anti-conflicto antes de ampliar la siguiente cobertura autorizada.
