# QA glmdch — reconstrucción de suministro — 16-10-2026

Continuación autorizada por Pablo de #97/#470/#506; Silo sólo QA sintético. Base remota verificada `dev@3a25ab0bfbfe0bb5aa8009906505ccb86559d723`, rama #506 `6ce59404cf3908ff8f0a6b0ee81e429f0fe371de`, main `6dfb9546a4873baff15955cf86abfd7d47e3d111`. Se conserva el avance de diagnóstico y delimitadores ya validado. No reconstruir esas correcciones ni repetir el SHA anterior para conseguir verde.

Fuentes vigentes consultadas: comentarios #97/#506/#501, adenda QA Drive hasta cierre6ce5940, búsqueda de novedades en carpeta oficial desde13:07UTC (sin resultados). PR513 cerrada registra dominio público https://sistema.granlogiamixta.cl/; configuración HTTPS pendiente, no prueba DNS/certificado. No contradice la limitación QA sintético ni habilita consola Keycloak pública. Suministro/vendor Dockerfile de pgsty/silo@00a375c4, cliente pgsty/mc@0e24a70a0a428de4ae2b7853db3499ba1cb34f37 (commit GitHub verified), ledger oficial https://silo.pgsty.com/about/security-advisories/ y matriz de componentes.

## Cambio

Workflow nuevo `QA Silo rebuilt candidate`, sólo pull_request hacia dev, runner efímero ubuntu24.04, contents:read, acciones por SHA, sin secretos institucionales ni publicación a registry. Construye candidato experimental (no reemplaza imagen Compose ni paquete desplegable):

- Mantiene servidor de imagen vendor por digest/firma de pgsty/silo ya conocido.
- Cliente desde checkout exacto0e24a70a, limpio y hash de git archive; toolchain oficial Go1.27.2-alpine por digest, CGO=0/GOTOOLCHAIN=local, mod=readonly/trimpath. buildvcs=false explícito porque el compilador Alpine no necesita git; metadatos fuente vinculados por pipeline/manifest, sin fingir sello vendor sobre el nuevo cliente.
- Patcher UBI9 oficial por digest actualiza pcre2/pcre2-syntax por repositorios firmados oficiales y conserva inventario RPM. Repositorios actualizados son una entrada temporal: NO se afirma reproducción bit a bit futura; la identidad del resultado se fija por image ID y SHA256 del archivo.
- Rootfs final en scratch con cliente corregido, LICENSE/NOTICE/CREDITS del cliente y servidor, build-info e inventario. No quedan capas finales con el cliente anterior.
- Escaneo Trivy por binario/checksum conocido, caché nueva, SBOM CycloneDX, informe íntegro y bloqueo HIGH/CRITICAL (incluido sin fix), sin ignore/VEX/waiver.
- Laboratorios IAM, recuperación y SDK .NET existentes se repiten realmente contra image ID del build. Override de laboratorio exige flag explícito + sha256 de64hex; docker pull de ese ID se convierte sólo en inspección local. No se sustituyen operaciones S3 ni controles por dobles.
- Guarda imagen docker-save y evidencia de revisión7d, marcada NOT-A-DEPLOYMENT-RELEASE. No tar como release institucional ni como licencia jurídica de producción.

## Validación inicial

3 regresiones offline de contrato negativo y7 de diagnóstico PASS; sintaxis Python y diff-check PASS. Sin Docker local: build/escaneo/pruebas reales pendientes de workflow del commit que contiene este archivo. Estado exacto se registra en #506/#97/Drive sin commit posterior de resultados. No inferir remediación por código fuente o build exitoso.

## Siguiente tarea

1. Leer primer resultado de reconstrucción. Si build falla, diagnosticar build.log sin volcar secretos (pipeline sólo insumos públicos); corregir causa antes de repetir. Verificar RPM pcre2 corregido, Go1.27.2/x-net del binario mcli y escaneo fresco del candidato.
2. CVE-2026-39414 se asocia por scanner al módulo histórico github.com/minio/minio. Ledger declara fix3252d5b7f y follow-up JSON Lines (no sólo CSV). Comprobar ambos en fuente exacta y binario/negativas antes de proponer VEX específico; no ignorar genéricamente por tratarse de un fork. No se ha añadido VEX ni excepción en este incremento.
3. Sólo con suministro/triage validados: publicar artefacto con procedencia y digest controlado, actualizar Compose/guards/tests, gates y nuevo instalable del mismo SHA. No adoptar un image ID local en configuración institucional.
4. Preparar HTTPS para sistema.granlogiamixta.cl con proxy existente, consola/master Keycloak privados y IP real confiable; verificar DNS/red/certificado/acceso limitado desde GitHub. #501 mantiene gates propios y main sin promoción.
5. BD2/OIDC, backup externo cifrado/RPO-RTO y smoke/UAT integral siguen pendientes separados del laboratorio de datos.

