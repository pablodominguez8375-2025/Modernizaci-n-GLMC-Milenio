# Handoff #47 — tipos distintos de planchas y evidencia formal (09-10-2026)

Fuente oficial: Constitución y Reglamento General de la Orden (Drive Proyecto Centenario, art. 3.2 b/c), más Protocolo de Ceremonias 2026. La norma exige un trabajo sobre simbolismo del grado y otro de cultura general relacionado con enseñanza masónica; ambos deben presentarse, calificarse, aprobarse en Cámara del Medio y acompañar la solicitud de ascenso.

Este corte no crea la acreditación final. Añade `AdvancementWorkPaperEvidencePolicy`, un chequeo puro de integridad *estructural* para dos referencias documentales de autores/talleres/grado/período correctos, categorías diferentes y DocumentId distintos. Las versiones de un mismo documento no cuentan doble; tampoco reuniones o actas no identificadas.

**Sin autorización automática:** los campos `InstitutionallyAccredited` y `AuthorizesCeremony` son siempre falsos. Las referencias aportadas como candidatos NO han sido autenticadas contra las fuentes maestra ni firmadas por Cámara del Medio. No se modifican modelos, endpoints, roles, migraciones, UI, políticas de autorización ni mínimo vigente. Tests puros documentan casos normales, duplicados, categorías erróneas, grado/Taller/autor/fechas, documentos idénticos y falta de referencias.

Relación con PR #404: éste consolida extractos repetidos en `AdvancementWorkPaperReviewPolicy`; este corte agrega archivos independientes, sin tocar el PR #404. Ambos son trabajos de Issue #47 `agente:chatgpt`.

Siguiente fase: clasificar formalmente el tipo de trabajo con versión histórica; ligar acta/Extracto de Tenida a la presentación real, registrar calificación y voto/acto de aprobación de Cámara del Medio y contrastar con contenido documental y referencias firmadas. Sólo un verificador autorizado y persistente podrá convertir evidencias en cumplimiento. Issue #48 (dispensa) permanece abierto; art. 3.4 no permite reducir los dos trabajos. No tocar main ni desplegar srv01 (#97).
