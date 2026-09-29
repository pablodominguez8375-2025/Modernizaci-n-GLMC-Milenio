# 2026-09-29 — Correcciones UI y menú móvil (sin cambio de identidad)

Alcance pedido por el Product Owner: corregir defectos visuales detectados en la revisión UX sin alterar la identidad corporativa aprobada (tipografía y paleta sin cambios).

- Mi ficha: la comilla de la cita ya no se pinta como bloque dorado (`ppt-fidelity.css`).
- Mi ficha: el sello del Taller muestra su número (o la inicial) en vez de la «C» fija.
- `displayFormat.ts`: `organizationDisplayName` evita «Taller Demostrativo Nº 1 · Nº 1`; `countLabel` concuerda singular/plural («1 pendiente»); `formatDateOnlyCl` formatea fechas sin hora en es-CL sin corrimiento de día. Aplicado a selectores y tarjetas de Taller en 20 vistas.
- Tesorería del Taller: en móvil (≤720 px) los egresos se muestran como tarjetas y la acción «Autorizar» queda visible sin scroll horizontal; fechas en formato es-CL.
- Menú móvil: se quitan `max-height: 46vh/50vh` de `institutional-theme.css`, que anulaban la altura compacta de `mobile-nav-compact.css` (regresión de PMGM-UI-001).
- Lema oficial «Camino al centenario 1929-2029» (decisión PO 29-09-2026, PMGM-ADR-014 actualizado); `theme-color` al azul institucional #06148E.
- Gobierno: AGENTS.md §12 y PMGM-GOV-002 §8 registran la autorización permanente del PO para integrar a `dev` alcances aprobados con CI exact-head SUCCESS.
- Pruebas: `displayFormat.test.ts`, `institutionalMotto.test.ts`.

Estado: integrado en `dev` → Demo Pages → instalable QA empaquetado. No instalado en `srv01` (Issue #97 pausado), sin UAT, `main` sin cambios.
