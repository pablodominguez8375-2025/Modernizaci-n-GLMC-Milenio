# Iconografía dorada en mosaicos azules del portal de miembros

Fecha: 26-09-2026

## Cambio

Se corrige el contraste de los mosaicos de estado de Tesorería y Hospitalaria: se conserva el fondo azul institucional y los símbolos insertos se muestran en dorado `#F3C609`. Los iconos de llamados sobre fondo azul reciben la misma regla.

## Verificación

Se incorpora una prueba de regresión que comprueba el color dorado de la iconografía de estas superficies. Integrado por squash en `dev` como `9931df6c8b2199f0c2b17edf1d31f6fa2f762886` mediante PR #172. Pasaron los checks exact-head: PMGM CI #1573, Showcase/Pages #846, QA Installable #484 y Pre-UAT #364. QA Actions #10896758099: digest `sha256:6c604107882f69834a71873200aba95b53d7b390a01b3bfda614c0f47f914d31`; BUILD-INFO confirma SOURCE_SHA y MANIFEST valida 754/754. Pages #10896848056 contiene `qa-current.json` del mismo SHA y el ZIP con SHA-256 `b4b22f6bdd2301997d145aee7e6915284c762ec190e2f73c079b291cb78e5039`; su MANIFEST también valida 754/754. La captura `mi-ficha-1440x900.png` del Showcase muestra los símbolos dorados sobre cuadros azules.

## Alcance

Cambio de presentación; no altera roles, permisos, datos, estados semánticos ni flujos. `srv01` sigue pausado; no representa aceptación de QA/UAT ni promoción a `main`.
