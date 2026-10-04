# Continuidad #266 — aplicación efectiva de permisos

Base dev e5697003dcd7749269a586518ccb35ad1ca50d75; main 6dfb9546a4873baff15955cf86abfd7d47e3d111. Reclamo ChatGPT, 04-10-2026.

Drive: nueva adenda Claude #315 revisada, Administración ordenada ya integrada; preservar sus cambios. Línea Base #267 confirma catálogo/evaluador terminados y aplicación pendiente.

Primer corte: proyección minimizada de permisos del usuario, navegación y operaciones de Tesorería del Taller. Las autorizaciones institucionales siguen siendo obligatorias. Reservas: Authorization/DynamicAccess*, SessionEndpoints; Treasury/LodgeTreasuryEndpoints; frontend App (sólo acceso), api/dynamicAccess/pmgmApi, LodgeTreasuryPage/Panel y pruebas; nuevo DB-006 y este handoff. No modificar identidad, workflows, START-HERE ni reservas ajenas.

Estado: análisis/reclamo, sin funcionalidad entregada. Cierre estimado: este corte técnico sujeto a gates exact-head. Aplicación de otros módulos conserva seguimiento #266. Sin migración prevista; contrato/acceso requiere registro GOV-004. Main/srv01/UAT intactos; despliegue QA pendiente.

## Implementación en rama

Proyección propia por Taller `GET /api/session/treasury-access`, private/no-store. Restricción técnica adicional a todos los handlers locales de Tesorería y preparación/consulta local del Cuadro mensual; IDs se resuelven desde la entidad. Print requiere POST autorizado/auditado. Baja lógica de recibo usa delete, corrección edit; no borrar físicamente. UI consulta sin formularios, sujetos demo exactos, navegación Tesorería y refresco de revocación. Ver DB-006 para semántica antes/después e incorporación histórica al control técnico.

Nuevos archivos: DynamicTreasuryAccess.cs, DynamicTreasuryAccessTests.cs, useTreasuryAccess.ts, api/treasuryAccess.test.ts y DB-006. Se extiende además LodgeReceiptAdjustments/TreasuryStatementEndpoints/TreasuryStatementPage y panel de ajustes; no migración. 406 frontend PASS, lint/build PASS antes del corte final. SDK .NET no disponible en este workspace: compilación y pruebas PostgreSQL deben verificarse en CI, sin declarar pruebas locales inexistentes.

Estado: draft pendiente de gates exact-head e integración/publicación. No afecta otros módulos de #266, que siguen pendientes. No usar la demo del PR #315 como evidencia de #316. Main/srv01/UAT sin cambios. SHA y resultados finales van en recibos del PR y Línea Base.
