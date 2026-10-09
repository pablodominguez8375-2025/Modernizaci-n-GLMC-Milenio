# Handoff — Issue #47, revisión versionada de antigüedad (2026-10-09)

## Contexto verificado
- GitHub rama base: `dev@6eb28bd5e470665485bd7ab10af51384a78cf3bb`, PR #407 integrada (dos clases de trabajos, aún sin acreditación institucional).
- Fuentes institucionales: Reglamento General arts. 3.2 d/e (dos años de actividad continuada para Aumento/Exaltación), art. 3.4 (posible reducción en dispensa hasta 50 % con unanimidad) y Protocolo de Ceremonias 2026, carpeta Drive Proyecto Centenario. No cambiar automáticamente reglas ni definir dispensas por ausencia de expediente.
- La evidencia cronológica del PR #402 ya distingue meses completos, cobertura de membresías y eventos que interrumpen. Ese cómputo no certifica continuidad. Los tres mínimos históricos de la PR #396 no contienen antigüedad: no deben fingir que representan la cuarta condición.

## Entrega de este PR
1. `AdvancementSeniorityRulePolicy.cs`: regla institucional independiente para antigüedad mínima **configurable**, respaldada por fuente, con versiones `InstitutionalRuleSetting` por grado, fechas de vigencia y rechazo si no existe una única versión válida. No trae 24 meses por defecto ni convierte cobertura en certificado.
2. `AdvancementSeniorityRuleEndpoints.cs`: GET y POST `/api/ceremonias/reglas/avance/antiguedad`, autorización de escritura limitada al admin institucional de Gran Logia, lectura para evaluación de ceremonias; conserva versiones y auditoría de creación/cierre de vigencia. Usa fecha Chile para no reescribir historia.
3. `CeremonyEndpoints.cs`: registra las rutas anteriores.
4. `AdvancementAttendanceEndpoints.cs`: en la consulta privada de asistencia, incluye el snapshot y dictamen de revisión de antigüedad (sin datos en público). Mantiene `minimumSeniorityRuleApplied=false` para el guard real y `institutionalContinuityCertified=false`; informa `minimumSeniorityRuleReviewed` sólo si tiene regla unívoca y válida, con revisión en estado pending/candidate.
5. Tests puros + HTTP PostgreSQL: versión/vigencia, mínimo distinto de 24, interrupciones, gaps, mínimo no logrado, POST admin, respuesta sin autorización y evento de auditoría.

## Límites
- Esta evaluación **NO** integra aún antigüedad al `CeremonyEligibilityPolicy` ni al POST de autorización real; necesita completar el expediente institucional y cotejar la regla versionada al aplicar el guard. `AuthorizesCeremony=false` incluso cuando llega al umbral.
- No habilitar dispensa con cadenas/booleanos, no descontar automáticamente años, no convertir un traslado no documentado en continuidad y no certificar las dos planchas del art. 3.2 b/c por sólo subirlas.
- No introduce migraciones ni cambia `main`, frontend, usuarios o `srv01` (Issue #97 sigue pausando UAT física).
- Integración sólo tras gates CI, Showcase y QA Installable SUCCESS sobre el HEAD exacto, sin conflictos con cambios ajenos. Registro en adenda de Drive de la sesión.

## Próximo incremento
Conectar el expediente persistente de presentación de trabajos y aprobación de Cámara del Medio; verificar continuidad institucional con autoridad responsable y snapshot auditable; luego enlazar el cuarto mínimo y toda la evidencia real al guard de autorización con seguridad fail-closed. Issues #47 y #48 continúan abiertos.
