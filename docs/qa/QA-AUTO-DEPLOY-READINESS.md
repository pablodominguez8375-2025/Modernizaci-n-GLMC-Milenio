# QA glmdch — control de autorización para actualizaciones automáticas

**Estado:** fase inicial **solo lectura** del [Issue #525](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/issues/525); no instala, descarga ni inicia servicios. Fecha 2026-10-17.

## Punto real de partida

El Sponsor recuperó acceso SSH desde Android como `soporte@glmdch`, con `sudo -v` confirmado. Docker Engine 29.9.1, sin contenedores ni volúmenes y sin listeners 80/443. En `/opt/centenario/app` sigue el paquete `254ddab967989d161bc9ea39cfbb93cd8e683621`. `gh` 2.102.0 existe, **sin autenticación de usuario**. No se accedió por SSH mediante ChatGPT ni se montó QA. El archivo de credenciales de QA sintético `/etc/pmgm/srv01.env` se creó antes con root:root 600; no copiarlo ni registrarlo.

`dev@e1379d8b6543aa1ad518bf88d3d62834b9fb4fd8` ya aprobó CI, Silo suministro/recuperación, instalación QA empaquetada, HTTPS/OIDC, Pre-UAT y Showcase. `main` continúa bloqueada para modificaciones sin aprobación. El artefacto **completo** firmado (no el instalable de código ni Pages) es `qa-glmdch-signed-storage-e1379d8b6543aa1ad518bf88d3d62834b9fb4fd8`, run 38988506182, artifact 12084307024, caduca 2026-11-16.

## Primer componente implementado

`python3 scripts/qa/qa-agent-readiness.py` utiliza HTTPS público de GitHub, **sin autenticación**, para leer:

1. Commit exacto actual de `dev`.
2. Flujos completos `push` sobre ese mismo SHA para CI, Silo, instalable QA, HTTPS/OIDC y Showcase.
3. El único artefacto **no caducado** del flujo Silo con nombre exacto `qa-glmdch-signed-storage-` + SHA, metadatos y digest SHA-256.

No usa tokens, SSH, Docker, shell ni secretos. No descarga artefactos. Sólo produce `READY_FOR_SIGNED_STAGING_ONLY` o un código fijo de bloqueo, sin imprimir información sensible. Datos de API no son autoridad de identidad criptográfica: el siguiente módulo deberá **verificar firma Sigstore y manifiestos del zip exacto** antes de considerar un staging apto para instalación.

## Etapas pendientes antes de activar un temporizador automático

- Definir entrega desde repositorio público **sin token de escritura**: preferir release verificable desde CI, o credencial de GitHub *Actions:read* de alcance reducido en root-only; nunca login personal de escritura guardado por el agente.
- Incorporar test de firma, licencia, tar, SHA, rootfs y binario Silo; comparar SHA y atestación de workflow. Rechazar SHA retrocedido, changed base, runs ambiguos, expiración, digest distinto, ZIP parcial y cualquier URL/redirección inesperada.
- Preparar staging bajo ruta distinta de `/opt/centenario/app` sin sobreescribir el paquete actual, mantener `/etc/pmgm/srv01.env` intacto. Preparar recuperación, backups cifrados externos, políticas de retención, límite de disco y fallos simulados.
- Validar alcance 80/443, destino DNS/ingress HTTPS `https://sistema.granlogiamixta.cl/`, Caddy, Keycloak, BD2, G8, S3/IAM, backups, migraciones y salud. El resultado público anterior era HTTP 502.
- Sólo luego: introducir un servicio systemd y timer con permisos mínimos, pruebas de pausa/rollback, sin ejecutor genérico de PR, sin SSH inbound y con aprobación explícita para **primer arranque**. Cambios destructivos y migraciones requieren aprobación separada.
- Cada corte y bloqueo debe registrarse en [#97](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/issues/97), [#525](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/issues/525) y la Línea Base de Drive.

**No confundir** `READY_FOR_SIGNED_STAGING_ONLY` con despliegue, UAT ni QA funcional. El Sponsor no necesita instalar nada para revisar esta PR. No alterar `main` ni `srv01`.
