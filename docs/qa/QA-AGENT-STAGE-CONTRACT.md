# Issue #525 — Etapa 2: sondeo seguro y preparación automática del candidato QA firmado

**Fecha:** 18-10-2026. **Entorno exclusivo:** QA sintético de `glmdch`. **Estado:** implementación propuesta en PR hacia `dev`, **sin despliegue ni timers habilitados**. `main`, producción y `srv01` no se tocan. Esta etapa extiende el planificador de solo lectura (#532/#535/#537) y la descarga/staging firmados (#526/#528).

## Qué añade

1. `scripts/qa/qa-agent-sync.py` coordina el planificador, verificador antirollback, selector exacto de `dev`, descarga Actions limitada y `qa-signed-staging.py`.
2. **Bloqueo de primera instalación:** requiere `last-deployed.json` confiable generado por operador después de QA sintético **físicamente** supervisado. Antes de tenerlo no consulta el candidato ni utiliza token.
3. Sólo `FAST_FORWARD` del commit de QA aceptado; si ya se instaló ese SHA no descarga ni hace nada.
4. El agente fija `source_sha`, `artifact_id` y SHA-256 antes, durante (dos lecturas de adquisición) y después de preparar el paquete; bloquea cambio de HEAD, artefacto/digest y cualquier respuesta incorrecta. Si cambia el baseline durante el staging, bloquea la operación.
5. Toda extracción verifica digest externo, atestación Sigstore del workflow fijo, `SOURCE_SHA` y manifiesto íntegro a través de módulos existentes; no considera confiable un ZIP de Pages.
6. `qa-artifact-download.py` conserva token privado con propietario EUID y modo restringido, además de aceptar **únicamente** la copia systemd `${CREDENTIALS_DIRECTORY}/qa-token` root:0440, ACL del servicio y montaje de credenciales real read-only. No se concede acceso al archivo root original.
7. Servicio y timer `pmgm-qa-agent-stage.*` son **plantillas no instaladas**, inicialmente deshabilitadas. Sólo producen staging privado del candidato. Nunca invocan Docker, `INSTALAR.sh`, migraciones ni el proxy público.

## Separación de permisos (diseño objetivo, todavía no habilitado)

- Programa de agente: código propiedad de root en `/opt/centenario/qa-agent`, no editable por `pmgm-qa-agent`.
- Cuenta `pmgm-qa-agent` sin sudo ni membresía docker, con sólo escritura sobre `/var/lib/pmgm-qa-agent/staging` (owner de la cuenta, modo 0700).
- Fuente original baseline `/var/lib/pmgm-qa-agent/last-deployed.json`: root:root 0600. Fuente token Actions:read-only `/etc/pmgm/qa-agent-actions-readonly.token`: root:root 0600. Ambas se entregan vía `LoadCredential` sin publicar sus contenidos; ningún token de escritura del repositorio ni de producción.
- Token de artefactos restringido por GitHub a repositorio/Actions:read, separado de la futura credencial para publicar evidencias.
- Primer arranque **manual supervisado**, con backup cifrado off-host real y restauración, gates G1–G8, smoke y política reversible comprobados; no crear baseline automáticamente ni inferirlo del éxito CI.

## Bloqueos físicos que esta etapa NO resuelve

- #97: stack QA todavía no operativo. Resultado último comprobado: Paso 40, imagen/binario Silo firmados verificados en contenedor efímero, cero contenedores/volúmenes persistentes.
- #470/#479: BD2, IAM y Silo QA sintético deben aprobarse en tiempo de ejecución; prohibida la imagen MinIO histórica bloqueada.
- G6: prueba Restic local sintética PASS; **no** hay backup coherente de 2 BD + Silo con transferencia cifrada a Drive y restauración externa validada.
- G3: dominio canónico `https://sistema.granlogiamixta.cl/` respondía HTTP 502 en la evidencia anterior; no abrir puertos públicos sin control del ingress.
- G8/G9/G10: roles, smoke por grado, primer despliegue y aceptación UAT pendientes.
- El motor de **cambio de versión real** con respaldo, transacción, rollback y health-check aún no está implementado. No conectar este sondeo a un instalador con privilegios Docker/root hasta contar con un ejecutor restringido y gate de recuperación/rollback independiente.

## Regla de seguridad para cualquier IA

Nunca habilitar `pmgm-qa-agent-stage.timer` ni insertar credenciales desde un chat. No fusionar #518 a main para resolver esta etapa, ni modificar `main`/`srv01` por conveniencia. Si una selección es inválida, cambió durante la descarga, falló la firma o falta el baseline, la operación termina con código `BLOCKED_QA_AUTOSTAGING`, sin ejecutar Docker. El staging de archivos **no equivale a una instalación, servicio operativo ni UAT**.

**Continuidad:** Issue #525 arquitectura y etapas, Issue #97 estado físico, `docs/qa/QA-AGENT-PLANNER-SYSTEMD.md`, `docs/qa/COMPROBAR-QA.md`, Línea Base QA en Drive. Siguiente PR: ejecutor local de actualización con operaciones allowlist, recibos de backup/restauración y rollback verificable; primera activación y acceso QA privado siguen bajo la decisión vigente de puesta en marcha supervisada.
