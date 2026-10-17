# QA glmdch — preparación aislada de instalable firmado

Fecha: 17-10-2026. Estado: prototipo de ingeniería para Issue #525; **no despliega** ni modifica el host.

## Propósito

El primer componente `qa-agent-readiness.py` (PR #526) selecciona el SHA de `dev` aprobado y el único artefacto completo de `QA Silo supply and recovery` de ese mismo SHA. El siguiente componente `qa-signed-staging.py` trabaja **exclusivamente con un ZIP ya descargado** y su digest esperado, comprueba el SHA-256 completo, estructura cerrada del ZIP externo, procedencia Sigstore del ZIP interior, `BUILD-INFO.txt`, `MANIFEST.sha256` y presencia del bundle Silo. No interpreta como suficiente el archivo `*.verification.json` guardado por CI. Rechaza firma inválida, archivo alterado, rutas `../`, symlinks, duplicados, nombres ambiguos y tamaños anormales. Nunca ejecuta `INSTALAR.sh`, archivos del paquete, scripts provenientes del ZIP o Docker. No toca ni copia `/etc/pmgm/srv01.env`.

## Contrato de seguridad

CLI (solo cuando sea integrable con un canal de entrega aprobado):
```text
python3 scripts/qa/qa-signed-staging.py --artifact /ruta/zip-externo-actions \
  --artifact-sha256 sha256:<DIGEST_REAL> --source-sha <DEV_SHA_VALIDADO> \
  --output /ruta/nueva/de/staging
```
`--output` debe apuntar a una ruta que **no exista**, con directorio padre ya creado. Se utiliza preparación privada con `umask 077`, chequeo completo y movimiento atómico a la carpeta final. Estado exitoso `SIGNED_CODE_STAGED_NOT_INSTALLED` implica firma del instalable y hashes de sus archivos correctos, **NO** integridad runtime Silo, funcionamiento del binario, aprobación de IAM, backup/restore, HTTPS, DNS ni aptitud de desplegar el stack.

## Pendientes

1. Entrega del artefacto por release/pasarela read-only o token limitado `actions:read`, sin PAT amplio de usuario, respetando firma y digest.
2. Cotejar `rebuild-report.json` y firmas individuales de Silo + licencia, CVE, RootFS, binario, incluyendo Docker 29/containerd, durante el **preflight** ya existente.
3. Protección antirollback de SHA de `dev` y control manual de primera publicación, migraciones destructivas y actualizaciones.
4. Backups cifrados fuera de VM, ensayo restauración, health checks, rollback safe y pausa automática tras fallo.
5. Prueba del 502, DNS, proxy 80/443, TLS y OIDC antes de exponer QA al público.
6. Instalar servicio y temporizador solo tras revisión, auditoría y primer despliegue supervisado. No ejecutar workflows no confiables del repo público en un runner self-hosted con permisos root.

Referencia: Issues #97 y #525. `main` y `srv01` fuera de alcance.
