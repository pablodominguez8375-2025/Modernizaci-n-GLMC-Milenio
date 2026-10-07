# Gran Tesorería — permisos efectivos de Orden — #266

Continuación autorizada por PO. Base dev89772b2d72639e09872b2b1f5bfceee65fca17cc; main6dfb9546a4873baff15955cf86abfd7d47e3d111 congelada. Drive revisado: sólo recibo de cierre anterior en Línea Base; sin novedades aplicables en raíz/subcarpetas del proyecto.

Corte: grants treasury de Orden view/write para cuadros mensuales/conciliación, regularidad Taller/persona y derechos ceremoniales; crear tarifario conserva create ya integrado. Intersección con autoridad institucional. Proyección privada de acceso propio, API/demo/UI concordantes y invalidación por contexto. Sin migración, tarifas/cálculos/cargos/firmas preservados.

Reserva: nuevo Authorization/DynamicGrandTreasuryAccess.cs; TreasuryEndpoints.cs, TreasuryStatementEndpoints.cs, Ceremonies/CeremonyEndpoints.cs (sólo RecordCeremonyRightPaymentAsync); nuevos tests; GrandTreasuryPage.tsx y nuevos tests/hook; RegularityPage.tsx; CeremonyRightsPage.tsx; api/dynamicAccess.ts y pmgmApi.ts/tests nuevos; este handoff y registro datos nuevo. App.tsx excluido: archivo caliente incluido en PR histórica #116, NO MERGE; navegación global sigue pendiente. Solape no caliente pmgmApi.ts con #116 declarado. srv01/UAT pausados; despliegue QA pendiente.

PR draft #350 reclamado antes de programación. Implementación en rama: backend/demostración/pantalla y pruebas de capacidades view/write; dos unitarias y un HTTP PostgreSQL nuevos. Gates exact-head CI/Showcase/QA requeridos; evidencia y cierre posteriores se registrarán en GitHub/Drive.

Registro de diccionario/estructuras sin migración: [GOV-004](../modelo-datos/cambios/2026-10-06-issue-266-gran-tesoreria-acceso.md). Despliegue QA pendiente. Solape no caliente CeremonyEndpoints.cs y pmgmApi.ts con #116 declarado; App excluido.

Anti-conflicto: PR #58 sin solape; #60 histórica solapa TreasuryEndpoints.cs no caliente, declarado; #116 histórica solapa CeremonyEndpoints.cs/pmgmApi.ts no calientes, declarado. App.tsx excluido. Frontend447/lint/build y privacy/classification/migration locales PASS; .NET local no disponible, backend/HTTP PostgreSQL se ejecutarán en CI exact-head.
