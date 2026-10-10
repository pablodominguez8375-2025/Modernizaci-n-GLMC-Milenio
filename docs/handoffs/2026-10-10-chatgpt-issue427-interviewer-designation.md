# Handoff de cierre funcional — Designación formal de entrevistadores (#427)

Fecha: 10-10-2026, Chile. Sponsor aprobó expresamente la propuesta.
Issue: https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/issues/427
PR reservado ChatGPT: https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/pull/428
Rama: `feature/issue427-venerable-interview-assignments-20261010-gpt`.
Base inicial: `dev@52b0893c52c5b37e9521c3972ff0f416eaa06ad9`. Nunca promover `main` ni tocar `srv01`, Issue #97.

## Fuente normativa y responsabilidad

Protocolo institucional oficial 2026 de Drive (ID `12SS3w2JCmpbMnC9p9KUL4jytsGle6Bp2`): el Consejo de Administración o la Cámara del Medio **programa tres entrevistas** una vez aprobada la insinuación para publicar. El Venerable **formaliza el acuerdo**, no lo sustituye unilateralmente; se exige órgano, fecha y referencia identificable del acta, además de la deliberación unánime inicial y publicación previa.

## Implementación efectiva en PR

- `core.candidate_interview_assignments`, entidad `CandidateInterviewAssignment`, migración `20261010153000_AddCandidateInterviewAssignments`, índices parciales únicos de Maestro/posición por expediente, FK a maestro institucional/ceremonia; auditoría con sujeto designador y bitácora de reemplazos. En `CeremonyRequest`, indicador `RequiresFormalInterviewAssignments` para nuevas solicitudes; expedientes históricos existentes reciben `false` en migración para no falsear el expediente antiguo.
- `CandidateInterviewAssignmentPolicy`: 3-6 IDs distintos no vacíos, órgano Council/Chamber válido, fecha válida no futura, acta de 5-500 caracteres; casos adicionales justificados.
- `CandidateInterviewAssignmentEndpoints`: cola privada de candidatos publicados para Venerable; selector desde membresías activas reales del mismo Taller con grado Maestro e historial de estado vigente; POST designación sólo rol `lodge_venerable` de ese Taller, no autorizado a Secretaría ni a otro Taller; un vínculo unívoco de cuenta institucional por Maestro es necesario para notificar. Reemplazo exige motivo y no permite ocultar informes entregados. Auditoría y concurrencia por lock de expediente.
- Migración NotificationDbContext `20261010153100_AddCandidateInterviewAssignmentNotification`: template clasificada `restricted`, notificación interna idempotente por asignación a cada Maestro, sin divulgar nombre de candidato ni informe en cuerpo; reintento si fallan avisos, no marcar enviados si hay fallo.
- Maestro autenticado: `GET /api/insinuados/entrevistas/mis-designaciones` restringido a su identidad institucional, (3.er grado), Taller activo; entrega privada `PUT /api/insinuados/solicitudes/{requestId}/entrevistas/{assignmentId}/contenido` PDF/DOCX de identidad exacta con verificación de firma/formato, virus, nombre y fecha; `POST /solicitudes/{requestId}/entrevistadores-designados/{assignmentId}/entregar` sólo valida documento privado realmente subido a esa misma asignación y marca completado.
- Secretaría: `CandidateWorkflowPanel` visualiza estado de los tres informes y sólo permite registro del paquete por informes validados de todos los designados y cuestionario/autobiografía. No puede subir archivos en nombre de Maestros designados; `POST /antecedentes` verifica en backend las referencias, IDs y cantidad mínima. Nuevas solicitudes sin designación formal se bloquean (no tres nombres libres); históricos preexistentes protegidos por flag.
- Frontend responsivo `CandidateInterviewAssignmentsPage.tsx`: menú Venerable «Designar entrevistadores», «Mis entrevistas» para Maestro y menú personal, campos del acta, tres Maestro distintos, programación, suplencias con motivo, seguimiento y avisos; mensajes de error/reintento. App.tsx / candidateIntakeApi.ts / dynamicMethodPolicies y DynamicViewAccess: autenticación y permisos de vista `member` + defensa profunda en endpoint por usuario real / rol / Taller.
- QA demo sólo datos ficticios. `main`, `srv01` sin cambios.

## Pruebas/seguridad y límites operativos

- `CandidateInterviewAssignmentPolicyTests`: aceptado min 3, adicionales, no repeticiones, no acuerdo sin acta, fecha futura inválida.
- `CandidateInterviewAssignmentHttpTests` en shard b `.github/backend-integration-shards.json`: prueba PostgreSQL de privacidad/autorización contra rol Secretaría, inclusión exclusiva de Maestros de Taller, rechazo de Maestro externo y falta de identidad institucional.
- Histórico: hasta completarse migración y cuentas institucionales activas, ninguna notificación ni aprobación de entrevistas se simula. La notificación privada exige plantilla y NotificationDbContext migrados.
- Respaldo: CI/Showcase/QA deben estar en **SUCCESS para el HEAD final idéntico**; si no están no integrar. El primer CI intermedio detectó C# SQL raw literal y referencias; se corrigieron. Una ejecución previa al test HTTP aprobó unit tests pero fue sustituida por commits; no reclamar gate exact-head verde sin comprobación.
- Después de merge se requiere CI/Showcase/QA de `dev`; indexación de esta página en `START-HERE.md` mediante **PR documental nuevo** según GOV-003; registro final en Issue #427 y Línea Base Maestra Drive `1ncl0d--Bny8PzvtP38muPo5H2-JM5QUDGqH3lJ5ZtPM`.

## Criterio para cerrar

No cerrar #427 hasta tres gates exact-head, merge + comprobación postmerge, entrega de notificaciones privadas sin datos sensibles, supervisión de los tres informes y pruebas de roles/segregación; separar QA/UAT física suspendida Issue #97. Los resultados de gates se anotarán en comentario al issue y Drive y pueden cambiar con nuevos commits.
