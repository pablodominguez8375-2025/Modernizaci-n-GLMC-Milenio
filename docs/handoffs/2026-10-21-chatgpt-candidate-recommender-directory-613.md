# Issue #613 — búsqueda privada de hermano/a recomendante de la Orden

Fecha 21-10-2026. Base `dev@32f9d4902085d0cbf5898d5976a4b3a29858b585`. Rama `feature/candidate-origin-real-recommenders-613-20261021-gpt`.

## Contrato y alcance backend
`GET /api/insinuados/taller/organizaciones/{organizationId}/recomendantes?q=...`.
- Secretaría del **Taller receptor** debe tener `CanManageLodgeSecretariat` y una sesión válida. Sin autorización retorna 403, también si intenta ámbito de otro Taller.
- Requiere búsqueda por nombre de 3 a 80 caracteres. Prohíbe consulta vacía o de todos los hermanos.
- Acepta recomendantes **activos de cualquier Taller de la Orden**, sin exigir grado Maestro ni pertenencia al Taller receptor. Filtra membresía de fecha vigente y último evento institucional elegible.
- Como máximo 30 resultados `{id,name}`, sin RUT, email, domicilio, cargo, grado, Taller, tokens ni datos de contacto. `Cache-Control: private, no-store`.
- El nombre y la identidad del recomendante son PII de acceso limitado. El servidor vuelve a verificar su identidad/estado al POST `/api/insinuados/taller/solicitudes` y no acepta identidad declarada como evidencia por UI. El recomendante nunca es entrevistador de este candidato ni patrocinador por el solo hecho de recomendar.
- Sin migraciones: el expediente ya tiene `CandidateOriginKind`, `CandidateRecommenderMemberId` y `CandidateOriginOtherDescription` desde #621–#625. No se alteran ni inventan los expedientes legacy con origen NULL.

## Evidencia
- Prueba HTTP/PostgreSQL `CandidateRecommenderDirectoryHttpTests` en shard A: denegación por rol/ámbito, mínimo 3 caracteres, recomendante Compañera de otro Taller elegible, miembro inactivo excluido, respuesta mínima id/nombre y persistencia al crear solicitud nueva sin asumir patrocinio.
- Registro de modificaciones de acceso a datos (GOV-004): consulta acotada de PII de la Orden, sin columnas nuevas, solo nuevos eventos de lectura bajo permiso de Secretaría del Taller receptor. Revisar tasas/abuso de búsqueda antes de producción.
- Las pruebas CI exact HEAD deben aprobar y el PR requiere conciliación `behind=0` para integrar solo a `dev`.

## Bloqueos conocidos y handoff
El frontend actual muestra origen de forma demostrativa y **NO envía los campos del origen real** en `CreateCandidateWorkshopRequest`. Un intento de ampliar `frontend/src/api/candidateIntakeApi.ts` fue BLOQUEADO por los controles de seguridad de la herramienta, por lo que **no se incluyó esa edición ni una UI real** en esta rama. Requiere revisión autorizada del contrato TS y selector UI por identidad real, con campos `originKind`, `recommenderMemberId`, `originOtherDescription`, nueva prueba de cliente y sin reutilizar `PresentersJson`. No sortear el bloqueo mediante otros mecanismos.

Pendientes #613 adicionales: PDF/formulario institucional con origen y patrocinador diferenciados, correcciones auditadas del patrocinante, tratamiento legacy, QA/UAT real glmdch. La demo pública usa datos sintéticos; un CI verde no acredita actualización física QA ni publicación en GitHub Pages. No tocar `main`.
