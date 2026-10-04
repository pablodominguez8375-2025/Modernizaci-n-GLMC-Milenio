# Continuación urgente — acceso efectivo al tarifario

Issue #266. Base dev `be69bc341f99daa9e9542b776c073adab31ae651`; main `6dfb9546a4873baff15955cf86abfd7d47e3d111`.

## Revisión previa de fuentes y reservas

Drive Proyecto Centenario y sus dos subcarpetas revisados desde 19:36 UTC del 04-10: sólo cambian Línea Base, instrucciones tarifario v2 y su adenda por el cierre anterior. Sin nueva regla, identidad o alcance institucional. Línea Base conserva la revisión comprobada en el cierre del tarifario y exige continuar #266 por módulos.

PR abiertas #116, #60 y #58 históricas inspeccionadas; no hay archivos calientes compartidos con este corte ni se edita ninguna de sus ramas. #275 y #308 se concilian como completadas: PR #276 y #309 ya integradas, con CI/Showcase/QA/Pre-UAT postmerge SUCCESS. UAT/despliegue institucional separado en #97, pausado.

## Alcance reservado antes de programación

Catálogo/versiones y registro de decretos de Gran Tesorería: grants `treasury/view` y `treasury/create` en Orden, intersectados con autoridad institucional vigente. Proyección propia privada y mínima; revocación no restituye permisos antiguos. API, pantalla y demo conservan la misma política. Sin cambios de tarifas, datos históricos, cargos o atribuciones.

Archivos previstos: helper DynamicTariffAccess, GrandTreasuryTariffEndpoints, prueba HTTP; API/demo, TariffDecreePage, hook y pruebas frontend; nuevos diccionario y registro de acceso. Sin migración, App, tema, workflow o SessionEndpoints. Las restantes operaciones de Gran Tesorería/Hospitalaria y otros módulos siguen pendientes en #266: este corte no certifica aplicación global.

## Validación e integración

Implementación: proyección propia GET `/api/tesoreria/tarifarios/decretos/acceso`; GET catálogo exige view y POST registro create, en Orden y con autoridad institucional vigente. Backend reevalúa cada petición, demo comparte política, pantalla refresca por catálogo/foco/30 s y retira datos/formulario cuando pierde acceso. Se conserva la auditoría de alta, inmutabilidad y tarifas anteriores.

Diccionario y estructuras: [DB-008](../modelo-datos/PMGM-DB-008-acceso-efectivo-tarifario.md); [registro antes/después](../modelo-datos/cambios/2026-10-04-issue-266-acceso-tarifario.md). Sin migración.

Frontend local: 414 PASS, lint/build SUCCESS sobre el árbol final, incluido refresco sin cerrar el asistente por una consulta periódica sin cambios. Backend política y HTTP PostgreSQL añadidos, ejecución pendiente CI por ausencia de SDK .NET local. Gates propios pendientes; no certificar todavía. START-HERE se enlazará en PR documental posterior al merge funcional. Main/srv01/UAT sin cambios; despliegue QA pendiente. Recibos de PR #320 completan el SHA exacto y gates; handoff postmerge agregará evidencia final.
