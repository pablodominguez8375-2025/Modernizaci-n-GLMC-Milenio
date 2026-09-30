# Handoff — cierre móvil v0.63 y continuidad — 2026-09-30

Seguimiento #223; PR funcional #226 y documental #227. Agente ChatGPT, por delegación de Claude (documento del29-09). Pablo autorizó expresamente («Si») reasignar los tres hot files de #43/#116; excepción registrada antes de programar. Sus otros cambios y ramas se conservan y deben rebasarse sobre dev manteniendo este contrato.

## Estado técnico

- dev inicial41a3575d57e8795878ebe165aeff986f17b23a08. #226 integrada por squash en b4ba936fd6b70cc693f8c6f4c57ed1c97bc839f5. Main inicial y final6dfb9546a4873baff15955cf86abfd7d47e3d111, sin cambios.
- Archivos funcionales: frontend/src/App.tsx, institutional-theme.css/test.ts; changelog/2026-09-30-mobile-header-v063.md; docs/handoffs/2026-09-30-chatgpt-mobile-v063.md. PR #227 solo añade este handoff nuevo y una línea a START-HERE, sin reescribir bloques.
- Nombre completo «Proyecto Centenario» en móvil, dos líneas si hace falta, white-space normal/line-height1.1, sin ellipsis ni max-width restrictivo. Demo/SHA corto visible12px, selector debajo. UI QA v0.63. Mantiene logo/paleta/tipografía corporativa y lema oculto.

## Verificación

- Frontend231/231 tests, lint y tsc/build OK; diff sin espacios defectuosos. Contratos verifican nombre sin truncamiento y SHA visible sin regresión en breakpoints posteriores.
- Chromium local360/390/480/620/720/768/1024/1440 sin overflow global/de encabezado ni solapamiento;360/390 con nombre íntegro y SHA12px, capturas inspeccionadas.
- Script existente capture-showcase-views.mjs, sin modificar:21 perfiles y495 estados de módulos/subvistas a360×800, más capturas representativas de8tamaños; exit0. Evidencias GitHub exact-head: artifact11093143269, digest sha256:aef349bdf23d0bca5f67122b49cff96409a9b189a64b6e7b94e517d543486cd3.
- HEAD exacto #226 e049cf365b7c6151ee315a0cdcb4e3d81431dcd7: CI1704/36708323646, Showcase1026/36708323661, QA664/36708323647, todosSUCCESS.
- Post-merge b4ba936: CI36709358286, Showcase/Pages36709358299 (incluido Deploy testing showcase), QA36709358311 y Pre-UAT36709358305, todosSUCCESS. Pre-UAT es nombre de workflow, no aceptación institucional.
- Pages y downloads/qa-current.json verificaron sourceSha=b4ba936fd6b70cc693f8c6f4c57ed1c97bc839f5, incluyendo PR #224/#225/#226 sobre #221/#222. ZIP público Proyecto-Centenario-QA-srv01-b4ba936fd6b7.zip descargado: SHA-2564e41e0d44f387c240bd57d869030f8f6d71fb83f97c2de5d69a5bffb08db71a9 y804checksums internos verificados. BUILD-INFO SOURCE_SHA=b4ba936, BUILD_RUN_ID=36709358137 (evento merged de Showcase). Push y merged del mismo SHA generan paquetes con digest diferentes; se registra el realmente servido.
- Artifact QA push11093256677, digest sha256:bb777cc0a82a987682f0b1199351329f641c0564c578c16499d6aaedcc30b52d es metadato distinto del ZIP público.

## Continuidad y límites

El HEAD final exacto, gates de #227, SHA final Pages y paquete correspondiente se registrarán en #223/#227 y la Línea Base tras la integración documental; no confundir con b4ba936, checkpoint funcional de este handoff. La Línea Base y el documento delegado deben reflejar cierre completo y título EJECUTADA tras retorno verificado. Tareas1/2 ya cerradas en #224/#225; esta intervención completa tarea3.

Trabajo de Tesorería separado en checkout original (commit local430ee44), intacto y excluido de este PR. No modificar lema/fuentes/escala global, main ni workflows por este encargo. Srv01 sigue pausado por #97; no instalación/despliegue, QA física ni UAT. #43/#116 abiertos con resto de trabajo conservado. Claude revisa la entrega en su siguiente sesión.

Pages: https://pablodominguez8375-2025.github.io/Modernizaci-n-GLMC-Milenio/
Documento delegado: https://docs.google.com/document/d/1VYIG6y5rnU0Ei-ve8qHPpMapx8qXEvpBL3REiCDBHM4/edit
Línea Base: https://docs.google.com/document/d/1ncl0d--Bny8PzvtP38muPo5H2-JM5QUDGqH3lJ5ZtPM/edit
