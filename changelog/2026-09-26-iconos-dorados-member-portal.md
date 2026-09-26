# Iconografía dorada en mosaicos azules del portal de miembros

Fecha: 26-09-2026

## Cambio

Se corrige el contraste de los mosaicos de estado de Tesorería y Hospitalaria: se conserva el fondo azul institucional y los símbolos insertos se muestran en dorado `#F3C609`. Los iconos de llamados sobre fondo azul reciben la misma regla.

## Verificación

Se incorpora una prueba de regresión que comprueba el color dorado de la iconografía de estas superficies. La validación visual y los gates post-merge deben registrarse contra el SHA integrado.

## Alcance

Cambio de presentación; no altera roles, permisos, datos, estados semánticos ni flujos. `srv01` sigue pausado; no representa aceptación de QA/UAT ni promoción a `main`.
