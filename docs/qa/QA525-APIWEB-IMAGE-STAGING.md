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
- Bloqueo conocido: si ya existe `prepared.json` (recibo del corte 6eed4) devuelve `PREPARE_ALREADY_EXISTS`.
  **Prohibido** mover, renombrar o borrar `prepared.json` a mano y prohibido ejecutar el legado sin autenticar. La única vía es el
  archivado autorizado ya existente en `scripts/qa/operators/qa525-update.py --archive-expired-prepared` (contrato estricto de
  `archive_expired_prepared()`): actas prepared/review/post/GO coherentes, nonce NO consumido, jornales reconciliados, imágenes
  prebuilt intactas, snapshot y salud del WEB anterior antes y después, marcador write-ahead durable
  (`expired-prepared-archive-pending.json`), fsync, y todas las actas quedan en `review-archive-*` (nada se borra ni se oculta).
  Requiere `--source`, `--digest` y `--operator-name soporte@glmdch` explícitos, autorización expresa del Sponsor y revisión de ChatGPT.
- Si el estado de 6eed4 NO cumple el contrato: STOP. Se usa `--status` (solo lectura, `archive_reconciliation_required`) y
  `--inspect-pending-archive` para ver un archivado interrumpido; la recuperación es `--reconcile-pending-dual` con revisión firmada,
  nunca una edición manual. Un archivado interrumpido deja el marcador y bloquea toda actualización hasta reconciliar.
- Una sola preparación target por SHA X: no se repite `--prepare` si ya existe un `prepared.json` válido para X.
- Pendiente de ChatGPT: confirmar que la cadena de fuentes pinneada de ese operador acepta el corte X (toma el «SHA firmado actual»).
- Lectura posterior (solo lectura, un comando por vez): `docker image inspect --format '{{.Id}} {{index .Config.Labels "org.opencontainers.image.revision"}}' <id>` para cada id de `prepared.json`.
  Los ids de imagen son públicos (no son secretos) y se registran en el handoff.

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
