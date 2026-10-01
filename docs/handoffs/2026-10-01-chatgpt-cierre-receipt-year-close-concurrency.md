# Cierre de concurrencia entre ajustes y cierre anual — 01-10-2026

Issue #191; PR funcional #250 y PR documental posterior. Continúa [handoff técnico](2026-10-01-chatgpt-receipt-year-close-concurrency.md).

## Integración y comportamiento

Inicio dev `5f5d2f41b6e8af7900a49e69606bad086093d8f2`; main `6dfb9546a4873baff15955cf86abfd7d47e3d111`. PR #250 integrada por squash en dev `f28e2d975e472b534131440b3c14cf4b8ecd0057`, desde head `e0e41984c626385534824b821d45458da749d42b`. Gates de ese head SUCCESS: CI `36904985224`, Showcase `36904985526`, QA Installable `36904985554`. Base viva, PR abiertos y reservas/archivos revisados antes del squash; ningún cruce funcional con #116/#60/#58. #1 es comparador de promoción y no se toca.

Ocho pruebas HTTP con PostgreSQL fuerzan ambos órdenes de confirmación de anulación/corrección frente al cierre anual, en CLP y USD sintéticos. La primera lectura serializable de ejercicio cerrado queda retenida hasta que la petición competidora confirma; no hay esperas arbitrarias ni carreras dependientes del planificador. Se verifican originales inmutables, caja/crédito, importe y contador del cierre, auditoría sólo de commits, reintentos explícitos e idempotencia del ajuste.

Una anulación modifica la proyección de caja: el competidor con snapshot antiguo debe responder 409. Una corrección sin movimiento de caja puede confirmar junto al cierre bajo un orden serializable válido, o devolver 409. Si el cierre ganó, un ajuste retroactivo sigue bloqueado; un ajuste explícito del ejercicio actual conserva el cierre anterior. Si la anulación ganó, el cierre rechazado puede reintentarse con saldos actualizados.

CI inicial `36904459172`, head `338e9d0d724127e2e9ef9f7fe3f74c981fb06ba1`: 382 ejecutadas, 380 PASS, 2 FAIL, 0 omitidas. Anulación primero CLP/USD devolvía 500 en el cierre por PostgreSQL 40001 durante su inserción. TRX artifact `11182524459`, ZIP SHA-256 `d98bad11ceb19406db0e6ad39d77b09ee6f1e7b9f1597048886d8952a415e622`, TRX interno `8aa2c60f6488150f899e204616efe682eb2013385af40c7bd378dbce592865d7`, comprobados.

LodgeTreasuryEndpoints añade sólo un wrapper de 15 líneas a CloseAnnualPeriodAsync: reconoce 40001/23505 y las envolturas Npgsql conocidas; devuelve 409 después del rollback y disposición de la transacción. Core, cálculos y reglas de cierre se conservan. Sin reintento automático ni relajación de las aserciones.

CI reparado: 382 ejecutadas/382 PASS, 0 FAIL/omitidas. Los ocho casos nuevos se verificaron en artifact `11182863398`, ZIP SHA-256 `81aab7730c68b3b4667bc59ed264c0cea43c479cb4edf86e289c12e68092a261`, TRX interno `bc826de53b6c2622f44cb2b73c1c97fc83179b5fb72ccb1a4a37b15fe3ed7c33`. Logs confirman PMGM_TEST_POSTGRES activo; no son retornos sin conexión. Frontend y los cinco jobs CI aprobaron. Privacidad 12, clasificación 99, migraciones 58 y diff-check PASS. Sin SDK .NET local: evidencia backend proviene de CI real. No acredita OIDC/SMTP/S3 operativo ni UAT física.

Cinco archivos: endpoint, prueba nueva, PMGM-QA-RECIBOS-AJUSTES-191.md, changelog y handoff técnico nuevos. Blobs remotos cotejados completos con el contenido local. Sin UI, migraciones, permisos, tarifas ni decisiones contables nuevas. Demo conserva contratos/adaptadores existentes; estas carreras se prueban en PostgreSQL, no en el mock público.

