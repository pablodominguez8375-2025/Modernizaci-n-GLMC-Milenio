# Handoff — Claude — densidad cómoda en escritorio (fases 1 y 2) — 02-10-2026

- **Issue y aprobación:** Issue #284 (`agente:claude`). El PO aprobó el 02-10-2026 las dos fases de la adenda «Densidad y espacio en escritorio (PROPUESTA)».
- **Revisión previa de Drive (GOV-003 §11 4.1):** sin archivos nuevos desde la última revisión. Los archivos de credenciales ya informados siguen sin abrir.
- **Base y versión:** `dev@6d44e21`, UI QA v0.76.
- **Alcance:** los cambios aplican solo desde 1280 px (80 rem). Celular y tablet no cambian.

## Fase 1 — transversal (`desktop-density.css`)

- Márgenes del contenido: 2 rem (antes 52 px). Ancho máximo: 105 rem, unos 1.680 px (antes 1.500 px).
- Encabezado de página compacto:
  - título de 1,75 rem;
  - se oculta la etiqueta superior;
  - descripción en una sola línea.
- Inicio: cada bloque toma la altura de su contenido (`align-items: start`), sin mitades vacías.

## Fase 2 — dos zonas

- **Agenda** (`CalendarPage.tsx`):
  - lista a la izquierda;
  - panel lateral fijo (sticky) a la derecha con período, filtros, acciones de administración e indicadores.
  - Cada actividad ocupa una fila compacta: día, hora y lugar, título y estado.
- **Avisos** (`NotificationsPage.tsx`):
  - lista a la izquierda, con el texto de cada aviso limitado a 2 líneas;
  - panel lateral con filtros, «Marcar todos como leídos» y «Actualizar».
- En celular, el panel lateral se muestra arriba, porque va primero en el DOM.

## Medición (perfil Hermano, alto de página a 1440 px)

| Vista | Antes | Después |
|---|---|---|
| Agenda | 1.896 px | 1.325 px |
| Avisos | 1.844 px | 1.607 px |

Sin desbordes en 1920, 1440, 1280 (letra Muy grande), 1024 y 390.

## Verificación

- 297 tests en verde (nuevo: `desktop-density.test.ts`).
- tsc y oxlint OK.
- Auditoría móvil completa en local.

## Impacto en datos (PMGM-GOV-004)

Ninguno.

## Pendiente

- Decisión del PO sobre el lema, que hoy se ve en la cabecera en pantallas de más de 1.500 px.
- Propuesta siguiente: vistas de listados + acciones con formularios.
