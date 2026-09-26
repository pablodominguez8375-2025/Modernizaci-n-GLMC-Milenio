# Resaltado dorado del menú activo del Portal del Hermano

Fecha: 2026-09-26  
Estado: integrado en `dev`  
Alcance: corrección visual acotada al acceso activo de la navegación institucional.

## Cambio

Los accesos activos del menú del Portal del Hermano usan fondo dorado institucional (`#F3C609` / `#FBAE17`) con texto e iconos azul oscuro (`#06148E`). El fondo azul de navegación y los accesos inactivos conservan sus colores institucionales. La regla se aplica en escritorio y navegación compacta móvil.

## Causa

`member-portal.css` definía el acceso activo con una capa blanca translúcida y texto blanco, por lo que no mostraba el resaltado dorado esperado.

## Corte integrado

Merge SHA: `d13428684b13f649d6732e9be4e555e90a94248d`.

CI exact-head, Showcase/Pages, QA Installable y Pre-UAT package terminaron SUCCESS. `qa-current.json` y BUILD-INFO identifican ese SHA; el ZIP Pages valida con SHA-256 `80c3340dca8bd006f391a48a986431a6f01529791fdbb0023754bdb3156fc021`; MANIFEST valida 755/755 entradas en Pages y QA Actions. La evidencia visual móvil/escritorio muestra el menú activo dorado.

## Validación

Se incorpora una prueba del contrato visual para los estilos de escritorio y móvil. La CI exact-head del PR validará la suite frontend; no se ejecuta instalación ni QA/UAT física en `srv01`.
