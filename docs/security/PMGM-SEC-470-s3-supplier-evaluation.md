# PMGM-SEC-470 — Suministro seguro S3: decisión pendiente (15-10-2026)

**Estado: investigación. PR #487 integrado en `dev`; PR #490 propone permitir una única nueva versión corregida de Silo SOLO EN PREFILTRO de laboratorio. No implantar, no fusionar PR #479 ni desbloquear QA física o producción.**

## 1. Motivo y evidencia

- Repositorio MinIO Community `minio/minio`: declara **no mantenido**. La rama #479 presenta fallos de descarga por Quay/Docker Hub y su versión `RELEASE.2025-09-07T16-13-09Z` es anterior a reparaciones de seguridad. **No usar imagen obsoleta ni mirror sin procedencia.** Fuente: https://github.com/minio/minio
- **PGSTY Silo** compatible con API S3: versión `RELEASE.2026-09-16T00-00-00Z` **PROHIBIDA** por SN-2026-015/016. **Nueva publicación del 15-10-2026:** [release oficial](https://github.com/pgsty/silo/releases/tag/RELEASE.2026-10-15T00-00-00Z) y [notas de aceptación del fabricante](https://silo.pigsty.io/blog/release/silo-20261015/) confirman que **RELEASE.2026-10-15T00-00-00Z** incluye correcciones Object Lock de los PR #234 y #235; fuente `7a3ca0f82100cce06289a465f54139da2639f642`, imagen Docker clásica multi-plataforma por índice OCI **`docker.io/pgsty/silo@sha256:aaac6bce13eb99316b3c88bd20143fc3c38ab607585256a411a095c44ee57f62`**. El fabricante afirma pruebas de imagen 196/196 y provenance OCI. **Solo se habilita evaluación preliminar de laboratorio**: este proyecto todavía NO ha ejecutado pull, contrastado el índice OCI con el registro, auditado los permisos IAM ni probado datos/backup. El componente mcli compatible puede seguir en fecha 2026-09-16 y por sí solo no corrige un servidor vulnerable. Confirmar licencia AGPLv3/condiciones institucionales y demás avisos vigentes.
- **MinIO AIStor Free**, producto de MinIO bajo licencia propietaria, tiene documentación oficial de contenedor `quay.io/minio/aistor/minio`, pero exige **archivo de licencia** emitido por fabricante; la modalidad Free permite un recurso de cómputo, sin soporte. No disponemos de licencia validada en este proyecto ni de prueba de Pull/IAM con ese servidor. Fuente: https://docs.min.io/aistor/installation/container/install/ . Cualquier evaluación exige revisar si puede ser usado por esta Corporación sin fines de lucro según términos vigentes.
- Otras soluciones compatibles con S3 requieren integración/configuración IAM distinta a los comandos `mc admin ...`; no asumir equivalencia con la política de #479. No se evalúan ni autorizan en esta entrega.

## 2. Prefiltro automático propuesto

`scripts/check-pilot-s3-supplier.py` es **independiente**, sin redes, sin Docker ni lectura del contenido de licencias. Produce JSON:
- Proveedor `minio-community`, Silo anterior al 15-10-2026, Silo sin el **digest exacto admitido** y terceros no evaluados: `BLOCKED`, código 1.
- Proveedor `silo` con imagen estrictamente igual a `docker.io/pgsty/silo@sha256:aaac6bce13eb99316b3c88bd20143fc3c38ab607585256a411a095c44ee57f62` **y** `--ack-lab ACEPTO_SOLO_LAB_SIN_DESPLIEGUE`: `LAB_PRECHECK_ONLY`, código 0. No necesita licencia AIStor, pero **sí** evaluación de licencia AGPLv3, prueba de procedencia OCI, nuevas CVE y política IAM real antes de seleccionar proveedor.
- Proveedor `aistor`: solo devuelve `LAB_PRECHECK_ONLY`, código 0, con imagen exacta `quay.io/minio/aistor/minio@sha256:<digest hex de 64>`, ruta de licencia **no vacía y fuera del repositorio**, y `--ack-lab ACEPTO_SOLO_LAB_SIN_DESPLIEGUE`. **NO comprueba** vigencia, integridad ni autenticidad de licencia, no verifica existencia del digest remoto, y nunca certifica seguridad.

La única utilidad de `LAB_PRECHECK_ONLY` es permitir preparar una prueba **exclusivamente en entorno efímero sintético**. No es una aprobación de producción, de imagen, de versión o de despliegue. No integrar este resultado en el arranque de `srv01` ni usar credenciales de la Obediencia. El digest publicado por un proveedor NO equivale a validar su autenticidad ni accesibilidad de forma independiente. La licencia jamás se versiona.

## 3. Etapas mínimas para desbloquear PR #479

1. Confirmar con Sponsor/administración **modelo de licenciamiento** y proveedor elegido; si es AIStor Free, obtener licencia oficial por canal institucional y custodiarla fuera de GitHub/Drive público; si es Silo, revisar las condiciones de AGPLv3/operación institucional. Ni una ni otra opción está seleccionada.
2. Probar `docker manifest inspect`/pull en CI con versión y digest fijados, confirmar procedencia, SBOM, firmas/provenance y avisos de seguridad de la versión **en el momento de la prueba**, no usar `:latest` ni etiquetas mutables.
3. Ejecutar **servidor real** en contenedores efímeros con claves ficticias, sin puertos al exterior, y medir IAM: root solo en provisioning; servicio API tiene Get/Put/DeleteObject en `pmgm-documents/*`, ListBucket solo bucket documental; HEAD 404 para ausente; denegación de otro bucket, listar todos y `mc admin`.
4. Validar HTTPS/OIDC, escaneo de adjuntos, backup/restore con objetos ficticios, aislamiento frente a PostgreSQL BD2, cifrado, borrado/retención y rollback; documentar resultados exactos y límites.
5. Con permisos y validación, actualizar PR #479 desde `dev` vivo por protocolo multi-IA y superar triple gate exact-HEAD. **Solo entonces** evaluar merge a `dev`; QA física, srv01 y main siguen separados.

## 4. Gobierno y límites

PR #490 actualiza únicamente `scripts/check-pilot-s3-supplier.py`, `tests/test_s3_supplier_gate.py` y esta matriz, que ya estaban en `dev` tras #487. Sin cambios en `.github/workflows/ci.yml`, `infrastructure/docker-compose.pilot.yml`, `scripts/preflight-pilot.sh`, política S3 ni `scripts/ci-minio-policy.sh` reservados por #479.

**Fuentes de verdad:** GitHub Issue #470, PR #479, PR #487, este informe, START-HERE y Línea Base Maestra Drive. La revisión diferencial de Drive del 15-10 no reveló nuevas instrucciones sobre el proveedor S3. main y srv01 intactos.

## 5. Registro de cambio fechado 15-10-2026 — levantamiento condicionado de bloqueo Silo

Las referencias anteriores de 14-10 siguen siendo históricamente correctas **para la versión 2026-09-16**; esta adenda corrige su alcance temporal, no declara seguro el sistema. Se rechazará cualquier digest distinto de la imagen OCI de lanzamiento del servidor corregido. Estado `LAB_PRECHECK_ONLY` **no** valida pull Docker, firmas/attestation, licencia, integridad efectiva de objetos, autenticación, políticas de bucket, retención ni despliegue. Validar la release en un laboratorio S3 efímero sin credenciales institucionales antes de reabrir el PR #479. Fuentes primarias: https://silo.pigsty.io/blog/release/silo-20261015/ y https://github.com/pgsty/silo/releases/tag/RELEASE.2026-10-15T00-00-00Z .
