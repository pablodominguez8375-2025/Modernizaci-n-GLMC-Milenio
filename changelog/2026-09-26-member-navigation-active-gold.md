# Resaltado dorado del menú activo del Portal del Hermano

Fecha: 2026-09-26  
Estado: propuesto para integración en `dev`  
Alcance: corrección visual acotada al acceso activo de la navegación institucional.

## Cambio

Los accesos activos del menú del Portal del Hermano usan fondo dorado institucional (`#F3C609` / `#FBAE17`) con texto e iconos azul oscuro (`#06148E`). El fondo azul de navegación y los accesos inactivos conservan sus colores institucionales. La regla se aplica en escritorio y navegación compacta móvil.

## Causa

`member-portal.css` definía el acceso activo con una capa blanca translúcida y texto blanco, por lo que no mostraba el resaltado dorado esperado.

## Validación

Se incorpora una prueba del contrato visual para los estilos de escritorio y móvil. La CI exact-head del PR validará la suite frontend; no se ejecuta instalación ni QA/UAT física en `srv01`.
