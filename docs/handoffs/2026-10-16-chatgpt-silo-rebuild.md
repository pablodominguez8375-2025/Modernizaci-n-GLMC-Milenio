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
