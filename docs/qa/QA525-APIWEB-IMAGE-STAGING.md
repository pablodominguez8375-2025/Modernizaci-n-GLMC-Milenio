# QA525 — Staging de imágenes API+WEB antes del GO API_WEB_QA_ONLY

Estado: procedimiento de PREPARACIÓN. No es autorización de APPLY, no consume ningún GO y no toca contenedores, BD,
identidad ni volúmenes. Cada comando en glmdch requiere revisión de ChatGPT sin P0/P1 y OK puntual del Sponsor; Claude
no accede al host.

## Por qué existe
El vuelo ejecuta `docker compose up --no-build --pull never` para `api` y `web`: las imágenes target tienen que estar
ya en Docker. Los ids de imagen salen de `docker build` y no se conocen de antemano; por eso se fijan DESPUÉS del
staging, en la sección `apiweb.target.images` de la configuración que el Sponsor pinnea antes de `sudo`.

## Orden obligatorio (un cambio en dev invalida el staging)
1. Fusionar #1016 a dev (autorización expresa del Sponsor, CI exact-head verde, 0 P0/P1). Congelar dev: **el SHA X resultante es el corte**.
2. Release firmada de X (+ recibo OPS v3 y firma APP de X).
3. Staging de imágenes de X (este documento). Si dev avanza después, hay que repetir 2 y 3.
4. Pinnear en la configuración: `apiweb.target.images` (ids observados), `apiweb.target.web_sha` = X,
   `apiweb.target.layers`, `apiweb.baseline_sha256` (lectura solo lectura, ver abajo) y evidencia G6 v2.
5. GO LAUNCHER_ONLY (módulos nuevos pinneados) y luego GO API_WEB_QA_ONLY.

## Staging (operador BUILD_ONLY existente)
`scripts/qa/operators/qa525-current-image-preparation.py --prepare` (alcance `BUILD_ONLY_SIGNED_API_WEB_NO_CONTAINER_CHANGES_NO_APPLY`):
verifica la fuente firmada y el host, construye API y WEB con `--pull=false` y etiqueta `org.opencontainers.image.revision=<sha>`,
comprueba que el snapshot de los 7 servicios en ejecución es idéntico antes y después, y escribe `prepared.json`
(`may_apply=false`) con `api_image_id` y `web_image_id`.
- Predecesor APPLIED (caso 6eed4, hechos físicos 4/4 PASS): `prepared.json` existente con jornal `dual-<sha>` en `APPLIED`, ids de
  `prepared.json` == `journal.new_images` == contenedores vivos y salud pública del WEB anterior OK. En ese caso `--prepare`
  ya NO devuelve `PREPARE_ALREADY_EXISTS`: reutiliza `qa525-update.py::previous_prepared_is_applied` (solo lectura), exige que TODOS los
  jornales `dual-*.json` estén `APPLIED`, que no exista marcador `expired-prepared-archive-pending.json` ni reconciliación pendiente
  y que el SHA objetivo sea distinto. Solo DESPUÉS de verificar la release firmada/Sigstore y el host (dos veces), construir las imágenes y
  comprobar que el snapshot de los 7 servicios no cambió, archiva de forma atómica y durable (marcador write-ahead, renombrado a
  `review-archive-<hex24>`, fsync, marcador borrado como commit, rollback byte a byte ante fallo) `prepared.json` y las actas
  `/etc/pmgm/qa525-sensitive-reviewed.json`, `qa525-single-operator-postdb-approved.json` y `qa525-supervised-cut.json` que existan, y publica el
  nuevo `prepared.json` con un único rename. El jornal `dual-<sha>` y la evidencia se conservan. No aplica, no gasta GO, no toca contenedores.
- Autoridad sobre actas externas (`/etc/pmgm`): solo se archivan las actas del predecesor ya APPLIED (el acta de corte debe tener
  `target_sha` == SHA del predecesor, si no `PREPARE_ACTA_NOT_PREDECESSOR` y no se mueve nada). Nada se borra: todo queda en
  `review-archive-<hex24>`. Antes del primer cambio se exige mismo dispositivo (`st_dev`) entre estado y actas
  (`PREPARE_ARCHIVE_CROSS_DEVICE`, evita EXDEV no atómico). Se re-hashea lo archivado (TOCTOU) y justo antes de publicar se
  revalida snapshot Docker idéntico, salud del predecesor y `prepared.json` sin cambios (`PREPARE_RUNTIME_DRIFT_BEFORE_COMMIT`).
- Recuperación (marcador propio `prepared-successor-archive-pending.json`, estado `ARCHIVE_INCOMPLETE_NO_DEPLOYMENT`; bloquea
  `--prepare` y `qa525-update.py`): `qa525-current-image-preparation.py --recover-prepare-archive` (root, glmdch, locks, supervisado).
  Plan completo antes de mover: si el recibo nuevo está publicado (digest == marcador) avanza (roll-forward, antiguos quedan
  archivados); si no, devuelve cada archivo byte a byte (roll-back). Cualquier ambigüedad = STOP sin cambios y marcador intacto.
  El marcador se autentica por propiedad (root, 0600, nlink 1, tamaño acotado) y por allowlist estricta (`prepared.json` + las 3 actas, sin duplicados ni symlinks);
  una revalidación final verifica el predecesor en su ruta ARCHIVADA. NUNCA usar `--reconcile-pending-dual` para este marcador ni borrar/mover a mano.
