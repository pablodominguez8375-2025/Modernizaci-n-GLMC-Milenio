# #427 — Prueba HTTP de tres Maestros con identidades independientes (10-10-2026)

## Alcance y control
- Issue: https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/issues/427
- PR: https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/pull/434
- Rama `test/issue427-three-interviewer-http-20261010-gpt`, desde `dev@b3a1f29c5e538740e9e05afb6636bb5ad6ee5f28` (PR #432 ya integrada).
- No tocar `main` ni desplegar `srv01` (Issue #97). Solo datos sintéticos; no cambian endpoints ni normas de negocio.

## Cambios en esta PR de pruebas (sin integración todavía)

1. `CandidatePublicationHttpWorkflowTests.cs` conserva el sujeto autenticado histórico por defecto y admite `X-Publication-Test-Subject` solo en el esquema HTTP de pruebas. Conecta además `NotificationDbContext` al PostgreSQL temporal de integración, sin variar autenticación de producción.
2. `CandidateInterviewAssignmentPositiveHttpTests.cs` crea tres Maestros activos del mismo Taller, con eventos de grado 3 e identidades institucionales `member_identity_links` distintas (issuer de prueba esperado). Siembra publicación vigente y deliberación previa aprobada.
3. El Venerable sintético registra las tres designaciones con acta; la prueba comprueba tres notificaciones privadas `restricted`, ningún nombre del candidato en su cuerpo y un único elemento de «Mis entrevistas» por identidad.
4. Prueba que un Maestro no puede aceptar tareas de otro (HTTP 404), sí acepta su propia asignación (HTTP 200), reintento idempotente y persistencia `AcceptedAtUtc` y tres auditorías. En bloque `finally` limpia vínculos de identidad, avisos, historial institucional, grados y expedientes sintéticos.
5. `.github/backend-integration-shards.json` incorpora la clase nueva una sola vez en shard b, preservando el control de cobertura de todas las pruebas Integration.

## Validaciones y pendientes

- PR draft hasta que los tres gates del HEAD final CI/Showcase/QA Installable terminen SUCCESS. La compilación local .NET no está disponible; usar GitHub Actions para validar C# y PostgreSQL.
- Esta prueba cubre el segmento positivo `designación → avisos → 3 aceptaciones`. **Todavía NO acredita el recorrido completo de carga de 3 PDF/DOCX, entrega de sus informes y aprobación del paquete.**
- Issue #427 sigue abierto por esa segunda mitad E2E, reprogramación individual y aceptación visual móvil. No afirmar completo por aprobar esta PR.
- Tras merge, indexar este handoff mediante PR documental separado y registrar resultados exact-head y SHA de dev en Línea Base Maestra de Drive. No fusionar mientras PR #433 esté pendiente de su secuencia de integración.
