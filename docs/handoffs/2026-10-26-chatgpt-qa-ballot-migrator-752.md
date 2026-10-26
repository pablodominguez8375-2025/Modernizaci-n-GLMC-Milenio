# #752 — Migraciones de votaciones de Tenidas (entrega en curso)

Fecha 2026-10-26. Inicio desde dev@301a576c6d75187aeeb71901d9c276bf7b4e334c. PR draft reservado según PMGM-GOV-003. Evidencia: cuatro HTTP 500 en GET /api/gestion-logial/tenidas/{meetingId}/votaciones en CSV de Soporte QA agregado, sin copiar información original ni identificadores. Se investiga omisión de LodgeManagementDbContext en migrador oficial BD2. No tocar BD física, despliegue, main ni la Ficha existente de Libertad Nº 23. Registro final se agregará al cerrar corte.
