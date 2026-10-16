# QA glmdch — perfil candidato Silo (NO DESPLEGAR todavía)

Fecha de trabajo: 15/16-10-2026. Issue #505, PR #506 (DRAFT). Fuente: dev desde #503 (controles de almacenamiento), Silo 20261015. El nombre `srv01` en Compose/scripts es histórico; servidor designado `glmdch`.

## Dirección pública aprobada del QA — 16-10-2026

El Sponsor estableció **https://sistema.granlogiamixta.cl/** como URL pública única y canónica de QA glmdch para pruebas **solo con datos sintéticos**. Origen sin slash terminal para la configuración: `PMGM_QA_WEB_PUBLIC_URL=https://sistema.granlogiamixta.cl`. Fuente de verdad: PR documental [#513](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/pull/513) integrada en `dev@db82dd1c826ba1a8c1bbc610ea8536078fefe3e0`, Issue #97 y adenda QA Drive.

**Estado distinto de DNS o despliegue:** solo hay una decisión y plantilla; NO se ha verificado que el sitio esté publicado ni se han configurado DNS, certificados, firewall o proxies en `glmdch` desde esta sesión. El servicio QA histórico expone web en 8081 y Keycloak en 8180 **únicamente en loopback**. No publicar estos HTTP directamente ni cambiar `PMGM_QA_BIND_ADDRESS` a `0.0.0.0` para abrir el dominio.

**Dependencias técnicas de publicación segura:** reservar una entrada TLS 443 con reverse proxy autorizado (Caddy/Nginx) y verificar DNS A/AAAA/CNAME, certificado con cadena correcta y renovación, HTTPS/HSTS sin mezclas inseguras, frontend, API privada, Keycloak OIDC y issuer externo real. La plantilla deja `PMGM_QA_OIDC_PUBLIC_URL=REEMPLAZAR_URL_HTTPS_OIDC_VALIDADA` **a propósito**: el QA Compose actual NO sirve automáticamente Keycloak en `/identity`. No suponer ese path ni añadirlo a `KC_HOSTNAME` sin configurar y probar `KC_HTTP_RELATIVE_PATH`, routing del proxy y la URL del backend. Una vez montado el ingreso, ajustar en el realm los `redirectUris`, `post.logout.redirect.uris` y `webOrigins` para el dominio HTTPS QA, retirando las autorizaciones `localhost`/IP por HTTP en la configuración PUBLICADA. No modificar el realm del piloto o producción por arrastre.

