# QA glmdch — perfil candidato Silo (NO DESPLEGAR todavía)

Fecha de trabajo: 15/16-10-2026. Issue #505, PR #506 (DRAFT). Fuente: dev desde #503 (controles de almacenamiento), Silo 20261015. El nombre `srv01` en Compose/scripts es histórico; servidor designado `glmdch`.

## Estado: implementación candidata en rama, sin QA real
Este perfil NO demuestra que el servidor esté operativo, no selecciona formalmente al proveedor ni acredita licencia, suministro, IAM efectivo, restauración integral o UAT. **No ejecutar INSTALAR.sh ni docker compose up**, incluso con `--skip-smoke`, hasta documentar las aceptaciones de más abajo. La existencia de ZIP instalable y un CI parcial/verde no son aprobación de instalación.

### Incrementos de la PR
- `infrastructure/docker-compose.srv01.yml`: imagen Silo `docker.io/pgsty/silo@sha256:aaac6bce13eb99316b3c88bd20143fc3c38ab607585256a411a095c44ee57f62`; tres servicios IAM/documentos/backup, S3 privado sin ports; web/Keycloak por defecto en 127.0.0.1.
- `infrastructure/silo/pmgm-qa-documents-policy.json` junto con `scripts/qa/qa-provision-silo.sh`: política documental limitada y creación de usuarios API y backup distintos; root sólo en server/init. Contrato equivalente a #479, consolidar duplicado de política en la integración definitiva.
- `infrastructure/srv01.env.example` y `scripts/preflight-srv01-qa.sh`: identidades/contraseñas obligatorias distintas, sin placeholders, bind loopback/RFC1918; variables nuevas con placeholders en Compose exclusivamente para que el CI histórico pueda renderizar sin secretos. El preflight real los rechaza.
- `scripts/backup-srv01-qa.sh`/`restore-srv01-qa.sh`: cliente de backup con identidad sin root; restore requiere bucket vacío y no permite `mirror --remove`. Esta copia lógica conserva sólo objetos actuales: **NO garantiza** metadata/versiones/Object Lock/IAM ni un respaldo institucional suficiente.
- `tests/test_qa_glmdch_silo_candidate.py`: pruebas sintéticas estáticas; no equivalen a instancia Silo/SSO reales.

### Puertas pendientes (orden obligatorio)
1. **Seguridad y licencia**: decisión institucional de proveedor (Silo comunitario AGPL-3.0-or-later u opción S3 administrada), responsable operación/actualizaciones, soporte, SBOM/provenance/arquitectura linux/amd64, digest OCI y avisos de seguridad vigentes. Sin selección formal no habilitar servidores.
2. **CI y calidad**: revisar PR #506 desde HEAD vivo dev, corregir tests/fixtures heredados de credenciales MinIO (en particular `tests/test_qa_storage_guards.py`), validar `docker compose config`, 5 tests nuevos, CI+Showcase+QA Installable SUCCESS exact-head. No fusionar esta PR mientras esté en DRAFT.
3. **Segregación BD/OIDC**: implementar y verificar en perfil QA de nueva instalación el patrón BD2 opt-in ya aplicado en el piloto (API sin DDL, migraciones separadas, Keycloak rol propio y auditoría append-only). Actualmente el perfil QA usa `pmgm_app` compartido: NO se considera seguridad operacional completa.
4. **S3 real**: validar provisioning IAM desde QA candidato con Silo real, dos cuentas no-root, negativas 403 de otros buckets/admin; HEAD ausente 404, PUT/GET/DELETE autorizados; evitar fuga de credenciales. Verificar que contenedores se reinician idempotentemente tras instalación.
5. **Backup/restauración**: diseñar y probar recuperación coherente de PostgreSQL (pmgm y Keycloak), objetos, metadatos, versiones, IAM/secretos, custodia cifrada externa, RPO/RTO. No interpretar `mcli mirror` como respaldo íntegro de retención/Object Lock.
6. **glmdch**: comprobar CPU, MemTotal, filesystem, servicios, puertos existentes, LXD, Docker Compose 5.6.0, conectividad, NAT/firewall y conservación de servicios. No usar UFW inactivo como certificación de aislamiento. Nada de usuarios, claves, SSH, Docker up ni reglas de firewall se ha hecho desde esta sesión.
7. **Despliegue posterior**: autorizado por Sponsor en corte SHA exacto; sólo datos sintéticos; reserva de recursos y rollback; preflight y manifests antes de pull/build/up; smoke SSO/backend/DB/S3/ClamAV real + pruebas QA/regresión y UAT documentada.
8. **Paridad**: verificar GitHub Pages `downloads/qa-current.json` y ZIP QA sobre el mismo `SOURCE_SHA` integrado antes de reportar versión desplegada. No tocar main.

### Comandos de inspección sin cambios para el operador (no ejecutados)
```bash
hostnamectl
docker --version
docker compose version
docker ps -a --format 'table {{.Names}}\t{{.Status}}\t{{.Ports}}'
df -h /opt
free -h
ss -lnt
```
No compartir en chat configuraciones `docker compose config` completas, passwords, claves SSH, secretos, tokens ni IP pública. Verificar `/opt` y servicios existentes antes de extraer ZIP.

### Preparación local del paquete, sólo tras revisión/selección
```bash
# Dentro del paquete ya autenticado/verificado y contra archivo .env privado:
sha256sum -c MANIFEST.sha256
docker compose -p pmgm-srv01 --env-file /etc/pmgm/srv01.env -f infrastructure/docker-compose.srv01.yml config --quiet
PMGM_QA_ENV_FILE=/etc/pmgm/srv01.env bash scripts/preflight-srv01-qa.sh
```
El preflight valida configuración, **no IAM/QA real**. No ejecutar instalación mientras las puertas anteriores sigan abiertas.

### Limitaciones conocidas
- `glmdch` tiene Docker y Compose instalados según reportes del operador, pero no se ha constatado por SSH en esta sesión.
- Se requieren aprobación expresa del proveedor, pruebas reales, revisión de respaldo y protección de datos. Esta rama no arranca contenedores.
- `main` y servicios existentes no deben modificarse. Issue #97 sigue abierto para montaje/QA/UAT.
