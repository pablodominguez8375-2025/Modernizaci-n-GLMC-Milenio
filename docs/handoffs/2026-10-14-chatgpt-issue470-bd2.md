# Handoff #470-BD2 — aislamiento PostgreSQL del piloto (14-10-2026)

**Estado: PR #484 DRAFT — no fusionar ni desplegar.** Issue #470. Rama `feature/issue470-postgres-runtime-migrator-20261014-gpt`. Dev base verificada `84adbfe14f1b6eda15f98e2747a122844e8cf510`; main `6dfb9546a4873baff15955cf86abfd7d47e3d111` sin modificar. Coordinación multi-IA conforme GOV-003 §16: evitar PR #479 MinIO, Compose base, preflight base, workflow compartido y UI Claude.

## Entrega de código (todavía no operacional)

- `backend/src/PMGM.Api/Program.cs`: bandera `Database__RunMigrationsAndExit=true`, reutiliza el pipeline existente de nueve DbContext y finaliza antes de iniciar endpoints, workers o datos demo; rechaza la configuración sin migraciones.
- `infrastructure/postgres/002-create-pilot-db2.sh`: solo initdb de **volumen PostgreSQL NUEVO**; roles `pmgm_migrator`, `pmgm_runtime`, `pmgm_reports`, todos `NOSUPERUSER/NOCREATEDB/NOCREATEROLE/NOREPLICATION`, con claves separadas. Migrador propietario de base `pmgm` y esquemas, Keycloak permanece en su BD aislada.
- `infrastructure/docker-compose.pilot-db2.yml`: **overlay opt-in** con proyecto Compose separado `pmgm-pilot-db2` para evitar que el script de arranque legacy reutilice accidentalmente un volumen nuevo BD2. Servicio `migrator` one-shot, `db-grants` posterior y API con rol runtime DML y migraciones de arranque deshabilitadas.
- `scripts/grant-pilot-db2.sh`: derechos DML mínimos sobre tablas/secuencias por esquema; ninguna capacidad de CREATE/ALTER/DROP/TRUNCATE/TRIGGER/REFERENCES para runtime. Propietario de `core.audit_events` permanece migrator: API solo SELECT/INSERT, no UPDATE/DELETE/TRUNCATE. Rol `pmgm_reports` tiene CONNECT y USAGE al esquema `reporting` pero **NINGUNA tabla/vista autorizada aún**: autorizar explícitamente SELECT en vistas individualmente revisadas y sin datos personales cuando corresponda.
- `scripts/preflight-pilot-db2.sh`, `scripts/start-pilot-db2.sh` y `scripts/check-pilot-db2-roles.sh`: claves independientes >=20 caracteres, reconocimiento explícito `PMGM_DB2_NEW_VOLUME_ACK=CONFIRMO_VOLUMEN_NUEVO`, rechazo de volumen existente, SQL de metadatos y pruebas negativas transaccionales de DDL, trigger audit, DELETE/TRUNCATE audit y lectura de auditoría por reportes.
- `tests/test_pilot_db2_isolation.py`: contrato estructural, `bash -n` de scripts y `docker compose config --format json` en runner Docker. Se incluye desde la suite existente `tests/test_pilot_keycloak_db_isolation.py` para pasar por el gate de infraestructura sin cambiar `.github/workflows/ci.yml` que comparte PR #479.

## Activación futura SOLO en laboratorio NUEVO (sin datos reales)

1. No desplegar mientras PR esté DRAFT o #479 siga sin resolver distribución segura de almacenamiento S3.
2. Crear entorno privado desde `infrastructure/pilot.env.example`, **fuera de Git**. Añadir `PMGM_PILOT_RUNTIME_DB_PASSWORD`, `PMGM_PILOT_MIGRATOR_DB_PASSWORD`, `PMGM_PILOT_REPORT_DB_PASSWORD` (secretos únicos >=20 caracteres, distintos también de clave admin y Keycloak).
3. Con autorización de ambiente NUEVO, sin volumen anterior, usar `PMGM_PILOT_ENV_FILE=/ruta/pmgm.env PMGM_DB2_NEW_VOLUME_ACK=CONFIRMO_VOLUMEN_NUEVO bash scripts/start-pilot-db2.sh`; el preflight sale antes de crear o tocar volúmenes previos si se detecta que existen.
4. Verificar `scripts/check-pilot-db2-roles.sh`, login OIDC/HTTPS, smoke real, migraciones de todos los contextos y prueba de backup/restore con el *mismo* overlay. Las rutinas actuales de respaldo/restore admiten `PMGM_CI_COMPOSE_OVERRIDE=infrastructure/docker-compose.pilot-db2.yml`, pero el simulacro BD2 completo **todavía no está validado**.
5. No usar `scripts/start-pilot.sh` sin overlay para un piloto BD2 ni migrar un entorno real existente en esta fase. El volumen anterior necesita evaluación de DBA, backup cifrado externo, ventana, traspaso de ownership y rollback explícitos aprobados por Sponsor.

## Gates y bloqueo de aceptación

- La existencia del código no demuestra que los grants y nueve migraciones EF funcionen bajo `pmgm_migrator` en PostgreSQL real; exigir CI, Showcase, QA Installable exact-HEAD y un smoke BD2 efímero autenticado antes de marcar PR Ready.
- Docker y .NET no están disponibles en la sesión local del agente; pruebas de sintaxis y Compose delegadas al runner mediante tests. Si falla un gate, mantener este PR **DRAFT** y registrar job/causa.
- Ninguna autorización para producción, `srv01` (#97), carga de datos reales, `main`, o cierre integral de #470. El issue sigue OPEN por MinIO IAM (#479), IP QA, operación BD2 y migración controlada de volúmenes persistidos.
- Demo Pages no equivale a backends desplegados: no afirmar SHA Pages sin manifest `downloads/qa-current.json` leído y validado.
- Ante bloqueo, próximo agente retoma de PR #484 e Issue #470, no reprogramar soluciones paralelas ni tocar archivos reservados por Claude/MinIO.

