# Handoff — Claude — UX navegación por rol (P0+P1) — 01-10-2026

- Issue #241 (`agente:claude`). Alcance aprobado por el PO el 01-10-2026: opción 2 = P0 + P1 de la adenda de Drive «Revisión UX de menús y vistas por rol».
- Base: `dev@d8bf696`. `main` sin cambios.

## Qué cambia

- **P0 — sello del Taller:** el número queda centrado dentro del círculo en Mi ficha (`member-portal.css`).
- **P0 — menú móvil:** desaparece el panel de 158 px con scroll oculto. El menú completo se abre como hoja a pantalla completa (en el VM, 14 de 14 opciones visibles).
- **P0 — próximos hitos:** la demo agrega eventos móviles relativos a la fecha actual (`calendarApi.ts`, solo en modo demo), así que la ventana de 21 días nunca queda vacía.
- **P1 — barra inferior móvil (≤720 px):** Inicio, Pendientes o Mi ficha, Agenda, Avisos y Menú, con contadores (`MobileTabBar.tsx`, `role-navigation.css`).
- **P1 — entrada:** todos los perfiles entran a Inicio, no a Mi ficha.
- **P1 — Inicio por rol:**
  - Saludo y resumen de pendientes.
  - Para perfiles operativos, la bandeja «Mis pendientes» (`rolePendingTasks.ts`) aparece antes de los indicadores.
  - La ficha técnica (.NET/PostgreSQL), «Controles incorporados» y «Cobertura QA» solo aparecen al Administrador.
- **P1 — contadores:** se muestran en el menú lateral (`data-badge`) para Tesorería, Carga de insinuados, Ceremonias y Gestión Logial.
- **Móvil:** indicadores en 2 columnas y sin la campana duplicada en la cabecera.
- Etiqueta `UI QA v0.64`. `capture-showcase-views.mjs` abre la hoja de menú móvil para auditar.

## Verificación local

- 50 archivos de test y más de 249 tests en verde; oxlint con 0 advertencias; tsc OK.
- Playwright sobre el build demo en 390×844: sin desbordamiento horizontal (Hermano, VM, Administrador), menú con 14 de 14 opciones, sello «23» dentro del círculo y sin errores de página.

## No incluido (P2/P3, pendientes de aprobación)

Franja de demo en la cabecera de escritorio, búsqueda global, opción de colapsar la barra lateral y revisión de tablas operativas. El lema sigue oculto. No se toca `srv01` ni `main`.
