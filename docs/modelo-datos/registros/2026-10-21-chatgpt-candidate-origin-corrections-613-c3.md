# GOV-004 — #613 C3, correcciones justificadas del origen

**Fecha:** 21-10-2026. **Base:** dev@f27ee46905b9562902ab5216dcb85c4f1022b6bf. **PR:** #623 (draft hasta gates exact-head).

## Cambio de estructura
Nueva tabla privada `core.candidate_origin_corrections`, sin datos iniciales ni modificación de filas históricas de `core.ceremony_requests`. Migración EF `20261021154000_AddCandidateOriginCorrectionHistory`, reversible solo para QA/ensayos autorizados y sujeta al control de migraciones existente. No permitir rollback destructivo sobre datos institucionales.

| Campo | Tipo | Significado |
|---|---|---|
| Id | uuid PK | Identificador irrepetible de la corrección |
| CeremonyRequestId | uuid FK restrictiva | Expediente afectado |
| PreviousKind / NewKind | varchar(24) | Origen anterior y posterior |
| PreviousRecommenderMemberId / NewRecommenderMemberId | uuid NULL | Miembro recomendante anterior/nuevo; privado |
| PreviousOtherDescription / NewOtherDescription | varchar(200) NULL | Descripciones anterior/nueva; privadas |
| Reason | varchar(500) requerido | Motivo humano explícito (10–500 caracteres) |
| ChangedBySubject | varchar(320) requerido | Sujeto OIDC exacto autorizado |
| ChangedAtUtc | timestamptz | Momento de escritura UTC |

La tabla tiene FK, índice por expediente y fecha, validación de razón/tipo nuevo; trigger PostgreSQL rechaza UPDATE y DELETE (historial append-only). No existe endpoint de listado de correcciones ni publicación transversal de estos campos.

## Contrato de escritura
`PUT /api/insinuados/solicitudes/{requestId}/origen/corregir` exige rol de Secretaría autorizada del mismo Taller, sujeto autenticado y `{originKind,recommenderMemberId?,originOtherDescription?,reason}`. No tiene efecto si no hay cambios. Valida tres orígenes estrictos por `CandidateOriginPolicy.ValidateNew` y acredita hermano activo de cualquier Taller usando fuente institucional del servidor. Niega casos cerrados y recomendantes designados entrevistadores incluso si la designación histórica fue reemplazada. Usa el mismo lock de transacción PostgreSQL que la designación, con relectura después del lock para evitar carreras. Guarda origen vivo, historial y `AuditEvent` en una sola transacción.

**Privacidad:** motivo, descripciones e IDs personales solo en tabla privada. Auditoría general `candidate.origin.corrected` registra referencia, tipo de origen anterior/nuevo y bandera de cambio del recomendante, sin motivo ni identidad del recomendante. No enviar datos personales a GitHub, Drive, logs o demo. Aplicar Ley 21.719 y definir plazo de retención institucional antes de producción.

## Pruebas y límites
Test HTTP/PostgreSQL `CandidateOriginCorrectionHttpTests`: motivo inválido, denegación de miembro común, cambio antes/después y auditoría, historial inmutable, repetición sin cambio, recomendante incompatible. La prueba corre en shard A de integración. Validar CI, Showcase y QA instalable del HEAD exacto antes de cualquier merge. No afirmar QA físico ni UAT; UI real, PDF institucional y cierre #613 pendientes.
