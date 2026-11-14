# QA525 · Actualizador automático `auto-web` — Contrato v1.3 (normativo)

Issue: #1010 (F1 off-host). Base: cuerpo de #1010 (v1) + autorrevisión de ChatGPT v1.1 (comentario 6869253560) + revisión de Claude (comentario 6878486156: P1-1, P1-2, P1-3) + v1.3: 4 P1 de las revisiones de ChatGPT 5904808610/5904814986/5904937511 y su corrección cerrada (comentario 6878675183).
Estado: **contrato y código INACTIVOS**. Nada aquí instala, habilita el temporizador ni autoriza aplicar en glmdch. La activación requiere F2 sombra, F3 supervisada y la política firmada por el Sponsor.

## 1. Decisiones congeladas (de #1010, sin cambios)
- El host solo reconoce `auto-web`. Todo lo demás es `manual` (GO firmado como hasta hoy).
- `auto-web` cambia **solo** la imagen WEB, que CI arma y atesta por digest. En glmdch están prohibidos `docker build`, `npm install` y cualquier compilación.
- El actualizador no clasifica datos. El QA puede tener datos reales consentidos; ese consentimiento queda fuera del software.
- Política del Sponsor: 7 días, máximo 5 releases, con host, entorno, workflow y clasificador fijados.
- Un solo asset por release. El temporizador nace apagado.
- No hay token de escritura de GitHub ni de Drive en el host. Journal y recibo son de root.
- El rollback de WEB se hace una sola vez. Después el estado queda `BLOCKED`, con `critical=true` si el rollback falla.

## 2. Sobre `pmgm-qa-auto-web-envelope-v1` (contrato 1.3)
Código: `scripts/qa/qa525-vnext-release-envelope.py` (`build`, `check`, `release_id`, `build_args_sha256`, `image_binding`, `verify_loaded_image`).

| Sección | Campos exactos |
|---|---|
| raíz | `schema`, `release_id`, `repository`, `source_repository_id` (int), `environment="qa"`, `mode="auto-web"`, `sequence` (int), `issued_at`, `not_before`, `expires_at` |
| `installed` | `sha`, `web_image_digest`, `api_image_digest`, `compose_bytes_sha256`, `identity_digest`, `silo_image_digest` |
| `candidate` | `sha`, `web_image_digest` (Id = digest de config OCI), `web_image_archive_sha256`, `web_sbom_sha256`, `base_image_digests{build,final}`, `build_args_sha256`, `oci_shape_sha256`, `rootfs_diff_ids_sha256`, `base_diff_ids_sha256` |
| `lineage` | `merge_base`, `ahead_by`, `total_changed_files`, `changed_files_complete`, `changed_files[]` |
| `classifier` | `version`, `sha256`, `result`, `reasons[]` |
| `ci` | `workflow`, `workflow_ref`, `run_id`, `run_attempt`, `issuer`, `subject`, `source_sha` |

Cambios respecto de v1.1 (v1.2 y v1.3):
- `changed_files` pasa a ser una lista de objetos `{path, status, mode[, previous_path]}`, ordenada por `path` y generada con `git diff --raw --no-renames`, no con la API compare. Así se ven los symlinks (120000), submódulos (160000) y ejecutables (100755), que la API compare no informa.
- **P1-1:** se agrega `candidate.base_image_digests`. Si difiere de `allowed_base_image_digests` de la política, el release es `manual`, porque la base de nginx o node cambió sin ningún archivo modificado (`frontend/Dockerfile:1,24` va por tag).
- **P1-2:** se agrega `candidate.build_args_sha256`, el JSON canónico de **exactamente los 7 ARG** de `frontend/Dockerfile:8-14`. Si falta o sobra una clave, se rechaza. Reglas fijas (las de `qa-lite-web-build.py`): `VITE_USE_MOCKS="false"`, `VITE_API_BASE_URL=""`, emisor `https://`, `VITE_OIDC_CLIENT_ID="pmgm-web"`, `VITE_OIDC_SCOPE="openid profile"`, `VITE_OIDC_ALLOW_HTTP_LOCAL="false"` y `VITE_OIDC_ALLOW_HTTP_QA` en `true` o `false`. Los valores exactos los fija la política del Sponsor a partir del Compose real (FUENTE ANTES QUE CÓDIGO). El archivo `ops/qa525/autoweb/web-build-args.json` es la entrada del workflow; si no coincide con la política, el release se rechaza sin gastar nada.
- **v1.3 · Forma y capas OCI ligadas:** `image_binding(config, content_layer_count)` calcula tres hashes:
  - `oci_shape_sha256`: arquitectura, SO y `Cmd`, `Entrypoint`, `Env`, `ExposedPorts`, `Healthcheck`, `StopSignal`, `User`, `Volumes`, `WorkingDir`, sin `Labels`;
  - `rootfs_diff_ids_sha256`: todas las capas;
  - `base_diff_ids_sha256`: todas las capas menos las 2 de contenido (`COPY dist` y `RUN chmod`).

  La política fija `oci_shape_sha256`, `base_diff_ids_sha256` y `content_layer_count=2`. Si la forma o la base cambian, el release es `manual`. En el host, `verify_loaded_image` recalcula los tres hashes sobre la imagen **cargada**, antes de gastar nada; ya no hay un booleano.
