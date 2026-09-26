# Menú activo dorado frente a hover — 26-09-2026

## Hallazgo

En la navegación del Portal del Hermano, «Mi calendario» aparecía azul translúcido después de tocarlo en móvil o al mantener el puntero encima en escritorio, aunque correspondía a la vista activa.

## Causa

Las reglas `.sidebar .nav-item:hover:not(.disabled)` de `institutional-theme.css` y `member-portal.css` tenían especificidad `0,4,0`, superior a `.sidebar .nav-item.active` (`0,3,0`). El estado `:hover`, que puede quedar pegado después de un toque en móvil, sobrescribía el fondo dorado y el texto azul oscuro.

## Corrección

Ambas reglas hover excluyen ahora también `.active` mediante `:not(.active)`. Así el elemento activo conserva el degradado institucional `#F3C609` / `#FBAE17`, el texto e icono `#06148E`, y el hover sigue funcionando en los demás accesos. No cambian rutas, permisos ni lógica de navegación.

Se incorporó una prueba de contrato que exige que todos los selectores hover de ambas capas excluyan `.active`. La prueba falló con el código previo y pasó después; la suite frontend completa terminó con 205/205 pruebas, lint sin errores y build productivo exitoso.

## Verificación y estado

- Integrada como `dev@9eef03623fc5e441bb254d057ed533dfce443413`. Post-merge: PMGM CI #1589, Showcase/Pages #870, QA Installable #508 y Pre-UAT #371 — SUCCESS.
- `qa-current.json`/Pages: SOURCE_SHA exacto; checksum del ZIP publicado `484678413c8cbe69eff430f0cb2181cf0ddff3d96ade998e54884f5e7abc89d1`. Pages artifact #10916241204 digest `sha256:55bce43a008c067a71f0ad172fd860ca8f040ad1a42b4dc057182a8ee583ffc1`.
- QA Actions artifact #10915768285 digest `sha256:acb23f754addd0c40a36cbd2b34e08542a17e1028c40ff1d34b91bdc123e3549`; BUILD-INFO identifica el SHA integrado y MANIFEST valida 757/757. ZIP Actions SHA-256 `4d11c972686e23defa9822036a69bd96238fd0aafbdf0bcfb9b05ffbf3c0e0a7`; difiere del ZIP Pages por ser una construcción independiente.
- Pre-UAT artifact #10915832891 digest `sha256:0bfcdaedbf9757892526089e5a8a91ff8cfc3eb15533ab3c2abc1fc1fa56cd5f`.
- `srv01` sigue en pausa. No se ejecutaron instalación ni smoke autenticado en el servidor, regresión física ni UAT. Checks y artefactos no equivalen a esas actividades ni a aceptación de QA/UAT.
- `main` permanece intacta; Issue #97 continúa abierto.
