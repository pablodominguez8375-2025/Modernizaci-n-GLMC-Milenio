# PMGM-SEC-470 — Flujo HTTP documental con PostgreSQL, Silo y ClamAV reales

**15-10-2026 · PR #497 DRAFT · alcance: laboratorio CI 100% sintético, sin despliegue institucional.**

## Contexto y objetivo

Los PR #491, #493, #494 y #495 integrados en `dev` comprobaron IAM S3 real, recuperación lógica, el cliente AWS .NET y ClamAV sobre contenido Silo. Los tests de endpoints HTTP existentes usan un almacenamiento en memoria y un scanner de prueba, así que **no prueban esa integración total**. Esta entrega ejecuta la clase productiva `S3DocumentObjectStore`, el scanner productivo `ClamAvDocumentMalwareScanner`, el controlador HTTP documental y las escrituras PostgreSQL y auditoría juntas.

El test emplea `DocumentManagementWebApplicationFactory`, que sustituye JwtBearer por una identidad administrativa **sintética de CI**. Se prueban endpoints HTTP reales de la API, **NO un login Keycloak/OIDC real**, autorización por talleres ni sesiones productivas. No convertir este resultado en afirmación de UAT.

## Ejecución real y evidencia

`tests/test_silo_http_document_workflow.py` es un test de la suite Infra ya existente. Prepara una red Docker efímera sin puertos publicados y sin volúmenes institucionales. Descarga imágenes de laboratorio: Silo corregida 2026-10-15 fijada por digest OCI `docker.io/pgsty/silo@sha256:aaac6bce13eb99316b3c88bd20143fc3c38ab607585256a411a095c44ee57f62`; ClamAV `clamav/clamav:1.4`, PostgreSQL `postgres:17-alpine` con tmpfs de datos y SDK `mcr.microsoft.com/dotnet/sdk:10.0`. Los tags de ClamAV, PostgreSQL y SDK son mutables y deben fijarse por digest en una revisión de supply chain antes de una adopción operativa. No están autorizados para desplegarse por este test.

Los secrets de Silo y PostgreSQL se generan aleatoriamente y se pasan sólo a los contenedores efímeros; Silo recibe una cuenta documental no-root y bucket `pmgm-documents` con IAM mínimo, usando política de las pruebas #491. El runner .NET ejecuta exclusivamente `backend/tests/PMGM.Api.Tests/Integration/SiloDocumentHttpLifecycleRealProbe.cs`. El archivo termina `Probe.cs` para respetar el sharding CI que exige listar explícitamente cada `*Tests.cs`; el test real es opt-in por `PMGM_TEST_REQUIRE_REAL_HTTP_DOCS=1`. El runner **exige TRX con exactamente 1 prueba Passed**, evitando éxito con filtro vacío.

### Aceptación funcional

1. Iniciar DB sintética PostgreSQL y ejecutar migración de la aplicación. Crear colección, documento y versión desde rutas HTTP con identidad autenticada de CI; no usar información personal.
2. Subir texto limpio a Silo mediante `PUT /api/documentos/versiones/{id}/contenido`; obtener estado `uploaded`, verificar en PostgreSQL, denegar sobrescritura (409) y descarga prematura (409).
3. Analizar desde `POST .../analizar`: el servicio ClamAV real responde limpio, cambia a `available`; publicar y descargar vía endpoints de administración y Biblioteca Virtual. Comparar bytes SHA-256 completos.
4. Crear versión con firma estándar **EICAR inocua**: el scanner real debe marcar `rejected`; publicar y descargar esta versión **no puede** resultar exitoso (409).
5. Crear tercera versión sintética y simular scanner inaccesible por el puerto 1 de loopback **dentro del contenedor del test**, manteniendo Silo y DB reales. El endpoint debe devolver **503** y persistir `scanning`, impedir descarga/publicación y registrar `documents.content.scan_unavailable` (fail-closed).
6. Comprobar en PostgreSQL las tres versiones, SHA-256 y `ScanReference` clean/rejected/null, y los eventos `documents.content.uploaded`, `documents.content.scan_clean`, `documents.content.scan_rejected`, `documents.content.scan_unavailable`, `documents.content.downloaded` y `library.content.downloaded`. La auditoría no debe contener los bytes ni la firma EICAR.
7. Limpiar de manera incondicional los contenedores/red y directorio temporal (incluida la BD), sin abrir puertos, copiar datos reales ni operar servidores institucionales. Docker, migraciones, IAM o C# test fallidos significan **CI roja**.

## Incidencia real CI y mitigación acotada (15-10-2026)

Primer ciclo exact-HEAD `546ad4d6f47bcccae49210e42384c59ab4b168b3`: PMGM CI **#38810551701 FAILED**; todos los jobs de backend, frontend, smoke autenticado y HTTPS aprobaron, pero `Infrastructure configuration #116684536276` falló con **2 de 47 tests**. El nuevo test de HTTP NO llegó a HTTP: falló al ejecutar `mcli admin user add` durante el aprovisionamiento sintético. También falló la prueba YA EXISTENTE de recuperación Silo en `mcli alias set`. Los mensajes estaban deliberadamente redactados por `run()`; no hay evidencia suficiente para afirmar la causa del rechazo de `mcli`, pero sí una vulnerabilidad a fallos transitorios de disponibilidad administrativa luego del readiness.

