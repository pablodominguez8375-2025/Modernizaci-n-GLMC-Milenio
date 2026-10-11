# GOV-004 — Issue #48: expediente de dispensa de ascenso

PR #440, rama `feature/issue48-dispensation-dossier-20261010-gpt`, base
`dev@57f79249e2ca891036c141e640ff2bcf4d6809dc`. Cobertura: expediente de
dispensa para Aumento de Salario y Exaltación. No es un diccionario completo.

## Antes y después

Antes existía una evidencia lógica no persistida con referencias libres. Después,
`core.advancement_dispensation_cases` conserva una solicitud única por ceremonia,
la norma vigente, reglas de mínimos, fundamento, Cámara del Medio, acta, votación,
nómina congelada de Maestros habilitados, reducciones y resolución de Régimen
Interior. El autorizador vuelve a verificar los hechos vivos antes de usarla.

## Diccionario físico

| Campo | Tipo PostgreSQL | Nulo | Regla / clasificación |
|---|---|---:|---|
| Id | uuid | no | Identificador técnico |
| CeremonyRequestId | uuid | no | FK restrictiva y única; interno |
| OrganizationId / MemberId | uuid | no | Alcance Taller/Hermano; restringido |
| InstitutionalRuleId | uuid | no | Regla art. 3.4 congelada |
| AdvancementRuleId / SeniorityRuleId | uuid | no | Versiones de mínimos |
| Foundation | varchar(2000) | no | Fundamento; confidencial |
| MeetingId / FullMinuteId / BallotId | uuid | no | Evidencia de Cámara; confidencial |
| EligibleMasterIdsJson | jsonb | no | UUID de Maestros; confidencial, no se expone en GET |
| ReductionsJson | jsonb | no | Código y reducción autorizada |
| EligibleCount / PositiveCount / NegativeCount | integer | no | Resultado congelado |
| Status | varchar(40) | no | council_approved, approved, rejected |
| SubmittedBySubject / ResolvedBySubject | varchar(320) | según etapa | Identidad institucional; restringida |
| SubmittedAtUtc / ResolvedAtUtc | timestamptz | según etapa | Auditoría |
| ResolutionReference | varchar(500) | sí | Obligatoria al resolver |
| ResolutionNotes | varchar(2000) | sí | Observaciones; confidenciales |

## Contratos y controles

- POST/GET `/api/ceremonias/solicitudes/{id}/avance/dispensa`.
- POST `.../dispensa/resolver` sólo Régimen Interior.
- Secretaría del Taller registra; el mismo sujeto no puede resolver.
- La votación debe tener asunto exacto `dispensation:{requestId:N}`, acta aprobada,
  Cámara de grado Maestro, cero votos negativos y todos los habilitados positivos.
- Sólo se reducen `seniority_months`, `meeting_attendance` e
  `instruction_attendance`, hasta el 50 %. Tesorería y planchas quedan fuera.
- La regla `ceremony.advancement.dispensation` se versiona en
  `institutional_rule_settings`, fuente Reglamento General art. 3.4.

## Privacidad y retención

Acceso privado de Secretaría del Taller, evaluadores institucionales y Régimen
Interior. La nómina no se devuelve en el contrato de lectura. Auditoría registra
referencias y conteos, sin nombres. Se aplica la retención institucional de
expedientes de ceremonia; este cambio no crea una política nueva.
