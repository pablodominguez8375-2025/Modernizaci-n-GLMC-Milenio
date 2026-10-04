# Handoff — Claude — menús simples en celular y PC — 04-10-2026

- **Issue:** #312 (`agente:claude`). El PO lo aprobó el 03-10-2026 («Revisa nuevamente los menús… hay algunos sobrecargos»).
- **Coordinación:** se implementó después de que ChatGPT integrara el PR #311 (Fase B, menú único «Insinuaciones e Iniciación»), porque ese PR también tocaba `App.tsx`.
- **Revisión previa de Drive:** solo la carpeta Proyecto Centenario. El documento de instrucciones de la Fase B quedó marcado «EJECUTADA» y la Línea Base fue consolidada.
- **Base:** `dev@f7484ec`.
- **Versión:** UI QA v0.87.

## Cambios

### Celular y tablet (≤ 980 px)

- La hoja «Menú» ya no repite lo que está en la barra inferior:
  - Se marcan con `data-in-tabbar` Inicio, Agenda, Avisos y Mi ficha. Mi ficha solo se marca cuando la barra la muestra, es decir, en perfiles no operativos.
  - Se ocultan los títulos de grupo.
- Las opciones se muestran como **tarjetas en 2 columnas** (ícono y nombre), sin la línea de ayuda. El contador de cada opción va en la esquina.
- Altura de la hoja con letra Normal:
  - **Venerable Maestro:** 1.108 → 780 px. Cabe en pantalla, sin desplazamiento, también con letra Muy grande.
  - **Autoridad de Gran Logia:** 1.455 → 865 px.
  - **Hermano:** solo 2 tarjetas.

### PC (≥ 981 px)

- **Menú lateral más compacto:** cada opción mide 40 px de alto y los títulos de grupo son más bajos.
- **«Tamaño de letra»** pasa al botón **«Aa»** de la cabecera (`TextSizeMenu` en `TextSizeControl.tsx`) y sale del menú lateral.
- **Grupos plegables** (`navGroups.ts`, `useCollapsibleNavGroups`). Solo se activan si el contenido del menú no cabe en la pantalla. Se mide el alto real del contenido, porque el menú se estira con la página.
  - Sin preferencia guardada, se pliegan desde el final solo los grupos necesarios.
  - Nunca se pliegan «Mi espacio» ni el grupo de la vista actual.
  - La preferencia de cada persona se guarda en `pmgm.navCollapsedGroups`.
  - Los títulos de grupo funcionan como botones: tienen `aria-expanded`, chevron dibujado en CSS y se pueden usar con teclado.

### Tests

- Se actualizaron `accessibility.test.ts` y `menus-sin-repetir.test.ts` (de ChatGPT) para el nuevo marcado: `nav-section` puede llevar atributos y el `<nav>` tiene `ref`.
- Se agregó `menus-simples.test.ts`.

## Verificación

- 396 tests en verde. tsc y oxlint en OK.
- Playwright:
  - Hoja del celular con perfiles Hermano, VM y Autoridad, con letra Normal y Muy grande: sin errores.
  - PC a 1440×900:
    - VM: se pliegan solo «Mi Taller» y «Biblioteca».
    - Autoridad: se pliegan 3 grupos.
  - El panel «Aa» abre bien.
- Auditoría móvil completa en local: «OK, 21 perfiles, 597 vistas».

## Impacto en datos

Ninguno. Solo se agregan preferencias de interfaz en el navegador.
