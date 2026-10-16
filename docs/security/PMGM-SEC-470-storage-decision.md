# #470/#479 — Decisión de almacenamiento y controles QA

15-10-2026 · PR #503 · propuesta para revisión, **sin selección institucional ni despliegue**.

## Trabajo existente y reservas

Base viva `dev@77557f87d2979b6ef23f636c7c99bec329f70618`, main `6dfb9546a4873baff15955cf86abfd7d47e3d111`. PR #479 DRAFT, HEAD `89990ff66dcb20dcbd8fa8e30d9f02717bee55f8`, reserva Compose/preflight/env piloto, IAM MinIO y ci.yml. #503 no los modifica ni copia una segunda implementación de provisioning. #500 agenda y #501 inspector SSH no se solapan con estos scripts. PR #1 es promoción bloqueada por UAT.

Reutilizar lo integrado: `check-pilot-s3-supplier.py` (prefiltro sólo laboratorio), `tests/test_silo_real_iam.py` (contrato IAM), recuperación lógica, SDK .NET, HTTP/PostgreSQL/ClamAV y corrección de streams no-seekable de #497. La descripción histórica de #479 que no reconoce IAM Silo real no sustituye esas evidencias posteriores. No reabrir el fallo de stream como si siguiera sin arreglo.

## Propuesta de proveedor

**Candidato recomendado para evaluar/adoptar condicionalmente: PGSTY Silo Server 20261015**, porque conserva protocolo S3 y ya tiene evidencia real de compatibilidad con el producto. Es un fork comunitario con mantenimiento propio, no una imagen MinIO antigua ni un mock. La selección definitiva pertenece al Sponsor/administración tras revisar las condiciones siguientes.

| Identidad | Valor verificable |
| --- | --- |
| Release | `RELEASE.2026-10-15T00-00-00Z` |
| Fuente declarada | `7a3ca0f82100cce06289a465f54139da2639f642` |
| Imagen índice OCI | `docker.io/pgsty/silo@sha256:aaac6bce13eb99316b3c88bd20143fc3c38ab607585256a411a095c44ee57f62` |
| Suministro probado por el proyecto | CI #38826083411, Infrastructure job #116733754388 SUCCESS, 47 tests en 180.649 s sobre dev exacto; tests no omiten el pull real ni las negativas IAM |
| Seguridad declarada | Release incluye reparación SN-2026-015/016 Object Lock; 20260916 sigue prohibida |

