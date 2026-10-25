# Issue #613 C3 — corrección auditada del origen del candidato (21-10-2026)

Agente ChatGPT. Base inicial `dev@f27ee46905b9562902ab5216dcb85c4f1022b6bf` después de la integración C2, PR #622.

## Objetivo
Permitir corrección privada del origen únicamente a Secretaría de su Taller, exigiendo motivo, validación nuevamente de recomendante activo por fuente servidor, traza inmutable antes/después y protección de la independencia de entrevistadores. API y persistencia aditiva; UI/impresión formal quedan en coordinación con Claude.

## Controles y reservas
- Reutilizar `CandidateOriginPolicy.ValidateNew`; clientes legacy con origen NULL continúan sin forzar datos ficticios.
- Bloquear corrección de expedientes cerrados o si el nuevo recomendante estuvo designado entrevistador del expediente.
- Serializar con el mismo advisory lock PostgreSQL usado en asignación de entrevistadores; revalidar dentro de la transacción.
- Guardar las versiones anterior/nueva y el motivo en registro privado append-only con actor y fecha UTC. El evento de auditoría general solo guardará metadatos mínimos, sin identidad personal del recomendante ni motivo libre.
- Seguridad por Taller y sesión, sin exponer a listados públicos ni autorización implícita.
- Pruebas C3 HTTP/PostgreSQL, además de regresión C2; CI/Showcase/QA instalable exact-head obligatorios.
- No publicar en main, no desplegar a QA físico ni introducir datos reales.

## Estado
Reservada rama C3 para avance. Completar con SHA, archivos y gates. Si CI falla, NO fusionar.

## Archivos de implementación (PR #623)
- `backend/src/PMGM.Api/Modules/CandidateIntake/Entities/CandidateOriginCorrection.cs`: registro privado anterior/nuevo, causa, actor y UTC.
- `backend/src/PMGM.Api/Modules/CandidateIntake/CandidateOriginAccreditation.cs`: política compartida de acreditación de hermano activo.
- `backend/src/PMGM.Api/Modules/CandidateIntake/CandidateOriginCorrectionEndpoints.cs`: PUT restringido; motivo, no-op, bloqueo de casos cerrados o recomendantes entrevistadores; transacción + auditoría.
- `backend/src/PMGM.Api/Data/PmgmDbContext.cs`: modelo e índice de historial.
- `backend/src/PMGM.Api/Migrations/20261021154000_AddCandidateOriginCorrectionHistory.cs`: tabla y trigger append-only.
- `backend/src/PMGM.Api/Modules/CandidateIntake/CandidateWorkshopIntakeEndpoints.cs`: nueva fuente de acreditación compartida; mismo contrato C2.
- `backend/src/PMGM.Api/Modules/CandidateIntake/CandidateIntakeEndpoints.cs`: registra nueva ruta privada.
- `backend/src/PMGM.Api/Modules/CandidateIntake/CandidateInterviewAssignmentEndpoints.cs`: segundo control con relectura después del advisory lock, para evitar corrección concurrente.
- `backend/tests/PMGM.Api.Tests/Integration/CandidateOriginCorrectionHttpTests.cs` + `.github/backend-integration-shards.json`: negativo por rol/motivo, cambio + persistencia, trigger, idempotencia e incompatibilidad.
- `docs/modelo-datos/registros/2026-10-21-chatgpt-candidate-origin-corrections-613-c3.md`: diccionario GOV-004.

## Continuidad
PR #623 es un borrador hasta CI/Showcase/QA instalable exact-head SUCCESS y relectura de HEAD `dev`. QA físico y UAT NO ejecutados. La UI Claude deberá invocar el contrato nuevo y hacer visible la causa solo a autorizados. Quedan PDF/formulario privado y obligatoriedad para nuevos clientes, con compatibilidad legacy conservada. Asegurar revisión de privacidad y retención. Este handoff no autoriza `main`.
