# QA glmdch — planificador de solo lectura y temporizador de referencia

Fecha: 17-10-2026. Trazabilidad: GitHub Issues #525 y #97. **No instalar ni activar en glmdch antes del primer QA físico supervisado.**

Se incorpora el planificador scripts/qa/qa-agent-plan.py, que:
- Lee el registro privado last-deployed.json de la última versión QA aceptada. Si falta, bloquea antes de consultar GitHub.
- Reutiliza qa-agent-readiness.py para verificar SHA exacto de dev, gates post-merge y único artefacto Silo completo por SHA.
- Reutiliza qa-antirollback.py para comprobar ancestro del SHA instalado y bloquear retrocesos, diverged/rebase o metadatos inconsistentes.
- Emite exclusivamente JSON de planificación (actualizado/sólo staging posible/bloqueado).

**No descarga ni instala**, no usa tokens de GitHub, no ejecuta Docker ni escribe registros de despliegue.

Las plantillas infrastructure/systemd/pmgm-qa-agent-plan.service y .timer son **archivos de referencia no instalados ni habilitados**. Servicio: usuario no privilegiado pmgm-qa-agent, filesystem read-only, sin capacidades Linux; ejecución de planificador de solo lectura. Temporizador con revisión horaria y desplazamiento aleatorio, sin acciones sobre QA. El primer arranque debe permanecer bloqueado y una eventual habilitación requiere aprobación expresa luego de QA supervisado.

PENDIENTES: instalación inicial real supervisada y creación auditada de last-deployed.json sólo tras aceptación; orquestador de actualización operativa con backup cifrado externo, restauración verificada, control de migraciones, identidad firme Silo en Docker 29/containerd, HTTPS/OIDC/BD2/G8, health/smoke, pausa/rollback, cumplimiento de condiciones #470/#479, corrección de HTTP 502 y regulación de licencia/ciberseguridad. El paquete firmado preparado por código no certifica funcionamiento de QA.

El Sponsor confirmó SSH desde Termius (cuenta soporte con sudo), Docker 29.9.1, sin contenedores ni volúmenes, puertos 80/443 sin escuchas, gh sin autenticación y paquete físico anterior SHA 254ddab. Nunca pedir ni guardar contraseñas, token o env institucional en chats, repos o Drive. Mantener main intacta, srv01 pausado y datos exclusivamente sintéticos.
