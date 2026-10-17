# QA glmdch — planificador de solo lectura y temporizador de referencia

Fecha: 17-10-2026. Trazabilidad: GitHub Issues #525 y #97. **No instalar ni activar en glmdch antes del primer QA físico supervisado.**

Se incorpora el planificador scripts/qa/qa-agent-plan.py, que:
- Lee el registro privado last-deployed.json de la última versión QA aceptada. Si falta, bloquea antes de consultar GitHub.
- Reutiliza qa-agent-readiness.py para verificar SHA exacto de dev, gates post-merge y único artefacto Silo completo por SHA.
- Reutiliza qa-antirollback.py para comprobar ancestro del SHA instalado y bloquear retrocesos, diverged/rebase o metadatos inconsistentes.
- Emite exclusivamente JSON de planificación (actualizado/sólo staging posible/bloqueado).

**No descarga ni instala**, no usa tokens de GitHub, no ejecuta Docker ni escribe registros de despliegue.

Las plantillas infrastructure/systemd/pmgm-qa-agent-plan.service y .timer son **archivos de referencia no instalados ni habilitados**. Servicio: usuario no privilegiado pmgm-qa-agent, filesystem read-only, sin capacidades Linux; ejecución de planificador de solo lectura. Temporizador con revisión horaria y desplazamiento aleatorio, sin acciones sobre QA. El primer arranque debe permanecer bloqueado y una eventual habilitación requiere aprobación expresa luego de QA supervisado.

### Custodia del baseline (corrección de contrato de permisos)

El registro original `/var/lib/pmgm-qa-agent/last-deployed.json` se crea **solo después de un primer QA sintético supervisado**. Debe pertenecer a `root:root`, modo `0600`, dentro de directorio administrado por root. Nunca cambiar su propiedad a `pmgm-qa-agent`, pues ello permitiría modificar el SHA de confianza y anular el propósito del antirollback.

La plantilla `pmgm-qa-agent-plan.service` usa `LoadCredential=qa-baseline:/var/lib/pmgm-qa-agent/last-deployed.json`. **systemd** (gestor privilegiado) lee el original y entrega al proceso no privilegiado una copia privada de solo lectura en `${CREDENTIALS_DIRECTORY}/qa-baseline`. En Ubuntu/systemd actualizado, la copia entregada puede aparecer como propiedad de `root`, modo `0440` y ACL restringida al usuario del servicio, dentro de un montaje de credenciales **solo lectura**. El parser `qa-antirollback.baseline()` admite esa excepción exclusivamente cuando la ruta coincide con `$CREDENTIALS_DIRECTORY/qa-baseline`, bajo `/run/credentials/`, con directorio protegido por root y montaje read-only. Fuera de esa ruta conserva la regla estricta de propietario EUID y ningún permiso para grupo/otros; el original siempre exige `root:root 0600` y no se comparte con el agente. Sin original verificado, el servicio no comienza.

Antes de activar físicamente hay que comprobar la semántica real de credenciales de la versión systemd de Ubuntu glmdch (UID, permisos y contenido del archivo entregado), permisos de las rutas, y los tests de error ante credencial ausente o corrupta. Las pruebas de CI validan el contrato estático y un ensayo dinámico en un runner Ubuntu efímero, pero no acreditan que el servicio funcione en el host `glmdch`. El temporizador permanece deshabilitado. Referencia upstream: https://github.com/systemd/systemd/blob/main/docs/CREDENTIALS.md.

PENDIENTES: instalación inicial real supervisada y creación auditada de last-deployed.json sólo tras aceptación; orquestador de actualización operativa con backup cifrado externo, restauración verificada, control de migraciones, identidad firme Silo en Docker 29/containerd, HTTPS/OIDC/BD2/G8, health/smoke, pausa/rollback, cumplimiento de condiciones #470/#479, corrección de HTTP 502 y regulación de licencia/ciberseguridad. El paquete firmado preparado por código no certifica funcionamiento de QA.

El Sponsor confirmó SSH desde Termius (cuenta soporte con sudo), Docker 29.9.1, sin contenedores ni volúmenes, puertos 80/443 sin escuchas, gh sin autenticación y paquete físico anterior SHA 254ddab. Nunca pedir ni guardar contraseñas, token o env institucional en chats, repos o Drive. Mantener main intacta, srv01 pausado y datos exclusivamente sintéticos.

### Prueba de integración de credenciales en runner efímero (#525)

El job `runtime-credentials` del workflow existente `.github/workflows/qa-agent-planner.yml` ejecuta **solo en el runner GitHub-hosted ubuntu-24.04**, nunca en `glmdch`, la prueba `tests/qa_systemd_credential_probe.py` mediante `systemd-run` en servicios efímeros. Genera un JSON de referencia completamente **sintético**, guarda el original como root:root 0600 en directorio root-only 0700 y entrega con `LoadCredential` una copia al servicio `nobody` sin capacidades de escritura. Exige que la copia sea un archivo regular de `root`, modo `0440`, dentro del montaje de credenciales de solo lectura, inaccesible para escritura, y que el servicio no pueda abrir el original protegido; reutiliza el parser real `qa-antirollback.baseline()`. La prueba negativa debe rechazar el JSON con `qa_only=false` por `BASELINE_SCHEMA_INVALID`. No lee claves ni tokens, y no despliega ni instala Docker o el agente.

**Esta prueba aporta evidencia de laboratorio, no certificación física G7/G9/G10.** Antes de activar en Ubuntu `glmdch`, se exige verificar la versión real de systemd, UID/modo/montaje de la credencial, dependencias, ruta y original custodio root, ausencia de baseline inicial, restauración externa y primer despliegue sintético supervisado. El timer permanece sin instalar y deshabilitado. No imprimir contenidos ni rutas confidenciales en GitHub/Drive.
