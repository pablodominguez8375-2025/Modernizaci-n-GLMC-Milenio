# Handoff multi-IA — #37, CSV y números con signo (12-10-2026)

## Contexto y control
- Issue: https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/issues/37 — etiqueta `agente:chatgpt`.
- PR: https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/pull/463 — rama `fix/issue37-numeric-csv-export-20261012-gpt`, destino exclusivo `dev`, draft hasta gates completos.
- Inicio verificado: `dev@eea4b727c03fed291d4d9edb61889e84be389eb4`, `main@6dfb9546a4873baff15955cf86abfd7d47e3d111` (main sin cambios).
- Consultadas: AGENTS.md, START-HERE.md, protocolo multi-IA, Issue #37, exportaciones recientes PR #460-462 y Línea Base Maestra de Drive.
- No invadir trabajos de Claude, ni tocar archivos calientes, migraciones, servidor srv01 (#97) o `main`.

## Brecha corregida
La exportación CSV del backend aplicaba el prefijo de neutralización de fórmulas a todos los valores, incluso números JSON legítimos negativos. Por ejemplo, un campo numérico `-10.5` terminaba como `'-10.5`, perdiendo su semántica numérica al abrirlo en una planilla, mientras la demo preservaba el número.

Se restringe la excepción a **la columna Valor**, solamente cuando el `JsonElement` es Number y el tipo histórico del campo autorizado es `number`, `currency` o `percentage`. Las etiquetas, claves, otros metadatos y todos los campos de texto siguen neutralizando valores que empiezan por `=`, `+`, `-` o `@` tras blancos y controles. No se modifica ningún valor almacenado ni la API o permisos.

## Archivos
- `backend/src/PMGM.Api/Modules/FormConfiguration/CandidateFormCaptureCsv.cs` — EscapeCell recibe indicador `trustedNumber` exclusivamente para la columna autorizada.
- `backend/tests/PMGM.Api.Tests/Unit/CandidateFormCaptureCsvTests.cs` — tres casos `number`, `currency`, `percentage` con valor JSON negativo y texto aparentemente numérico.
- Este handoff.

## Pruebas, publicación y siguiente paso
No atribuir éxito automático a la implementación. Verificar CI, Showcase y QA Installable del **HEAD exacto de PR #463** y el HEAD de `dev` al momento de integrar, corregir si corresponde y fusionar por squash sólo con gates SUCCESS. Verificar `downloads/qa-current.json` para reportar SHA real de GitHub Pages; la demo sintética no requiere cambios de UI para esta corrección.

La Issue #37 **permanece abierta** por cobertura de otros módulos, permisos granulares, política de retención/supresión y UAT. El instalable no significa despliegue: srv01 permanece en pausa; main no se promueve. Al integrar, añadir únicamente una línea de enlace a START-HERE.md y registrar el resultado final en GitHub/Drive para relevo.
