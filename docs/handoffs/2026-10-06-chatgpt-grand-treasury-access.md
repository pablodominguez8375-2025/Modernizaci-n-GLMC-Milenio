# Gran Tesorería — permisos efectivos de Orden — #266

Continuación autorizada por PO. Base dev89772b2d72639e09872b2b1f5bfceee65fca17cc; main6dfb9546a4873baff15955cf86abfd7d47e3d111 congelada. Drive revisado: sólo recibo de cierre anterior en Línea Base; sin novedades aplicables en raíz/subcarpetas del proyecto.

Corte: grants treasury de Orden view/write para cuadros mensuales/conciliación, regularidad Taller/persona y derechos ceremoniales; crear tarifario conserva create ya integrado. Intersección con autoridad institucional. Proyección privada de acceso propio, API/demo/UI concordantes y invalidación por contexto. Sin migración, tarifas/cálculos/cargos/firmas preservados.

Reserva: nuevo Authorization/DynamicGrandTreasuryAccess.cs; TreasuryEndpoints.cs, TreasuryStatementEndpoints.cs, LodgeTreasuryCompletionEndpoints.cs (sólo pagos de derechos con autoridad Orden); nuevos tests; GrandTreasuryPage.tsx y nuevos tests/hook; RegularityPage.tsx; CeremonyRightsPage.tsx; api/dynamicAccess.ts y pmgmApi.ts/tests nuevos; este handoff y registro datos nuevo. App.tsx excluido: archivo caliente incluido en PR histórica #116, NO MERGE; navegación global sigue pendiente. Solape no caliente pmgmApi.ts con #116 declarado. srv01/UAT pausados; despliegue QA pendiente.

Estado inicial: reclamo previo a programación. Gates exact-head CI/Showcase/QA requeridos; evidencia y cierre posteriores se registrarán en GitHub/Drive.
