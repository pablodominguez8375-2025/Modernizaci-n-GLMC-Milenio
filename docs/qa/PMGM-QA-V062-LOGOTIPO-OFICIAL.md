# PMGM QA v0.62 — Logotipo oficial en plataforma

**Alcance:** incorporar el logotipo de la Gran Logia Mixta de Chile a la cabecera compartida de la aplicación y de la Demo Pages.
**Rama:** `dev` mediante PR; el SHA integrado se registra al cerrar los gates.
**Estado previo:** cabecera mostraba una “C” provisional; paleta y configuración de identidad institucional ya estaban integradas en v0.61.

## Fuente aprobada

- Google Drive: carpeta **Proyecto Centenario**.
- Archivo: `Logo Gran Logia Mixta de Chile.svg`.
- Drive ID: `1_BLXseShbQX-ioGMNLd5xKPYGtLMEvFm`.
- Ruta versionada: `frontend/public/brand/logo-gran-logia-mixta-chile.svg`.
- SHA-256 del archivo original copiado sin transformación: `d8e4660f95ffbdccb8c62fdc39eb8acd8f1aa5f27e8c17845c4bda64853d24bb`.
- Guía: `Guía de uso del logotipo de la GLMCh`, carpeta del proyecto, actualizada el 22-09-2026.

## Reglas de aplicación

1. Se sirve el SVG oficial original desde `public`; no se reconstruye ni se extrae una imagen del manual.
2. La imagen conserva proporciones con `object-fit: contain`; no se recorta, estira ni recolorea.
3. Se coloca en un soporte blanco y se conserva un espacio de protección equivalente a una cuarta parte del tamaño de la marca alrededor de sus lados.
4. El tamaño del recurso y su dirección usan `BASE_URL`, para funcionar en la raíz y bajo el subdirectorio de GitHub Pages.
5. El nombre del proyecto permanece junto al logotipo. El texto alternativo queda vacío porque el botón de marca ya tiene nombre accesible completo.

## Verificación automatizada

El capturador del Showcase espera en cada viewport que el recurso de imagen termine de cargar, tenga dimensiones naturales, esté contenido sin deformación, use soporte blanco, no se superponga con los controles y no provoque desbordamiento horizontal en móvil. El recorrido existente conserva capturas de 360×800, 390×844 y 1440×900 para perfiles de QA.

Los resultados exactos de pruebas, Showcase/Pages, instalable QA y Pre-UAT se agregan aquí al integrar el PR. El despliegue físico, smoke y regresión de `srv01` continúan pendientes de acuerdo con Issue #97; una publicación de Pages o un ZIP no equivale a QA operacional.
