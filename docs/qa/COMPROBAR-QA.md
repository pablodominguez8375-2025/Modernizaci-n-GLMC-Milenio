# Comprobar QA (glmdch)

Estado: preparado en PR #501 hacia `dev`, asociado a #97. Sin aprovisionamiento ni ejecución remota. glmdch sustituye al srv01 histórico; instalar/desplegar el stack, smoke con escrituras y UAT siguen pendientes.

## Archivos y contrato

- `.github/workflows/check-qa.yml`: despacho manual exclusivo, destino glmdch mediante Environment `qa-readonly`, cinco minutos, concurrencia serial, checkout del SHA de la ejecución sin credencial persistente. Sólo permite ejecutar desde `main` una vez aprobada su incorporación mínima.
- `scripts/qa/centenario-qa-readonly`: helper Python 3 en modo aislado, sin sudo. Petición exacta `centenario-qa-readonly-v1`, sin argumentos, PTY ni stdin. Inventaría CPU, MemTotal, filesystem de `/opt`, servicios SSH/UFW/Docker/containerd, presencia de paquetes permitidos y listeners TCP/UDP agregados por puerto/alcance. Nunca conserva direcciones, PIDs, usuarios ni contenido de aplicación. No consulta sockets Docker/LXD, logs, claves, env ni BD.
- `scripts/qa/check-qa.sh` y `validate-report.py`: conexión estricta y validación cerrada de identidad/esquema/hash. La salida cruda y stderr SSH no se publican. Archivos SSH temporales modo privado, limpieza con trap incluso ante error. Un fallo de runner puede impedir el trap: usar exclusivamente runners efímeros.
- `qa-readonly-tests.yml`: pruebas sintéticas de PR sin Environment ni secretos; no altera `ci.yml`, reservado por #479.

El resumen separa comprobación PASS/FAIL de QA NO_VERIFICADO y firewall NO_VERIFICADO_REQUIERE_OPERADOR. SSH exitoso no acredita preparación, instalación, smoke ni aceptación QA. Ausencia de Docker observada en inventario previo sigue pendiente; revisar el mapa de paquetes/servicios del informe sin confundirlo con disponibilidad de un daemon.

## Configuración futura — requiere aprobación aparte; no ejecutada

1. Revisar PR #501 y gates del HEAD exacto (PMGM CI, Showcase, QA Installable y QA readonly contract tests). No fusionar esta entrega: fue solicitada para revisión. `dev` es la base de desarrollo; `main` permanece intacta.
2. GitHub exige que `workflow_dispatch` exista en la rama predeterminada, actualmente `main`. Tras aprobación expresa del Sponsor, preparar una PR mínima independiente a `main` con check-qa.yml y sus tres scripts cliente/validador/helper (el helper se usa también para hash). No promover `dev` completo, cambiar default branch ni debilitar el guard de rama. Verificar antes compatibilidad de Actions y políticas reales del repositorio.
3. Acordar conectividad de salida del runner hacia glmdch TCP externo 22107. No ampliar firewall ni instalar runner en glmdch. Si sólo hay LAN/VPN, diseñar y aprobar conectividad privada/runner dedicado aislado antes de adaptar `runs-on`. Confirmar por operador firewall IPv4/IPv6/NAT y mapeo externo 22107→22: UFW activo no demuestra reglas correctas.
4. Con autorización posterior, un administrador prepara cuenta dedicada `qa_check`, sin sudo/docker/lxd, sin escritura en aplicación y sin credenciales adicionales. Shell ejecutable necesaria para forced command (no `nologin`); contraseña bloqueada, autenticación sólo por clave. No copiar la clave o grupos de centenario-wsl. Home, `.ssh` y `authorized_keys` administrados por root, sin escritura para la cuenta. El helper, intérprete y todos los directorios padres deben ser root-owned y no escribibles por esa cuenta. Verificar rutas absolutas `/usr/bin/python3`, `/usr/bin/systemctl`, `/usr/bin/dpkg-query`, `/usr/bin/ss` antes de instalar; no instalar paquetes automáticamente.
5. Ejemplo de instalación del helper **sólo después de aprobación**, desde el commit revisado en estación del operador y con transferencia controlada:

   ```bash
   sudo install -o root -g root -m 0755 centenario-qa-readonly /usr/local/libexec/centenario-qa-readonly
   sha256sum /usr/local/libexec/centenario-qa-readonly
   ```

   Comparar hash con `scripts/qa/centenario-qa-readonly` del commit que ejecutará el workflow. Cualquier diferencia bloquea el informe. No dar sudo a qa_check. No crear regla sudoers.