- **v1.3 · `sequence` consecutiva:** `sequence == last_applied_sequence + 1` (`ENVELOPE_SEQUENCE_GAP`), dentro de `[first_sequence, last_sequence]` de la política.
- `source_repository_id` es numérico, igual que en #989; un nombre de repositorio solo no basta.

Canonicalización: JSON ASCII con `sort_keys`, separadores compactos y salto final. Sin claves duplicadas, sin floats, sin NaN y con los campos exactos en cada nivel. `release_id` es el SHA256 de los bytes canónicos sin ese campo. Vigencia: `issued_at <= not_before < expires_at`, con un máximo de 72 h.

## 3. `expect`: lo que aporta el host, nunca el sobre
`check(raw, expect, classifier_module, now)` exige `expect` con estos campos exactos:
- `repository`, `source_repository_id`, `classifier_sha256`, `workflow`, `workflow_ref`, `subject`, `allowed_base_image_digests`, `build_args_sha256`, `oci_shape_sha256`, `base_diff_ids_sha256`, `content_layer_count`, `first_sequence`, `last_sequence`: vienen de la **política verificada** (`qa525-vnext-policy.py`, ChatGPT).
- `installed`: son **mediciones frescas de solo lectura** del host (inspección docker y bytes de Compose). Los valores `installed.*` del sobre son esperados y deben coincidir byte a byte (v1.1).
- `last_applied_sequence`: viene del journal root del actualizador.

Decisiones posibles: `AUTO_WEB_CANDIDATE_CHECKED_NO_APPLY`, `MANUAL_NO_AUTOMATIC_ACTION` o `NO_OP_BEFORE_NOT_BEFORE`. Cualquier `Stop` es un rechazo (`reject`). Ninguna de estas decisiones autoriza aplicar.

## 4. Política del Sponsor v1.2 (verificador: ChatGPT)
Campos: `schema`, `policy_id`, `policy_generation` (monotónica por host y entorno), `host_id`, `environment="qa"`, `allowed_mode="auto-web"`, `classifier_sha256`, `allowed_workflow_ref` (ruta del workflow + `refs/heads/dev` + `source_repository_id`), `allowed_base_image_digests{build,final}` (P1-1), `build_args_sha256` (P1-2), `oci_shape_sha256`, `base_diff_ids_sha256`, `content_layer_count` (v1.3), `valid_from`, `expires_at` (≤ 7 días), `max_releases` (≤ 5), `first_sequence`, `last_sequence`, `nonce`.

La firma usa un namespace propio, distinto de OPS (`qa525-ops-v2@pmgm`) y de GO (`qa525-go-cut-v1@pmgm`). La revocación se hace instalando una generación mayor que autoriza cero releases.

## 5. Clasificador, perfil F4-A (código = norma; `classifier_sha256` lo fija)
Archivo: `scripts/qa/qa525-vnext-release-classifier.py`, versión 3, `PROFILE = F4A_STATIC_NON_EXECUTABLE`.

La primera activación (F4-A) **solo** admite contenido no ejecutable cuyos bytes CI identificó por número mágico (`sniff`: PNG, JPEG, WebP, WOFF2, ICO, máx. 2 MiB). Además, el tipo detectado debe coincidir con la extensión. Rutas:
- `frontend/src/**` con `.png .jpg .jpeg .webp .woff2`;
- `frontend/public/brand/**` con `.png .jpg .jpeg .webp .ico`;
- imágenes sueltas en `frontend/public/*` (sin `.ico`).

**Siempre `manual` en F4-A:**
- `.ts .tsx .css .svg .js .jsx .mjs .html .json .md` (contenido ejecutable o activo, `F4A_ACTIVE_CONTENT_MANUAL`). Una pantalla podría agregar `fetch`, importar un cliente de la API o tocar datos sin pasar por ninguna ruta de la denylist. Requieren un gate de contenido/E2E futuro.
- Borrados (`F4A_REMOVAL_MANUAL`, `BRAND_REMOVAL`), tipo detectado distinto del esperado (`CONTENT_KIND_MISMATCH`), renombres y modos distintos de `100644`.
- Directorios `api/ auth/ oidc/ keycloak/ accounts/ config/ runtime/ env/` y nombres con `auth oidc keycloak token secret password credential session env runtime sw vite manifest config`, más `main.*` y `*.d.ts`.
- Cualquier ruta fuera de las permitidas.
- Lista incompleta, total distinto, lista desordenada o duplicada, y 300 archivos o más.

