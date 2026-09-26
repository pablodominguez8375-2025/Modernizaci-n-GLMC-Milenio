# Menú activo dorado frente a hover — 26-09-2026

## Hallazgo

En la navegación del Portal del Hermano, «Mi calendario» aparecía azul translúcido después de tocarlo en móvil o al mantener el puntero encima en escritorio, aunque correspondía a la vista activa.

## Causa

Las reglas `.sidebar .nav-item:hover:not(.disabled)` de `institutional-theme.css` y `member-portal.css` tenían especificidad `0,4,0`, superior a `.sidebar .nav-item.active` (`0,3,0`). El estado `:hover`, que puede quedar pegado después de un toque en móvil, sobrescribía el fondo dorado y el texto azul oscuro.

## Corrección

Ambas reglas hover excluyen ahora también `.active` mediante `:not(.active)`. Así el elemento activo conserva el degradado institucional `#F3C609` / `#FBAE17`, el texto e icono `#06148E`, y el hover sigue funcionando en los demás accesos. No cambian rutas, permisos ni lógica de navegación.

Se incorporó una prueba de contrato que exige que todos los selectores hover de ambas capas excluyan `.active`. La prueba falló con el código previo y pasó después; la suite frontend completa terminó con 205/205 pruebas, lint sin errores y build productivo exitoso.

## Verificación y estado

- CI exact-head, integración a `dev`, publicación Pages y artefacto instalable: pendientes.
- `srv01` sigue en pausa. Este cambio, los checks y los artefactos no implican instalación real ni aceptación de QA/UAT.
- `main` permanece intacta; Issue #97 continúa abierto.
