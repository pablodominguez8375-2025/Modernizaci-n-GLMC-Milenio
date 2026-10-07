# Handoff — UX Mi ficha en el menú lateral — 07-10-2026 (Issue #364)

## Pedido del PO
Las pestañas internas de Mi ficha (Mis datos / Mis pagos / Mis asistencias) recargaban la pantalla con «menú dentro de vista». El PO pidió:
- «Mis pagos» y «Mis asistencias» como opciones del menú lateral, bajo «Mi ficha»; «Mis datos» es la propia «Mi ficha».
- Grupos del menú lateral colapsables, incluido «Mi espacio».

## Decisiones que se reemplazan (no regresión)
- Reemplaza la decisión del PO del 06-10-2026 «Mi ficha en tres pestañas». Los tres paneles y su contenido se conservan sin cambios; solo cambia dónde se elige la sección.
- Ajusta la regla del 03-10-2026 «los grupos se pliegan solo si el menú no cabe»: en PC ahora todos los grupos se pliegan siempre. Se conserva que el grupo de la vista activa queda abierto y que se recuerda la preferencia. En celular y tablet no cambia.

## Cambios
- `App.tsx`: vistas `memberPayments` y `memberAttendance` (mismo permiso `member`), subítems bajo «Mi ficha», búsqueda global y pestaña inferior móvil.
- `MemberPortalPage.tsx`: prop `section`; sin `WorkspaceTabs`; título y subtítulo por sección; «Editar mis datos» solo en Mis datos.
- `navGroups.ts` y `common-views.css`: grupos siempre plegables en PC; estilo de subítem.
- Pruebas: `mi-ficha-pestanas.test.ts` y `menus-simples.test.ts` actualizadas.

## Coordinación
Base `dev@05d01fb`. Sin solape con el PR #363. El PR #58 (base antigua, borrador) también toca `MemberPortalPage.tsx`: rebasar sobre `dev` vivo antes de cualquier avance. Main sin cambios.

## Verificación
- `tsc -b`, `oxlint` y vitest (91 archivos, 463 pruebas) en verde en local.
- Demo con Chromium: sin pestañas dentro de la vista, «Mi espacio» se pliega y reabre, sin errores de página; celular 360 px sin desborde.
- Pendiente: CI exact-head, Showcase/Pages y paquete QA del SHA del PR.

## Estado
Implementado en código y verificado en demo local. Visible en GitHub Pages solo tras integrar a `dev`. QA `srv01` sigue pausado (Issue #97): no integrado ni probado en QA/UAT.
