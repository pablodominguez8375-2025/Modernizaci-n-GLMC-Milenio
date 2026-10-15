# PMGM-SEC-470 — Suministro seguro S3: decisión pendiente (15-10-2026)

**Estado: investigación / PR #487 DRAFT. No implantar, no fusionar PR #479, no desbloquear QA física ni producción.**

## 1. Motivo y evidencia

- Repositorio MinIO Community `minio/minio`: declara **no mantenido**. La rama #479 presenta fallos de descarga por Quay/Docker Hub y su versión `RELEASE.2025-09-07T16-13-09Z` es anterior a reparaciones de seguridad. **No usar imagen obsoleta ni mirror sin procedencia.** Fuente: https://github.com/minio/minio
- **PGSTY Silo**, fork comunitario compatible con API S3/IAM de MinIO, distribuye versión `RELEASE.2026-09-16T00-00-00Z` con manifiesto OCI multi-arquitectura y publicación de SBOM/provenance (digest publicado: `sha256:635197cb9f36d01bee221d34d1c7d7960f6a95c48b0b6c01d99cd13bdae51a46`). **BLOQUEADO**: aviso SN-2026-015, *PutObjectRetention Authorization Bypass*, marcado a 14-10 como parche candidato sin release corregida; cualquier versión publicada hasta el 16-09 es afectada según proyecto. Fuentes: https://github.com/pgsty/silo/releases y https://silo.pgsty.com/blog/security/ . No inferir que hay parche hasta publicación y pruebas independientes.
- **MinIO AIStor Free**, producto de MinIO bajo licencia propietaria, tiene documentación oficial de contenedor `quay.io/minio/aistor/minio`, pero exige **archivo de licencia** emitido por fabricante; la modalidad Free permite un recurso de cómputo, sin soporte. No disponemos de licencia validada en este proyecto ni de prueba de Pull/IAM con ese servidor. Fuente: https://docs.min.io/aistor/installation/container/install/ . Cualquier evaluación exige revisar si puede ser usado por esta Corporación sin fines de lucro según términos vigentes.
- Otras soluciones compatibles con S3 requieren integración/configuración IAM distinta a los comandos `mc admin ...`; no asumir equivalencia con la política de #479. No se evalúan ni autorizan en esta entrega.

## 2. Prefiltro automático propuesto

`scripts/check-pilot-s3-supplier.py` es **independiente**, sin redes, sin Docker ni lectura del contenido de licencias. Produce JSON:
- Proveedor `minio-community`, `silo` y terceros no evaluados: `BLOCKED`, código 1.
- Proveedor `aistor`: solo devuelve `LAB_PRECHECK_ONLY`, código 0, con imagen exacta `quay.io/minio/aistor/minio@sha256:<digest hex de 64>`, ruta de licencia **no vacía y fuera del repositorio**, y `--ack-lab ACEPTO_SOLO_LAB_SIN_DESPLIEGUE`. **NO comprueba** vigencia, integridad ni autenticidad de licencia, no verifica existencia del digest remoto, y nunca certifica seguridad.

La única utilidad de `LAB_PRECHECK_ONLY` es permitir preparar una prueba **exclusivamente en entorno efímero sintético**. No es una aprobación de producción, de imagen, de versión o de despliegue. No integrar este resultado en el arranque de `srv01` ni usar credenciales de la Obediencia. La licencia jamás se versiona.

## 3. Etapas mínimas para desbloquear PR #479

1. Confirmar con Sponsor/administración **modelo de licenciamiento** y proveedor elegido; si es AIStor Free, obtener licencia oficial por canal institucional y custodiarla fuera de GitHub/Drive público.
2. Probar `docker manifest inspect`/pull en CI con versión y digest fijados, confirmar procedencia, SBOM, firmas/provenance y avisos de seguridad de la versión **en el momento de la prueba**, no usar `:latest` ni etiquetas mutables.
3. Ejecutar **servidor real** en contenedores efímeros con claves ficticias, sin puertos al exterior, y medir IAM: root solo en provisioning; servicio API tiene Get/Put/DeleteObject en `pmgm-documents/*`, ListBucket solo bucket documental; HEAD 404 para ausente; denegación de otro bucket, listar todos y `mc admin`.
4. Validar HTTPS/OIDC, escaneo de adjuntos, backup/restore con objetos ficticios, aislamiento frente a PostgreSQL BD2, cifrado, borrado/retención y rollback; documentar resultados exactos y límites.
5. Con permisos y validación, actualizar PR #479 desde `dev` vivo por protocolo multi-IA y superar triple gate exact-HEAD. **Solo entonces** evaluar merge a `dev`; QA física, srv01 y main siguen separados.

## 4. Gobierno y límites

Archivos nuevos de #487 más import test CI en `tests/test_pilot_keycloak_db_isolation.py`; sin cambios en `.github/workflows/ci.yml`, `infrastructure/docker-compose.pilot.yml`, `scripts/preflight-pilot.sh`, política S3 ni `scripts/ci-minio-policy.sh` reservados por #479.

**Fuentes de verdad:** GitHub Issue #470, PR #479, PR #487, este informe, START-HERE y Línea Base Maestra Drive. La revisión diferencial de Drive del 15-10 no reveló nuevas instrucciones sobre el proveedor S3. main y srv01 intactos.
