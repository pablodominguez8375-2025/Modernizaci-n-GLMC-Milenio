# PMGM-SEC-470 — laboratorio Silo real + ClamAV real + ciclo de vida documental (.NET)

**15-10-2026 · PR DRAFT · evidencia de componentes, NO flujo HTTP/OIDC institucional ni despliegue.**

## Brecha probada

El PR #494 validó `S3DocumentObjectStore` y AmazonS3Client reales frente a un Silo por digest, con usuario no-root IAM mínimo; #491 probó API SigV4 y #493 recuperación lógica. El backend YA incorpora `ClamAvDocumentMalwareScanner`, política MIME `DocumentContentTypePolicy` y `DocumentVersionLifecycle`. Queda pendiente demostrar la **cadena conjunta sobre contenido Silo realmente recuperado** y que un scanner inaccesible no se interprete como limpio.

## Implementación propuesta

- `backend/tests/PMGM.Api.Tests/Integration/SiloDocumentMalwareRealProbe.cs`: test xUnit opt-in obligatorio al recibir `PMGM_TEST_REQUIRE_REAL_SILO_CLAMAV=1`. Desde la cuenta restringida de Silo: guarda y lee dos documentos **text/plain** sintéticos, texto limpio y firma inocua EICAR de test, compara SHA-256, comprueba que ambos son texto MIME válido, escanea su Stream de respuesta S3 por `ClamAvDocumentMalwareScanner` con protocolo INSTREAM **real**. Limpio produce evidencia `clamav:clean:`; EICAR produce `clamav:rejected:` y nombre de detección. Verifica reglas del producto: `Uploaded → Available` sin paso Scanning denegado, `Scanning → Available` permitido solo para limpio con evidencia y SHA; `Scanning → Rejected` permitido para infectado con evidencia, `Scanning → Available` sin evidencia denegado. El scanner en puerto local inexistente debe lanzar `SocketException`, sin autorización de estado Available. Borra únicamente los dos objetos sintéticos generados con claves UUID.
- `tests/test_silo_dotnet_clamav.py`: en CI infraestructura Docker crea una red privada efímera, Silo por digest OCI de la versión corregida 2026-10-15 `docker.io/pgsty/silo@sha256:aaac6bce13eb99316b3c88bd20143fc3c38ab607585256a411a095c44ee57f62`, y ClamAV `clamav/clamav:1.4` (tag usado también por suite de integración vigente; fijación digest futura para supply-chain). Sin puertos publicados ni volúmenes institucionales, claves aleatorias y política IAM documental restringida, espera puerto 3310 por red local, ejecuta xUnit productivo en contenedor SDK .NET 10 únicamente con variables sintéticas, **exige TRX con exactamente un test Passed**. Falta pull/clamd/lifecycle => CI roja; no se cambia por S3Mock ni se omite el test. Limpia contenedores/red y resultados temporales.
- Se añade import de unittest al gate existente `tests/test_pilot_keycloak_db_isolation.py`; no se edita `.github/workflows/ci.yml` reservado PR #479. El archivo C# acaba en `Probe.cs`, no en `*Tests.cs`, porque la CI exige incluir cada archivo Integration *Tests.cs en un shard preasignado; el runner ejecuta esta clase por filtro exacto y confirma su resultado TRX.

## Límites estrictos

Esta prueba **no** levanta ni valida endpoints HTTP de negocio, OIDC/Keycloak, roles Secretaría o Gran Archivero, persistencia PostgreSQL/documentVersion de aplicación, auditoría, TLS/proxy, entrada/salida del antivirus mediante API HTTP, metadatos/retención/Object Lock, copia completa/RPO/RTO, ni cumplimiento jurídico Ley 21.719. Sólo enlaza clases productivas aisladas contra Silo+ClamAV. La prueba de indisponibilidad debe seguir **fail-closed** en el flujo HTTP de negocio: `ScanContentAsync` ya guarda estado `scanning`/503 en error de escáner; queda QA real pendiente.

**Nueva designación QA Drive 15-10:** glmdch reemplaza srv01, con autorización Sponsor exclusivamente de inventario/diagnóstico read-only. Ni este PR ni su suite se conecta a esos servidores, instala software o usa datos reales. No cambiar `main`, `srv01`, `glmdch`, ni PR #479 MinIO, que sigue bloqueado sin aprobación de proveedor, análisis licencias AGPL/SBOM y validación integral.

## Aceptación

Esperar GitHub Actions `Infrastructure configuration` con prueba real ejecutada y TRX Passed, luego PMGM CI, Showcase y QA Installable **SUCCESS de HEAD exacto** y base dev viva. Fusionar exclusivamente a dev según GOV-003 y autorización permanente. Registrar resultado real en Issue #470 y Línea Base Maestra Drive, distinguiendo laboratorio de QA institucional.
