# Handoff — Issue #47: matriz institucional de revisión de ascensos

Responsable ChatGPT, rama `feature/advancement-review-matrix-20261009-gpt`, creada desde `dev@40834e7d38a3b29f6ce463116cf7066cbd27af3b` el 09-10-2026 (Chile).

Alcance: unificar para un expediente de Aumento/Exaltación la regla versionada de tenidas, instrucciones, planchas y antigüedad, los conteos de actividad del grado y la cobertura cronológica del hermano. Cada requisito debe explicar mínimo, evidencia disponible y validación institucional pendiente. No contar excusas como presencias, ni planchas cargadas como presentadas. `authorizesCeremony=false` siempre, sin alterar el endpoint POST de autorización real. Consulta sólo para evaluadores de ceremonias autorizados y Secretaría del Taller, sin caché. PR vinculada al Issue #47; #48 continúa con expediente de dispensa pendiente.

No se crea esquema, migración ni permiso. El trabajo de Claude en PR #410 es exclusivamente frontend, sin solapamiento. `main` estable y `srv01` físico pausado (Issue #97).

Pendiente posterior: constancia firmada de presentación y aprobación de ambos tipos de plancha, certificación institucional de continuidad, verificación de la dispensa y snapshot inmutable. Integrar únicamente tras CI, Showcase y QA Installable exact-head SUCCESS.
