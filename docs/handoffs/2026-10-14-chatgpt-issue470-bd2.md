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


## Extensión de pruebas — 14-10-2026, noche de Chile

Después del primer corte se añadió al `tests/test_pilot_db2_isolation.py` una prueba de **PostgreSQL 17 real y efímero en GitHub Actions**, sin puertos abiertos ni volúmenes persistidos: inicializa ambos scripts `001`/ `002`, autentica migrador/runtime/reportes con claves CI sintéticas, crea una tabla de auditoría exclusivamente de prueba desde el rol migrador, ejecuta `grant-pilot-db2.sh` y verifica INSERT/SELECT runtime, denegación de DDL, DISABLE TRIGGER, UPDATE/DELETE/TRUNCATE, SELECT a reportes, y privilegios predeterminados para tablas posteriores. El contenedor y la red se eliminan siempre.

La prueba verifica **roles/grants reales**, pero crea una tabla sintética: todavía **no valida las nueve migraciones EF Core completas sobre el nuevo perfil BD2**, el smoke HTTP de runtime ni restore de un volumen BD2. Permanecer DRAFT hasta cubrir esas brechas y el bloqueador MinIO #479. No se modificó `.github/workflows/ci.yml`, que sigue reservado por PR #479: la prueba se descubre desde la suite de infraestructura existente. El nuevo HEAD tras esta extensión es `29d52f98fbce2f53479e976861ff8880ef3942e9`; los gates de este HEAD deben verificarse, no reutilizar resultados del SHA anterior. 


## Continuación 15-10-2026 00:45 Chile — validación EF real y restauración BD2

En PR #484 se verificó la triple compuerta sobre HEAD previo `9eb67d7a6ca9ee078fa9ff28933469db93661a16`: PMGM CI #38646916679, Showcase #38646916642 y QA Installable #38646916492 **SUCCESS**. Después se amplió la implementación; las validaciones previas **no acreditan el HEAD nuevo**.

**Nueva prueba operacional sintética:** `tests/test_pilot_db2_real_migrations.py` integra prueba PostgreSQL 17 con volumen efímero, construye la misma imagen real de API .NET 10, ejecuta las nueve migraciones EF Core bajo `pmgm_migrator`, aplica grants, levanta API bajo `pmgm_runtime`, exige GET `/health/ready`, comprueba negación de DDL y mutación de auditoría, y aisla reportes sin acceso a tablas. Está incluida desde la suite Keycloak infraestructura existente sin modificar workflow caliente de PR #479. En la primera ejecución de esta prueba el job `Infrastructure configuration` pasó SUCCESS; esto NO valida MinIO ni sesión OIDC de usuario.

**Endurecimiento restore:** `scripts/restore-pilot.sh` ahora identifica el overlay exacto BD2 y exige `PMGM_PILOT_DB2_RESTORE_APPROVED=CONFIRMO_RESTORE_BD2`; restaura `pmgm` con `--role=pmgm_migrator` (Keycloak mantiene `--role=pmgm_keycloak`), reaplica `db-grants` y valida `check-pilot-db2-roles.sh` antes de operar MinIO y reabrir API. Sin bandera + overlay BD2 se deniega antes de una operación destructiva, mientras el flujo legacy sin overlay se conserva.

**Prueba de recuperación en CI sintética:** ampliación `test_pilot_db2_real_migrations.py` incluye `pg_dump` del esquema EF creado realmente, `pg_restore` con rol migrador y `--no-owner --no-privileges`, nueva aplicación de grants, chequeo de propietario/auditoría/historial migraciones, y comprobación de recuperación API `/health/ready`. El test estructural `tests/test_pilot_db2_isolation.py` revisa el consentimiento BD2 y el orden de re-grants previo a MinIO. Esta extensión requiere gates NUEVOS exact-HEAD; nunca declarar SUCCESS antes de verificar.

**Límites pendientes:** bloqueo MinIO imagen segura PR #479; por lo tanto no hay prueba real de IAM S3, backup/restore documental completo, ni UAT QA física srv01 #97. No modificar main, no migrar volúmenes existentes. La separación BD2 solo está en PR draft #484; el Issue #470 sigue abierto por IAM S3, IP real en infraestructura y aceptación operacional. Continuar desde último HEAD vivo de rama y evidencia GitHub Actions, no desde el SHA antiguo de este documento.
