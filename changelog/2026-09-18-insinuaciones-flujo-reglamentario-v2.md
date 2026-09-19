# Cierre funcional — flujo reglamentario de insinuaciones v2

Fecha de integración funcional: 2026-09-18  
PR funcional: #106  
Merge funcional: `125635e5cbf23919d135965311f7c24bb086cea1`

## Resultado

El Proyecto Centenario extiende el dominio existente de insinuaciones sin crear módulos paralelos.

Cambios principales:

- cómputo de 7 días desde presentación en 1.er grado;
- plazo de 7 días impuesto server-side;
- bloqueo transversal por rechazo de 3.er grado o balotaje final;
- panel de flujo reglamentario en Secretaría del Taller;
- entrevistas privadas PDF/DOCX;
- revisión de 3.er grado;
- balotaje hasta tres trámites;
- solicitud formal de Iniciación;
- paridad del contrato funcional en GitHub Pages con datos ficticios.

## Validación del PR #106

- PMGM CI: success;
- backend build/tests PostgreSQL/S3/ClamAV: success;
- frontend lint/build/tests: success;
- privacy/data classification/migration gates: success;
- first-implementation smoke + recovery: success;
- piloto HTTPS/OIDC + recovery: success;
- Showcase PR: success;
- QA installable PR: success.

## Triple salida del merge funcional

- `dev`: `125635e5cbf23919d135965311f7c24bb086cea1`;
- Pages deployment: success para el mismo SHA;
- Pages URL: `https://pablodominguez8375-2025.github.io/Modernizaci-n-GLMC-Milenio/`;
- ZIP QA público: `Proyecto-Centenario-QA-srv01-125635e5cbf2.zip`;
- SHA-256 ZIP público: `49f6df5eea488e996919884abd726cb6253a3b6e7dfbdc32d7e473d1e0408c1a`;
- Artifact Actions QA: ID `10567925634`;
- Digest artifact: `sha256:453e786f955f6039204bace2efac855073547a83739466c958fc51f7609a40a4`.

## Regresión operacional

Se agrega `QA-022` al kit srv01 para verificar con datos ficticios el flujo de insinuación completo. El cierre de regresión pasa de 21/21 a **22/22**.

Issue #97 permanece abierto hasta desplegar físicamente el corte vigente en `srv01`, ejecutar smoke, regresión 22/22 y UAT institucional. No se promueve a `main` antes de ese cierre.
