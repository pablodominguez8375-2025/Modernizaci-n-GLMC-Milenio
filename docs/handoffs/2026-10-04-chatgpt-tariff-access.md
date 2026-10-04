# Continuación urgente — acceso efectivo al tarifario

Issue #266. Base dev `be69bc341f99daa9e9542b776c073adab31ae651`; main `6dfb9546a4873baff15955cf86abfd7d47e3d111`.

## Revisión previa de fuentes y reservas

Drive Proyecto Centenario y sus dos subcarpetas revisados desde 19:36 UTC del 04-10: sólo cambian Línea Base, instrucciones tarifario v2 y su adenda por el cierre anterior. Sin nueva regla, identidad o alcance institucional. Línea Base conserva la revisión comprobada en el cierre del tarifario y exige continuar #266 por módulos.

PR abiertas #116, #60 y #58 históricas inspeccionadas; no hay archivos calientes compartidos con este corte ni se edita ninguna de sus ramas. #275 y #308 se concilian como completadas: PR #276 y #309 ya integradas, con CI/Showcase/QA/Pre-UAT postmerge SUCCESS. UAT/despliegue institucional separado en #97, pausado.

## Alcance reservado antes de programación

Catálogo/versiones y registro de decretos de Gran Tesorería: grants `treasury/view` y `treasury/create` en Orden, intersectados con autoridad institucional vigente. Proyección propia privada y mínima; revocación no restituye permisos antiguos. API, pantalla y demo conservan la misma política. Sin cambios de tarifas, datos históricos, cargos o atribuciones.

Archivos previstos: helper DynamicTariffAccess, GrandTreasuryTariffEndpoints, prueba HTTP; API/demo, TariffDecreePage, hook y pruebas frontend; nuevos diccionario y registro de acceso. Sin migración, App, tema, workflow o SessionEndpoints. Las restantes operaciones de Gran Tesorería/Hospitalaria y otros módulos siguen pendientes en #266: este corte no certifica aplicación global.

## Validación e integración

Pendientes de implementación y gates propios; no certificar todavía. START-HERE se enlazará en PR documental posterior al merge funcional. Main/srv01/UAT sin cambios; despliegue QA pendiente.
