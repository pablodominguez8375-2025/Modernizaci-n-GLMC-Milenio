# Handoff — Claude — menús de secciones en tarjetas horizontales en todo el sistema — 03-10-2026

- **Issue:** #302 (`agente:claude`).
- **Decisión del PO (03-10-2026):**
  - Los menús deben ser operativos en PC y celular, sin obligar a bajar en vertical para encontrar opciones.
  - Los menús horizontales usan **tarjetas grandes**, como «Espacio operativo por cargo» de Secretaría.
  - Se aplica a todo el sistema.
- **Revisión previa de Drive:** solo la carpeta Proyecto Centenario. La LINEA BASE MAESTRA fue consolidada el 03-10 a las 19:16 UTC; no hay archivos nuevos.
- **Base y versión:** `dev@b36f0ff`, UI QA v0.84. El cambio es solo de CSS (`common-views.css`).

## Cambios

- **Bajo 1440 px** (celular, tablet y PC mediano), los cuatro menús de secciones usan el mismo patrón: una fila de **tarjetas grandes** que se desliza hacia el lado.
  - Ancho de tarjeta: `min(15rem, 72vw)`. Alto mínimo: 4,5 rem. Bordes redondeados.
  - Muestran el título y la descripción breve.
  - La tarjeta activa se marca con borde azul y fondo celeste.
  - Las tarjetas se ajustan al deslizar (scroll-snap).
  - Menús alcanzados:
    - `TreasuryRoleNavigation`: Tesorería del Taller y Gran Tesorería. Reemplaza la franja compacta de la v0.83 y ahora usa el mismo contenedor que Secretaría.
    - `SecretariatRoleNavigation`: «Espacio operativo por cargo».
    - `WorkspaceTabs`: pestañas internas de Secretaría del Taller y Gran Secretaría.
    - `.system-page-tabs`: Parámetros del sistema.
- **Desde 1440 px:** Secretaría y Tesorería mantienen la columna lateral fija. Parámetros mantiene su fila de pestañas.

## Verificación

- Pruebas con Playwright en 390, 1024 y 1440 px, sobre 5 vistas: Tesorería del Taller, Secretaría, Gran Secretaría, Parámetros y Gran Tesorería.
  - **390 y 1024 px:** todos los menús se muestran en fila (flex), con tarjetas de 240 px de ancho y entre 72 y 96 px de alto.
  - **1440 px:** la columna lateral se mantiene.
  - Sin desbordes en ningún ancho.
- 316 tests en verde.
- Auditoría móvil completa en local.

## Impacto en datos

Ninguno.
