# Handoff — encabezado móvil v0.63 — 2026-09-30

Agente: ChatGPT. Seguimiento: #223. Base dev: 41a3575d57e8795878ebe165aeff986f17b23a08.

## Reserva y autorización

Pablo autorizó expresamente el 30-09-2026 («Si») reasignar desde #43/#116 al nuevo PR móvil únicamente frontend/src/App.tsx, frontend/src/institutional-theme.css y frontend/src/institutional-theme.test.ts, conservando el resto del trabajo de ambos PR. Se registra la excepción antes de programar. #1 es la comparación dev→main y no se modifica.

## Alcance

Mostrar «Proyecto Centenario» completo en móvil; mantener Demo y SHA corto visibles con fuente mínima de 12 px; actualizar UI QA a v0.63; contratos CSS, changelog y evidencia visual 360/390. No cambiar logo, colores, tipografía corporativa, lema, main ni srv01. Sin QA física/UAT.

## Estado

PR #226. Implementación terminada: nombre con white-space normal/line-height1.1 y sin ellipsis/max-width restrictivo; Demo/SHA móvil12px en fila sobre selector; App UI QA v0.63. Contratos CSS añadidos. Tests231/231, lint y tsc/build correctos. Chromium local:360,390,480,620,720,768,1024,1440 sin overflow global/de encabezado ni solapamiento; nombre íntegro y SHA12px comprobados en360/390. Matriz existente de capturas en curso; gates exact-head pendientes. Integración autorizada a dev únicamente con CI, Showcase Pages y QA instalable en SUCCESS sobre el HEAD exacto. START-HERE se actualizará mediante PR documental separado posterior al merge.

Archivos tocados: los tres reservados, este handoff y changelog/2026-09-30-mobile-header-v063.md. Main inicial/final previsto:6dfb9546a4873baff15955cf86abfd7d47e3d111. SHA de integración/Pages y SHA-256 QA se registrarán en handoff documental posterior y #223 tras verificar publicación. Las ramas #43/#116 deben rebasarse sobre dev conservando este contrato; no se modifican sus demás cambios. Pruebas locales preparadas de Tesorería siguen aisladas en checkout original. Documento delegado: https://docs.google.com/document/d/1VYIG6y5rnU0Ei-ve8qHPpMapx8qXEvpBL3REiCDBHM4/edit.
