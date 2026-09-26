# Icono dorado del acceso a Biblioteca Virtual en Mi ficha

Fecha: 2026-09-26  
PR: #177  
Alcance: corrección visual del mosaico de acceso a Biblioteca Virtual por grado.

## Causa

El componente ya renderizaba el SVG institucional del libro, pero `memberLibraryShortcut.css` asignaba el azul marino `#06148E` tanto al fondo del mosaico como al color heredado del trazo. El icono quedaba invisible sobre el cuadro azul.

## Cambio

El mosaico conserva su fondo azul institucional y el icono del libro usa el dorado institucional `var(--brand-gold)`. Se agrega una prueba de regresión para impedir que fondo y trazo vuelvan a compartir el mismo color. No cambian permisos, rutas, acceso por grado ni comportamiento de la Biblioteca.

## Validación

Pendiente CI exact-head, Showcase/Pages y gates post-merge. `srv01` permanece pausado; no se ejecutará instalación, smoke autenticado, regresión física ni UAT.