- `--archive-expired-prepared` y `--reconcile-pending-dual` NO aplican a ese caso (son para actas vencidas/archivado interrumpido).
  **Prohibido** mover, renombrar o borrar `prepared.json` a mano y prohibido ejecutar el legado sin autenticar. Si queda
  `expired-prepared-archive-pending.json` tras un corte, usar `--inspect-pending-archive` y `--reconcile-pending-dual` (revisión firmada).
- Si el estado de 6eed4 NO cumple el contrato: STOP. Se usa `--status` (solo lectura, `archive_reconciliation_required`) y
  `--inspect-pending-archive` para ver un archivado interrumpido; la recuperación es `--reconcile-pending-dual` con revisión firmada,
  nunca una edición manual. Un archivado interrumpido deja el marcador y bloquea toda actualización hasta reconciliar.
- Una sola preparación target por SHA X: no se repite `--prepare` si ya existe un `prepared.json` válido para X.
- Pendiente de ChatGPT: confirmar que la cadena de fuentes pinneada de ese operador acepta el corte X (toma el «SHA firmado actual»).
- Lectura posterior (solo lectura, un comando por vez): `docker image inspect --format '{{.Id}} {{index .Config.Labels "org.opencontainers.image.revision"}}' <id>` para cada id de `prepared.json`.
  Los ids de imagen son públicos (no son secretos) y se registran en el handoff.

## Verificación previa al nonce y archivo de aborto (código, #1016)
Antes de gastar el GO (dentro de la verificación del GO, con los 5 locks retenidos) el host ejecuta `precheck`: (1) el archivo de aborto
`/root/.qa525-apiweb-abort` NO debe existir (cualquier tipo de archivo, también un symlink, cuenta como aborto; un error de lectura también);
(2) las dos imágenes target deben existir con el id exacto y la etiqueta de revisión esperada. Si algo falla: STOP con el GO sin consumir.
El Sponsor crea ese archivo (root, 0600) para detener un vuelo: bloquea solo las etapas hacia adelante, nunca el rollback.

## Verificación dentro del vuelo (código, #1016)
Antes de enlazar cualquier comando de mutación, `require_staged_images` (host) comprueba, dentro del plano retenido y por el
guardia de solo lectura (`image inspect` por id), que existan EXACTAMENTE `target.images.api` y `target.images.web` y que
sus etiquetas de revisión sean `app_source_sha` (API) y `target.web_sha` (WEB). Si falta una imagen o la etiqueta no coincide:
STOP antes de la primera mutación (`APIWEB_STAGED_IMAGE_MISSING` / `APIWEB_STAGED_IMAGE_LABEL_MISMATCH`).
Nada de esto construye, descarga, carga ni arranca nada.

## Baseline (lectura solo lectura, sin inventar valores)
`apiweb.baseline_sha256` = resumen canónico del baseline que `qa525-vnext-state-reader` produce al leer los 10 contenedores del proyecto
`centenario-qa-lab2` (inspector del host). El Sponsor lo obtiene con el inventario de solo lectura ya aprobado en la misma ventana en que
fija los layers; el vuelo lo vuelve a comparar bajo los 5 locks y se detiene si difiere.

## Captura del baseline canónico (helper de solo lectura)
`scripts/qa/operators/qa525-apiweb-baseline-capture.py --capture` (root en glmdch; único flag) lee los 10 contenedores de `centenario-qa-lab2`
con el MISMO guardia docker de solo lectura (`ps -a`, `inspect --format`, `image inspect --format`; argv exactos) y el MISMO
`StateReader.capture_baseline` del vuelo, dos veces; si las lecturas difieren, STOP. Imprime el baseline canónico y su resumen
`sha256:<hex>` = valor de `apiweb.baseline_sha256`. No escribe archivos, no arranca/detiene/construye/descarga nada y no gasta GO
(`may_apply=false`). Exige 2 targets running, 5 dependencias running y 3 one-shots `exited 0`; si no, STOP. Gate de root: ANTES de importar cualquier módulo exige `hostname == glmdch`, euid 0 y cadena de código confiable (el propio helper,
`qa525-vnext-host-adapters`, `-containment`, `-state-reader`, `-baseline-plan`: cada componente de la ruta root-owned, sin symlink, sin
escritura de grupo/otros); imprime el sha256 de cada archivo para compararlo con el manifiesto de la release firmada (evidencia, no una
raíz de confianza nueva). Los módulos se ejecutan con los MISMOS bytes que se hashearon, sin releer el archivo (excepción conocida: `containment`, que `host-adapters` carga desde disco bajo la misma cadena root-only). `docker` corre con `/usr/bin/docker` verificado (root, sin symlink, sin escritura de grupo/otros) y entorno limpio, sin `DOCKER_HOST`/`DOCKER_CONFIG`/`DOCKER_CONTEXT` del invocador. Ejecutar DESPUÉS de `--prepare`.
`apiweb.target.layers` es INDEPENDIENTE y sigue pendiente: NO se derivan copiando el baseline ni inventando overlays; se fijan desde las rutas
absolutas Compose efectivas y las imágenes target autorizadas, validadas con `baseline-plan.validate_target(base, target)` y una lectura física
bajo locks que compruebe los paths y la ausencia de migrator (decisión de ChatGPT).
