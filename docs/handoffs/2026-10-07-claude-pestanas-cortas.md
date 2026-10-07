# Handoff — Claude — corrección de pestañas de Mi ficha — 07-10-2026

- **Issue:** #356 (`agente:claude`).
- **Reporte del PO:** captura del celular con la v0.91. En Mi ficha, el menú «Mis datos / Mis pagos» no se deslizaba hacia la derecha y bajo las tarjetas aparecían dos líneas.
- **Versión:** UI QA v0.92. El cambio es solo CSS (`common-views.css`).

## Causa

El contenedor `.workspace-tabs` y la lista `[role=tablist]` eran **dos zonas de desplazamiento anidadas**: la lista tenía `min-width: max-content` y el contenedor `overflow-x: auto`. El deslizamiento táctil se perdía entre ambas. Además, se veían a la vez la barra de desplazamiento y el borde inferior del contenedor, lo que producía la «línea doble».

## Corrección (bajo 1440 px)

- **Contenedor:** pasa a ser visible, sin borde y con `min-width: 0` y `max-width: 100%`.
- **Con 4 opciones o menos** (Mi ficha, Hospitalaria, Gran Secretaría, etc.): todas las tarjetas se muestran **en una sola fila de anchos iguales, sin necesidad de deslizar**. En Mi ficha, 3 tarjetas de 113×56 px a 390 px.
- **Con 5 opciones o más** (por ejemplo, las 9 secciones de Gestión Logial): la lista es la **única** zona que se desliza hacia el lado. Se verificó a 390 px: desplazamiento interno de 2.260 px y sin desborde de página.

## Verificación

- 451 tests en verde.
- Playwright en Mi ficha y Secretaría a 390 px.
- Auditoría móvil completa en local.
