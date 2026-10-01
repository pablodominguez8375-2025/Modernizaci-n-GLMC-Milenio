# Portal de insinuados: autorización HTTP — 2026-10-01 UTC

Issue #44; PR #233; agente ChatGPT. Base dev 6b82802d1fe2588dda6788ab8f54d9593cf8029c; main 6dfb9546a4873baff15955cf86abfd7d47e3d111. PO pidió «Sigue» tras la auditoría #230/#231/#232.

## Implementación

Se agregan tres pruebas HTTP/PostgreSQL en CandidatePublicationHttpWorkflowTests.cs y QA específica PMGM-QA-CANDIDATE-PUBLICATION-HTTP.md. Cubren consulta desde otro Taller, DTO mínimo, vigencia, publicación sin foto, anónimo 401, foto pública protegida y cese al vencer, aislamiento de ficha/foto/descarga documental y acceso legítimo del secretario propietario. Auth de prueba con claims por petición; autorización real. PostgreSQL real; adaptador de almacenamiento en memoria, no prueba S3/OIDC.

Tres archivos nuevos incluyendo este handoff, más una corrección de fecha de fixture en MemberSelfServicePostgreSqlTests.cs. Sin cambios funcionales, migraciones, permisos, UI ni reglas institucionales. No solapamientos con PR #116/#60/#58/#131/#45; no se editan archivos calientes ni ramas históricas. PR #45 conserva la sincronización REQ-025. La ampliación a la prueba existente fue reclamada en #44/#233 antes de editar.

## Verificación y estado

Git diff --check sin errores. Backend local no disponible (sin SDK/PG/Docker); se usa CI con PMGM_TEST_POSTGRES y TRX real. CI/Showcase/QA installable del head final todavía pendientes; no se declara aceptación técnica ni cierre de #44 hasta aprobarlos. Se verificó la publicación inicial dev@6b82802 con ZIP SHA-256 56d63643450ab765f88c486cb0792cf82172c33bc8d59b007bba2ecbddaf6d68, 811 checksums y BUILD-INFO correcto.

Primer CI 36801111893: 354 casos, dos fallos. Se corrigió la comparación del orden textual de Cache-Control usando directivas Private/NoStore. La prueba previa de Mi ficha esperaba cuota del mes UTC, pero el producto usa fecha America/Santiago; al cruzar el mes UTC seguía septiembre en Chile. Su fixture ahora usa fecha institucional. No se altera la expectativa monetaria ni la regla del producto. El head corregido debe superar todos los gates antes de integrar.

La Línea Base Maestra fue consultada y debe recibir el resultado con lectura de retorno. START-HERE recibirá una línea en el PR documental posterior al merge. Evidencia/gates/head/integración/publicación final se registrarán en #44, PR #233, handoff posterior y Drive.

Srv01 pausado #97: despliegue QA pendiente, sin instalación, QA física ni UAT. Main intacta. Este incremento completa pruebas de un comportamiento ya existente; no modifica la UI QA v0.63.
