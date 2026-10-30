# #471 — Retención de solicitudes de derechos: cierre formal antes de anonimización

**Fecha:** 2026-10-30. **Estado:** cambio de desarrollo aislado, pendiente de CI, revisión y aprobación; no desplegado.

- Rama: `fix/privacy-retention-open-request-20261030-gpt`, base inicial `dev@ceaca327cacb983850a66a9dbdb41a8d4a3c4316`.
- Issue: [#471](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/issues/471), hallazgo y alcance anunciado en comentario 6521736776.
- Trabajo paralelo: PR #804 (autoservicio de solicitudes, separado y sin tocar archivos compartidos). No integrar ninguno durante la aplicación física del corte #803/QA525.

## Hallazgo y protección

`PrivacyRetentionEndpoints.ExecuteEvaluationAsync` ejecutaba `anonymize` sobre `DataSubjectRequest` si la evaluación estaba lista y sin legal hold, pero **sin exigir el cierre de la solicitud**. El adaptador anula `PersonId`, `ResponsibleSubject`, `Resolution`, `Grounds`, `EvidenceReference`. Si se invoca sobre una solicitud en trámite, puede quitar la vinculación necesaria para resolverla.

Ahora el adaptador llama `RetentionExecutionPolicy.CanAnonymizeDataSubjectRequest` **antes de cambiar campos**. Acepta únicamente `Status == closed` y `ClosedAtUtc != null`. En otro caso HTTP 409 con código `DATA_SUBJECT_REQUEST_NOT_FORMALLY_CLOSED` y `requiresManualReview=true`. La evaluación permanece pendiente para una decisión formal; no se autoriza borrado duro ni ninguna anonimización de otras entidades.

Pruebas unitarias añadidas a `RetentionExecutionPolicyTests`: rechazos de `under_review` con/sin fecha, `active`, `closed` sin fecha, estado desconocido; aceptación solo `closed` con fecha. CI exact-head pendiente de registrar. No se hace afirmación de prueba PostgreSQL de este flujo hasta completar test de integración específico.

## Inventario técnico de ejecución automática actual

| Tipo de entidad | Acción automática de expiración | Estado observado en código | Control pendiente |
| --- | --- | --- | --- |
| `DataSubjectRequest` | `anonymize` | Único adaptador explícito, borra vinculación y narrativas restringidas; ahora solo tras cierre formal, evaluación lista, acción coincidente y sin hold | Revisión jurídica de causal/plazo, revisión de uso de identidad y prueba HTTP/PostgreSQL |
| `DataSubjectRequest` | `delete` | Bloqueado, revisión humana | Justificación legal y acto expreso, sin habilitar automáticamente |
| `Membership`, `Person`, otras entidades | `anonymize/delete` | No hay adaptador automático; fallo cerrado y revisión humana | Inventario de categorías, retención y relaciones, análisis de documentos y auditoría |
| Cualquier entidad | `keep` / `review` | `keep` no cambia registros; `review` exige gestión humana | Versionar decisiones con jurídico y responsables |

**No confundir este inventario técnico con una tabla legal de plazos:** no se aprobaron plazos de conservación aquí. Las políticas versionadas `DataRetentionPolicy` y holds vigentes siguen siendo fuente de verdad; para aplicar acciones destructivas a otras entidades se requieren decisiones jurídicas, diseño de exportación/retención y pruebas específicas.

## Límites de seguridad y continuidad

Sin migraciones, cambios de API/WEB de QA, modificaciones a `main`, servicios de `glmdch`, `srv01`, ni datos reales. No tocar `PrivacyEndpoints.cs` o tests asociados reservados en PR #804. Mantener `dev` estable para el paquete QA firmado `qa-signed-ceaca327cacb983850a66a9dbdb41a8d4a3c4316`; migración #801 ya aplicada físicamente una vez y G6 pre/post PASS. Antes de fusionar: verificar CI exact-head y comprobar que el corte físico QA #525 terminó o se autorizó explícitamente un nuevo corte. Registrar resultado y acciones en GitHub/Drive.

**Pendiente fuera de este PR:** UAT de retención con HTTP/PostgreSQL, exportación ARCO y revisión legal Ley 21.719; ninguna se declara completa.