## Fuentes y revisión de históricos

GitHub y Drive fueron consultados antes de intervenir: ramas, PR/issues/comentarios, Actions y reservas; START-HERE, AGENTS, README, Estado Maestro, NEXT, GOV-001/002/003, requisitos/ADR/pruebas y handoffs pertinentes. Carpeta Proyecto Centenario y Línea Base Maestra leídas. Instrucciones 29-09 figuran EJECUTADA y no se repiten; #237/#240 y sus recibos conservan evidencia original y legacy_not_recorded. Los resultados de cortes anteriores no se certifican como actuales.

- #131: ya cerrada sin merge y sustituida por #246/#247; no rehacer.
- #116/#66: draft y NO FUSIONAR TODAVÍA vigentes. Sin decisión específica que levante la restricción ni aceptación integral Afiliación/Incorporación; políticas ya existentes no completan comisión, cronología, materialización y UI.
- #60/#59: persisten transiciones y reincorporación institucionales, con historia/origen/destino y separación Past Activo/En sueño. Eventos/categorías tarifarias presentes no completan el circuito.
- #58/#36: cartola en Mi ficha ya existe sobre Tesorería vigente. No integrar el segundo ledger CLP de la rama antigua. Conciliar cargos genéricos/vencimientos, comprobante documental, privacidad y crédito visible antes de reemplazar/cerrar; foto y descarga segura siguen pendientes.
- #191: recepción, imputación, anticipos/crédito, ajustes e inmutabilidad y carreras ajuste/ajuste e imputaciones ya integradas; esta intervención completa cobertura ajuste/cierre anual. Permanecen presentación financiera institucional de anticipos/recuperación de mora y aceptación operacional. No inventar cuentas contables, devoluciones efectivas ni tarifas.
- #1: promoción dev → main sigue draft, sujeta a autorización y UAT institucional.

#44/#235, derechos de ceremonia, sustitución #45 y Perú USD 6 preservados. Claude #242/#244, UI v0.65, sin cambios; P3 pendiente de aprobación. UI/identidad/responsive/accesibilidad continúan a cargo de Claude.

## Publicación y registros persistentes

Este PR documental agrega exactamente una línea a START-HERE y este handoff nuevo, sin reescribir la base. Reserva/alcance de #191 y draft #250 precedieron al código; la Línea Base registra reserva, reproducción y reparación con lecturas de retorno.

El archivo no anticipa su propio squash ni el paquete final. Los recibos persistentes de [Issue #191](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/issues/191), PR #250 y el PR documental registran el dev final, gates postmerge, downloads/qa-current.json, ZIP/SHA-256, MANIFEST, SOURCE_SHA y BUILD_RUN_ID real con Build/Deploy SUCCESS. Consultar el recibo final para el corte publicado y verificación GitHub/Drive; no reutilizar paquetes de cortes anteriores.

Inicio Pages `5f5d2f41b6e8af7900a49e69606bad086093d8f2`: ZIP SHA-256 `84e36330a56adfde73e18107e600525d18d1570fcf5e806bebd877cdc11f48bf`, MANIFEST 840/840, SOURCE_SHA y BUILD_RUN_ID `36900878885` comprobados. Es evidencia del inicio, no de esta intervención.

Demo: https://pablodominguez8375-2025.github.io/Modernizaci-n-GLMC-Milenio/ . Main congelado; **srv01 pausado, despliegue QA pendiente**, sin instalación, QA física ni UAT.

## Siguiente alcance

Retomar desde HEAD vivo y GAP-001 sección 6. #191 no se cierra como aceptación institucional por CI verde. Próxima conciliación técnica posible: remanentes #58 sobre el modelo vigente, definiendo primero requisitos de cargos/vencimientos y comprobantes; las decisiones contables/tarifas requieren fuente autorizada. #116 conserva su restricción, #1 y operación #97 sus gates independientes. Mantener claim/draft, anticruces y evidencias del mismo SHA.
