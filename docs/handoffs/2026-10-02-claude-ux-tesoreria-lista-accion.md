# Handoff — Claude — UX Tesorería: lista primero y acciones bajo demanda — 02-10-2026

- Issue #268 (`agente:claude`). Es la segunda entrega de la adenda de Drive «Tesorería y Secretaría: separar listados de acciones». El PO aprobó continuar el 02-10-2026; la primera entrega fue Secretarías, en el PR #258.
- Base: `dev@5ed6797`. `main` sin cambios.
- **Solo presentación.** No cambian reglas contables, API, validaciones, permisos ni cálculos. Las reglas de Tesorería siguen siendo dominio de ChatGPT/Codex.

## Qué cambia (UI QA v0.70)

### Tesorería del Taller (`LodgeTreasuryPanel.tsx`, `LodgeReceiptAdjustmentsPanel.tsx`)

- Los 7 formularios que estaban siempre abiertos ahora se abren con un botón, en un panel lateral (`ActionDrawer`):
  - **Cuotas y Cobranzas:**
    - «Registrar pago». El botón «Registrar pago» de la fila del hermano abre el mismo panel con los datos precargados; para eso se agregó apertura controlada (`open`/`onOpenChange`) a `ActionDrawer`.
    - «Imputar saldo a favor».
    - «Corregir o anular un registro».
  - **Ingresos y Egresos:** «Registrar ingreso» y «Registrar egreso». El egreso pide confirmación e informa que queda pendiente del Venerable Maestro.
  - **Configuraciones:** «Editar parámetros contables» y «Configurar cuota mensual».
  - **Reportes:** el período (Desde/Hasta), «Actualizar reporte» y «Exportar CSV» quedan visibles. «Registrar cuadratura de caja» se abre en el panel, y los filtros del libro de movimientos quedan contraídos en «Filtrar el libro de movimientos (N coinciden)».
- Todo lo que no se puede deshacer pide confirmación con un resumen antes de ejecutarse.
- Ayuda «¿Qué hago aquí?» en Cuotas y Cobranzas.

### Cuadro mensual y Gran Tesorería (`TreasuryStatementPage.tsx`)

- «Registrar transferencia o depósito» se abre en un panel.
- «Enviar a Gran Tesorería» y «Confirmar recepción bancaria y conciliar» piden confirmación (`ConfirmAction`).

### Consistencia

- Las etiquetas `lodge-kicker` pasan a letra normal, igual que el resto de las «eyebrow» (punto D).

## Verificación

- 285 tests en verde. El nuevo `treasury-actions.test.ts` comprueba:
  - que los 7 formularios están dentro de un panel;
  - las confirmaciones;
  - la apertura desde la fila;
  - los filtros contraídos;
  - el uso de unidades rem.
- oxlint 0 y tsc OK.
- Playwright, perfil Tesorero del Taller, en las 6 pestañas a 1440, 768 y 390 px, con letra Normal y Muy grande:
  - 0 desbordes, también con el panel abierto;
  - Ingresos y Egresos baja de ~1.674 a ~1.181 px en escritorio.

## Impacto en datos (PMGM-GOV-004)

**No hay cambios de modelo, contratos ni reglas de datos.** Se usan los mismos handlers, campos y validaciones.

## Pendiente

- El lema sigue oculto. `srv01` y `main` sin cambios.