Para conservar la seguridad y aclarar el diagnóstico, se actualiza únicamente la función compartida de **laboratorio** `_provision` en `tests/test_silo_real_recovery.py`, aplicada a todos los tests que ya la importaban: seis intentos máximo separados por dos segundos por paso, verificación previa `mcli admin info` después de configurar alias, error FAIL-CLOSED con nombre de paso fijo si no se estabiliza. **No se ignoran fallos** de políticas, usuarios o permisos; las afirmaciones IAM negativas y positivas posteriores siguen obligatorias. No se imprimen argumentos/passwords ni stdout/stderr del comando. Ningún código productivo se modificó. Este cambio añade un quinto archivo al PR, pues el mismo helper falló en una prueba previa ya integrada.

La rama requiere **nuevos** resultados de CI, Showcase y QA sobre el SHA del cambio de estabilización, nunca reutilizar checks del primer intento fallido.

**Resultado sólo acreditado** cuando Infrastructure CI indique SUCCESS, TRX exactamente 1 Passed y gates PMGM CI, Showcase y QA Installable completos SUCCESS sobre **el mismo HEAD del PR**. No modificar CI workflow reservado por #479 ni eliminar negativas por conveniencia.

## Límites institucionales y pendientes

No prueba login real OIDC/Keycloak, usuarios o roles por Taller, certificados HTTPS de QA, carga de más de 50 MB, metadatos históricos, capacidades Object Lock/retención, backups completos cifrados, licenciamiento AGPLv3, RPO/RTO ni autorización de proveedor. Los avisos SN-2026-015/016 del fabricante Silo siguen requiriendo validación independiente de seguridad.

Nueva autorización Sponsor registrada en Drive: **glmdch reemplaza a srv01 como QA designado**, pero sólo está permitido diagnóstico e inventario **de lectura**. El test no se conecta a ambos servidores; éxito de un artefacto llamado QA srv01 Installable **no** demuestra instalación ni concesión de permisos de escritura. `main` sin cambios. PR #479 sigue DRAFT/bloqueada; #470 continúa OPEN. Fuente de coordinación: START-HERE, AGENTS, GOV-003 y Línea Base Maestra.

## Archivos exactos

- `backend/tests/PMGM.Api.Tests/Integration/SiloDocumentHttpLifecycleRealProbe.cs` — test HTTP integral opt-in (nuevo).
- `tests/test_silo_http_document_workflow.py` — laboratorio Docker y verificación de TRX real (nuevo).
- `tests/test_pilot_keycloak_db_isolation.py` — un import para ejecutar la prueba desde el gate Infra existente.
- `tests/test_silo_real_recovery.py` — sólo la función helper `_provision` compartida para readiness/reintento FAIL-CLOSED de IAM (añadido tras incidencia).
- `docs/security/PMGM-SEC-470-silo-http-workflow.md` — este procedimiento.

Sin editar frontend/UI Claude, backend productivo, Compose, MinIO IAM o workflow reservados al PR #479, infraestructura QA, secretos institucionales ni `main`.

## Corrección de compatibilidad de streaming S3 detectada por la prueba HTTP (15-10-2026)

La prueba de cliente .NET aislada PR #494 había pasado porque recibía un `MemoryStream` rebobinable. El test HTTP del PR #497 usa **`HttpRequest.Body` real, no-seekable** y descubrió una brecha en `S3DocumentObjectStore.StoreAsync`: API HTTP 500 y `AmazonS3Exception` sin respuesta S3, diagnosticada con un wrapper **sólo de test** que registró `put:http=0:code=none:seekable=False` sin exponer claves, credenciales ni nombres de objetos. Documentación AWS SDK de `PutObjectAsync` indica que ese método puede requerir longitud conocida/stream rebobinable, no asumir soporte directo de `HttpRequest.Body` no-seekable. Se corrigió **en la clase productiva S3DocumentObjectStore**, exclusivamente dentro de este PR, manteniendo intacto `IDocumentObjectStore`: cuando `CanSeek == false` copia asíncronamente a `MemoryStream` acotado por `DocumentStorage:MaxUploadBytes` y por el límite de `int`, retorna al inicio, manda el contenido a AWS SDK sin deshabilitar firma SigV4 ni checksums, limpia el buffer temporal in-memory (incluyendo cancelación/errores) y no crea archivos de texto claro en disco. La ruta seekable preexistente permanece igual. El endpoint ya comprueba Content-Length y tamaño reservado previamente.

**Riesgo residual operativo:** el buffering introduce memoria temporal hasta el límite configurable (predeterminado 50 MiB) por carga concurrente. El despliegue debe definir control de concurrencia y recursos antes de QA productiva; esta PR no cambia capacidad de infraestructura, no autoriza instalar en glmdch ni aprueba Silo institucional. Mantener evidencia de pruebas CI y revisión de memoria/DoS. **No confundir hipótesis con cierre:** falta observar que el CI real de la nueva revisión pruebe éxito del flujo completo con PostgreSQL/Silo/ClamAV; no fusionar si falla. Además, CI #38814870884 sufrió 502 transitorio del registro `quay.io` para imagen Keycloak, independiente del defecto de streaming, que aprobó en la siguiente ejecución sin alterar la versión de Keycloak.

Fuente técnica AWS: https://github.com/aws/aws-sdk-net/issues/3146 ; https://docs.amazonaws.cn/en_us/sdk-for-net/v4/developer-guide/net-dg-performance.html .
