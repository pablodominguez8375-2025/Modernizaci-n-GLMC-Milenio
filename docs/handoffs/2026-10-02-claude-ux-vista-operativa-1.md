# Handoff — Claude — patrón «vista operativa» (entrega 1) — 02-10-2026

- **Issue y aprobación:** Issue #286 (`agente:claude`). El PO aprobó el 02-10-2026 el patrón «vista operativa» y su orden de aplicación.
- **Lema «Camino al centenario 1929-2029»:** el PO decidió dejarlo **visible por ahora** en pantallas de más de 1.500 px. No se toca.
- **Revisión previa de Drive (GOV-003 §11 4.1):** no hay archivos nuevos desde la última revisión. Siguen sin abrirse las planillas y los archivos de credenciales ya informados.
- **Base y versión:** `dev@59ba87a`, UI QA v0.77.
- **Alcance:** solo presentación. Se usan los mismos handlers, validaciones y API.

## Cambios

| Vista | Archivo | Cambios |
|---|---|---|
| Hospitalaria del Taller y Gran Hospitalaria | `HospitalariaPage.tsx` | «Registrar movimiento» (con ayuda «¿Qué hago aquí?»), «Preparar rendición», «Cambiar tarifa», «Registrar pago» (reposición por fila) y «Registrar transferencia» se abren en un panel con confirmación. «Enviar a Gran Hospitalaria» sigue visible como acción principal. |
| Regularidad (Gran Hospitalaria y otras vistas que la usan) | `RegularityPage.tsx` | «Actualizar regularidad» se abre en un panel. |
| Gestor Documental | `DocumentManagementPage.tsx` | «Nueva colección» y «Registrar documento» se abren en un panel. |
| Ceremonias | `CeremoniesPage.tsx` | «Validar Régimen Interior» pasa de un `<details>` a un panel con confirmación. |

## Prueba de contrato (`operational-view.test.ts`)

Verifica que no quede ningún `<form>` fuera de `ActionDrawer` en estos archivos:

- Hospitalaria
- Ceremonias
- Tesorería del Taller
- Ajustes
- Planchas
- Regularidad

## Verificación

- 305 tests en verde. tsc y oxlint OK.
- Playwright a 1440 y 390 px, sin desbordes:
  - **Hospitalaria del Taller:** 0 formularios abiertos (antes 11 campos visibles).
  - **Gran Hospitalaria:** 0 formularios; altura de 4.019 a 3.781 px.
  - **Gestor Documental:** 0 formularios (antes 2).
  - **Ceremonias:** 0 formularios.
- Auditoría móvil completa en local.

## Pendiente (próximas entregas según el orden aprobado)

- **Régimen Interior:** queda 1 formulario propio.
- **Control de miembros.**
- **Calidad de datos:** acciones en lote.
- **Carga de insinuados:** es dominio de Admisiones y se coordinará con ChatGPT.
