# Navegación de permisos — #266

Base dev e0300e7a7c8b32b281e00340a6581baa03546747; main6dfb9546a4873baff15955cf86abfd7d47e3d111. ChatGPT reclama App.tsx (sólo acceso), hooks de navegación y pruebas frontend. Sin PR vivas de Claude en archivos calientes; #357 ya integrada. Drive revisado: sólo adenda #357 consolidada, sin nuevas reglas.

Alcance: navegación de Gran Tesorería, Gran Hospitalaria y Hospitalaria local intersectada con sus proyecciones propias existentes. Revocación/denegación no deben dejar menús, buscador o vista protegida abiertos. No nuevos permisos institucionales, migraciones, CSS, workflows o cambios en perfiles del administrador. Mantener legado autorizado y contratos/backend vigentes.

Estado: reclamo; implementación y gates pendientes. #266 conserva otros módulos/operaciones. Main/srv01/UAT intactos.

## Implementación

Navegación y buscador global intersectan capacidades institucionales con acciones view de GET /api/tesoreria/acceso, GET de Gran Hospitalaria y GET local de Hospitalaria por Taller (contratos existentes). Sólo se consultan ámbitos autorizados por el cargo. Una proyección fallida deniega ese módulo sin anular otros. Vista revocada vuelve a Inicio y no monta datos protegidos. Estado separado por cliente, sujeto y perfil; respuestas tardías se descartan por generación/desmontaje. Suscripción a cambios del catálogo invalida inmediatamente; foco/sondeo cada 30 segundos comprueban sin retirar un acceso estable mientras llega la respuesta. Backend conserva autoridad final en cada solicitud.

El administrador sigue creando/editando perfiles en los componentes existentes; no se cambian reglas de edición de perfiles de sistema. El grant técnico restringe, no concede atribuciones institucionales.

Archivos: App.tsx, useFinancialNavigationAccess.ts, financialNavigation.test.ts, operational-view.test.ts y este handoff. Sin cambios de datos/DTO persistente ni migración. Registro GOV-004: docs/modelo-datos/cambios/2026-10-07-issue-266-navegacion.md.

Pruebas locales: 457 frontend PASS; lint/build PASS. Tres pruebas nuevas: no consultar sin autoridad, errores parciales/View obligatorio y grant real demo/revocación/cambio de sujeto. Gates remotos y Pages/QA del SHA nuevo pendientes. Base funcional previa Pages e0300e7; no confundir con publicación de #359.
