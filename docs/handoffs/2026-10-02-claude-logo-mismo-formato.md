# Handoff — Claude — logo con el mismo formato en celular y tablet — 02-10-2026

- **Instrucción del PO (02-10-2026):** usar el mismo logo y el mismo formato en celular y tablet.
- **Revisión previa de Drive** (GOV-003 §11 4.1): no hay archivos nuevos desde la revisión anterior.
- **Antes del cambio:** todas las pantallas ya usaban la versión reducida V2 azul sobre placa blanca (PR #277), pero en celular el logo se achicaba a 38 px (≤720 px de ancho) y a 34 px (≤380 px).
- **Cambio:** se eliminaron esas reducciones. El logo mide 44 px de alto (2,75 rem) en escritorio, tablet y celular, con la misma placa y la misma área de protección. En celular, «Proyecto Centenario» puede pasar a dos líneas.
- **Verificación:**
  - Playwright a 1440, 768, 390, 360 y 320 px, con letra Normal y Muy grande: el logo mide lo mismo en todas las pantallas (122×44, o 152×55 con Muy grande) y no hay desborde horizontal.
  - 288 tests; tsc y oxlint en verde.
- **Versión:** UI QA v0.73.
- **Impacto en datos (PMGM-GOV-004):** ninguno; el cambio es solo de CSS.
