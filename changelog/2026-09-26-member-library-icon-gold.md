# Icono dorado del acceso a Biblioteca Virtual en Mi ficha

Fecha: 2026-09-26  
Estado: integrado en `dev`  
PR: #177  
Merge SHA: `79401d05faf7cf25ab2523f283471cb10e7221a5`

## Causa

El componente ya renderizaba el SVG institucional del libro, pero `memberLibraryShortcut.css` asignaba el azul marino `#06148E` tanto al fondo del mosaico como al color heredado del trazo. El icono quedaba invisible sobre el cuadro azul.

## Cambio

El mosaico conserva su fondo azul institucional y el icono del libro usa `var(--brand-gold)`. Se agregó una prueba de regresión para impedir que fondo y trazo vuelvan a compartir el mismo color. No cambiaron permisos, rutas, acceso por grado ni comportamiento de Biblioteca.

## Validación post-merge

PMGM CI run 36252401785, Showcase/Pages 36252401856, QA Installable 36252401796 y pre-UAT package 36252401790: SUCCESS.

Pages artifact 10909143548, digest `sha256:07a0639775f31f6f18a48ec0c95b88874eb19803e1df6d1f790080fad53cff01`; `qa-current.json` declara el SHA integrado y el ZIP publicado valida con SHA-256 `4b0cc9dfb77cfeea461db5b98234da1f949db19e5a3d4cecfe8ad89a722785ee`. BUILD-INFO confirma el SOURCE_SHA y MANIFEST valida 756/756. El artefacto QA Actions 10910090647 también identifica el SHA y su MANIFEST valida 756/756. Showcase evidence artifact 10908979113; `mi-ficha-1440x900.png` muestra el libro dorado sobre azul.

`srv01` sigue pausado. No se ejecutaron instalación, smoke autenticado en el servidor, regresión física ni UAT; los artefactos no representan esas validaciones.
