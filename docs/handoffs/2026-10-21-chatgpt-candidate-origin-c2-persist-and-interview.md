# #613 C2 — Origen persistido y exclusión del recomendante

Base dev@8dd95641c960d8197bfd74a0e783142d9de13eca, 21-10-2026. Agente ChatGPT. Alcance C2: campos opcionales de origen en CeremonyRequest, migración aditiva core.ceremony_requests; validación de recomendante activo en servidor al dar de alta por Secretaría Taller; exclusión al listar y asignar maestros entrevistadores. Campos NULL históricos = sin registrar. Preservar formularios antiguos hasta adaptar UI en Claude; registrar brecha de obligatoriedad de origen para nuevas solicitudes sin campos por compatibilidad.

No se toca main, QA físico, ni se autoriza voto. C3 posterior: edición de origen con motivo/auditoría y vistas de expediente/PDF; perfiles y protocolos de acceso se preservan. Gates exact-head obligatorios.

## Código entregado a esta rama
- `CeremonyEntities.cs`, `PmgmDbContext.cs`, nueva migración aditiva de `core.ceremony_requests` con FK e invariantes SQL.
- `CandidateWorkshopIntakeEndpoints.cs`: alta atómica con evidencia de membresía activa en algún Taller de la Orden, campos de origen opcionales para clientes actuales. Sin retorno de nombres del recomendante en la bandeja.
- `CandidateIntakeEndpoints.cs`: lectura de origen solo en la ficha privada del expediente.
- `CandidateInterviewAssignmentEndpoints.cs`: filtro de maestros elegibles y rechazo POST por incompatibilidad del hermano recomendante.
- `backend/tests/PMGM.Api.Tests/Integration/CandidateOriginPersistenceHttpTests.cs`: pruebas de PostgreSQL/HTTP de alta válida/inválida y 409 al asignar recomendante.
- Diccionario GOV-004: `docs/modelo-datos/registros/2026-10-21-chatgpt-candidate-origin-persistence-613-c2.md`.
- Reserva: pendiente CI exact-head; corrección con razón y auditoría, PDF y UI real posteriores. No desplegado.
