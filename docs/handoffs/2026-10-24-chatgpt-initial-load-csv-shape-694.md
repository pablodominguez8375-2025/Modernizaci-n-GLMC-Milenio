# #694 fase C — revisión de estructura CSV exclusivamente offline

Agente ChatGPT · 24-10-2026 · rama `feature/initial-load-csv-shape-694-20261024-gpt` · base `dev@799318f98819167d7fc79ca66c259a6d760d9f3b`.

Dependencia funcional: #698 integrado, tres archivos CSV vacíos; #700 política de contenido prevalidado en PR separado, no utilizar su resultado sin merge ni CI vigente.

Alcance estrictamente nuevo y sin solape: `backend/src/PMGM.Api/Modules/Bootstrap/InitialLoadCsvShapePolicy.cs` y `backend/tests/PMGM.Api.Tests/InitialLoadCsvShapePolicyTests.cs`. Permitir validar en memoria las cabeceras exactas y el formato de archivos CSV de Talleres, Hermanos y CargosVigentes antes de una futura fase de lectura, con límite de bytes/filas/celdas, sintaxis de comillas CSV, y error por fila/campo sin devolver contenido. Soportar BOM UTF-8 decodificado por consumidor futuro y LF/CRLF. No guardar valores, no escribir al disco, no abrir endpoint HTTP ni unir con preflight #700 hasta terminarlo.

No usar datos reales; tests únicamente sintéticos. El resultado no acredita identificación, consentimiento, cifrado, permiso del Máster ni autorización de importación; `CanApply` siempre false, `WritesPerformed` cero. Jurídico Ley 21.719 aún pendiente para nóminas reales. Ni main, ni QA físico, ni versiones de Claude. Hacer CI/Showcase/QA Installable 3/3 para HEAD exacto y atrás=0 antes de cualquier integración a dev.

Handoff: PR #702 previsto por etapa; #694 sigue abierto tras esta fase y exige plan/apply auditado, transaccional, reversible, idempotente y cierre de carga controlado. Registrado en GitHub y Drive.
