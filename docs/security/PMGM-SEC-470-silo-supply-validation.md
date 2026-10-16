# Silo QA aprobado — suministro, runtime y bloqueo de vulnerabilidades

16-10-2026 · #97 / #479 / #505 / PR #506. **No proponer despliegue mientras este bloqueo siga abierto.** Producción y main no forman parte del alcance.

El Sponsor aprobó Silo autogestionado exclusivamente para QA glmdch con datos sintéticos: #97 comentario 6207122522 y #479 comentario 6207124434. Se continúa el candidato existente #506, sin duplicar la rama ni editar los archivos piloto/ci.yml reservados por #479. Se preservaron los commits concurrentes hasta 9dc8e94 mediante lease; no se forzó ninguna referencia.

## Evidencia independiente obtenida

Primera ejecución completa del workflow **QA Silo supply and recovery**, run [38844711290](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/actions/runs/38844711290), job 116792536701, source HEAD `b03e295cb77111f8c1adc5e9833952f461eb1f87`. Evidencia pública sin secretos: artifact `12022902930`, SHA-256 del ZIP envolvente `10cfb5f0bd93466309d84ee8728ef759d3621353334cda7ddbfeaa85c62f6c6f`. Contiene runtime, informe Trivy íntegro, identidad del suministro, LICENSE y prueba de attestation del publicador. Resultados finales de HEAD posteriores se registran en PR/#97/Drive, sin reusar este gate como si validara código posterior.

| Control | Resultado medido |
| --- | --- |
| Runtime exacto | Docker Engine 29.9.1 instalado desde APT oficial firmado y Compose 5.6.0 cuyo binario se contrastó por SHA-256 de release oficial |
| Índice OCI | Bytes SHA-256 `aaac6bce13eb99316b3c88bd20143fc3c38ab607585256a411a095c44ee57f62` |
| Manifest linux/amd64 | Bytes SHA-256 `1c210bc1004fdd2396eafd7ebcefe19f2db04dbffae8e761e8d38d3fd9be3548` |
| Config plataforma | Bytes SHA-256 `77c299e4c52af38002abe3c7a16c3659398cdc77331bf5d8641d95d0964cd13a`; os=linux, architecture=amd64 |
| Provenance OCI | SLSA v1 ligada al manifest hijo y commit fuente `7a3ca0f82100cce06289a465f54139da2639f642`; blob `ada625673cd023f1fffcd0fffd225ba5561b5325523be3d312d4783d10115e66` |
| Firma/autoría | `gh attestation verify oci://docker.io/pgsty/silo@sha256:... --repo pgsty/silo` SUCCESS; se conserva JSON de verificación. No se confunde provenance sin firma con autoría autenticada |
| Licencia fuente | LICENSE del commit fuente, GNU AGPL v3, hash `0d96a4ff68ad6d4b6f1f30f713b18d5184912ba8dd389f86aa7710db079abcb0`. Identidad técnica comprobada; no aprobación jurídica de producción |
| Provisioning real | Un test real con Compose original, sin puertos S3 publicados, cuentas sintéticas separadas y políticas reales: PASS |
| API IAM | PUT/HEAD/GET y SHA, HEAD ausente 404; bucket ajeno/admin denegados y sin listado ajeno: PASS |
| Backup IAM | Cuenta distinta sin administración ni DeleteObject: negativa DELETE 403 PASS |
| Recuperación real de datos QA | Backup por scripts reales de pmgm + pmgm_keycloak + objetos, bloqueo de bucket no vacío antes de SQL, restore `--data-only` con filas/SHA exactos: PASS. Test 1/1 en 26.973 s |

No se accedió a glmdch. Todos los contenedores/volúmenes de esa prueba pertenecían al proyecto UUID del runner y se eliminaron al terminar. Los laboratorios existentes de SDK/HTTP/ClamAV/IAM siguen en PMGM CI. No se sustituyó S3 por un mock para obtener esos resultados.

## Resultado CVE: bloquea este digest

Trivy **0.76.0**, binario contrastado con SHA-256 `e24b1c51293c1b15e4ba8d45bbc7891ba053f90278ec2698f3562c993e145fe7`, base descargada en caché efímera nueva, escaneo remoto de la imagen fijada. Resultado inicial: **11 HIGH, 0 CRITICAL, 50 MEDIUM, 14 LOW y 3 UNKNOWN**. Son hallazgos paquete/CVE; hay IDs repetidos entre paquetes, no once CVE distintas.

