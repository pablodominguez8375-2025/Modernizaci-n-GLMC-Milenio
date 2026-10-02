# Handoff — Claude — fix íconos de accesos de Inicio — 02-10-2026

- Issue #270 (`agente:claude`). Reportado por el PO con una captura móvil de la v0.70.
- **Síntoma:** en los 4 accesos de Inicio, el ícono no se veía dorado ni centrado en su cuadro azul.
- **Causa:** la regla antigua `.metric-card > span { display:block; color: var(--muted) }` de `styles.css` tiene especificidad 0,1,1. La clase nueva `.home-shortcut-icon` tiene especificidad 0,1,0, así que la regla antigua le ganaba. Como resultado, el cuadro dejaba de ser grid centrado y el ícono, que usa `currentColor`, salía gris oscuro sobre fondo azul.
- **Corrección:** en `home-consistency.css` se agregó un selector de mayor especificidad (`.dashboard-page .home-shortcuts .home-shortcut > .home-shortcut-icon`) con grid centrado y color `--brand-gold`.
- **Ajuste en móvil:** el número y el texto quedan juntos y centrados verticalmente.
- **Versión:** UI QA v0.71.
- **Verificación:**
  - Playwright a 390 y 1440 px: los 4 íconos en rgb(243,198,9) y desfase de centrado 0/0 px.
  - 286 tests en verde, incluido el nuevo test de contrato.
- **Impacto en datos (PMGM-GOV-004):** ninguno. El cambio es solo CSS.
