# Cierre técnico — permisos de Hospitalaria del Taller

Issue #266 / PR funcional #322. Inicio dev a07aab8e37a8998812850de61e0b80a5503f69cd; HEAD validado 6ce125ef121429b8c9d2dc680f779eb2be119da9; squash funcional dev 1d8bd769ddb1f7818b816173de2e68b407263426. Árbol 261e81b2c4efb6ea7b12861f506a45068642b981 idéntico al probado. Main conserva 6dfb9546a4873baff15955cf86abfd7d47e3d111.

## Entrega

Movimientos, autorizaciones, rendiciones y reposiciones/pagos/transferencias locales requieren autoridad institucional vigente intersectada con hospitalaria/view,create,write en sujeto y Taller exactos. Revocación, vigencia/inactivación y resolución del Taller persistido se comprueban en servidor. Un grant técnico no crea cargos ni acuerdo del Consejo. Proyección propia mínima no-store, pantalla bloqueada mientras comprueba acceso, controles retirados según acciones, datos/respuestas/borradores aislados por contexto. Adaptador demo reproduce restricciones técnicas; su autoridad visual sigue intersectada con capacidades existentes y no sustituye JWT real.

Sin migración ni cambios de tarifas, firmas, cifras, estados financieros, auditoría o relaciones. [DB-009](../modelo-datos/PMGM-DB-009-acceso-efectivo-hospitalaria.md) y [registro GOV-004](../modelo-datos/cambios/2026-10-04-issue-266-acceso-hospitalaria.md) integrados con el código. [Handoff inicial](2026-10-04-chatgpt-hospitalaria-access.md) conserva la evaluación Drive previa. Archivos: nuevo DynamicHospitalariaAccess; LodgeHospitalariaEndpoints y HospitalariaEndpoints; nuevas pruebas Authorization/Integration; HospitalariaPage/test; useHospitalariaAccess; api/dynamicAccess, pmgmApi y hospitalariaAccess.test; DB-009, registro e inicial (14 archivos en PR funcional).

## Gates y evidencia exact-head

| Gate | Run | Resultado |
|---|---|---|
| PMGM CI | 37246060742 | SUCCESS |
| Showcase | 37246060724 | SUCCESS |
| QA Installable | 37246060716 | SUCCESS |

Backend PostgreSQL 487/487 PASS, cero fallos/omisiones; frontend416 PASS, lint/build PASS. Nuevas pruebas de política y HTTP real: consulta administrada, creación, escritura denegada, revocación, sin escalamiento al Tesorero, autoaprobación impedida y aprobación del Venerable con write. Parámetro de Taller ajeno no sustituye el ámbito real. Smoke autenticado, HTTPS/recuperación e infraestructura SUCCESS. Gates privacidad12/clasificación99/migraciones61 PASS.

TRX artifact11319705419: SHA2561d86eb58d32fbef8a004bc4091361ba12881493efa264080c130d994eb2fdaf2; TRX09a6823ef417f3776bb86229ace307fd56e8ef1fbde3cb77c2288c3fb72950bd. Descargados, CRC y resultados de las dos pruebas nuevas comprobados; PostgreSQL configurado en logs.

Responsive artifact11318349760: SHA2566f949df098faa89633db7897f40546bdf0ec8e7ad7c6ec7a6db04784f82817fd. Descargado/CRC verificado: 761 PNG; auditoría21 perfiles/593 estados móviles PASS. Inspección directa: Hospitalaria del Taller 360x800 y Gran Hospitalaria 1440x900. La captura local muestra encabezado/selector hasta el primer pliegue; no certifica todos los controles bajo el pliegue. No hay captura específica de perfiles dinámicos administrados; su restricción está cubierta por pruebas de API/demo y pantalla inicial cerrada.

QA artifact11319435834: SHA2565e6e7e2c70aa449e5fb0a2678d2fc846b6ba34ca8568dbd1ca07cd1da1687aae. ZIP Proyecto-Centenario-QA-srv01-6ce125ef1214.zip SHA25666a31e1a63798bc22cbba7ccbf6824cb43bba462f2af11676bf2b5c2e9bf71c5. CRC/1024 MANIFEST/SOURCE_SHA/BUILD_RUN_ID37246060716 comprobados. Evidencia de rama; no confundir con el paquete público posterior al merge.

## Publicación y continuidad

Postmerge de 1d8bd769... y publicación en curso al crear este handoff. Pages inicial a07aab8e... y paquete7fce4e7e... comprobados nuevamente durante la intervención. SHA público final, checksum y gates posteriores se completan en los recibos de PR #322, PR documental e Issue #266 y Línea Base; no reutilizar el paquete inicial como evidencia nueva. URL: https://pablodominguez8375-2025.github.io/Modernizaci-n-GLMC-Milenio/

Línea Base actualizada/leída de retorno con historial preservado y fecha nativa; revisión inicial y segunda revisión sólo encuentran cierres/avances propios, sin nuevas instrucciones normativas en Proyecto Centenario/subcarpetas. PR documental reserva sólo este handoff nuevo y una línea en START-HERE; gates exact-head propios antes de squash. Sin cambios funcionales en ese PR.

## Pendientes y reservas

#266 permanece abierta: Gran Hospitalaria, regularidad institucional, otras operaciones de Gran Tesorería, navegación global y módulos restantes. App.tsx excluido por colisión caliente con #116. Cruce no caliente #116 sólo pmgmApi.ts; #60/#58 sin cruces. PR #1 promoción bloqueada, no desarrollo independiente. Reservas funcionales liberadas tras squash; no modificar ramas históricas ni identidad/workflows/migraciones.

#66 conciliada mediante comentario: residual técnico #275 entregado por #276/#309, pero criterio de aceptación funcional/UAT integral aún pendiente; conservar abierta y #116 NO FUSIONAR TODAVÍA. #191 conserva definiciones contables institucionales pendientes. Main/srv01/UAT pausados; **despliegue QA pendiente**, sin certificar schema físico, instalación ni aceptación institucional.

