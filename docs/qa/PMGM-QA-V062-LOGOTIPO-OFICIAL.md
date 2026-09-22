# PMGM QA v0.62 — Logotipo oficial en plataforma

**Alcance:** incorporar el logotipo de la Gran Logia Mixta de Chile a la cabecera compartida de la aplicación y de la Demo Pages.
**Estado:** integrado y desplegado desde `dev`.
**PR:** #137, merge squash.
**SHA integrado:** `e0dd6e66fe5d6ab7f6be6a2d4c7170f4729c06a0`.
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

## Resultado exact-head y salidas

| Gate / salida | Ejecución | Resultado |
|---|---:|---|
| PMGM CI | #1436 | SUCCESS |
| Showcase / GitHub Pages | #676 | SUCCESS; deploy del mismo SHA |
| QA Installable | #314 | SUCCESS |
| Pre-UAT Installable | #329 | SUCCESS |
| Suite frontend local | — | 183/183; lint y build SUCCESS |

- URL Demo: https://pablodominguez8375-2025.github.io/Modernizaci-n-GLMC-Milenio/
- Verificación live: badge `E0DD6E6`; logo carga (155×150 intrínseco), soporte blanco, `object-fit: contain`; cabecera sin solapamiento.
- Pages artifact #10723212264; digest `sha256:1be0343b760f6f65d5acfcf066582d56a6e4c76638955bf0eefb73cf44df375c`; vence 23-09-2026 22:56:18 UTC.
- Evidencia responsive #10723177636; digest `sha256:52b25c5f29c4948a19551148cd2cefb94f00a6cf29802344bb6e4d44114c5b03`; vence 06-10-2026 22:56:15 UTC.
- Instalador QA #10723132819, `proyecto-centenario-qa-srv01-e0dd6e66fe5d6ab7f6be6a2d4c7170f4729c06a0`; digest `sha256:a42964269eb9af176feacfd3f0f546b51072647fb828b8af062b2bded180497b`; vence 22-10-2026 22:55:08 UTC.

El repositorio público conserva el logo oficial sin transformación, SHA-256 `d8e4660f95ffbdccb8c62fdc39eb8acd8f1aa5f27e8c17845c4bda64853d24bb`. El despliegue físico, smoke y regresión en `srv01` continúan pendientes en Issue #97; Pages o un ZIP no equivalen a QA operacional. `main` sigue intacta en `6dfb9546a4873baff15955cf86abfd7d47e3d111`.