**Puertas pendientes:** PR #506 continúa bloqueada por 11 HIGH en Trivy del candidato Silo, además de pruebas de IAM/backup coherente, OIDC/BD2 y preflight host; la selección del dominio no exime de esos gates. `main` no se modifica ni el QA se declara operativo por este registro.
## Estado: implementación candidata en rama, sin QA real
El Sponsor aprobó Silo exclusivamente para QA glmdch y datos sintéticos el 16-10-2026 (#97 comentario 6207122522 y #479 comentario 6207124434). Esta selección no acredita servidor operativo, suministro, licencia efectiva del artefacto, IAM, restauración integral o UAT. **No ejecutar INSTALAR.sh ni docker compose up**, incluso con `--skip-smoke`, hasta documentar las aceptaciones de más abajo. La existencia de ZIP instalable y un CI parcial/verde no son aprobación de instalación.

### Incrementos de la PR
- `infrastructure/docker-compose.srv01.yml`: imagen Silo `docker.io/pgsty/silo@sha256:aaac6bce13eb99316b3c88bd20143fc3c38ab607585256a411a095c44ee57f62`; tres servicios IAM/documentos/backup, S3 privado sin ports; web/Keycloak por defecto en 127.0.0.1.
- `infrastructure/silo/pmgm-qa-documents-policy.json` junto con `scripts/qa/qa-provision-silo.sh`: política documental limitada y creación de usuarios API y backup distintos; root sólo en server/init. Contrato equivalente a #479, consolidar duplicado de política en la integración definitiva.
- `infrastructure/srv01.env.example` y `scripts/preflight-srv01-qa.sh`: identidades/contraseñas obligatorias distintas, sin placeholders, bind loopback/RFC1918; variables obligatorias sin valores fallback. El CI del instalable usa identidades sintéticas separadas y el guard de almacenamiento; nunca necesita credenciales de servidor.
- `scripts/backup-srv01-qa.sh`/`restore-srv01-qa.sh`: cliente de backup con identidad sin root; restore requiere bucket vacío y no permite `mirror --remove`. Esta copia lógica conserva sólo objetos actuales: **NO garantiza** metadata/versiones/Object Lock/IAM ni un respaldo institucional suficiente.
- `tests/test_qa_glmdch_silo_candidate.py`: pruebas sintéticas estáticas; no equivalen a instancia Silo/SSO reales.

### Puertas pendientes (orden obligatorio)
1. **Seguridad y licencia**: selección Silo QA registrada. Validar licencia AGPL-3.0-or-later del commit/artefacto, responsable operación/actualizaciones, soporte, SBOM/provenance/firma de publicador, arquitectura linux/amd64, digest OCI y avisos/CVE actuales. La aprobación QA no permite omitir estos controles ni elegir proveedor de producción.
2. **CI y calidad**: revisar PR #506 desde HEAD vivo dev, validar fixtures actualizadas, `docker compose config`, tests de candidato y controles, CI+Showcase+QA Installable y QA Silo supply and recovery SUCCESS exact-head. El workflow dedicado instala Docker 29.9.1 y Compose 5.6.0 únicamente en su runner efímero. No fusionar esta PR mientras esté en DRAFT.
3. **Segregación BD/OIDC**: implementar y verificar en perfil QA de nueva instalación el patrón BD2 opt-in ya aplicado en el piloto (API sin DDL, migraciones separadas, Keycloak rol propio y auditoría append-only). Actualmente el perfil QA usa `pmgm_app` compartido: NO se considera seguridad operacional completa.
4. **S3 real**: validar provisioning IAM desde QA candidato con Silo real, dos cuentas no-root (backup sin DeleteObject), negativas 403 de otros buckets/admin; HEAD ausente 404, PUT/GET/DELETE autorizados; evitar fuga de credenciales. Verificar que contenedores se reinician idempotentemente tras instalación.
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
- Se requieren los resultados de suministro/seguridad, pruebas reales, revisión de respaldo y protección de datos; la selección Silo ya está aprobada sólo para QA. Esta rama no arranca contenedores.
- `main` y servicios existentes no deben modificarse. Issue #97 sigue abierto para montaje/QA/UAT.


## Validación reproducible de esta adaptación

- `.github/workflows/qa-silo-validation.yml`: runner Ubuntu 24.04 efímero, Docker 29.9.1 desde APT oficial firmado, Compose 5.6.0 con SHA-256 contrastado contra release oficial; sin credenciales/Environment glmdch.
- `scripts/qa/verify-silo-supply.py`: descarga índice OCI por digest, verifica bytes de índice/manifest linux-amd64/config, vincula predicados OCI al subject; obtiene LICENSE del commit fuente, intenta verificar attestation GitHub con identidad pgsty/silo y escanea con Trivy 0.76.0 fijado por checksum. Firma ausente, verificación incompleta o HIGH/CRITICAL bloquean la validación; no hay allowlist de CVE ni sustitución por otro proveedor. El inventario CVE se intenta incluso si falta otra prueba de suministro.
- Evidencia `supply-report.json`, `trivy-image.json` y licencia/attestation cuando estén disponibles: artefacto `qa-silo-validation-<HEAD>` por 14 días. Un predicado OCI ligado por digest no equivale por sí solo a firma del publicador.
- `tests/test_qa_silo_runtime.py`: reutiliza SigV4 existente y levanta exclusivamente PostgreSQL/Silo/provisioning en proyecto UUID aislado del runner, sin puertos S3 publicados; prueba políticas API/backup, backup real de ambas BD y objetos, rechazo de bucket no vacío antes de SQL y restauración exacta SHA/filas. No ejecuta este test contra glmdch. Limpia sólo su proyecto UUID.
- `--data-only` de restore realiza recuperación destructiva de datos pero no reabre la aplicación ni ejecuta smoke: deja estado explícito `smoke PENDIENTE`. Se usa en CI para la prueba de datos; no constituye aceptación de OIDC/QA integral. La ruta normal conserva smoke obligatorio.
- `PMGM_QA_COMPOSE_PROJECT` permite aislar el proyecto de laboratorio; por defecto los scripts conservan pmgm-srv01. No reutilizar proyectos/volúmenes institucionales en pruebas.
- La política de backup tiene ListBucket/GetObject/PutObject exclusivos del bucket documental, sin DeleteObject ni administración. El archivo de respaldo se crea con umask 077. Secretos de las tres identidades nunca se imprimen.

## Alcance de recuperación que sigue sin certificarse

La prueba nueva acredita copias actuales coherentes de PostgreSQL/Keycloak/objetos en un laboratorio sin escritores concurrentes. No acredita backup cifrado fuera del host, recuperación de versiones/retención/hold, KMS/secretos, política de destrucción, RPO/RTO institucionales o alta disponibilidad. No activar versionado/Object Lock sin definir cómo exportar/restaurar sus metadatos. No usar mirror como sustituto silencioso de esas garantías. Antes de un despliegue, acordar estos controles y repetir aceptación real de OIDC, roles BD2, proxies y recursos del host según #97.
