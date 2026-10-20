# QA-AUTO-LIGERO — instalación utilizable #525 / PR #576

## Resultado y alcance

Continúa el QA sintético existente `centenario-qa-lab2` de glmdch. Instalador WEB-only con bootstrap supervisado único, aplicación posterior por timer, firma+manifest+gates de dev, respaldo externo cifrado, salud pública y reversión con journal para interrupciones. Sin cambios de API/BD/Keycloak/Silo/red, sin migraciones ni `main`/srv01/producción. No se crean credenciales y ningún runner recibe acceso al host.

El código permanece en PR #576 hasta CI del HEAD final; consultar la descripción de la PR para SHA/base y resultado vigente. Docker real builder+adapter+rollback de la fase anterior aprobó `663ba1a9...` run 39343571108. La nueva integración agrega bootstrap operativo, estado instalado y recuperación de interrupción: su resultado debe verificarse de nuevo. No afirmar instalación o timer activo hasta salida real de glmdch y `/qa-version.json`.

## Evidencia física recibida del Sponsor

- Compose activo `/opt/centenario/qa-lab-firstboot/recovery-v2/compose.recovery.json`, propietario uid0, modo0600.
- SHA256 `bed395ce374659b98ede0637afcbbe428feaf7c2c4bdeb4b351d2b6015aa1b71`.
- `web.image=None`, build.context `/opt/centenario/qa-staging/4ef456fee3a578a9e6418f3f7072af5dd79eb045/frontend`, env_file=[]. No publicar JSON completo, environment ni args. No confundir nombre recovery-v2 con fracaso de recovery-v3.
- Imagen física de respaldo: `sha256:25c51921f26ebac2734fc5b53fc0c9a3553fc6801d86d1fd455ae10053127696`. Su Git SHA no está demostrado. Bootstrap permite `previous_sha=null` exclusivamente con ese ID fijado y sin installed.json; no inventa versión anterior. Actualizaciones posteriores requieren baseline conocido y ascendencia fast-forward.
- Backup cifrado 19-oct 17:52:18Z `/home/soporte/centenario-qa-g6-CIFRADO-20261019T175218Z.tar.gz`, SHA256 `bed4ee196074a1919234da44c6bbd66c8a7cd16bdce70fba97ed1e34df8f9cae`; copia privada Drive `1Sl_qrcJDZIHEMByngwAUrCrewguWVhT1` (312993 bytes), binario externo verificado previamente, metadatos reconfirmados. No se recibe ni copia su clave.
- Issuer real preservado: `https://sistema.granlogiamixta.cl/identity/realms/pmgm`. API ready real devuelve JSON `{status:ready,database:postgresql}`, no texto Healthy.

## Piezas finales

- `qa-lite-install.py`: CLI real `--bootstrap`, `--apply`, `--recover`, opción `--enable-timer`. Respeta pausa local, root/hostname/paths/config, configura env VACÍO explícito (no descubre .env), vincula archivos de mounts de Compose, descarga y verifica paquete real, construye web, registra pending/installed atómicos y verifica salud y SHA público. Solo aplica `compose up --no-deps --no-build --pull never ... web`.
- `qa-lite-bootstrap.py`: descarga por commit inmutable ocho dependencias con SHA256 individual fijado, prepara gh si falta y lanza unidad transitoria `pmgm-qa-lite-firstboot`. Instalación sobre servidor, no depende de mantener abierto Termius. El hash de este bootstrap y su commit se deben fijar en el comando entregado al Sponsor; no curl|bash.
- `qa-lite-web-build.py`: configuración pública VITE preservada por hash, issuer correcto/mocks=false, verificador criptográfico antes y después, ID+label de imagen y metadata pública `/qa-version.json`. No modifica árbol firmado; agrega una capa de build confiable con solo environment y SHA.
- `qa-lite-compose-adapter.py`: socket local fijo, root/control de config, flock, dependencias e identidad de imagen, mounts/red/puertos conservados y rollback a ID anterior. La CLI propia continúa siendo solo probe; el instalador controla apply.
- `qa-agent-readiness.py`: conserva `evaluate` estricto. Nueva selección del corte más reciente completamente verde que siga siendo ancestro de dev vivo, dentro de 100 runs recientes; valida nuevamente últimos intentos+artefacto exacto. Evita quedar sin QA durante construcción de commits posteriores. Nunca usa PR, fork, intento fallido, artefacto expirado ni historia reescrita. No retrocede una baseline instalada.
- `qa-public-release-download.py`: descarga sin token, origen/release/redirect CDN fijados, tamaño y digest del envelope. Descarga no equivale a firma válida.
- `qa-signed-publish.py` y workflow NUEVO `qa-signed-publish.yml`: únicamente push dev del repo oficial, espera gates y verifica firma+manifest antes de publicar prerelease/tag QA `qa-signed-<SHA>`. Sin sobreescritura de assets existentes. El host no necesita token GitHub. No usa workflow_run, que dependería de existir en rama por defecto; no se modifica main.
- `qa-signed-staging.py`: opción tokenless por llamada borra tokens heredados y usa configuración gh vacía; default y consumidores anteriores se conservan. Se verifica identidad workflow, SHA fuente y manifiesto completo; no se ejecuta instalador descargado.

