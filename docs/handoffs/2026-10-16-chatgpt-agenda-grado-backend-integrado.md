# Handoff definitivo — Agenda por grado, backend integrado (#499 / PR #500)

Fecha: 16-10-2026, Chile. Responsable: ChatGPT. Esta es la continuación postmerge del handoff anterior `docs/handoffs/2026-10-15-chatgpt-issue499-calendar-grade.md`.

## SHA, integración y fuentes

- Base inicial de implementación: `dev@77557f87d2979b6ef23f636c7c99bec329f70618`.
- Base inmediatamente anterior al merge, con UI Claude y seguridad QA preservadas: `dev@663bd0393bb93a295990f47fdfb2121b165c8a40`.
- PR #500 HEAD `545d08833bdd75072fbce4750190d217953b8d93` integrada con squash en **`dev@d8e31acb1d66860f5ff50c920a363a9c66cdfbbb`** el 16-10-2026 12:20:03 UTC. Estado GitHub confirmado `merged=true`. No se modificó `main@6dfb9546a4873baff15955cf86abfd7d47e3d111`.
- GitHub: [PR #500](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/pull/500), [Issue #499](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/issues/499), [UI siguiente #508](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/issues/508).
- Drive, adenda sincronizada y verificada: [Agenda por grado](https://docs.google.com/document/d/1MvA6Phhl7nRBZxGMzLoNU7H5nQ1jSMJTR7v0omjvGe0). Fuente de reglas: descripción aprobada de Issue #499; no redefinirla.

## Contenido entregado

- Migración EF Core `20261015230000_AddCalendarEventGrade`: agrega columna `Grade` nullable, eventos históricos generales. `CalendarDbContext` y entidad de calendario actualizados.
- Valores: `null` / `all` general, `apprentice` primero, `fellow_craft` segundo, `master` tercero. Normalización desde grados de Tenidas e Instrucciones; tipos de Ceremonia iniciación, aumento, exaltación.
- `CalendarSourceSyncService` e interceptor sincronizan los grados fuente. `CalendarEventProjection` entrega propiedad `Grade`.
- `CalendarGradeVisibilityService` aplica en el servidor: grado efectivo + membresía activa por Taller, cargos vigentes con fechas verificadas para todos los grados **de su Taller**; sin fuga de otro Taller. Eventos generales del Taller también exigen membresía activa; eventos institucionales de Gran Logia conservan RBAC y no se confunden con Taller por tener `OrganizationId`.
- GET `/api/calendar` y `/api/calendar/ics` filtran antes de proyectar/exportar; respuesta privada no-cache. Formulario manual/API valida grado contra el contrato; no depende de filtros cliente.
- Tests: `CalendarGradeVisibilityTests` (incluidas regresiones `all/null` propios/ajenos y evento general Gran Logia), `CalendarGradeAccessPostgreSqlTests`, `CalendarPostgreSqlTests`, `CalendarSourceSyncPostgreSqlTests`; test de PostgreSQL asignado a shard `b`.

## Gates y artefactos

- **HEAD PR** `545d08833bdd75072fbce4750190d217953b8d93`: PMGM CI [#38892358611](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/actions/runs/38892358611) SUCCESS; Showcase [#38892358348](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/actions/runs/38892358348) SUCCESS; QA Installable [#38892358495](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/actions/runs/38892358495) SUCCESS. GitHub reportó todos los jobs requeridos verdes. No trasladar estos checks automáticamente a otro SHA.
- **Postmerge dev** `d8e31acb...`: QA Installable run #38899779221 SUCCESS; artifact ID **12045453005**, nombre `proyecto-centenario-qa-srv01-d8e31acb1d66860f5ff50c920a363a9c66cdfbbb`, 8.002.999 bytes, caducidad 15-11-2026T12:20:16Z. ZIP envolvente SHA256 **no recalculado/confirmado** en esta intervención. CI postmerge #38899779345 y Showcase #38899779264 estaban en ejecución al primer registro.
- **GitHub Pages publicado:** todavía NO se ha verificado `downloads/qa-current.json` ni el SHA publicado; no afirmar que Pages ya incluye esta versión. `Build showcase SUCCESS` en PR demuestra compilación, no publicación; reconsultar el deploy de dev.
- **QA real glmdch:** sin despliegue/smoke/UAT institucional por esta PR. Artifact no equivale a instalación física.

## Continuación paralela

- **UI/responsive**: Issue #508 creado como especificación lista para que el PO/Claude lo reclame formalmente con `agente:claude` y PR DRAFT desde dev vivo. Etiquetas General / 1° / 2° / 3°, resumen de alcance, fixtures sintéticos con perfiles, tests responsive 360/768/1440 y paridad API/ICS. **No asignar directamente a Claude en su lugar ni tocar archivos UI reservados**.
- **Avisos de agenda e instrucción**: comprobar si el emisor de avisos consulta directamente entidades de Tenida/Docencia, sin el filtro Agenda; si existe ruta paralela, abrir Issue backend para aplicar el mismo contrato sin filtrar sólo en React.
- **QA**: solo pruebas sintéticas y gates, no modificar Docker/glmdch; los pendientes de almacenamiento Silo están en PR #506 y #479 con reservas separadas.
- Cualquier cambio posterior crea PR nueva desde SHA `dev` vivo y respeta GOV-003. Nada de `main` ni promoción a producción sin UAT y autorización.

## Archivos

La PR #500 modificó `.github/backend-integration-shards.json`, `CalendarDbContext`, nueva migración EF, `CalendarCodes`, `CalendarEndpoints`, `CalendarGradeVisibilityService`, `CalendarProjectionService`, `CalendarSourceProjectionInterceptor`, `CalendarSourceSyncService`, `InstitutionalCalendarEvent`, `Program.cs` y suites backend de integración/unidad. La PR documental de cierre sólo agrega este handoff y una línea de entrada `START-HERE.md`.
