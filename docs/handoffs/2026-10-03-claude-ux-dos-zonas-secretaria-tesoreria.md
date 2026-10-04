# Handoff — Claude — Secretarías y Tesorerías en dos zonas (escritorio) — 03-10-2026

- **Issue:** #292 (`agente:claude`). El PO pidió el 03-10-2026 continuar con la propuesta.
- **Revisión previa de Drive:** solo hubo cambios en la LINEA BASE MAESTRA (consolidación externa) y en el documento de instrucciones para ChatGPT. No hay archivos nuevos de terceros.
- **Base y versión:** `dev@7baa347`, UI QA v0.80. El cambio es solo de CSS, en `desktop-density.css`.

## Cambios (solo desde 1440 px / 90 rem)

- **Secretaría del Taller (Gestión Logial), Gran Secretaría, Tesorería del Taller y Gran Tesorería:** el menú de secciones (`WorkspaceTabs` / `TreasuryRoleNavigation`) pasa de mosaicos horizontales arriba a una **columna fija a la izquierda** de 15 rem. Muestra el nombre y la descripción breve de cada sección y se mantiene visible al desplazarse. El trabajo ocupa la zona derecha.
- **Resumen de Secretaría:** la grilla `lodge-cockpit-grid` pasa a 3 columnas para que las tarjetas no queden apretadas.
- **Bajo 1440 px, en tablet y en celular:** sin cambios, con las pestañas horizontales de siempre.
- **Técnica:** se usa `:has()`, así que no se tocó el TSX.

## Verificación

- 312 tests en verde (`desktop-density.test.ts` amplía el contrato). tsc y oxlint OK.
- Playwright a 1920, 1440 y 1280 px en las 4 vistas:
  - sin desbordes;
  - sin textos recortados en las celdas;
  - el menú de secciones mide 240 px y queda a la izquierda.
- Auditoría móvil completa en local.

## Nota

En 1440 px el contenido gana alto porque la zona de trabajo es más angosta. A cambio, el menú de secciones queda siempre a mano. Si el PO prefiere menos desplazamiento, el umbral se puede subir a 1680 px.

## Impacto en datos (PMGM-GOV-004)

Ninguno.
