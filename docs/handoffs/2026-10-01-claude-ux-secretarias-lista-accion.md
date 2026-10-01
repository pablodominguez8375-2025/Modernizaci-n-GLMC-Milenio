# Handoff — Claude — UX Secretarías: lista primero, acción bajo demanda — 01-10-2026

- Issue #257 (`agente:claude`). Pedido y aprobación del PO el 01-10-2026: «Parte de forma formal con Secretaría y dejamos Tesorería para después».
- Base: `dev@216043e` (PR #256). `main` sin cambios.

## Qué cambia (UI QA v0.67) — solo presentación

- **Componentes nuevos** (`actionKit.tsx`, `action-kit.css`):
  - **`ActionDrawer`:** abre el formulario en un panel lateral, que en móvil ocupa la pantalla completa.
    - El formulario se mantiene montado, así que se conserva lo escrito.
    - Se cierra al guardar; con `keepOpen` queda abierto para registros repetidos.
    - Con `confirmMessage` pide confirmar antes de ejecutar.
  - **`ConfirmAction`:** confirmación en línea.
  - **`RowMenu`:** menú «⋯» con las acciones secundarias.
  - **`WorkspaceTabs`:** pestañas.
  - **`HelpNote`:** ayuda plegable «¿Qué hago aquí?».
- **Gestión Logial / Secretaría del Taller:** reemplaza el índice de anclas por pestañas: Resumen, Tenidas y actas, Correspondencia, Cuadro y reuniones, Archivo y cierre, Consejo, Docencia y Cartas de retiro.
  - **`LodgeSecretariatPanel`:** acepta `section` (`daily` | `roster` | `documents`). Queda montado una sola vez y se oculta con `hidden`.
  - **Formularios ahora en panel:** correspondencia, pendientes, tabla de agenda, carga manual del Cuadro, importación Excel, reuniones, Consejo (sesión, asistencia, acuerdo, revisión), carta de retiro, nueva Tenida, asistencia de Tenida, redacción del acta e instrucción.
  - **Confirmación previa:** «Cerrar Tenida», «Remitir extracto a Gran Secretaría», acuerdo del Consejo y carta de retiro.
  - **Altura de la página:** baja de ~5.900 px a ~950–1.850 px según la pestaña.
- **Gran Secretaría:** pestañas Bandeja de trabajo (con contador), Templos y salas y Documentos emitidos.
  - Reservar espacio, Nuevo templo o sala y Emitir documento se abren en panel; Reservar y Emitir piden confirmación.
  - «Emitir Plancha» pide confirmación.
  - En Extractos, «Marcar recibido» queda visible; «Descargar» y «Observar» pasan al menú «⋯».

## No regresión

No hay cambios de reglas, permisos, API ni campos: todos los formularios y campos existentes se conservan. Lo único que cambia es dónde y cuándo se muestran.

## Verificación local

- 54 archivos y 269 tests en verde, incluido el nuevo `actionKit.test.ts`; oxlint con 0 advertencias; tsc OK.
- Playwright sobre el build demo:
  - las pestañas ocultan el resto;
  - no quedan formularios abiertos en Correspondencia, Cuadro, Consejo ni Retiros;
  - en «Emitir documento oficial» aparece la confirmación y, al confirmar, se ejecuta y el panel se cierra con el mensaje de éxito;
  - sin errores de página.
- Auditoría `capture-showcase-views.mjs` en 360×800 en verde: 21 perfiles y 554 vistas. Se ajustó el script para que vuelva al estado inicial del módulo si una pestaña desaparece al cambiar de función por cargo.

## Conflictos conocidos

- PR #116 (ChatGPT/Codex, draft) reemplaza `GrandSecretariatPage.tsx` y ya tenía conflictos. Al rebasarlo habrá que conservar las pestañas y `ActionDrawer` de este PR.

## Pendiente

- Segunda entrega: Tesorería del Taller y Gran Tesorería, que el PO dejó para después.
- El lema sigue oculto; `srv01` y `main` sin cambios.