Cada entrada lleva `{path, status, mode, content_kind}`. `content_kind` es `none` solo en los borrados.

## 6. Tabla F1 (simulador `scripts/qa/qa525-vnext-autoweb-simulator.py`; pruebas `tests/test_qa525_vnext_release_envelope.py`)
| # | Caso | Resultado | ¿Gasta un release? |
|---|---|---|---|
| 01 | Solo recursos estáticos no ejecutables (F4-A) | auto-web | sí |
| 02 | Migración, API, Compose, identidad, Silo o secretos | manual | no |
| 03 | Archivo de la denylist, desconocido o binario | manual | no |
| 04 | No es fast-forward, merge-base distinto o `ahead_by<=0` | reject | no |
| 05 | Lista truncada, total distinto o límite | manual | no |
| 06 | `sequence` repetida, con salto (≠ última + 1) o fuera de la ventana de la política | reject | no |
| 07 | Antes de `not_before` | no-op | no |
| 08 | Sobre vencido, política inválida o atestación inválida | reject | no |
| 09 | Workflow o clasificador no coinciden | reject | no |
| 10 | Digest, forma o capas de la imagen cargada no coinciden (`verify_loaded_image`) | reject | no |
| 11 | Deriva de API, Compose, identidad o Silo (medida) | reject | no |
| 12 | Falla la salud de WEB/OIDC/versión o un invariante posterior | rollback único + BLOCKED | sí |
| 13 | Falla el rollback | BLOCKED `critical=true` | sí |
| 14 | Corte en APPLYING o VERIFYING | BLOCKED, sin reintento | sí |
| 15 | Dos ejecuciones concurrentes | no-op `LOCK_BUSY` | no |
| 16 | Host ya en BLOCKED | no-op (bloqueo persistente) | no |
| 17 | Se corta la red durante la descarga | no-op | no |
| 18 | Dos assets o asset sobre el tope | reject | no |
| 19 | Candidato igual al instalado | reject `NOT_DESCENDANT` | no |
| + | Base distinta, forma OCI distinta o capas base distintas; TS/TSX/CSS/SVG en F4-A | manual | no |

## 7. Reparto vigente (ChatGPT, comentario 6878532693)
- **Claude** (PR #1012): este contrato, el clasificador, el sobre, `scripts/qa/qa525-vnext-release-envelope-ci.py` (productor CI), `.github/workflows/qa-release-envelope.yml` (solo `workflow_dispatch`; arma la imagen con bases resueltas por digest y `--pull=false`, los 7 ARG del archivo fijo, `git diff --raw`, el sobre, un único asset atestado y ninguna publicación), el simulador y sus pruebas. Aparte, el puerto WEB de mínima autoridad (#1019).
- **ChatGPT:** `qa525-vnext-policy.py`, el compositor oneshot `qa525-vnext-auto.py`, el generador manual, la unidad/timer apagados y los gates de infraestructura de #1021 (cadena multi-release, capacidad del plano físico, terminal ligado).

## 8. Lecciones de los actualizadores anteriores (obligatorias para `auto.py` y el workflow)
1. **NO QUEMAR:** validar todo (política, sobre, mediciones, imagen cargada) antes de gastar una `sequence`, un cupo de release o un nonce. Esto quedó codificado en la prueba `test_nothing_but_apply_paths_spend`.
2. **FUENTE ANTES QUE CÓDIGO:** las constantes (proyecto `centenario-qa-lab2`, servicio `web`, rutas de Compose, roles) se cotejan con el Compose y el `.env` reales mediante el inventario de solo lectura, no se suponen. Ejemplo: el rol `postgres` en lugar de `pmgm_app` (#1004).
3. **Sin capas huérfanas:** un rollback no puede dejar una capa Compose image-only colgando, como pasó con la capa 6eed4 en #525. El cambio de WEB se hace por digest con `compose up --no-build --pull never` y se verifica el estado después del rollback.
4. **Modos de archivo explícitos:** `sudo-rs` hereda `umask 077` (fallo de GO-H v6). Cada archivo que se escriba debe llevar `fchmod`/`O_CREAT` con su modo.
5. **Identidad de módulos:** cargar los módulos hermanos por hash fijado, no por `__file__` ni por rutas relativas (#1016).
6. **Una sola ruta:** no puede haber dos receptores ni dos actualizadores activos a la vez (#963/#966). `auto-web` comparte el plano de 5 locks y el launcher root.
7. **Higiene del repositorio:** nunca usar `git add -A` (se colaron `.pyc`); las clases de prueba vNext deben tener nombres únicos.
8. **La cuota de GitHub no gobierna nada:** en APPLY no hay red. La descarga ocurre antes y queda sellada.
