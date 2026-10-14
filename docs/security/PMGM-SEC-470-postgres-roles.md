# PMGM-SEC-470 — Rol PostgreSQL limitado para Keycloak (fase BD1)

Estado: implementación de código en PR de desarrollo; **no es una migración ejecutada en servidor**. Fecha de diseño: 14-10-2026. Issue #470 (segregación de credenciales), #469 (Keycloak), #97 (srv01 físico pausado).

## Riesgo corregido para instalaciones nuevas

El piloto de referencia utilizaba `pmgm_app` tanto para la aplicación como para la base `pmgm_keycloak`; `pmgm_app` es el SUPERUSUARIO definido por `POSTGRES_USER`. Una credencial filtrada de Keycloak permitiría administrar cualquier base de PostgreSQL.

Esta fase crea un rol independiente `pmgm_keycloak` con LOGIN y sin superusuario/creación de bases/roles/replicación; la base `pmgm_keycloak` pertenece a ese rol y la instancia `keycloak` utiliza su contraseña `PMGM_PILOT_KC_DB_PASSWORD`, distinta de `PMGM_PILOT_DB_PASSWORD`. En la inicialización se revoca a PUBLIC el derecho CONNECT/TEMPORARY a ambas bases. En consecuencia, el usuario de Keycloak no debería poder conectarse a `pmgm`; esto se comprueba con la API de catálogos PostgreSQL en CI.

**Límite explícito:** `pmgm_app` sigue siendo superusuario por el diseño del primer arranque y porque la aplicación aplica migraciones EF Core. La separación entre usuario aplicación de solo DML, rol de migración y usuarios de reporte **aún no se implementó**. No afirmar cumplimiento pleno del hallazgo #470.

## Instalación NUEVA (volumen PostgreSQL vacío)

1. Crear un archivo de entorno privado desde `infrastructure/pilot.env.example`, añadir `PMGM_PILOT_KC_DB_PASSWORD` fuerte, aleatoria, de al menos 20 caracteres y distinta de otras contraseñas. Nunca adjuntar su valor a GitHub, tickets o Drive.
2. Ejecutar `scripts/preflight-pilot.sh`; comprueba existencia, longitud y no reutilización de secretos. Solo ejecutar `scripts/start-pilot.sh` tras autorización de piloto, con disponibilidad de MinIO y aceptación de infraestructura.
3. La imagen oficial PostgreSQL ejecuta `infrastructure/postgres/001-create-pilot-keycloak.sh` una sola vez al inicializar el directorio de datos. El script crea rol `pmgm_keycloak`, base `pmgm_keycloak` y revoca CONNECT/TEMPORARY a `PUBLIC`. Los usuarios de Keycloak se almacenan en la base propia; el script no contiene passwords.
4. Para confirmar en una instalación aislada: ejecutar `PMGM_PILOT_ENV_FILE=/ruta bash scripts/check-pilot-keycloak-db-role.sh`. Debe validar propietario, NO superuser y ausencia de CONNECT a `pmgm`. El smoke HTTPS comprueba OIDC, login/rutas y que la aplicación sigue funcionando.

## Volúmenes EXISTENTES: migración **NO automatizada**

Si el volumen PostgreSQL ya existe y fue creado con la versión antigua, el directorio `docker-entrypoint-initdb.d` **no vuelve a ejecutarse**: cambiar la variable `KC_DB_USERNAME` sin migrar bloqueará el login. Por eso:

- No actualizar un piloto persistido de manera automática desde esta PR. **No borrar el volumen**, recrear bases ni copiar datos sin respaldo.
- Primero ejecutar y verificar `scripts/backup-pilot.sh`, almacenar copia cifrada fuera de la VM y documentar versión/SHA del paquete. Confirmar acceso previo a Keycloak, congelar escrituras, planificar ventana y obtener autorización operativa separada.
- Un DBA debe preparar la migración del rol con transacciones/privilegios explícitos, verificar ownership de todos los objetos, secuencias, extensiones y migrations history, revocaciones CONNECT, copia de respaldo y capacidad de rollback. No existe en esta PR un script de conversión de un volumen ya inicializado; marcar este control como pendiente UAT/operador.
- Si se detecta rol ausente en entorno existente, **no** añadir permisos SUPERUSER a `pmgm_keycloak` ni temporalmente restablecer de forma permanente el usuario `pmgm_app` en Keycloak. Revertir la actualización del stack con procedimiento controlado y evidencia de restauración.

## Copias y recuperación

`scripts/backup-pilot.sh` mantiene respaldo administrativo con `pmgm_app` para ambas bases; no incorpora secreto a la copia. `scripts/restore-pilot.sh` sigue usando la cuenta administrativa para la restauración, pero añade `pg_restore --role=pmgm_keycloak --no-owner` **solo** a la base `pmgm_keycloak`, evitando restaurar objetos con propiedad del superusuario. El check de rol verifica propietarios de tablas públicas; resta validar restauraciones de casos institucionales autorizados. El simulacro CI se realiza solo con datos sintéticos.

## Gates automatizadas / evidencias

- `tests/test_pilot_keycloak_db_isolation.py`: regresión estructural del bootstrap, Compose, preflight, backup/restore y workflows.
- `scripts/check-pilot-keycloak-db-role.sh`: consulta SQL no destructiva contra PostgreSQL real para comprobar propiedad, NO superusuario, no CONNECT a `pmgm`, y propiedad de tablas Keycloak.
- CI ejecuta comprobación antes del smoke y después del simulacro de recuperación; fallo de estos gates bloquea integración. Triple CI/Showcase/QA Installable del SHA del PR es obligatorio para merge a dev.
- Ninguna prueba sintética autoriza desplegar en srv01, usar datos reales, fusionar dev a main ni declarar QA/UAT institucional.

**Siguiente fase:** aislar rol API runtime no privilegiado del usuario de migraciones (y roles reportes), con estrategia de separación de EF Core startup/migrator, GRANT DML y pruebas de denegación de DDL/UPDATE de auditoría. Dejar Issue #470 abierto hasta completar esos controles.
