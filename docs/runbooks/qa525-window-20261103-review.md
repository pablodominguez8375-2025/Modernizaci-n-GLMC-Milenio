# QA525 — guion ÚNICO de revisión para ventana launcher-only 03-11-2026

**Estado del documento: BORRADOR. NO EJECUTAR pasos privilegiados.** Se solicita dictamen independiente de Claude y aprobación expresa, fresca, del Sponsor después de revisar gates y antes de una ventana física. Esta rama NO se debe fusionar antes del corte: cambiar `dev` invalida el SHA de la release firmada. **Solo `glmdch`; nunca `srv01`, main, producción, API/WEB, Docker/Compose, EF, Keycloak o SMTP.**

## Identidad del corte

- `dev=cfe4e6573ece2ae81414cc252047d36cc56e5c73`; `main=6dfb9546a4873baff15955cf86abfd7d47e3d111`.
- ZIP firmado `qa-signed-cfe4e6573ece2ae81414cc252047d36cc56e5c73.zip`, SHA-256 `fab78644c2fe95d2f747794c0f85b21cb6ed438679bb11fd629e9a9e54b5d358`.
- Auditor Sigstore+manifest **PASS sin root** en `/tmp/qa525-audit-EigD7wok`, 2146 archivos, manifest `133cd7f7d90fcdad7fbf2f480a05e2b84ecbcbc92b997ea58f3afb2d842f6ab6`. Bootstrap nuevo autenticado: SHA-256 `a5c76cfc0f94f0bf1c93fa2c83602bd1ead1ac61b65472f98f277042fa7f9769`. Instalador signed root: `dbfa762030b9ae45cfa64cb2ae72b65c4faff8fb28ec49072e0ea3c77eead65c`.
- Launcher físico antiguo SHA-256 `545d7faa2af91e33e54e90d25e913e85fc20ab465ef5f6c0e90489a77537c429`, `root:root 0700`.
- G6 02-11 local archivo SHA-256 **PASS**: `/home/soporte/centenario-qa-g6-CIFRADO-20261102T110545Z.tar.gz` con `772a645f09823648ed25a287f837653aef4cced2ba95413422d8f20f65a9d915`; informe aislado **PASS solo mismo host**, `production_touched=false`, DR otro host PENDING, snapshot atómico NOT_PROVEN, IAM NOT_INCLUDED.
- **Bloqueos actuales:** `/etc/pmgm/qa525-g6-reviewed.json` está vinculado a OTRO G6, no al de 02-11; copia off-host del archivo 02-11 NO PROBADA; `prepared.json` es del source anterior `a184936d`, `fresh_backup_created=false` en ese corte; `/opt/centenario/qa525-launcher-recovery` F1 AUSENTE; `/etc/pmgm/qa525-bootstrap-admission.json` y aprobación launcher `qa525-launcher-replace.json` AUSENTES. NO-GO físico.
- Comentario de Claude a revisar: [#525 / 6591739612](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/issues/525#issuecomment-6591739612). Evidencia G6 local: [#525 / 6591795211](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/issues/525#issuecomment-6591795211).

## PRE-ventana — un solo comando read-only, hoy permitido

Operador `soporte@glmdch`: copiar/revisar el código de [qa525-prewindow-readonly.sh](../../scripts/qa/operators/qa525-prewindow-readonly.sh) y ejecutarlo **sin root en el shell** (usa `sudo` solo para `stat`, `sha256sum`, `lslocks`, `find`, `jq` observacionales). No editar el resultado. Reporta flags sin nombres de nonce, contenido JSON ni IDs de Drive; `QA_PHYSICAL=NO_GO` deliberadamente permanente hasta acto separado. La comprobación es una **instantánea**: revalidar locks y hashes justo antes de ventana. Si falla una consulta, STOP.

Este archivo de script es un **borrador** (solo `bash -n` revisado en entorno de autoría); Claude debe revisar y el operador inspeccionar antes de utilizarlo, aunque sea de solo lectura.

## Guion de ventana — pasos futuros BLOQUEADOS hasta dictamen y gates

**Paso 0 — Sponsor valida alcance y DR; OPERADOR; salida GO_PREWINDOW solo con evidencia.** Confirmar copia off-host del **mismo G6 02-11**, SHA-256 comparado de forma independiente, y su file ID en Drive bajo custodia (NO publicar ID). El acta G6 histórica actual tiene 3 campos que **no** coinciden y no se modifica por atajo. Una nueva acta custodiada, versionada y aprobada deberá vincular exactamente `g6_archive_name`, `g6_archive_sha256`, `g6_restore_report_name`, `g6_drive_file_id` y `g6_offhost_sha256_verified` a la copia 02-11, con acta antigua conservada y hash registrado. **No transferir `true` desde G6 antiguo.** El Sponsor debe aceptar *por escrito para launcher-only* los límites DR: otro host PENDING, snapshot NOT_PROVEN, IAM NOT_INCLUDED. Si no hay copia verificable o aceptación, **ABORTAR**.

**Paso 1 — Ancla independiente antes del primer byte de Python root; operador con revisión Claude.** Confirmar código exacto y SHA anclado desde manifiesto auditado y revisión independiente del bootstrap; no confiar solo en que el propio programa se verifique. Con checks de ancestros `/opt`, `/opt/centenario`, `qa525-root-source` y `/etc/pmgm`, promover **sin sobreescribir** desde `/tmp` a archivos NUEVOS bajo `/opt/centenario/qa525-root-source` (root:root, 0600): ZIP `qa-signed-cfe4e6573ece2ae81414cc252047d36cc56e5c73.zip` y bootstrap `qa525-signed-root-bootstrap-cfe4.py`. Los comandos propuestos, **solo después de autorización para PRE y revisión independiente**, son:
~~~sh
SHA=cfe4e6573ece2ae81414cc252047d36cc56e5c73
SRC=/tmp/qa525-audit-EigD7wok
DST=/opt/centenario/qa525-root-source
sudo test ! -e "$DST/qa-signed-$SHA.zip" &&
sudo test ! -e "$DST/qa525-signed-root-bootstrap-cfe4.py" || exit 2
sudo install -m 0600 -o root -g root -T -- "$SRC/qa-signed-$SHA.zip" "$DST/qa-signed-$SHA.zip"
sudo install -m 0600 -o root -g root -T -- "$SRC/qa525-signed-root-bootstrap-cfe4.py" "$DST/qa525-signed-root-bootstrap-cfe4.py"
sudo sha256sum -- "$DST/qa-signed-$SHA.zip" "$DST/qa525-signed-root-bootstrap-cfe4.py"
~~~
La salida root debe corresponder EXACTAMENTE a los hashes fijados arriba antes de ningún `sudo python3`. Estos comandos no están autorizados ahora; el script original root `qa525-signed-root-bootstrap-prep.py` (sha `c10037ac...`) debe permanecer intacto. Si ruta, modo, hash o ancla no coinciden, ABORTAR, preservar fuentes para revisión.

**Paso 2 — F1; operador bajo aprobación de PRE.** Validar que `/opt/centenario/qa525-launcher-recovery` no existe ni como symlink. Luego `sudo install -d -m 0700 -o root -g root -- /opt/centenario/qa525-launcher-recovery` y `sudo stat -c '%u:%g %a %F' -- /opt/centenario/qa525-launcher-recovery`: esperar `0:0 700 directory`, vacío. Si existe de modo inesperado o con entradas, **no chmod/chown/limpiar**, ABORTAR.

**Paso 3 — Custodia prepared a184 y otros journals; operador.** No borrar ni editar `/opt/centenario/qa525-signed-prepare/prepared.json`: registrar SHA-256 root con `sudo sha256sum`, modo 0600, estado y vínculo anterior; no usar `fresh_backup_created=false` para actualizar G6 nuevo. **No archivar todavía manualmente:** el archivado seguro solo se realiza mediante la función revisada, firmada y expresamente aprobada `--archive-expired-prepared` cuando el launcher autorizado esté instalado y haya sido verificada su procedencia; no inventar operación de `mv` como sustituto. Si ese requisito impide avance, informar a Claude y **ABORTAR**, sin saltar el gate.

**Paso 4 — Admisión A fresca; operador, solo después 0–3.** Confirmar `/etc/pmgm/qa525-bootstrap-admission.json`, `qa525-recovery-bootstrap-admission.json` y `qa525-launcher-replace.json` ausentes; comprobar que `/opt/centenario/qa525-root-source/$SHA` no existe y los cinco locks sin ocupación. Con raíz PRE-root *ya* autenticada por herramientas root independientes, proponer:
~~~sh
sudo /usr/bin/python3 -I -S -B \
  /opt/centenario/qa525-root-source/qa525-signed-root-bootstrap-cfe4.py \
  --verified-envelope /opt/centenario/qa525-root-source/qa-signed-cfe4e6573ece2ae81414cc252047d36cc56e5c73.zip \
  --source-sha cfe4e6573ece2ae81414cc252047d36cc56e5c73 \
  --expected-zip-sha256 fab78644c2fe95d2f747794c0f85b21cb6ed438679bb11fd629e9a9e54b5d358 \
  --trusted-bootstrap-sha256 a5c76cfc0f94f0bf1c93fa2c83602bd1ead1ac61b65472f98f277042fa7f9769
~~~
Salida esperada `QA525_SIGNED_SOURCES_PREPARED_NO_LAUNCHER_CHANGE`, 12 fuentes, admisión root0600 TTL 900 s. **No es permiso de instalar.** Cualquier `BLOCKED_ROOT_BOOTSTRAP`, expiración o fuente drift => ABORTAR. Ejecutar solo en la ventana tras dictamen de Claude y aprobación de Sponsor.

**Paso 5 — Revalidar cinco locks y candidato/permiso launcher-only; operador + Sponsor.** Releer bloqueos bajo `flock` exclusivo del instalador, no confiar solo en `lslocks` histórico. Obtener `candidate_operator_sha256` desde preflight firmado, **NO inventarlo**. Sponsor prepara en privado `/etc/pmgm/qa525-launcher-replace.json`, root0600, con **exactamente**: `source_sha,release_asset_sha256,old_launcher_sha256,candidate_operator_sha256,scope,action,environment,operator,approved,issued_at,expires_at,nonce`. Contrato: `scope=QA525_LAUNCHER_ONLY_NO_DOCKER_NO_IDENTITY_NO_MIGRATION`; `action=replace-root-launcher-supervised`; `environment=QA_GLMDCH_ONLY`; `operator=soporte@glmdch`; `approved=true`; nonce único SHA256-hex; `0<expires_at-issued_at<=900` y vigente al instante de swap. Nunca publicar el nonce ni JSON privado; nunca reusar el acta anterior. Si candado ocupado, baseline cambia, preflight no coincide o aprobación no cumple, ABORTAR.

**Paso 6 — Único reemplazo root A; operador, tras aprobación fresca.** Desde fuente nueva root retenida y con admisión A todavía vigente:
~~~sh
sudo /usr/bin/python3 -I -S -B \
  /opt/centenario/qa525-root-source/cfe4e6573ece2ae81414cc252047d36cc56e5c73/scripts/qa/operators/qa525-launcher-supervised-root-install.py \
  --replace-approved
~~~
Exigir recibo durable `LAUNCHER_ATOMIC_SWAP_COMMITTED` y SHA launcher nuevo exactamente igual al del candidate aprobado; registrar hashes sin nonce. Si falla, **STOP manual reconciliation**; no relanzar, no ejecutar API/WEB, no rollback informal.

**Paso 7 — Postflight; operador.** Inspección `sudo sha256sum /usr/local/sbin/pmgm-qa-update` (hash aprobado), existencia/propiedad de receipt root, ausencia de intent residual, estado journal de rollback B y cinco locks; revisar `/health/web`, `/health/ready`, OIDC, identidad y API/WEB intactos, sin llamar actualización de servicios. No declarar QA desplegado: máximo **LAUNCHER_REPLACED_ONLY** si todo PASS; después pedir autorización independiente para API+WEB.

**Paso 8 — Vuelta atrás B; solo si hay corte A COMMITTED y evidencia completa.** Necesita **admisión B firmada y distinta**, confirmación de receipt A y backup old hash, **autorización root0600 separada** `/etc/pmgm/qa525-launcher-rollback.json`, nonce nuevo B, cinco locks y pruebas del operador. No retroceder a mano ni reconstruir un registro. Estados de interrupción F2: (a) intent sin backup ⇒ preservar/abortar, conciliación manual; (b) backup válido + launcher viejo + intent ⇒ abortar sin reintento; (c) backup válido + launcher nuevo + intent sin receipt ⇒ posible swap, revisión manual, no afirmar éxito; (d) receipt B + intent B pendiente ⇒ `ROOT_RECOVERY_PENDING_MANUAL_RECONCILIATION`, manual; (e) receipt B e intent .superseded coherentes ⇒ rollback B finalizado, nonce consumido, no repetir. Claude revisa tabla F2 del runbook original.

## Dictamen requerido antes de cualquier root

**Claude**: revisar este guion y script para contradicciones de orden, seguridad del ancla de primer byte, TOCTOU/links y copia de root, condición de archivado old prepared, política G6 offhost y limitaciones DR, admisión A TTL, aprobación fresca SHA/nonce y resultados de fallo/rollback F2. Dar **GO/NO-GO solo documental**, NO instalar. **Sponsor:** aprobación escrita posterior de PRE y, separadamente, de launcher-only; cualquier decisión no conforme debe reprogramarse. **Main y producción intactos.**