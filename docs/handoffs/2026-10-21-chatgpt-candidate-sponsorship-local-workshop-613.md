# Handoff #613 — Sponsor local del Taller separado de origen (21-10-2026)

Decisión del Sponsor: **presentación/patrocinio oficial solo por miembro activo del Taller receptor**; recomendación de origen puede proceder de hermano activo de cualquier Taller. No confundir semánticas en formulario, expediente, API ni PDF.

Implementación rama `feature/candidate-presenter-workshop-boundary-613-20261021-gpt` desde `dev@5fa6dc1`:
- Nueva FK nullable `CeremonyRequest.CandidateSponsorMemberId` con migración EF y diccionario GOV-004.
- `CandidateOriginAccreditation.VerifyWorkshopSponsorAsync` acredita mismo Taller, distinta de `VerifyAsync` (origen Orden).
- POST alta de borrador permite `sponsorMemberId` validado por servidor. PUT privado de `/taller/solicitudes/{id}/patrocinador` acredita patrocinio si faltaba, sin reescribir una identidad formal previa.
- Ficha PUT bloquea expedientes modernos con origen acreditado pero sin sponsor de su Taller; al actualizar revalida situación institucional. Detalle privado retorna `sponsorMemberId`; ningún nombre se publica transversalmente.
- Pruebas HTTP/PostgreSQL `CandidateWorkshopSponsorHttpTests` + shard A. No se presume UAT ni deployment.

**Pendiente de continuación Claude/UI:** selector explícito «Presenta/Patrocina formalmente» filtrado por hermanos activos del Taller receptor; selector «Recomendado por hermano» separado, miembros de toda la Orden. Nunca autocompletar patrocinador externo como si fuera del Taller. PDF privado con roles separados, revisión de `PresentersJson` de libre texto y eventual regularización migratoria legacy antes de activar gate obligatorio global. QA/UAT institucional, y otras rutas de publicación/ceremonias deben pasar por gate de patrocinio. No tocar `main`, QA físico o datos reales sin evidencia.
