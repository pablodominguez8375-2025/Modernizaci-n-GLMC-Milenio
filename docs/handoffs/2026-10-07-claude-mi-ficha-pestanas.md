# Handoff — Claude — Mi ficha en tres pestañas — 07-10-2026

- **Issue:** #352 (`agente:claude`).
- **Pedido del PO (06-10-2026):** Mi ficha está muy recargada. Se pide:
  - separar los datos de miembro de los pagos;
  - agregar el historial de cargos;
  - crear la vista «Mis asistencias»;
  - en «Mis pagos», incluir las reposiciones de Hospitalaria.
- **Revisión previa de Drive:** solo la carpeta Proyecto Centenario. Se crearon las instrucciones v2 para ChatGPT y la v1 quedó marcada como REEMPLAZADA.
- **Base:** `dev@c2617b4`.
- **Versión:** UI QA v0.91.
- **Alcance:** solo frontend.

## Cambios en `MemberPortalPage.tsx`

**Pestañas.** Se agregan con `WorkspaceTabs` (tarjetas en celular, columna en PC):

1. **Mis datos**
   - ficha personal;
   - datos masónicos con acceso a la Biblioteca;
   - **Historial de cargos en el Taller**: por ahora muestra «Próximamente»;
   - Mis planchas.
2. **Mis pagos**
   - estado de Tesorería y de Hospitalaria;
   - cartola personal, visible siempre (se quitó el botón «Ver cartola»);
   - saldos a favor;
   - **Reposiciones por hermanos fallecidos** (.500): por ahora muestra «Próximamente».
3. **Mis asistencias**
   - asistencia a tenidas (resumen);
   - historial de instrucciones;
   - **Tenidas y ceremonias** (detalle): por ahora muestra «Próximamente».

**Otros cambios.**
- «Editar mis datos» se abre en un panel (`ActionDrawer` controlado). Ya no hay formulario abierto en la vista.
- Se retiró la cita decorativa.

**CSS (`common-views.css`).** Los paneles de Mi ficha usan `min-width: 0` para que la tabla de la cartola se desplace dentro de su propio contenedor sin ensanchar la página. Sin este ajuste había desborde a 390 px.

## Datos pendientes del backend (ChatGPT)

Las secciones marcadas «Próximamente» dependen de `/me/cargos`, `/me/asistencias` y `/me/hospitalaria`, según las instrucciones v2 en Drive (id 1nQ9GAePY0GLTpfMb62OqGqE9FYdKyFea98nPcQp8e3w).

## Verificación

- 450 tests en verde, incluido el nuevo `mi-ficha-pestanas.test.ts`. tsc y oxlint OK.
- Playwright en 390 y 1440 px con las 3 pestañas: sin desbordes ni errores.
- Auditoría móvil completa en local.

## Impacto en datos

Ninguno.