6. Generar fuera del chat una nueva clave Ed25519 exclusiva por mecanismo seguro. No publicar la privada en Git, Drive, logs o issue. Línea modelo `authorized_keys` (sustituir sólo la pública):

   ```text
   restrict,command="/usr/local/libexec/centenario-qa-readonly" ssh-ed25519 <PUBLIC_KEY> github-qa-readonly
   ```

   `restrict` desactiva forwarding, agent, X11, PTY y user rc; `command` impone el helper. Para proteger toda la cuenta, preparar además un bloque `Match User qa_check` en sshd con `AuthenticationMethods publickey`, `PasswordAuthentication no`, `KbdInteractiveAuthentication no`, `DisableForwarding yes`, `PermitTTY no`, `PermitUserRC no` y `ForceCommand /usr/local/libexec/centenario-qa-readonly`. Revisar con `sshd -t` y configuración efectiva `sshd -T -C ...` antes de cualquier recarga autorizada. Mantener sesión administrativa de rescate. No añadir comandos alternativos ni restricciones `from=` inventadas para IPs dinámicas de GitHub.
7. Crear Environment **qa-readonly** únicamente con aprobación posterior. Verificar que plan/visibilidad permiten revisores obligatorios, impedir autoaprobación y limitar deployment branches exclusivamente a `main`. Si esas protecciones no están disponibles, detener habilitación y acordar alternativa; no suplirlas con nombre del Environment. Crear allí (no a nivel global) secreto `QA_READONLY_SSH_KEY` y variables:

   | Variable | Valor a configurar por operador |
   | --- | --- |
   | QA_SSH_HOST | DNS/IP verificado del endpoint glmdch; sin esquema ni opciones SSH |
   | QA_SSH_PORT | `22107` |
   | QA_SSH_USER | `qa_check` |
   | QA_SSH_KNOWN_HOSTS | Línea `[host]:22107 tipo clave-publica-host` verificada por canal independiente |

   No confiar en `ssh-keyscan` sin comparación de huella; obtener huella desde consola/administrador de confianza. No hay contraseñas sudo en Actions.
8. Validación de acceso futura por operador: petición exacta devuelve JSON; shell libre, comando adicional, SCP y SFTP fallan; PTY, agent, X11, forwarding local/remoto deben quedar denegados por sshd. Las pruebas unitarias sólo acreditan rechazo del helper y sanitización, **no** la configuración real de sshd. Realizar esas pruebas sin secretos/datos institucionales y registrar resultado sanitizado. Revocación: retirar exclusivamente esta clave y deshabilitar Environment/workflow según procedimiento aprobado, preservando accesos existentes.
9. Primera ejecución manual posterior: seleccionar `main` aprobado, aprobar Environment, comprobar helper hash, hostname, informe y recibo. Artifact contiene sólo `report.json` validado y `receipt.json` con fecha, run/attempt, SHA workflow/source, versión/hash helper. Retención siete días. No publicar en Pages; registrar conclusión sanitizada en #97 y Drive. Fallo SSH/huella/esquema produce FAIL sin volcar salida remota.

## Verificación reproducible sin servidor

```bash
bash -n scripts/qa/check-qa.sh
python3 -m unittest discover -s tests -p test_qa_readonly.py -v
```

Ocho pruebas cubren contrato válido, comandos prohibidos/argumentos/SFTP/SCP/PTY, identidad/hash, campos inesperados, estados falsos, tamaño/JSON inválido, sanitización y rechazo de host malformado antes de SSH y limpieza/supresión de salida ante fallo SSH simulado. `actionlint` no está instalado en la estación: pendiente su ejecución por revisor/CI equipado. No se certifica configuración SSH real ni primera ejecución Actions.

Fuentes: [workflow_dispatch y sintaxis GitHub](https://docs.github.com/en/actions/reference/workflows-and-actions/workflow-syntax#onworkflow_dispatch), [protecciones de Environments](https://docs.github.com/en/actions/how-tos/deploy/configure-and-manage-deployments/manage-environments), [authorized_keys restrict/command](https://man.openbsd.org/sshd.8). Los SHA de checkout v4.2.2 y upload-artifact v4.6.2 fueron contrastados con los refs oficiales al preparar esta entrega.
