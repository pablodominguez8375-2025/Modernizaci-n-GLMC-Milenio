# Handoff — Claude — logo de cabecera V2 sobre placa blanca — 02-10-2026

- **Origen:** Issue #274 (`agente:claude`). El PO eligió la opción A el 02-10-2026.
- **Base:** `dev@a3d08b7`. `main` sin cambios.
- **Revisión previa de Drive** (regla de GOV-003 §11 4.1): no hay archivos nuevos desde la revisión anterior. Siguen pendientes, en espera de decisión del PO, la planilla «Gestion Adm GLMCH» y la adenda duplicada.

## Cambios (UI QA v0.72)

- `frontend/public/brand/logo-glmch-reducido-v2-azul.svg`: vector oficial V2 azul (SHA-256 `c1d978f7a95d0297e4e7c6c4ae2783b703ab2853ca8c4d89c38d85f9c359445e`).
- Se retira `logo-gran-logia-mixta-chile.svg` (SVG con JPEG embebido).
- `App.tsx`: la cabecera usa el V2, tiene texto alternativo institucional y ya no repite el subtítulo «Gran Logia Mixta de Chile».
- `home-consistency.css`: placa blanca con padding `calc(var(--logo-h)/4)` y alturas en rem.
- **Documentación:**
  - Nuevo PMGM-QA-V072-LOGOTIPO-REDUCIDO.
  - PMGM-UI-001 actualizado: navegación de tablet/móvil y uso del logo reducido.

## Verificación

- 287 tests, entre ellos la nueva prueba de contrato del logo.
- tsc y oxlint en verde.
- Auditoría Showcase: logo cargado, soporte blanco, `object-fit: contain`, sin solaparse con los controles y sin desborde en móvil.

## Impacto en datos (PMGM-GOV-004)

Ninguno: solo cambian un activo gráfico y la presentación.
