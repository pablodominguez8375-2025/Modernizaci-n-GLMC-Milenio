# QA G4 — PostgreSQL segregado en glmdch (PR #520, Issue #470)

**Alcance:** exclusivamente pruebas con datos sintéticos en `glmdch`. El dominio público previsto es `https://sistema.granlogiamixta.cl/`. **Despliegue QA pendiente.** No se cambia el piloto #479 ni `main`.

## Contrato

El perfil BD2 de QA (`PMGM_QA_DB2=true`) aplica un overlay sobre el Compose institucional. El administrador `pmgm_app` se reserva al bootstrap, backups y concesión de privilegios. Keycloak se conecta con `pmgm_keycloak`, las migraciones EF Core se ejecutan una sola vez en servicio `migrator` con `pmgm_migrator`, la API utiliza `pmgm_runtime` sin propiedad DDL y `pmgm_reports` no recibe acceso directo a tablas. La auditoría permite SELECT/INSERT al runtime, jamás UPDATE/DELETE/TRUNCATE/TRIGGER. Historial EF `__EFMigrationsHistory` vedado al runtime. Los permisos se conceden mediante el script ya probado `scripts/grant-pilot-db2.sh` reutilizado sin cambios en su contrato SQL.

`scripts/qa/prepare-silo-archive.py` incorpora el overlay cuando se declara BD2 antes de generar el Compose local firmado y `pull_policy=never` de Silo. `PMGM_QA_HTTPS=true` requiere BD2 en el preflight/instalador; no se permite que el perfil HTTP legado quede expuesto al dominio público.

## Primer arranque (TODAVÍA NO AUTORIZADO)

1. Debe existir el candidato completo firmado del mismo SHA, sin excepciones de suministro Silo.
2. Crear `/etc/pmgm/srv01.env` fuera de Git, permiso 600/640, con `PMGM_QA_DB2=true` y `PMGM_QA_DB2_NEW_VOLUME_ACK=CONFIRMO_VOLUMEN_NUEVO`, además de cinco credenciales de PostgreSQL distintas y de las demás, cada una de 20 caracteres o más. Nunca divulgar contraseñas.
3. `05-create-qa-db2.sh` se ejecuta únicamente al inicializar el volumen PostgreSQL **vacío**, antes del `10-init-srv01.sql` histórico. Si el volumen ya existe, el guard cancela todo: NO borrarlo, clonar datos, reusar el superusuario ni hacer la conversión silenciosa.
4. El instalador exige Silo firmado, valida volúmenes nuevos, inicia PostgreSQL, migrador restringido, concesión de permisos, API y Keycloak. `check-qa-db2-roles.sh` comprueba privilegios reales y denegaciones antes del smoke.
5. Las restauraciones BD2 usan propiedad explícita por base, reaplican grants y verifican permisos. Cuando la entrada es pública, `--data-only` sigue siendo obligatorio: no se reabre automáticamente un realm histórico.

## Pruebas y límites

- `python3 -m unittest discover -s tests -p test_qa_db2_contract.py`: estructura, sintaxis shell y Compose (cuando Docker exista).
- En un volumen QA desechable, después de iniciar y después de restaurar, ejecutar `PMGM_QA_ENV_FILE=/ruta/privada bash scripts/check-qa-db2-roles.sh`. No realiza alteraciones persistentes.
- Gate real por ejecutar en runner/host autorizado con PostgreSQL 17, Keycloak 26, backend .NET y Silo firmado; no confundir checks estructurales con G4 aceptado.
- Falta comprobar en `glmdch`: red/firewall, DNS/TLS, OIDC de extremo a extremo, cuentas sintéticas ligadas a fichas, custodia de backups, recuperación física, smoke, regresión y UAT.
- No usar `srv01` ni `main`, no cargar datos reales y no levantar QA con secretos en Git/Drive.
