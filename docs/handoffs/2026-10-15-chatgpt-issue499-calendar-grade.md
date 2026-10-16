# Issue #499 — Agenda por grado, contrato backend y continuidad (15-10-2026)

Estado: EN DESARROLLO (rama; no integrado). Issue: https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/issues/499

- `dev` de arranque: `77557f87d2979b6ef23f636c7c99bec329f70618`.
- `main` estable sin promoción de este trabajo; QA `glmdch` sin despliegue.
- Alcance: grado `all`, `apprentice`, `fellow_craft`, `master`; eventos históricos sin grado = generales.
- Corregir **servidor**, no solo UI: GET calendario y exportación ICS, grado efectivo por identity-link/degree-events, pertenencia activa y cargo vigente de su Taller, sin confiar en parámetros de consulta. Sin acceso entre Talleres.
- Propagar grado de Tenidas, Instrucciones y Ceremonias en interceptor/reconciliación; agregar migración EF Core y pruebas.
- Respetar separación de archivos: UI y demo correspondientes a Claude después de integrar backend.
- **No afirmar CI, Pages, QA, UAT ni integración hasta verificar.** No tocar `main`, `glmdch`, `srv01`, secretos ni datos reales.

## Registro de arranque

Se verificaron `START-HERE.md`, `AGENTS.md`, GOV-003, issue #499, `dev` y esquema de calendario. Falta readback final y evaluación completa de pruebas.

## Implementación entregada a PR draft #500

Cambios en rama (sin aprobación aún de CI exact-HEAD):
- `InstitutionalCalendarEvent.Grade` nullable (histórico general); nueva migración `20261015230000_AddCalendarEventGrade`.
- `CalendarCodes.Grade` controla valores admitidos: `all`, `apprentice`, `fellow_craft`, `master`; Tenidas `fellowcraft` se traduce al código de API.
- Sincronización `CalendarSourceSyncService` y `CalendarSourceProjectionInterceptor`: grado desde Tenidas/Docencia y `initiation/wage_increase/exaltation`; afiliación/incorporación generales.
- `CalendarGradeVisibilityService` resuelve `member_identity_links`/grado efectivo mediante el resolver institucional existente y revisa membresía vigente y asignaciones `OfficeAssignments` fechadas. Sin membresía activa o con cargo sin inicio verificable no hay excepción de acceso a grados superiores. Administradores institucionales autorizados conservan supervisión; nunca se extiende acceso de un Hermano a otro Taller.
- `CalendarEndpoints` aplica filtro **antes** de proyectar eventos tanto en GET como ICS; exportación/consulta con `private, no-store`. Registro manual y fuente validan grado, proyección DTO expone `Grade`.
- Pruebas `CalendarGradeVisibilityTests`, `CalendarGradeAccessPostgreSqlTests` y actualización de suites de migración/reconciliación. Registro CI de integración en `.github/backend-integration-shards.json` (shard b).
- Falla inicial de CI #38838974957 observada: nuevo test PostgreSQL no incluido en el manifiesto de shards; corrección registrada en commit posterior. No declarar gates SUCCESS hasta verificar últimas ejecuciones sobre SHA exacto.

## Estado y pendientes

- PR: https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/pull/500 (draft; sin merge).
- Adenda Drive registrada, verificada y movida a carpeta oficial Proyecto Centenario: https://docs.google.com/document/d/1MvA6Phhl7nRBZxGMzLoNU7H5nQ1jSMJTR7v0omjvGe0/edit
- `dev` de referencia al comienzo: `77557f87d2979b6ef23f636c7c99bec329f70618`.
- Ejecutar/leer CI, Showcase y QA Installable del HEAD final; corregir fallas, readback; sólo entonces evaluar squash a `dev`.
- Faltan validaciones de avisos automáticos de Agenda/Instrucción, UI/etiqueta de grado y datos sintéticos de demo con Claude; QA institucional y UAT posteriores. No tocar `main`, `glmdch` ni `srv01`.
- Los cargos históricos sin fechas deberán completar vigencia verificable antes de ejercer bypass por oficio.
