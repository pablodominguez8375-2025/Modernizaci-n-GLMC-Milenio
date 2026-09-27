# Resumen del Taller y acceso delegado — 2026-09-27

## Hallazgo y decisión

La revisión de los menús referenciales dejó «Libro de Oro» como una idea pendiente de validar. El Sponsor definió que debe reemplazarse por **Resumen del Taller**, una vista unificada para los miembros del Consejo de Administración.

## Corte integrado y publicado en demo

- Se reutiliza la proyección existente de Ficha de Taller; no se crea una portada paralela.
- Los ocho cargos del Consejo tienen acceso de lectura acotado a su Taller.
- El Venerable Maestro puede otorgar o revocar acceso de sólo lectura a un Maestro con membresía activa en su mismo Taller.
- Se pide fundamento, se audita quién/cuándo y se mantiene historial. El permiso pierde efecto si el hermano deja de estar activo o deja de tener grado de Maestro.
- El delegado no pasa a integrar el Consejo ni adquiere capacidades de otros módulos.
- El API aplica los permisos en servidor. La base incorpora concesiones revocables con unicidad de una concesión vigente por miembro/Taller.

## Verificación y estado

Frontend local: 206/206 Vitest, lint y build SUCCESS. El PR #183 se integró por squash en `dev@f625a0c1c7e7d4010051b5fe2ed0d7fa26a2809b`.

Gates post-merge exact-head SUCCESS: PMGM CI #1598 (run 36283920282), Showcase/Pages #882 (run 36283920245), QA Installable #520 (run 36283920204) y Pre-UAT #374 (run 36283920260). La publicación Pages completó correctamente. `qa-current.json` declara `sourceSha=f625a0c1c7e7d4010051b5fe2ed0d7fa26a2809b`, filename `Proyecto-Centenario-QA-srv01-f625a0c1c7e7.zip` y SHA-256 `2b0278074b0d36046c74eb7a0437b2325e3154635fab2dd3c160e235cf0044bf`. BUILD-INFO en Pages y QA confirma el mismo SOURCE_SHA; MANIFEST valida 762/762 entradas en ambos paquetes.

Artifacts Actions: Pages #10919578545, digest `sha256:25e4e9e3af1ecbe4f21b91d6789c0373f0731097d93ee0cecc5dea3070794f85`; QA #10919279685, digest `sha256:784c82463fa9d299b69f58fc8efe0b045b58afe00e498bdb55dc01c155229f2b`; Pre-UAT #10920025972, digest `sha256:a342808632d9434ec951335111d7b7ff88a389ba346831a040b9e11e8bbb0790`.

Demo Pages publicada: https://pablodominguez8375-2025.github.io/Modernizaci-n-GLMC-Milenio/. `srv01` sigue en pausa; no hubo instalación real, smoke en el servidor, regresión física ni UAT. Los gates/artefactos no equivalen a aceptación QA/UAT. Issue #97 sigue abierto y `main@6dfb9546a4873baff15955cf86abfd7d47e3d111` intacta.
