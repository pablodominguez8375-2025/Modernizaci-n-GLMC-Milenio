# Handoff — Claude — UX C+D: Inicio uniforme y consistencia — 02-10-2026

- Issue #264 (`agente:claude`). El PO aprobó el 02-10-2026 los puntos C y D de la adenda de Drive «Principio de diseño accesible y propuesta Inicio-Menú». Con esto se cierra la propuesta (A+B fue integrada en el PR #263).
- Base: `dev@02e16d1`. `main` sin cambios.
- Regla permanente del PO: vistas simples y claras para personas mayores o sin conocimiento informático, sin desbordes y con uniformidad visual.

## Qué cambia (UI QA v0.69)

### C. Inicio uniforme y compacto (`DashboardPage.tsx`, `home-consistency.css`)

- **Saludo:** el bloque grande se reemplaza por una franja con saludo, resumen, fecha larga y ámbito. La ficha técnica solo la ve el Administrador.
- **Indicadores:** los 4 indicadores con números de 31 px y textos diminutos pasan a ser **4 accesos de igual tamaño** que se pueden tocar. Cada acceso muestra ícono, número y texto en una línea, y en móvil ocupa 2 columnas:
  - Actividades en 21 días
  - Avisos sin leer
  - Insinuados publicados
  - Próxima actividad
- **Sin datos repetidos:** se eliminan el indicador «Mis pendientes» (ya está en la bandeja) y los accesos rápidos que duplicaban esos destinos.
- **Bloques de igual altura** en grilla:
  - 3 columnas con bandeja en escritorio;
  - 2 columnas en tablet, con la bandeja a lo ancho;
  - 1 columna en móvil.

  Agenda y Avisos muestran como máximo 4 ítems con «Ver todo». Los textos se cortan en 2 líneas.
- **Estados vacíos** que dicen qué hacer, por ejemplo «Estás al día…» o «…aparecerá aquí».

### D. Consistencia general

- Las etiquetas «eyebrow» de todos los módulos pasan a letra normal de 14 px, sin mayúsculas ni espaciado.
- Los encabezados de tarjeta se unifican en 1,25 rem.
- Los estados vacíos usan el mismo estilo.

## Verificación

- 56 archivos y 281 tests en verde (nuevo: `home-consistency.test.ts`). oxlint en 0 y tsc OK.
- Prueba con Playwright sobre el build demo:
  - anchos: 1440, 1024, 768, 390 y 360;
  - tamaño de letra: Normal y Muy grande;
  - perfiles: Hermano, VM y Administrador;
  - resultado: **0 desbordes** y bloques de igual altura.

## Impacto en datos (PMGM-GOV-004)

**No hay cambios de modelo, contratos ni reglas de datos.** Solo cambia la presentación.

## Pendiente

- Segunda entrega de Tesorería (Tesorería del Taller y Gran Tesorería con «lista primero y acciones bajo demanda»), a la espera de aprobación del PO.
- El lema sigue oculto. `srv01` y `main` sin cambios.
