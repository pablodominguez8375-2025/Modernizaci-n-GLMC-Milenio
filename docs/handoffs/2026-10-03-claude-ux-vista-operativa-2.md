# Handoff — Claude — patrón «vista operativa» (entrega 2) — 03-10-2026

- **Origen:** Issue #288 (`agente:claude`). El PO pidió continuar con el orden aprobado.
- **Revisión previa de Drive (GOV-003 §11 4.1):** no hay archivos nuevos.
- **Base:** `dev@68bef4f`. Versión UI QA v0.78. Solo cambia la presentación.

## Cambios

| Vista | Archivo | Cambio |
|---|---|---|
| Control de miembros | `InternalAffairsMemberControlPage.tsx` | Quedan visibles solo los filtros Buscar, Taller y Estado. Corte, Grado, Finanzas, «Sólo Past Active» y «Traslado pendiente» pasan a «Más filtros (n activos)». En las cargas históricas, «Aprobar y actualizar Cuadro» pide confirmación, y «Observar o rechazar» abre un panel con la observación obligatoria. |
| Derechos de ceremonia | `CeremonyRightsPage.tsx` | «Registrar abono» se abre en un panel con confirmación (antes era un `<details>`). |
| Docencia del Taller | `LodgeInstructionPage.tsx` | «Registrar sesión» se abre en un panel. |
| Parámetros › Usuarios | `SystemOperationsPanel.tsx` | «Crear usuario» se abre en un panel. |
| Regularidad (gestión) | `RegularityManagement.tsx` | «Actualizar regularidad» se abre en un panel. |

**Régimen Interior:** su único formulario es el filtro del reporte, que el patrón permite, así que no cambia.

## Verificación

- 310 tests en verde. `operational-view.test.ts` ahora cubre 10 archivos más Control de miembros.
- tsc y oxlint OK.
- Playwright a 1440 y 390 px, sin desbordes. Control de miembros muestra 3 filtros visibles y ningún formulario abierto.
- Auditoría móvil completa, ejecutada en local.

## Pendiente

- **Calidad de datos:** acciones en lote.
- **Carga de insinuados y balotaje** (`LodgeBallotPanel`): son del dominio de Admisiones y se coordinarán con ChatGPT.
