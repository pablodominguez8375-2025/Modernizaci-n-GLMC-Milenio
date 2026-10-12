# Handoff en curso — Issue #37 / validación de captura por versión — 11-10-2026

Estado: EN DESARROLLO. No interpretar como entrega integrada ni operacional.

- Base verificada: dev@bbdb278541091cca1ff99c2507ed04fca6ee853a; main@6dfb9546a4873baff15955cf86abfd7d47e3d111.
- Rama: `feature/issue37-submission-validation-20261011-gpt`.
- Issue: #37 (asignado a agente:chatgpt). Sin PR funcional paralelo al reservar.
- Alcance reservado: `backend/src/PMGM.Api/Modules/FormConfiguration/FormSubmissionPolicy.cs`, `backend/tests/PMGM.Api.Tests/Unit/FormSubmissionPolicyTests.cs`, este handoff.
- Objetivo: validar de forma pura/fail-closed valores recibidos frente a esquema confiable y versión exacta; tipos, opciones, obligatoriedad y campos no admitidos, sin confiar en metadatos aportados por cliente.
- Excluidos deliberadamente: EF/migraciones, API pública, RBAC, auditoría persistente, editor UI, Pages y despliegue físico srv01; estas fases de #37 siguen abiertas.
- Antes de merge: comprobar CI exact-head y mantener PR como draft mientras no esté verificado.
- Gobierno: no tocar main ni srv01 (#97); no permitir cargar datos personales reales en demo.
