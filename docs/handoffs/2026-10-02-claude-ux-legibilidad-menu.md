# Handoff — Claude — UX A+B: legibilidad y menú claro — 02-10-2026

- Issue #261 (`agente:claude`). El PO aprobó el 02-10-2026 los puntos A y B de la adenda de Drive «Principio de diseño accesible y propuesta Inicio-Menú». En esa misma aprobación queda autorizado el ajuste de la escala tipográfica de PMGM-UI-BASELINE-001; las familias tipográficas, los colores y el logo no cambian.
- Base: `dev@edf1cf8` (rebasado; iniciado sobre `08a8c36`). `main` sin cambios.
- Regla permanente del PO, aplicable a todos los agentes: vistas simples y claras para personas mayores o sin conocimiento informático, menús fáciles de identificar, sin desbordes al aumentar el tamaño y uniformidad visual.

## Qué cambia (UI QA v0.68)

### A. Legibilidad

- Todas las hojas de estilo usan `rem` (≈600 declaraciones convertidas con un script). Así se respeta el tamaño de letra que la persona configure en el navegador o en el sistema.
- Tamaño mínimo de letra: 14 px. En contadores, insignias y calendario el mínimo es 12 px.
- Nuevo control «Tamaño de letra», con tres opciones:
  - Normal: 100 %.
  - Grande: 112,5 %.
  - Muy grande: 125 %.

  Está disponible al final del menú lateral y de la hoja de menú, y en el menú de usuario. La elección se guarda en `localStorage` (`pmgm.textSize`) y se aplica antes de pintar la pantalla (`textSize.ts`, `TextSizeControl.tsx`).
- Foco visible de 3 px en dorado.
- Opciones del menú: 16 px de letra y 48 px de alto.
- Títulos de grupo en letra normal, sin mayúsculas diminutas.

### B. Menú claro

- Tablet (721–980 px) usa el mismo patrón que el celular: barra inferior y hoja de menú. Se elimina la grilla superior con scroll de 204 px.
- Grupos renombrados:

  | Antes | Ahora |
  |---|---|
  | Portal del Hermano | Mi espacio |
  | Procesos | Trámites |
  | Gestión institucional | Gestión y consultas |
  | Taller | Mi Taller |
  | Conocimiento | Biblioteca y documentos |

  «Sistema» se mantiene.
- Cada opción tiene una línea de ayuda en lenguaje simple (`NAV_HINTS` en `App.tsx`):
  - en la hoja de menú, para tablet y celular, aparece bajo el nombre;
  - en escritorio, aparece como tooltip.

  Los nombres institucionales de las opciones no cambian.
- La barra lateral de escritorio pasa a medir 16 rem.
- Las etiquetas de la barra inferior se adaptan al ancho de pantalla.

## Verificación

- 275 tests en verde. Incluye `accessibility.test.ts`, que verifica: sin `px` en tamaños de letra, mínimo de 12 px, control de tamaño, menú de 48 px/16 px, grupos simples y mismo patrón en tablet y celular.
- oxlint con 0 advertencias; tsc OK.
- Playwright sobre el build demo en 1440, 1024, 768, 390 y 360, con letra Normal y Muy grande, en 6 perfiles y en la hoja de menú: **0 desbordes**.
- Auditoría `capture-showcase-views.mjs` a 360×800 en verde: 21 perfiles y 554 vistas.

## Pendiente

- Segunda entrega, C + D: Inicio uniforme y compacto, y consistencia general (microtexto en mayúsculas de los «eyebrow», espaciados y encabezados).
- El lema sigue oculto. `srv01` y `main` sin cambios.

## Impacto en datos (PMGM-GOV-004)

**No hay cambios de modelo, contratos ni reglas de datos.** No se modifican entidades, schema, migraciones, DTO o API, catálogos, validaciones ni permisos. Los cambios son de presentación (CSS en rem, textos de ayuda y agrupación del menú).

La única persistencia nueva es una preferencia de interfaz guardada en el navegador del usuario (`localStorage` `pmgm.textSize` = `normal` | `large` | `xlarge`). No es un dato institucional ni personal, no viaja al servidor y no requiere registro de diccionario.
