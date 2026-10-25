# #694 — Fase Q: ledger PostgreSQL de cargas iniciales

Fecha 24-10-2026 (Chile). Agente ChatGPT. Base `dev@b4ce7e9319667ca954c14edf616f8086b721ad38` (PR #720 integrado). Esta fase crea únicamente la persistencia segura de huellas HMAC de lotes, sin nombres, RUT, emails, archivos CSV o información de personas.

Alcance reservado: `backend/src/PMGM.Api/Modules/Bootstrap/Entities/InitialLoadBatchLedger.cs`, `backend/src/PMGM.Api/Modules/Bootstrap/BootstrapDbContext.cs`, `backend/src/PMGM.Api/Migrations/20261025220000_AddInitialLoadBatchLedger.cs`, `backend/tests/PMGM.Api.Tests/Integration/InitialLoadBatchLedgerPostgreSqlTests.cs`, `.github/backend-integration-shards.json` y este handoff. Pruebas sintéticas con rollback. Exclusión: ningún endpoint nuevo, ninguna autorización jurídica automática, ningún importador operativo, ninguna modificación de main o glmdch.

El sistema asegura acceso, integridad y auditoría. La valoración de licitud, política institucional y visto bueno humano quedan fuera del software. Respetar 3/3 gates exact-head antes de integrar sólo a dev. Próxima fase: servicio transaccional idempotente de reserva/aplicación con JWT Máster, referenciar consentimiento institucional externo sin evaluarlo automáticamente; no activar datos reales sin autorización.
