# Handoff — Claude — ícono de pestaña/app (favicon) — 03-10-2026

- **Origen:** Issue #298 (`agente:claude`). Decisión del PO del 03-10-2026: «Crea uno tú y lo usas». La guía de Publicaciones no trae una versión que sea solo el símbolo.
- **Revisión previa de Drive:** solo en la carpeta Proyecto Centenario (GOV-003 §15). Sin archivos nuevos.
- **Base:** `dev@1c21627`. Versión UI QA v0.82.

## Cambios

- `frontend/public/brand/favicon-glmch.svg`: símbolo extraído **sin modificar** del vector oficial «Tamaño reducido V2 azul» (`#021493`), sin el nombre, sobre una placa blanca redondeada.
- `favicon-32.png` y `apple-touch-icon.png` (180 px): versiones rasterizadas del mismo SVG.
- `frontend/index.html`: se agregan `<link rel="icon">` para SVG y PNG, y `apple-touch-icon`. Vite antepone automáticamente la ruta base de Pages.
- `PMGM-UI-001`: queda registrada la decisión y la regla de reemplazo. Si Publicaciones entrega una versión oficial de solo símbolo, se reemplaza por esa.
- Test nuevo: `favicon.test.ts`.

## Verificación

- Vista previa revisada a 16, 24, 32, 48 y 64 px, sobre fondo claro y oscuro.
- Resultados: 315 tests en verde, tsc y oxlint OK.
- El build demo incluye los enlaces con la ruta base `/Modernizaci-n-GLMC-Milenio/brand/...`.

## Impacto en datos

Ninguno.
