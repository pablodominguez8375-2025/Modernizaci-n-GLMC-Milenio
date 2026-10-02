# Handoff — Claude — vistas comunes (Mi ficha, Agenda, Avisos, Biblioteca) — 02-10-2026

- **Origen:** Issue #280 (`agente:claude`). Instrucción del PO del 02-10-2026: aplicar la regla de vistas simples, sin espacios perdidos y para personas mayores a Mi ficha, Agenda, Avisos y demás vistas comunes, en una sola página cuando sea posible.
- **Revisión previa de Drive (GOV-003 §11 4.1):** no hay archivos nuevos además de los ya informados. Siguen sin abrir las planillas «Mesa de Ayuda» y «Gestion Adm GLMCH»; su revisión espera decisión del PO.
- **Base:** `dev@7fa3423`. Versión UI QA v0.74. Solo cambia la presentación.

## Cambios

### Mi ficha (`MemberPortalPage.tsx`, `MemberWorkPapersPanel.tsx`)

- **Bloques eliminados por duplicar otras vistas:**
  - el calendario mensual («Agenda Septiembre»), que ya está en Agenda;
  - «Próximas tenidas», que ya está en Inicio y Agenda;
  - «Notificaciones recientes», que ya está en Inicio y Avisos.
- **Accesos directos nuevos:** «Ver mi agenda», «Ver mis avisos» y «Tenidas de mi Taller».
- **Datos personales en una grilla de 3 columnas de igual altura:** Asistencia, Docencia, y Tesorería/Hospitalaria. En tablet pasa a 2 columnas y en celular a 1.
- **Historial de instrucciones:** queda plegado en «Ver historial de sesiones».
- **Mis planchas:**
  - el formulario «Subir una plancha» se abre en un panel (`ActionDrawer`);
  - «Reemplazar mi plancha» abre el mismo panel;
  - al guardar con éxito, el panel se cierra.
- **Etiquetas:** las etiquetas de tarjeta y la ruta de navegación («breadcrumb») pasan a letra normal, sin mayúsculas diminutas.

### Agenda, Avisos y Biblioteca (`common-views.css`)

- **Agenda:** indicadores compactos.
- **Avisos:** tarjetas con menos relleno.
- **Biblioteca:** encabezado compacto y, en celular, portada de tarjeta reducida.

## Medición antes → después (alto de página, perfil Hermano)

| Vista | Escritorio | Celular |
|---|---|---|
| Mi ficha | 2.712 → 1.728 px | 5.720 → 3.546 px |
| Agenda | 1.952 → 1.896 px | 3.824 → 3.564 px |
| Biblioteca | — | 6.317 → 6.117 px |

## Verificación

- 291 tests en verde (nuevo: `common-views.test.ts`). tsc y oxlint OK.
- Auditoría móvil local completa (`capture-showcase-views`).

## Impacto en datos (PMGM-GOV-004)

Ninguno: se mantienen los mismos datos, permisos y acciones.
