# Handoff — Issue #47: acta completa para revisión de planchas

Estado: PR en desarrollo, sin certificación institucional ni autorización.

Base inicial dev@`3a7f984759c109cdc05e895ed6e195b3f82d95e1`. main sigue reservado, y srv01 está pausado por #97.
Objetivo: exponer separadamente actas completas documentalmente revisables por Tenida vinculada a la plancha. No confundir documento existente con acto de presentación, calificación ni aprobación de Cámara del Medio.
Archivos reclamados: `AdvancementWorkPaperEndpoints.cs`, `AdvancementWorkPaperReviewPolicy.cs`, nuevo `AdvancementMeetingMinuteEvidencePolicy.cs`, pruebas unitarias correspondientes y este handoff.
No se agregan migraciones, mutaciones HTTP, permisos nuevos, UI, ajustes a /autorizar ni cambios en START-HERE (se indexará después del merge).
Checks exact-head y SHA dev/Pages/QA se anotarán después.
