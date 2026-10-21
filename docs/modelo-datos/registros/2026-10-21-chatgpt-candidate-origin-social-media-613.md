# GOV-004 — #613: origen redes sociales

Fecha: 21-10-2026. Rama: feature/candidate-origin-social-media-613-20261021-gpt.

**Contrato**: el canal de origen no constituye patrocinio institucional. Códigos del origen: `brother` (recomendación de un hermano), `internet` (página web), `social_media` (redes sociales), `other` (otra fuente, descripción obligatoria), y NULL para expedientes anteriores. El patrocinador formal pertenece al Taller receptor (PR #624) y se registra por separado.

**Estructuras modificadas**: no aparecen columnas nuevas. Migración aditiva `20261021173000_AddSocialMediaCandidateOrigin` reemplaza los CHECK existentes de `core.ceremony_requests` y `core.candidate_origin_corrections` para permitir `social_media`. Los demás orígenes permanecen iguales. La migración no transforma datos. El rollback se bloquea naturalmente si existen filas que usan el nuevo código, evitando pérdida silenciosa de evidencia.

**Validación y privacidad**: el canal redes sociales no admite ID de recomendante ni descripción «Otro»; un hermano recomendante se registra con `brother` y se verifica contra membresía institucional vigente según el contrato existente. Los datos privados no se incorporan a listados públicos. Las correcciones mantienen motivo y auditoría append-only.

**Pendientes**: UI y PDF institucional deben ofrecer ambas opciones Internet y Redes sociales claramente separadas. Se requiere revisión integral de QA y UAT antes de cerrar #613. `main` y producción se mantienen sin cambios.
