# Continuidad #427 — Aceptación expresa del Maestro (10-10-2026)

- Issue: https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/issues/427
- PR draft: https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/pull/432
- Rama: `feature/issue427-acceptance-20261010-gpt` (desde `dev@ca49ef0ee4781f323821259f7aca751cdc7ecf73`).
- `main@6dfb9546a4873baff15955cf86abfd7d47e3d111` preservada. `srv01`/UAT física siguen pausados por #97.
- El alcance corresponde a un **subincremento** del issue #427 ya aprobado por el Sponsor; no representa su cierre total.

## Cambio propuesto en PR #432 (no integrado todavía)

1. Cada Maestro activo formalmente designado debe aceptar personalmente la tarea antes de cargar el PDF/DOCX o entregar el informe.
2. La aceptación es idempotente y transaccional, restringida a la identidad institucional vinculada al mismo Maestro/Taller, grado tercero; devuelve prohibido a identidades ajenas.
3. El timestamp `AcceptedAtUtc` se almacena en `core.candidate_interview_assignments` mediante migración aditiva `20261010214500_AddCandidateInterviewAcceptance` y queda auditado como `candidate.interview.designation.accepted`.
4. Endpoints: POST `/api/insinuados/solicitudes/{requestId}/entrevistadores-designados/{assignmentId}/aceptar`; GET propios / GET para Secretaría-Venerable incluyen `acceptedAtUtc`; subida y entrega documental fallan hasta que acepte.
5. UI existente `CandidateInterviewAssignmentsPage`: botón **Aceptar designación** para cada tarea, seguimiento `Aceptación pendiente` / `Aceptada` / `Informe entregado`. Demo API con datos ficticios y prueba de aceptación antes de entrega.
6. Pruebas: política unitaria de estados y caso HTTP negativo sin identidad vinculada; la prueba positiva HTTP íntegra de tres Maestros aún está pendiente en #427.

## Archivos cambiados en esta PR

- `backend/src/PMGM.Api/Modules/CandidateIntake/Entities/CandidateInterviewAssignment.cs`
- `backend/src/PMGM.Api/Modules/CandidateIntake/CandidateInterviewAssignmentPolicy.cs`
- `backend/src/PMGM.Api/Modules/CandidateIntake/CandidateInterviewAssignmentEndpoints.cs`
- `backend/src/PMGM.Api/Modules/CandidateIntake/CandidateWorkflowEndpoints.cs`
- `backend/src/PMGM.Api/Migrations/20261010214500_AddCandidateInterviewAcceptance.cs`
- `backend/tests/PMGM.Api.Tests/CandidateIntake/CandidateInterviewAssignmentPolicyTests.cs`
- `backend/tests/PMGM.Api.Tests/Integration/CandidateInterviewAssignmentHttpTests.cs`
- `frontend/src/api/candidateIntakeApi.ts`
- `frontend/src/api/candidateIntakeApi.test.ts`
- `frontend/src/CandidateInterviewAssignmentsPage.tsx`
- Este handoff.

## Gates y continuidad

Los checks deben verificarse en GitHub **contra HEAD exacto del PR** antes de fusionar. Las ejecuciones en SHAs previos se cancelan automáticamente por concurrency; no confundir un QA Installable en verde en una revisión vieja con validación del HEAD final. La demo Pages se publica después de merge a dev y el instalable QA NO significa despliegue.

Al integrar, indexar este handoff en START-HERE mediante PR documental separada, registrar el SHA de dev, los gates, la versión Pages y la evidencia QA en Issue #427 y Línea Base Maestra de Drive. No promover main sin aprobación nueva.

**Pendientes del Issue #427 que esta PR no resuelve:** reprogramación individual diferenciada de entrevistas sin reemplazar maestros, prueba HTTP positiva real designación → 3 avisos → 3 aceptaciones → 3 documentos → paquete, y aceptación visual móvil/funcional. Mantener issue abierto.
