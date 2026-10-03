# Handoff — Claude — Hospitalaria y Gran Hospitalaria en pestañas — 03-10-2026

- **Issue:** #304 (`agente:claude`).
- **Instrucciones del PO (03-10-2026):**
  - Continuar con las mejoras de menús y vistas.
  - Vistas limpias y sin recargar.
  - Ningún formulario abierto: los formularios se abren desde botones.
- **Revisión previa de Drive:** solo la carpeta Proyecto Centenario. No hay archivos nuevos.
- **Base:** `dev@6083248`.
- **Versión:** UI QA v0.85.

## Cambios

### `HospitalariaPage.tsx`

- **Hospitalaria del Taller:** se divide en pestañas `WorkspaceTabs`, que con la regla de la v0.84 se ven como tarjetas horizontales en celular y tablet:
  - Resumen y autorizaciones (indicadores y socorros por autorizar, con contador)
  - Movimientos (registrar y libro del período)
  - Reposiciones
  - Rendición mensual
- **Perfil VM:** solo ve las pestañas Resumen y Movimientos.
- **Gran Hospitalaria:** se divide en estas pestañas:
  - Rendiciones recibidas (con contador)
  - Reposición y casos
  - Regularidad de miembros

### `App.tsx`

`RegularityPage kind="hospitalaria"` ya no se agrega debajo de la vista. Ahora entra por la prop `regularitySlot` y se muestra como pestaña propia (también en modo local, si el perfil tiene ambos permisos).

## Medición a 1440 px y 390 px

| Vista | Antes | Después |
|---|---|---|
| Gran Hospitalaria (entrada) | 3.978 / 6.963 px | 1.653 / 1.039 px |
| Hospitalaria del Taller (pestañas) | — | 1.217–2.829 px (la mayor es Reposiciones) |

## Verificación

- 317 tests en verde.
- Playwright: sin desbordes ni errores de página.
- Auditoría móvil completa en local.

## Impacto en datos

Ninguno.
