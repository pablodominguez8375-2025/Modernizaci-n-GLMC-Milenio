# Handoff — Claude — UX P3: listados y vistas operativas — 01-10-2026

- Issue #252 (`agente:claude`). El PO aprobó P3 completo el 01-10-2026.
- Base: `dev@fdbae28` (PR #251). `main` sin cambios.

## Qué cambia (UI QA v0.66)

- **P3-1 — Barra de listado común** (`listing.tsx`, `listing.css`):
  - búsqueda y filtros;
  - chips de filtros activos con «Limpiar»;
  - «Mostrando X de Y»;
  - orden;
  - «Cargar más» desde 50 registros.

  Se aplicó a Fichas de miembros (orden por nombre, Nº, grado o estado).
- **P3-2 — Exportar CSV según permiso:**
  - **Formato:** separador «;», BOM UTF-8 y pie con contexto, fecha y nota «uso interno, Ley 21.719». Solo incluye las columnas visibles.
  - **Fichas de miembros:** disponible para Gran Secretaría, Secretaría del Taller y Administración.
  - **Gestor Documental:** exporta el listado de la colección.
  - **Tesorería › Reportes:** ya tenía «Exportar CSV filtrado» y paginación (dominio de ChatGPT), así que no se tocó.
- **P3-3 — Vista Tarjetas/Tabla en Fichas** (Gran Secretaría y Administración): encabezado fijo, scroll propio, fila seleccionable con teclado.
- **P3-4 — Pantallas largas:**
  - **Parámetros del sistema:** pestañas «Operación y respaldos», «Perfiles y accesos» y «Parámetros (n)». Los paneles inactivos quedan montados con `hidden`, así que conservan los borradores. En móvil la página pasa de unos 20.900 px a unos 1.240 px.
  - **Gestión Logial y Carga de insinuados:** índice de secciones fijo, con anclas.
- **P3-5 — Correcciones:**
  - El sello de Gestión Logial muestra el número del Taller (23) en lugar de «M».
  - El selector «Taller» pasa a ser solo texto cuando hay un único Taller, en Fichas, Gestión Logial y Tesorería del Taller.

## Verificación

- 52 archivos y 261 tests en verde; oxlint con 0 advertencias; tsc OK.
- Playwright local:
  - la tabla y el orden funcionan;
  - la descarga del CSV tiene cabecera y filas correctas;
  - el sello muestra 23;
  - el índice de Carga tiene 6 secciones;
  - no hay errores de página.
- La auditoría `capture-showcase-views.mjs` en 360×800 se ejecuta completa (ver PR).

## Delegado a ChatGPT/Codex (backend)

- **Registro de auditoría en servidor.** Hoy la exportación es solo del lado del cliente y deja la trazabilidad en el pie del archivo. Falta registrar en el servidor cada exportación como un evento de auditoría (`export.members.csv`, `export.documents.csv`).
- **Pendiente:** endpoint `POST /api/system/audit-events/export` (o equivalente) y llamada desde `exportCsv`.

## Pendiente

El lema sigue oculto; `srv01` y `main` sin cambios.
