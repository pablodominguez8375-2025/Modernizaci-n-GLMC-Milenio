# QA525 · Runbook de actualización manual supervisada (API+WEB) — lo que funcionó el 17/18-nov-2026

Para cualquier IA/chat/persona que deba actualizar QA525 a mano en glmdch si el operador firmado o el actualizador automático fallan. Caso real: corte da274d65 (ruta B, ejecutada con éxito). Registro: #525 / #1028. Scripts y pruebas: rama `claude/qa525-recovery-da274d65-20261118` del repo. No contiene secretos; nunca agregar claves, PIN ni credenciales aquí, en GitHub ni en el chat.

## 0. Cuándo usarlo
- El operador (`pmgm-qa-update`) dejó el journal `dual-<SHA>.json` en `RECOVERY_MANUALLY_REQUIRED` (p. ej. `DUAL_ROLLBACK_FAILED_MANUAL_RECOVERY_REQUIRED`), el nonce quedó consumido y `TRANSACTION_ALREADY_EXISTS_OR_INCOMPLETE` impide reintentar el mismo SHA.
- Las imágenes nuevas YA están construidas en Docker (revisión = SHA) y el QA vivo está sano en la versión anterior.
- Si NO se cumple lo anterior, no uses esta ruta: usa el operador firmado (ruta A: recuperación + reseña JSON + aprobación/nonce nuevos).

## 1. Reglas que no se negocian
- Un comando a la vez, etiquetado con plataforma (Termius = SSH a glmdch como `soporte`, puerto 22107; Termux = Android) y GUARDAR EN BITWARDEN / RESPALDAR / NO GUARDAR, con criterio PASS/STOP.
- Toda ejecución que modifica el servidor exige: (1) script en GitHub con commit y SHA-256 exactos, (2) pruebas herméticas, (3) A0 físico de solo lectura del MISMO hash, (4) dictamen GO exact-hash de ChatGPT publicado en #525, (5) autorización literal del Sponsor que nombre el hash, (6) UNA sola ejecución.
- Ante STOP o caída de consola: NO repetir. Inspeccionar recibos y reportar.
- No usar el Restic v1 comprometido ni `restic key add/passwd` sobre él; no tocar G6 v2 (inactivo), prod, main ni dev congelado; no tocar nonce, eventos ni prepared salvo que el procedimiento aprobado lo diga.
- Verificar la vigencia de G6 antes de empezar (el script se detiene con menos de 1 h de margen).
- Las reseñas técnicas en JSON las publica el Sponsor a mano (las herramientas de Claude agregan un pie).

## 2. Secuencia que funcionó (nombres del corte da274d65; cambiar SHA/imágenes en cortes futuros)
1. **Revisar antes de actuar:** estado vivo en memoria `areas/qa525-go-corte.md`, #525/#1028, y buscar el PRECEDENTE del mismo estado en #525 (el recuperador 6eed4f00 del 04-nov ya existía).
2. **Diagnóstico solo lectura** (con `sudo -n`; `soporte` no está en el grupo docker ni entra a `/opt/centenario/qa525-signed-prepare` sin sudo): contenedores+imágenes, journal, eventos, `prepared.json`, overrides, recibos. Pedir claves+tipos crudos antes de escribir cualquier gate.
3. **Script de aplicación manual v4** (`qa525-manual-apply-<SHA>-<fecha>.sh`): sin argumento = A0 solo lectura; `EXECUTE_MANUAL_APPLY` = aplica. Estructura: 5 locks (flock), gates de estado (5 registros, journal en RECOVERY, baseline como OBJETO por servicio, fingerprints de dependencias, revisión de imagen candidata, versión pública = anterior), prerequisitos (capas + 4 overrides image-only root 0600), `compose up --no-deps --no-build --pull never --force-recreate --wait` API y luego WEB, con `timeout` por llamada.
4. **Verificación ligada a la instancia nueva (la clave del éxito):** sondeo HTTP DIRECTO a la IP del contenedor (API :8080 `/health/ready`; WEB :80 `/health/web` y `/qa-version.json` = SHA), exigiendo ID de contenedor distinto del previo + imagen esperada + running + sin reinicios, con plazo global (480 s avance / 480 s rollback) y reintentos de 2 s. Luego salud pública y verificación final. Rollback verificado con el mismo criterio (WEB y luego API).
5. **Recibos** `manual-apply-<SHA>-<ts>.started.json/.result.json` con sha del script, hash de overrides, hash del journal, imágenes y contenedores antes/después, versión pública. Un recibo previo bloquea un segundo apply.
6. **Ejecución única** → `QA525_MANUAL_APPLY=PASS_VERIFIED` → **postflight independiente de solo lectura** (`sudo -n docker ps`, `docker inspect` de imágenes de api/web, `/health/ready` 200, `qa-version.json` = SHA, recibo `MANUAL_APPLY_VERIFIED_API_WEB`).
7. **Conciliar el journal** (paso OBLIGATORIO, separado, con su propio dictamen y autorización): `qa525-reconcile-<SHA>-<fecha>.sh` cambia SOLO `status` de `RECOVERY_MANUALLY_REQUIRED` a `APPLIED` (mismo mecanismo atómico del operador: `.next` O_EXCL + `os.replace` + fsync), con respaldo del original y recibo. NO archivar el journal: el operador exige journal `APPLIED` para toda capa de imagen heredada (`reviewed_layers` → `COMPOSE_HISTORY_NOT_SUCCESSFULLY_APPLIED`). No agregar evento `DUAL_API_WEB_APPLIED` (el operador solo escribe el log; el único lector es la herramienta de archivo de cortes pendientes y rechaza si existe evento del SHA). Dejar prepared/cut/aprobaciones/nonce como están.
8. **Postflight de la conciliación:** journal `APPLIED` (root 600, sin `.next`), respaldo `.orig` con el hash original, recibo `only_status_field_changed:true`, `dual-events.log` sin cambios, `sudo -n /usr/local/sbin/pmgm-qa-update --status` = `services_running:true`, `may_apply:false`, `archive_reconciliation_required:false`.
9. **Registrar:** comentario en #525 y #1028, memoria `areas/qa525-go-corte.md`, documento de continuidad en el Proyecto. Si se publicó una versión funcional nueva, dar el link de GitHub Pages (en este corte no hubo).

