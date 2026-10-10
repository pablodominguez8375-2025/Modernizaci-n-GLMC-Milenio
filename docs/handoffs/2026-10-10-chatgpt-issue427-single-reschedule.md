# Handoff — #427 Reprogramación individual de Maestro entrevistador (10-10-2026)

Sponsor aprobó el flujo de 3 Maestros, programación, cambios auditados y avisos privados. Este subincremento crea una edición **de fecha de una sola entrevista** sin volver a designar el equipo ni borrar entregas. Parte desde dev@4706ede7d537ad40fee8996d0e545ce1c4d58de0.

## Reserva
Solo ChatGPT; no modificar archivos de PR #434, especialmente tests Integration y `.github/backend-integration-shards.json`. Archivo backend `CandidateInterviewAssignmentEndpoints.cs`, plantilla/códigos de notificación y migración aditiva, UI `CandidateInterviewAssignmentsPage.tsx`, API `candidateIntakeApi.ts`, nuevo test unitario.

## Criterios de cierre
- Sólo Venerable activo del mismo Taller, expediente publicado, asignación no sustituida ni informe entregado
- Fecha válida posterior al acuerdo y actual/futura; motivo obligatorio de 10–1000 caracteres; auditoría de antigua y nueva fecha, rol, actor
- Notificación `restricted` al Maestro afectado, sin nombre del postulante ni detalles sensibles
- No cambiar otras posiciones, IDs, aceptación personal, o historial
- CI, Showcase, QA SUCCESS exact HEAD, merge sincronizado a dev con GOV-003; main y srv01 intactos
- Documentar seguimiento en Issue #427 y Línea Base Maestra. START-HERE se indexa sólo postmerge.
