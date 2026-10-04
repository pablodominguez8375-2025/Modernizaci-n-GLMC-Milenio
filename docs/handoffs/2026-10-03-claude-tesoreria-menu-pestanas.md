# Handoff — Claude — menú operativo de Tesorería como pestañas — 03-10-2026

- **Origen:** Issue #300 (`agente:claude`). El 03-10-2026 el PO envió una captura móvil de la v0.82: el «Menú operativo» de Tesorería del Taller ocupaba toda la pantalla con 6 mosaicos apilados. Pidió que se desplace hacia el lado, como en Secretaría.
- **Revisión previa de Drive:** solo carpeta Proyecto Centenario. Sin archivos nuevos.
- **Base y versión:** `dev@ade3f2e`, UI QA v0.83. El cambio es solo CSS, en `common-views.css`.

## Cambio

- **Bajo 1440 px** (celular, tablet y computador mediano), el menú de Tesorería del Taller y Gran Tesorería (`TreasuryRoleNavigation`) cambia así:
  - deja de ser una pila de mosaicos con descripción;
  - pasa a ser una **franja de pestañas horizontales con desplazamiento lateral**, igual que las pestañas de Secretaría (`WorkspaceTabs`);
  - la pestaña activa se marca con línea dorada;
  - las descripciones y el título repetido se ocultan.
- **Desde 1440 px:** se mantiene la columna lateral fija que integró el PR #293.

## Verificación

- Playwright en 390 px (Tesorero del Taller y Gran Tesorero, con letra Grande) y en 1024 px:
  - la franja mide 56 px de alto (antes, unos 1.300 px de mosaicos en el celular);
  - sin desbordes.
- 316 tests en verde.
- Auditoría móvil completa en local.

## Impacto en datos

Ninguno.