| Grupo | Versión detectada | Corrección indicada / investigación |
| --- | --- | --- |
| pcre2 y pcre2-syntax | 10.40-6.el9 | Trivy indica 10.40-7.el9_8 para CVE-2026-103111, CVE-2026-86145 y CVE-2026-89161 (seis hallazgos por dos paquetes) |
| golang.org/x/net | v0.59.0 | v0.60.0, CVE-2026-78669 |
| Go stdlib | v1.27.1 | 1.26.9 / 1.27.2 para CVE-2026-78667, CVE-2026-78669 y CVE-2026-97031 |
| módulo histórico github.com/minio/minio | pseudo-version ligada a 7a3ca0f82100 | CVE-2026-39414 sin FixedVersion en Trivy; contrastar fork/commit/reachability antes de clasificar |

[Red Hat CVE-2026-103111](https://access.redhat.com/security/cve/cve-2026-103111) confirma problema PCRE2 JIT; no se declara inalcanzable sin analizar el binario y las rutas de entrada. [GO-2026-6607](https://pkg.go.dev/vuln/GO-2026-6607) y [GO-2026-6609](https://pkg.go.dev/vuln/GO-2026-6609) son referencias primarias para Go. La [matriz oficial Silo](https://silo.pgsty.com/compatibility/versions/) declara Server 20261015 con Go 1.27.2/x-net v0.60.0, pero mcli mantiene release 20260916. El inventario por fichero/binary debe separar servidor, cliente y paquetes base: una versión segura del servidor no actualiza automáticamente el cliente incorporado.

El [ledger oficial del fork](https://silo.pgsty.com/about/security-advisories/) declara arreglos de CVE-2026-39414 y SN-2026-015/016. Esa discrepancia exige verificar commit y binario o VEX firmado/revisado, **no** añadir una excepción genérica ni asumir falso positivo por ser un fork. No se modificó el umbral HIGH/CRITICAL, no hay `.trivyignore` y no se omiten vulnerabilidades sin fix.

### Remediación necesaria antes de una propuesta de despliegue

1. Obtener imagen clásica mantenida/reconstruida por el proveedor que corrija PCRE2 y **todos** sus binarios Go/mcli, o preparar una construcción reproducible desde fuente revisada con esas correcciones y su propia firma/SBOM. No sustituir por tag latest, mirror desconocido ni binario sin procedencia.
2. Si se evalúa variante distroless oficial, comprobar su digest/plataforma/firma/SBOM y resolver provisioning/backup con un cliente igualmente corregido. Distroless no trae shell/mcli: no sirve cambiar sólo el image de los tres servicios.
3. Resolver la coincidencia CVE-2026-39414 con evidencia de código/binario/VEX del fork. Esta revisión no exonera las dependencias PCRE2/Go; cualquier excepción requeriría evidencia específica, no aceptación global por QA.
4. Repetir escaneo y pruebas reales sobre el nuevo digest; actualizar el pin y todos los tests/prefiltros que lo fijan, generar otro instalable del mismo SHA final y conservar nuevas evidencias. No transformar la aprobación Silo QA en aprobación de este artefacto vulnerable.

**Estado técnico:** adaptación funcional comprobada en CI; validación de seguridad FALLIDA; despliegue NO PROPUESTO. No se fusiona esta PR con el gate de seguridad rojo ni se promueve main. La aprobación de QA continúa vigente, condicionada a superar el bloqueo de artefacto.

## Cambios de la continuación

- Se eliminaron fallbacks de credenciales en Compose. QA installable usa fixtures separadas y exige que el guard acepte el modelo sintético; verifica provisioning, políticas y suministro dentro del ZIP.
- Política de backup propia sin borrado; provisioning rechaza placeholders, identidades cortas y reutilización de contraseñas/usuario. API no recibe claves admin o backup.
- Restore conserva el fallo cerrado de ListBucket y añade `--data-only` con resultado explícito de smoke pendiente; proyectos CI se aíslan mediante PMGM_QA_COMPOSE_PROJECT. Los permisos del entorno son obligatoriamente 600/640 y backup usa umask 077.
- Workflow dedicado de suministro/recuperación no toca ci.yml ni el perfil piloto. Evidencia y escaneo se conservan incluso cuando el gate falla.

La recuperación acreditada es **lógica para datos actuales** en QA sintético. Custodia cifrada fuera de host, versiones/retención/Object Lock, KMS/secretos, RPO/RTO, OIDC/roles BD2 y condiciones de red/recursos de glmdch siguen siendo controles separados; no se certifican por este test ni se habilita un despliegue para probarlos a ciegas.