## Pruebas

100 comprobaciones locales: 48 conjunto lite, 6 descarga/publicación, 10 readiness, 6 política, 10 staging, 10 adquisición, 10 transporte existente; PASS, más py_compile/diff-check. CI propio debe confirmar: tests, Docker real (build/marker/bootstrap/apply/rollback/recuperación de interrupción/dependencias intactas) y firma+manifest REALES sin token host/gh login. Los verificadores G6/HTTPS del ensayo Docker son fixtures explícitos; no son validación física de glmdch. Firma real tiene job separado con descarga de fixture usando token efímero CI y verificador sin token.

## Operación y pendientes que siguen abiertos

1. Integrar #576 solo con gates completos del HEAD final y sin perder nuevos cambios de dev. Publicar envelope firmado y comprobar asset real/digest/estado antes de entregar comando de bootstrap.
2. Primer bootstrap supervisado por Termius; no hay conexión SSH desde esta sesión. Revisión posterior de journal y de `/qa-version.json` para registrar SHA físico. Hasta entonces: **despliegue QA pendiente**.
3. Timer `pmgm-qa-lite.timer`: intervalo15min. Pausa con archivo `/opt/centenario/qa-auto-lite/paused`. Journal de actualización interrumpida bloquea nuevos cambios hasta `--recover`.
4. **Limitación material:** respaldo válido48h; la copia actual expira para nuevos deploys el 21-oct 17:52:18Z. La renovación/copia externa desatendida aún requiere canal Drive/rclone del host. No prometer automatización permanente sin resolverlo. No se restaura BD para revertir WEB. Se conserva copia18-oct y DR segundo host sigue pendiente aceptado para QA.
5. Después del cambio: G10 Usuarios/Auditoría #558 y funcionalidad perfiles/vistas #523. No declarar esos casos resueltos ni UAT por CI/merge.
6. `/qa-version.json` contiene solo QA_SYNTHETIC y SHA público; nada de cuentas, roles, paths, credenciales o datos. No cambia diccionario ni estructuras de datos institucionales.

Fuentes: #97, #525, #576 y bitácora QA Drive `1I9bcJ7lKH4T2iQvxafY63HbOBr0ZsGZ8lDX3tSe2UfM`. Verificación gh offline basada en documentación oficial GitHub, con prueba real propia requerida antes de instalación.

## Corrección del bloqueo CI por cambio de día UTC

CI de cc6a97cf detectó 902/903 tests backend PASS y fallo en Complete_reconciled_statements...: TreasuryPayment.RecordedAtUtc tomaba UtcNow implícito mientras el fixture fija Cutoff19-oct. Al20-octUTC se considera pago registrado fuera del corte, según la regla productiva vigente. Se incorpora SOLO la fecha fija18-oct del fixture que ya preparó #581 (blob6c80914c, HEAD079f3740), y se agrega caso negativo explícito RecordedAtUtc20-oct que debe continuar bloqueado. Sin código productivo ni regla contable alterados. Archivo no hot, informado a #581 en comentario6280216128 conforme GOV-003 §4; mantener C11 en su rama y rebasar tras merge QA. La nueva prueba C# y los 903 existentes se ejecutarán en CI, .NET no disponible localmente; no afirmar prueba C# local. Docker operativo y firma real sin token del HEAD previo PASS; revalidar en nuevo HEAD.

## Cierre de publicación y espera inicial

El publicador mantiene la vigilancia del push después de publicar el último corte verde previo, para publicar también el último commit cuando terminen sus gates aunque no haya otro push. Regresión específica PASS. El instalador espera la publicación del asset (sin repetir API ni tocar contenedores) si sus gates terminan antes de completar la carga. Bootstrap SHA256 actualizado `6290674c095ec8ae5582188ab40c2712ca61755e09d06082977992eb9dbfe2cd`. La corrección C# de RecordedAtUtc respeta init-only creando un nuevo pago de fixture; Backend unit and policy tests de f7708f2d aprobó, verificar el último HEAD de integración.
