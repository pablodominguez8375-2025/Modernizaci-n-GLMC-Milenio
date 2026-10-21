# GOV-004 — #613 patrocinio formal vinculado al Taller receptor

**Fecha:** 21-10-2026. **Base:** dev@5fa6dc1eeedd301bc0b28df8f4ab2efe36f510a1.

## Decisión Sponsor
Un candidato puede llegar por recomendación de miembro activo de cualquier Taller, Internet u otro medio, pero **solo puede ser presentado o patrocinado formalmente por un hermano activo del Taller receptor**. El recomendante externo no es presentador del expediente receptor. Las identidades son independientes; pueden coincidir si es hermano activo del Taller receptor. El recomendante jamás es entrevistador del candidato.

## Estructura de datos
- `core.ceremony_requests.CandidateRecommenderMemberId` permanece referencia privada al **origen** (puede pertenecer a otro Taller).
- Nueva columna **`CandidateSponsorMemberId uuid NULL`**, FK restrictiva a `core.members.Id`, índice de consulta separado; migración `20261021162000_AddCandidateWorkshopSponsor`, reversible. NULL en expedientes legados/borradores = **presentador institucional no acreditado**, nunca se infiere por nombre.
- Antes de escritura, backend comprueba membresía activa del ID en `core.memberships` **con OrganizationId idéntico al Taller receptor**, rango de fechas y último estado institucional válido a fecha Chile. Para origen la acreditación anterior sigue pudiendo usar cualquier Taller.
- Alta opcional `sponsorMemberId`, nueva ruta privada PUT `/api/insinuados/taller/solicitudes/{requestId}/patrocinador` para asignación validada, y campo `sponsorMemberId` privado en detalle de ficha. No publicar identidad del patrocinador en bandejas ni portal.
- Asignación repetida mismo ID = idempotente; cambio de patrocinador ya registrado **bloqueado** hasta procedimiento de corrección institucional auditable posterior. No sustituir por texto libre.
- **Compatibilidad:** expedientes antiguos sin origen mantienen flujos legacy mientras se regulariza su patrocinio. Para los expedientes del circuito moderno con origen informado, el PUT de ficha no avanza a presentación formal sin miembro local acreditado. La globalización del requisito a todos los flujos y la regularización histórica siguen pendientes, no declarar norma cubierta en todo el sistema.
- `PresentersJson` y `CandidateIntakeUpsertRequest.Presenters` son texto heredado y NO se consideran evidencia de membresía; deben migrar a selector de miembro local, sin inventar correspondencias.

## Pruebas y aceptación
HTTP/PostgreSQL: recomendante otro Taller + patrocinador local válido; patrocinador externo rechazado `CANDIDATE_SPONSOR_NOT_ACTIVE_IN_WORKSHOP`; Internet/borrador sin patrocinador puede asignar después, exclusivamente hermano local. Integración CI/QA/UAT pendiente del merge y gate físico. `main` fuera de alcance.
