# Handoff — Claude — vista operativa (entrega 3): Calidad de datos con acción en lote — 03-10-2026

- **Issue:** #290 (`agente:claude`). Cierra el orden aprobado por el PO el 02-10-2026.
- **Revisión previa de Drive:** la LINEA BASE MAESTRA se modificó el 03-10 a las 15:17 UTC (consolidación externa). No hay otros archivos nuevos.
- **Base:** `dev@b070dbc`. Versión UI QA v0.79. Solo cambia la presentación.

## Cambios (`InternalAffairsDataQualityPage.tsx`)

- **Selección múltiple:** cada hallazgo tiene una casilla con área táctil de 44 px. Una barra fija permite «Seleccionar los N visibles» y muestra el contador de seleccionados.
- **Apertura de casos en lote:** el botón «Abrir N casos para corroborar» pide confirmación y llama a la misma operación `caseApi.openCase` por cada hallazgo. Al terminar muestra un aviso, por ejemplo: «5 casos quedaron disponibles en la cola; 1 ya estaba en revisión».
- **Lenguaje simple:**
  - el insignia «Detección read-only» pasa a decir «Solo consulta»;
  - el código técnico de la regla (por ejemplo `exaltation_before_wage_increase`) se reemplaza por su nombre legible. El código queda como `title`, visible al pasar el cursor.

## Delegado a ChatGPT

**Carga de insinuados y balotaje** son del dominio de Admisiones, con los PR #116 y #276 abiertos. Las instrucciones quedaron en Drive: «INSTRUCCIONES PARA CHATGPT — 2026-10-03 — Vista operativa en Carga de insinuados y balotaje».

## Verificación

- 311 tests en verde. tsc y oxlint OK.
- Playwright a 1440 y 390 px:
  - se seleccionaron los 6 hallazgos y se abrieron en lote;
  - aviso correcto: «5 casos quedaron disponibles en la cola; 1 ya estaba en revisión»;
  - sin desbordes ni errores de página.
- Auditoría móvil completa en local.

## Impacto en datos (PMGM-GOV-004)

Ninguno: se usa la operación existente, una vez por hallazgo seleccionado.
