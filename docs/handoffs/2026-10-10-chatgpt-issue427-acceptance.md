# Continuidad #427 — Aceptación personal de entrevistas (10-10-2026)

Issue: https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/issues/427
Rama: `feature/issue427-acceptance-20261010-gpt`
Base: `dev@ca49ef0ee4781f323821259f7aca751cdc7ecf73`
Estado: PR draft — implementación por verificar.

## Alcance reclamado por ChatGPT
Añadir aceptación expresa por el Maestro oficialmente asignado sin alterar el acuerdo colegiado, conservando pertenencia activa y segregación por Taller. Exigir aceptación antes de subir/entregar informe; reflejar aceptación en «Mis entrevistas» y seguimiento del Venerable con demo sintética. Registrar auditoría, pruebas de autorización y migración aditiva. No cerrar #427 hasta recorrido HTTP positivo de tres maestros y revisión móvil; no tocar main ni srv01.

Archivos previstos: `backend/src/PMGM.Api/Modules/CandidateIntake/{Entities/CandidateInterviewAssignment.cs,CandidateInterviewAssignmentPolicy.cs,CandidateInterviewAssignmentEndpoints.cs,CandidateWorkflowEndpoints.cs}`, `backend/src/PMGM.Api/Data/PmgmDbContext.cs`, nueva migración `backend/src/PMGM.Api/Migrations/*`, `backend/tests/PMGM.Api.Tests/{CandidateIntake/*,Integration/CandidateInterviewAssignmentHttpTests.cs}`, `frontend/src/api/candidateIntakeApi.ts`, `frontend/src/CandidateInterviewAssignmentsPage.tsx`, este handoff.

No interfiere con archivos de identidad, styles compartidos, workflows de Claude o `main`.
