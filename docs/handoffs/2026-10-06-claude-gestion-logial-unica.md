# Handoff — Claude — Gestión Logial con un solo menú y supervisión de Talleres — 06-10-2026

- **Issue:** #346 (`agente:claude`).
- **Aprobación del PO (06-10-2026):** puntos 1 a 4 de la adenda «Revisión menú Gestión Logial y Secretarías» y, para la Autoridad de Gran Logia, la **opción B**.
- **Revisión previa de Drive:** solo la carpeta Proyecto Centenario. ChatGPT cerró el PR #338 (zona de cuotas desde la Ficha) y el PR #342 (Menú móvil → Inicio). No hay archivos nuevos.
- **Base:** `dev@64934f2`.
- **Versión:** UI QA v0.90.

## Cambios

### Gestión Logial (`LodgeManagementPage.tsx`)

- **Un solo menú de 9 secciones.** La Ficha del Taller entra como pestaña `ficha` mediante `profileSlot`. Al abrir la vista `lodgeProfile`, se muestra esa pestaña (`initialTab`).
- **Resumen sin contenido repetido.** Se quitaron la ficha, los miembros, las «áreas de cada Taller», la agenda, los avisos y la cita. Quedan el cuadro de cargos, la próxima tenida, la asistencia, el plan de estudio y el seguimiento.
  - Altura: de **4.492 a 1.917 px** (PC, 1440 px).

### Secretarías (`App.tsx`)

- Se quitaron `SecretariatRoleNavigation` (carrusel por cargo) y las pestañas «Secciones del Taller» de `App.tsx`.
- Para Secretaría del Taller y Gran Secretaría, el menú lateral ahora ofrece:
  - **Afiliación e incorporación**
  - **Fichas de miembros**
  - **Gestor Documental**
  - **Ceremonias** (solo Gran Secretaría)

  Así se conservan todos los accesos que antes daba el carrusel.
- El ítem «Secretaría» se marca como activo solo en su propia vista.

### Autoridades de la Orden (opción B)

- `isGrandSupervision` se activa cuando el usuario cumple las tres condiciones:
  - tiene Gestión Logial;
  - no tiene Secretaría ni Tesorería de Taller;
  - tiene algún permiso de Gran Logia.
- En ese caso:
  - el menú y el título dicen **«Talleres (supervisión)»**;
  - se mantiene el selector de Taller;
  - la vista es de **solo lectura**: no puede gestionar Secretaría ni registrar instrucciones.
- Los cargos del Taller (Vigilantes, Orador) siguen viendo «Gestión Logial».

### Pruebas y auditoría

- Se ajustaron las pruebas `AdmissionsPage`, `listing` y `menus-sin-repetir`, y se agregó `gestion-logial-unica.test.ts`.
- En la auditoría `capture-showcase-views.mjs`:
  - el escenario de la Autoridad usa «Talleres (supervisión)»;
  - `openAdmissionProcedure` abre Afiliación desde el menú lateral.

## Verificación

- **438 tests en verde.** tsc y oxlint OK.
- **Playwright local:** sin errores; resultados por perfil:
  - **VM:** menú de 11 opciones.
  - **Secretaría del Taller:** 10 opciones, 1 solo menú en la vista.
  - **Gran Secretaría:** 12 opciones.
  - **Autoridad:** «Talleres (supervisión)».
- La auditoría completa queda a cargo del gate Showcase de este PR.

## Impacto en datos

**Ninguno:** solo cambian la navegación y la presentación.