**Despliegue QA pendiente.** No conexión ni cambios en glmdch, DNS, cuentas, secretos, firewall, servicios, datos reales ni main. #506 DRAFT, sin merge hasta superar suministro. Registrar avance y bloqueo en GitHub y adenda QA Drive.

## Continuación de triage, mismo lote

Se preservaron commits concurrentes ef24927d/e315793e de dominio/plantilla y se integró dev@db82dd1c (PR513) antes de publicar4b0cf521. El objeto inicial6802f3e NO fue HEAD. Rebuild4b0cf521, run38906552281/artifact12048552098, completó build y laboratorio IAM/recovery/SDK PASS56.325s. Escaneo de imagen reconstruida: **1 HIGH/0 CRITICAL**, CVE-2026-39414 del servidor; los10HIGH de base/cliente no aparecen. Image ID073c28f9..., archiveSHA25604ae83cc36dbd7e7f9cb641dc2b808fc48db8f1e49cb85b247394ede225f4944, artifactZIP SHA256b29b9825c93fe61e43253738f36ec8370ca7470a594f3a33aed6ff5098563458. Estos datos NO validan commits posteriores.

Prueba adicional real `test_silo_select_record_limit.py`: S3 Select COUNT(*) para CSV y JSON Lines, controles normales y registros de1MiB+1byte, exige frames event-stream con CRC válido y OverMaxRecordSize del input, salud posterior. Usa credenciales sintéticas '-'/'--' y cuenta limitada en servidor efímero. Parser CRC/errores tiene regresión offline; el workflow ejecuta la prueba binaria junto con los laboratorios anteriores.

`server_patch_evidence` contrasta patch ancestry contra fuente exacta7a3ca, tres blobs por hash Git, límites de1MiB y ruta JSON PReader sin SIMD, source commit en attestation del proveedor y SHA256 de binario server idéntico entre imagen firmada y reconstruida. Sólo después de prueba binaria PASS genera `server-cve-triage.json` como evidencia para revisión específica; NO añade ignore/VEX, NO filtra el hallazgo del escaneo ni cambia el gate. El servidor sigue bloqueado hasta resolver esa clasificación e identidad de publicación/adopción de imagen; no usar el tar de revisión como release.

## Clasificación específica tras la prueba binaria (supersede «pendiente de triage»)

En6bb7a4b, run38907270027, los4 laboratorios reales PASS42.016s, sin fallos de validación; binario SHA547a8181b6674d2b5c3ae2a3c515807f45e613764dfbbc82cf5ad9ad3800c4e4 conservado, patch ancestry/blobs/firma y negativas CSV/JSONL confirmados. Scanner crudo1HIGH/0CRITICAL por identificación Go heredada. Esa evidencia permite una declaración de ingeniería `fixed` para el fork parcheado, no una dispensa de riesgo por QA.

El incremento posterior genera OpenVEX `status=fixed` para PURL exacto `pkg:golang/github.com/minio/minio@v0.0.0-20261015052015-7a3ca0f82100`, exclusivamente cuando coinciden binario fijo, image ID actual, fuente/fix, los3blobs y regresión real. Guarda el informe crudo sin VEX, aplica una segunda evaluación con Trivy0.76.0 verificado y conserva `trivy-reviewed.json`; cualquier filtrado distinto de esa CVE/paquete/versión/target exactos falla. Cualquier otro HIGH/CRITICAL sigue bloqueando, aun sin fix. No se utiliza `.trivyignore` ni `--ignore-unfixed`.

El VEX lo emite ingeniería del Proyecto Centenario y NO se presenta como documento firmado por PGSTY. Trivy no impone por sí mismo hashes binarios del VEX; la función de binding del pipeline es obligatoria. Consumidores posteriores deben verificar procedencia y el SHA binario/imagen antes de usar esa declaración. No copiarla como excepción global ni reutilizarla ante nuevo server/digest/versión sin otra revisión.

Workflow añade permiso OIDC+attestations:write sólo para vincular tar, declaración, evidencia y reporte mediante acción GitHub attest-build-provenance v3 fijada a977bb373ede98d70efdf65b84cb5f73e068dcc2a. Verifica cada subject con el bundle generado, conserva bundle/resultados. No packages:write, push-registry, secretos institucionales ni main. Nuevo build/escaneo/revisión/pruebas/procedencia deben pasar en el mismo SHA antes de adoptar; el Compose sigue con digest antiguo y NO se considera corregido aún. Publicación/adopción controlada de la imagen, ingreso HTTPS/OIDC/BD2 y acceso glmdch siguen pendientes.

Fuentes primarias del formato/consumo: https://github.com/openvex/spec/blob/main/OPENVEX-SPEC.md y https://github.com/aquasecurity/trivy/blob/v0.76.0/pkg/vex/openvex.go (acepta estados fixed/not_affected); no se depende de supresión automática de VEX externo no verificado.