Fuentes primarias consultadas: [release y commit](https://github.com/pgsty/silo/releases/tag/RELEASE.2026-10-15T00-00-00Z), [mantenimiento y ausencia de SLA garantizado](https://silo.pgsty.com/about/security/), [licencia](https://silo.pgsty.com/about/license/). Evidencia local de servidor real: [IAM](PMGM-SEC-470-silo-iam-lab.md), [recuperación lógica](PMGM-SEC-470-silo-object-recovery.md), [HTTP/SDK/ClamAV](PMGM-SEC-470-silo-http-workflow.md). No se ejecutó aquí otro servidor ni se sustituyó S3 para repetir pruebas ya existentes.

**Límite del suministro:** el pull de CI prueba disponibilidad de ese digest al corte del run. No es un análisis independiente de firmas/SBOM/avisos ni una garantía de disponibilidad futura del registro. La consulta directa al endpoint Docker Hub no pudo completarse en esta sesión. Antes de adopción: contrastar índice y manifest hijo `linux/amd64`, firma/provenance contra identidad esperada del publicador, SBOM y vulnerabilidades actuales; conservar evidencia/fecha y copia autorizada por digest. Verificar también cliente de provisioning: Silo integra `mcli`, mientras los scripts históricos invocan `mc`; no suponer que cambiar sólo la imagen hará funcionar `minio-init`. No fijar un digest de cliente inventado: determinarlo con suministro real o reutilizar el `mcli` incluido en la imagen validada tras comprobar su entrypoint.

## Costos y decisión pendiente

| Opción | Costos concretos y requisitos |
| --- | --- |
| Silo autogestionado | No requiere la licencia propietaria de AIStor; software publicado bajo AGPL-3.0-or-later. Revisión institucional de obligaciones/avisos/fuentes y modificaciones; no se afirma aquí compatibilidad jurídica automática. Reutiliza glmdch, pero consume disco, RAM, CPU y horas de administración; agregar respaldo cifrado externo, monitorización, actualizaciones y simulacros. Sin SLA comunitario garantizado: asignar responsable o contratar soporte por cotización. La VM única no aporta alta disponibilidad. |
| Amazon S3 administrado | Alternativa mantenida por AWS, sin imagen de servidor ni digest que desplegar. Cuenta/facturación institucional, región/residencia de datos, IAM, endpoint/TLS y presupuesto; pruebas reales del producto con ese servicio antes de elegirlo. Factura por almacenamiento, operaciones, egreso, réplicas/backup, KMS si aplica y soporte. Ninguna cuenta/bucket/clave se creó. |

Para dimensionar la decisión: volumen medio **V GB**, versiones/retención **Rv GB**, backup independiente **B GB**, PUT/LIST **P**, GET/HEAD **G**, egreso **E GB**, RPO/RTO y soporte. AWS: `(V+Rv)×tarifa_regional + P×tarifa_operaciones + G×tarifa_operaciones + E×egreso + backup/KMS/soporte/impuestos`. [Precios oficiales](https://aws.amazon.com/s3/pricing/). Como referencia ilustrativa de AWS US East, su [ejemplo publicado](https://aws.amazon.com/blogs/storage/run-spark-31-faster-and-optimize-compute-costs-with-amazon-s3-express-one-zone-on-amazon-emr/) usa US$0,023/GB-mes S3 Standard: 100 GB representan US$2,30/mes **sólo almacenamiento primario**, no presupuesto para Chile ni costo total. Confirmar región y tarifas actuales antes de contratar. Silo no genera esa factura S3 primaria, pero su costo total no es cero: `VM/disco + backup/egreso + soporte + horas de operación`. No hay cotización de esos insumos ni inventario de volumen de datos para inventar un total mensual.

**Decisión a registrar:** Silo con responsable de operación y aceptación de licencia/soporte/backup, o S3 administrado con región y límite de gasto. Hace falta volumen/retención, RPO/RTO y dueño del servicio para cerrar el costo. AIStor es una ruta ya evaluada en el prefiltro: requeriría licencia oficial y nueva evidencia; no se propone descargar su imagen sin esos requisitos.

## Credencial documental: contrato compartido

Cuenta API exclusiva, distinta de administración/provisioning y de recuperación. Secretos sólo en archivo privado/gestor autorizado, nunca Git/Drive/artefacto/log. Reutilizar la política de #479 y el contrato ya probado en `tests/test_silo_real_iam.py`:

- `s3:ListBucket` exclusivamente `arn:aws:s3:::pmgm-documents` (permite HEAD ausente 404).
- `s3:GetObject`, `s3:PutObject`, `s3:DeleteObject` exclusivamente `arn:aws:s3:::pmgm-documents/*`.
- Sin `admin:*`, `s3:*`, otros buckets, gestión IAM, política/retención o bypass de governance. Anónimo denegado. No conceder permisos para versiones/multipart/retención por anticipado: si se necesitan, diseñar política separada y nuevas negativas.

Raíz sólo en provisioning privado temporal; API no debe recibir ninguno de sus componentes. Comprobar permisos efectivos además del JSON. Un listado global exitoso pero filtrado no basta para declarar fuga: exigir que el bucket ajeno jamás aparezca, y 403 a sus operaciones. Para S3 administrado no crear claves del root de cuenta: usar identidad IAM dedicada y credencial compatible con el SDK actual; sesiones temporales/workload identity requerirían revisar `S3DocumentObjectStore` antes de prometer su soporte. El nombre de bucket AWS es global: si pmgm-documents no está disponible, acordar un nombre documental único y parametrizar el guard/contrato antes de adopción; no abrir acceso a todos los buckets.

## Cambios independientes implementados en #503

1. `scripts/check-qa-storage-config.py`: captura privadamente JSON de `docker compose config`, valida bucket documental/endpoint/credenciales y rechaza credenciales administrativas en API, reutilización de usuario **o** contraseña root, imágenes de almacenamiento sin digest y upstream MinIO histórico aunque tenga digest. HTTPS obligatorio para endpoint externo; HTTP sólo cuando el hostname corresponde a un servicio privado del modelo. Salida limitada a códigos fijos, sin stderr crudo ni secretos. Resultado `CONFIG_CHECK_ONLY` **no** prueba permisos IAM, procedencia, licencia ni autorización de despliegue.
2. Preflight QA invoca el guard; instalador existente ya llama ese preflight **antes** de pull/build/up. El Compose histórico sigue presente como material de referencia, pero queda intencionalmente BLOQUEADO por latest/root. No se agregó un bypass ni se eligió proveedor por defecto. No se alteran rutas funcionales de la aplicación/UI.
3. Restore QA aplica el mismo guard antes de stop/up/SQL. Verificación de respaldo mantiene formato v1 y exige árbol exacto, sin enlaces simbólicos (incluidos directorios intermedios/manifiesto), rutas no canónicas/traversal, hashes/tamaños alterados o archivos extra ocultos con nombre manifest.json. Esta integridad local no certifica respaldo íntegro de S3 ni sustituye simulacro real.
4. Tests sintéticos de controles ejecutados mediante import en la suite Infra existente, sin cambiar ci.yml reservado. Las pruebas de CLI utilizan un ejecutable de registro de comandos para demostrar ausencia de mutaciones; no es un proveedor de almacenamiento ni se cuenta como IAM S3 real.

## Cambios que debe llevar la adaptación del proveedor

| Área | Cambio concreto después de la decisión |
| --- | --- |
| Compose QA y piloto | Eliminar latest; servidor/cliente por digest verificado o retirar ambos servicios si S3 es externo. API recibe `PMGM_QA_S3_ACCESS_KEY/SECRET_KEY` propios; endpoint/region/bucket/path-style explícitos. Quitar dependencia minio-init si externa, o readiness/provisioning compatible real si Silo. No exponer 9000/9001 públicamente. Conservar ClamAV real y `VITE_USE_MOCKS=false`. |
| Entorno/preflight | Validar secreto documental distinto de administración/backup y demás claves; archivo privado, sin root fallback. Reutilizar prefiltro de proveedor, y además registrar aceptación de suministro/licencia; no transformar LAB_PRECHECK_ONLY en autorización. Asegurar rutas de CA/credenciales y no imprimir config expandido. |
| Instalación | Adaptar listas pull y readiness al proveedor. No usar launcher histórico ni `--skip-smoke` como validación inocua: este último levanta stack. Replicar gates del perfil elegido y revisar BD2/proxies según QA real. |
| Recuperación | Diseñar copia coherente PostgreSQL pmgm + pmgm_keycloak + objetos/metadata/versiones + IAM/configuración + claves de cifrado custodiadas aparte; backup cifrado externo y manifest SHA fechado del corte. Cuenta backup/restore separada de API, privilegios justificados. Rehidratar a destino nuevo aislado, regrants BD2 si aplica, IAM negativo, SHA de objetos, OIDC/antivirus y auditoría. |
| Retención/rollback | Dueños de datos definen RPO/RTO, retención/inmutabilidad/borrado. Probar legal hold/Object Lock y versiones si se requieren; mc mirror actual sólo demuestra copia lógica actual y no esas garantías. No aplicar `mirror --remove` sobre bucket con retención o datos existentes como atajo. Rollback de release no revierte migraciones ni recupera objetos por sí solo. |

No tocar reservas #479 hasta actualizar su alcance desde dev vivo por el dueño ChatGPT, con esas decisiones registradas. No crear otra API, almacenamiento en memoria ni segunda política de provisioning paralela.

## Pruebas y nuevo instalable del mismo SHA

- Local: `bash -n` de scripts afectados y `python3 -m unittest discover -s tests -p test_qa_storage_guards.py -v`.
- PR #503 genera PMGM CI/Showcase/QA Installable sobre su HEAD exacto. El paquete archiva esos mismos scripts/tests/docs y BUILD-INFO/MANIFEST; registrar run/artifact/digest exactos en PR/#97/Drive, sin confundir hash del ZIP envolvente Actions con ZIP interior/Pages.
- El nuevo instalable de **esta fase independiente** permanece NO DESPLEGABLE por el guard mientras conserve configuración histórica. Un artefacto verde no resuelve selección de proveedor ni acredita QA.
- Tras adaptar #479: repetir IAM real, SDK HTTP/Silo o proveedor elegido, ClamAV/EICAR/fail-closed, OIDC real, backup/restore/retención y límites memoria/concurrencia; generar otro instalable del mismo SHA de esa implementación final. Prohibido omitir negativas por fallos de proveedor o usar S3Mock como aceptación.
- Integración a dev sólo con revisión/gates/autorización vigentes. Sin main, servidor, datos reales ni UAT en esta entrega; #470/#479/#97 permanecen abiertos.
