# Handoff #470-BD2 — separación PostgreSQL del piloto (14-10-2026)

Estado: PR DRAFT en desarrollo. Base dev: 84adbfe14f1b6eda15f98e2747a122844e8cf510. main sin cambios: 6dfb9546a4873baff15955cf86abfd7d47e3d111.

Objetivo: aislar runtime API (DML, no DDL), migrador EF Core one-shot y cuenta de informes SELECT solo para vistas expresamente autorizadas. Aplicación solo en volúmenes piloto NUEVOS mediante overlay opt-in; ningún cambio al compose piloto base, workflow compartido reservado en PR #479, ni servidor srv01.

Reserva de archivos: backend/src/PMGM.Api/Program.cs; infrastructure/docker-compose.pilot-db2.yml (nuevo); infrastructure/postgres/002-create-pilot-db2.sh (nuevo); scripts/grant-pilot-db2.sh, scripts/check-pilot-db2-roles.sh, scripts/preflight-pilot-db2.sh, scripts/start-pilot-db2.sh (nuevos); tests/test_pilot_db2_isolation.py (nuevo), docs/handoffs/2026-10-14-chatgpt-issue470-bd2.md (este archivo).

Condición de seguridad: ninguna credencial real en el repositorio; no modificar volúmenes persistidos; no publicar IAM MinIO ni asumir CI/UAT exitosas antes de evidencia exact-head.
