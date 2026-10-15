# PMGM-SEC-470 — AWS SDK .NET real contra Silo IAM restringido (15-10-2026)

**Estado: prueba experimental GitHub Actions. Sin selección de proveedor ni despliegue.**

## Objetivo y alcance

El PR #491 demostró IAM sobre Silo por REST SigV4 Python; #493 comprobó recuperación lógica. Esta entrega valida la **clase productiva C# S3DocumentObjectStore** de la API mediante el SDK real AmazonS3Client y credenciales de mínimo privilegio, sobre servidor Silo real, no S3Mock.

## Prueba

- Archivo nuevo backend/tests/PMGM.Api.Tests/Integration/SiloDocumentObjectStoreRealIntegrationTests.cs: test xUnit, se activa exclusivamente con PMGM_TEST_REQUIRE_REAL_SILO=1, exige todas las variables sintéticas (URL, keys, bucket permitido/prohibido), llama ExistsAsync/StoreAsync/OpenReadAsync/DeleteAsync con 32KiB de bytes aleatorios, compara SHA-256, prohíbe sobrescritura y verifica borrado. Además exige HTTP 403 con el mismo cliente y usuario para GET/HEAD/PUT/DELETE de otro bucket.
- Archivo nuevo tests/test_silo_dotnet_sdk.py: arranca servidor Silo 2026-10-15 por digest OCI exacto docker.io/pgsty/silo@sha256:aaac6bce13eb99316b3c88bd20143fc3c38ab607585256a411a095c44ee57f62 con política restringida sintética; arranca un contenedor oficial compilador .NET 10 (mcr.microsoft.com/dotnet/sdk:10.0, CI solamente) en la misma red Docker y sin puertos publicados, ejecuta dotnet test de la clase anterior y **exige TRX con un test Passed**, impidiendo falso positivo por filtro vacío. El SDK container usa un tag mutable SOLO para compilación de CI; revisar fijación de digest y cadena de suministro aparte. Al terminar borra contenedores, red y resultados; nada institucional.
- Import en tests/test_pilot_keycloak_db_isolation.py añade el test al gate infraestructura CI existente sin modificar .github/workflows/ci.yml ni archivos reservados del PR #479.

Si la imagen Silo o SDK no está disponible, falla compilación, el SDK .NET es incompatible o el aislamiento IAM falla, CI queda rojo: no sustituir por simulaciones ni omitir tests. El éxito no demuestra compatibilidad de endpoints autenticados OIDC, antivirus, TLS/proxy, retención Object Lock, metadatos, copias completas de volúmenes o RPO/RTO.

## Gobierno y nuevo estado de QA

**Novedad Drive del 15-10:** el Sponsor designó **glmdch como nuevo servidor QA sustituyendo srv01**. Solo autorizó inventario y diagnóstico de lectura. Instalación de Docker, alteración de firewall, despliegue y pruebas con escrituras NO autorizados; esta PR no accede a ninguno de esos servidores. Los nombres srv01 históricos en artefactos no constituyen permiso para desplegar.

Silo declara AGPL-3.0-or-later: sigue pendiente revisión jurídica de uso institucional, modificaciones y obligaciones de código fuente/servicio, y revisión de SBOM/attestation, licencia y CVE. Corrección SN-2026-015/016 declarada por proveedor, sin reproducir PoC en esta entrega. PR #479 permanece DRAFT/BLOQUEADO y #470 OPEN por decisión Sponsor, backup integral, IP auditada QA, licencias, migración de volúmenes y UAT.

## Integración

Revisar job Infrastructure CI, prueba C# real y TRX 1 Passed; requerir triple gate PMGM CI, Showcase y QA Installable SUCCESS del HEAD exacto, con base dev inalterada y mergeable, previo a fusionar exclusivamente en dev. Mantener documentación de continuidad en GitHub y Drive.