## 3. Causa raíz del fallo original (para no repetirlo)
El operador verifica la salud pública inmediatamente tras recrear la API (tarda ~6 s en escuchar y no tiene healthcheck Docker, así que `--wait` vuelve antes de que la app esté lista). El rollback verifica al instante y falla aunque el rollback físico sea correcto; el avance tenía ~1 s de margen. Precedente: 6eed4f00 (1.er intento falló, 2.º pasó). Los tests con Docker simulado no lo detectaron: ensayar con tiempos reales.

## 4. Errores cometidos y cómo se corrigieron (no repetir)
- Afirmé que las imágenes nuevas no existían → verificar con `docker image ls`/`inspect` antes de afirmarlo.
- No busqué el precedente en #525 primero → regla: buscar precedente del mismo estado antes de proponer ruta crítica.
- Leí `.baseline` como lista (era objeto por servicio) → el A0 se detuvo sin mutar; pedir claves y tipos crudos antes de escribir un gate.
- Mensajes STOP tragados dentro de `$(...)` → emitirlos a stderr (`>&2`).
- ChatGPT dio NO-GO tres veces a la ruta B (P0/P1 reales) → aceptar los hallazgos y versionar el hash; nunca ejecutar un hash dictaminado como NO-GO.
- Mi vigilante de comentarios filtró por fecha y no vio el dictamen → buscar por hash sin filtro de fecha.
- Un grep recursivo se colgó → comando liviano y Ctrl+C.
- Quise archivar el journal como conciliación (obsoleto y dañino tras el apply manual) → leer el código del operador (`reviewed_layers`) antes de elegir cómo cerrar el journal; probar compatibilidad con el código real.
- Mi chequeo de artefactos viejos bloqueó por el archivo histórico `route-a-recovery-*` del 04-nov (falso positivo) → los gates deben distinguir artefactos históricos legítimos de pendientes peligrosos (`.next`, marcador `expired-prepared-archive-pending.json`, `reconcile-*`); el A0 real lo detectó sin mutar.
- Un comando mío falló porque `soporte` no puede entrar a `/opt/centenario/qa525-signed-prepare` ni usar docker sin `sudo`: usar `sudo -n bash -c 'cd … && …'` y `sudo -n docker`.

## 5. Comandos de verificación rápida (solo lectura, Termius como `soporte`)
- Contenedores e imágenes: `sudo -n docker ps --format '{{.Names}} {{.Image}} {{.Status}}' | sort`
- Versión WEB directa: `curl -s --max-time 5 http://172.20.0.7:80/qa-version.json` (verificar la IP con `docker inspect` antes de confiar en ella).
- Salud API directa: `curl -s -o /dev/null -w '%{http_code}' http://172.20.0.6:8080/health/ready`
- Journal y recibos: `sudo -n bash -c 'cd /opt/centenario/qa525-signed-prepare && jq -c "{status,source_sha,new_images}" dual-<SHA>.json && ls -l --time-style=long-iso | grep -E "dual-|manual-apply|reconcile-"'`
- Estado del operador: `sudo -n /usr/local/sbin/pmgm-qa-update --status | jq -c .`

## 6. Referencias del corte da274d65
- Cut/release: da274d651d8e9e056525413406c0aab62131504f (outer sha256 f869f3e1…). Imágenes: API `09b66869…`, WEB `fb15b5c1…`; anteriores API `c620fd26…`, WEB `664c0e37…`.
- Aplicación manual v4: commit 301eef1e…, sha dc0c470c…; ejecución única PASS (comentario 6966395476), postflight (6966428945).
- Reconciliador v3: commit 0ae85695…, sha 8dd2329f…; dictamen #6966716591; ejecución PASS y postflight (6966791568, 6966847824).
- Recuperador `807ef482…` RETIRADO (archivar el journal rompe la cadena de capas).
- Siguiente fase: actualizador automático (#845/#1010) con los requisitos de `claude/qa525-lecciones-actualizador-auto.md`.
