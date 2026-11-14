# QA525 · Actualizador automático `auto-web` — Contrato v1.2 (normativo)

Issue: #1010 (F1 off-host). Base: cuerpo de #1010 (v1) + autorrevisión de ChatGPT v1.1 (comentario 6869253560) + revisión de Claude (comentario 6878486156: P1-1, P1-2, P1-3).
Estado: **contrato y código INACTIVOS**. Nada aquí instala, habilita el temporizador ni autoriza aplicar en glmdch. La activación requiere F2 sombra, F3 supervisada y la política firmada por el Sponsor.

## 1. Decisiones congeladas (de #1010, sin cambios)
- El host solo reconoce `auto-web`. Todo lo demás es `manual` (GO firmado como hasta hoy).
- `auto-web` cambia **solo** la imagen WEB, que CI arma y atesta por digest. En glmdch están prohibidos `docker build`, `npm install` y cualquier compilación.
- El actualizador no clasifica datos. El QA puede tener datos reales consentidos; ese consentimiento queda fuera del software.
- Política del Sponsor: 7 días, máximo 5 releases, con host, entorno, workflow y clasificador fijados.
- Un solo asset por release. El temporizador nace apagado.
- No hay token de escritura de GitHub ni de Drive en el host. Journal y recibo son de root.
- El rollback de WEB se hace una sola vez. Después el estado queda `BLOCKED`, con `critical=true` si el rollback falla.

## 2. Sobre `pmgm-qa-auto-web-envelope-v1` (contrato 1.2)
Código: `scripts/qa/qa525-vnext-release-envelope.py` (`build`, `check`, `release_id`, `build_args_sha256`).

| Sección | Campos exactos |
|---|---|
| raíz | `schema`, `release_id`, `repository`, `source_repository_id` (int), `environment="qa"`, `mode="auto-web"`, `sequence` (int), `issued_at`, `not_before`, `expires_at` |
| `installed` | `sha`, `web_image_digest`, `api_image_digest`, `compose_bytes_sha256`, `identity_digest`, `silo_image_digest` |
| `candidate` | `sha`, `web_image_digest`, `web_image_archive_sha256`, `web_sbom_sha256`, `base_image_digests{build,final}`, `build_args_sha256` |
| `lineage` | `merge_base`, `ahead_by`, `total_changed_files`, `changed_files_complete`, `changed_files[]` |
| `classifier` | `version`, `sha256`, `result`, `reasons[]` |
| `ci` | `workflow`, `workflow_ref`, `run_id`, `run_attempt`, `issuer`, `subject`, `source_sha` |

Cambios respecto de v1.1:
- `changed_files` pasa a ser una lista de objetos `{path, status, mode[, previous_path]}`, ordenada por `path` y generada con `git diff --raw --no-renames`, no con la API compare. Así se ven los symlinks (120000), submódulos (160000) y ejecutables (100755), que la API compare no informa.
- **P1-1:** se agrega `candidate.base_image_digests`. Si difiere de `allowed_base_image_digests` de la política, el release es `manual`, porque la base de nginx o node cambió sin ningún archivo modificado (`frontend/Dockerfile:1,24` va por tag).
- **P1-2:** se agrega `candidate.build_args_sha256`, el JSON canónico de los ARG `VITE_*`. Debe ser igual al de la política, y `VITE_USE_MOCKS != "false"` se rechaza siempre. Es la misma regla que ya aplica `scripts/qa/qa-lite-web-build.py`.
- `source_repository_id` es numérico, igual que en #989; un nombre de repositorio solo no basta.

Canonicalización: JSON ASCII con `sort_keys`, separadores compactos y salto final. Sin claves duplicadas, sin floats, sin NaN y con los campos exactos en cada nivel. `release_id` es el SHA256 de los bytes canónicos sin ese campo. Vigencia: `issued_at <= not_before < expires_at`, con un máximo de 72 h.

## 3. `expect`: lo que aporta el host, nunca el sobre
`check(raw, expect, classifier_module, now)` exige `expect` con estos campos exactos:
- `repository`, `source_repository_id`, `classifier_sha256`, `workflow`, `workflow_ref`, `subject`, `allowed_base_image_digests`, `build_args_sha256`, `first_sequence`, `last_sequence`: vienen de la **política verificada** (`qa525-vnext-policy.py`, ChatGPT).
- `installed`: son **mediciones frescas de solo lectura** del host (inspección docker y bytes de Compose). Los valores `installed.*` del sobre son esperados y deben coincidir byte a byte (v1.1).
- `last_applied_sequence`: viene del journal root del actualizador.

Decisiones posibles: `AUTO_WEB_CANDIDATE_CHECKED_NO_APPLY`, `MANUAL_NO_AUTOMATIC_ACTION` o `NO_OP_BEFORE_NOT_BEFORE`. Cualquier `Stop` es un rechazo (`reject`). Ninguna de estas decisiones autoriza aplicar.

## 4. Política del Sponsor v1.2 (verificador: ChatGPT)
Campos: `schema`, `policy_id`, `policy_generation` (monotónica por host y entorno), `host_id`, `environment="qa"`, `allowed_mode="auto-web"`, `classifier_sha256`, `allowed_workflow_ref` (ruta del workflow + `refs/heads/dev` + `source_repository_id`), `allowed_base_image_digests{build,final}` (P1-1), `build_args_sha256` (P1-2), `valid_from`, `expires_at` (≤ 7 días), `max_releases` (≤ 5), `first_sequence`, `last_sequence`, `nonce`.

La firma usa un namespace propio, distinto de OPS (`qa525-ops-v2@pmgm`) y de GO (`qa525-go-cut-v1@pmgm`). La revocación se hace instalando una generación mayor que autoriza cero releases.

## 5. Clasificador (código = norma; `classifier_sha256` lo fija)
Archivo: `scripts/qa/qa525-vnext-release-classifier.py` (versión 2).
- **Se permite:**
  - `frontend/src/**` con `.ts .tsx .css .svg .png .jpg .jpeg .webp .woff2`;
  - `frontend/public/brand/**` con `.svg .png .jpg .jpeg .webp .ico`;
  - imágenes sueltas en `frontend/public/*` (sin `.ico`).
- **Siempre `manual`:**
  - directorios `api/ auth/ oidc/ keycloak/ accounts/ config/ runtime/ env/` dentro de `frontend/src/`;
  - nombres con `auth oidc keycloak token secret password credential session env runtime sw vite manifest config`;
  - `main.*` y `*.d.ts`;
  - cualquier ruta fuera de las permitidas (`index.html`, `nginx*`, `*.conf`, `.env*`, Vite, `package*.json`, Docker, `.github/`, `scripts/`, `backend/`, `deploy/`…);
  - `.json` y `.md` dentro de `frontend/src`;
  - renombres, modos distintos de `100644`;
  - borrar archivos de `frontend/public/brand/` (identidad corporativa);
  - lista incompleta, `total_changed_files` distinto del número de entradas, lista desordenada o duplicada, y 300 archivos o más.

## 6. Tabla F1 (simulador `scripts/qa/qa525-vnext-autoweb-simulator.py`; pruebas `tests/test_qa525_vnext_release_envelope.py`)
| # | Caso | Resultado | ¿Gasta un release? |
|---|---|---|---|
| 01 | Solo UI, texto o imagen permitidos | auto-web | sí |
| 02 | Migración, API, Compose, identidad, Silo o secretos | manual | no |
| 03 | Archivo de la denylist, desconocido o binario | manual | no |
| 04 | No es fast-forward, merge-base distinto o `ahead_by<=0` | reject | no |
| 05 | Lista truncada, total distinto o límite | manual | no |
| 06 | `sequence` repetida o fuera de la ventana de la política | reject | no |
| 07 | Antes de `not_before` | no-op | no |
| 08 | Sobre vencido, política inválida o atestación inválida | reject | no |
| 09 | Workflow o clasificador no coinciden | reject | no |
| 10 | Digest de la imagen cargada no coincide | reject | no |
| 11 | Deriva de API, Compose, identidad o Silo (medida) | reject | no |
| 12 | Falla la salud de WEB/OIDC/versión o un invariante posterior | rollback único + BLOCKED | sí |
| 13 | Falla el rollback | BLOCKED `critical=true` | sí |
| 14 | Corte en APPLYING o VERIFYING | BLOCKED, sin reintento | sí |
| 15 | Dos ejecuciones concurrentes | no-op `LOCK_BUSY` | no |
| 16 | Host ya en BLOCKED | no-op (bloqueo persistente) | no |
| 17 | Se corta la red durante la descarga | no-op | no |
| 18 | Dos assets o asset sobre el tope | reject | no |
| 19 | Candidato igual al instalado | reject `NOT_DESCENDANT` | no |
| + | Cambio de base de la imagen (P1-1) o de su forma (Cmd/User/Env/capas) | manual | no |

## 7. Reparto (F1)
- **Claude** (PR #1012): este contrato, el clasificador, el sobre (`build`/`check`), el simulador con sus pruebas y la revisión exact-head de lo de ChatGPT.
- **ChatGPT:**
  - `qa525-vnext-policy.py`;
  - `qa525-vnext-auto.py` (orquestador oneshot que debe pasar los 19 vectores, con locks vNext y rollback único);
  - `.github/workflows/qa-release-envelope.yml` (arma la imagen con bases fijadas por digest y ARG fijos, `git diff --raw`, el sobre y la atestación);
  - el generador de kits y la unidad/timer apagados;
  - la medición de solo lectura de `installed` y de la forma de la imagen.

## 8. Lecciones de los actualizadores anteriores (obligatorias para `auto.py` y el workflow)
1. **NO QUEMAR:** validar todo (política, sobre, mediciones, imagen cargada) antes de gastar una `sequence`, un cupo de release o un nonce. Esto quedó codificado en la prueba `test_nothing_but_apply_paths_spend`.
2. **FUENTE ANTES QUE CÓDIGO:** las constantes (proyecto `centenario-qa-lab2`, servicio `web`, rutas de Compose, roles) se cotejan con el Compose y el `.env` reales mediante el inventario de solo lectura, no se suponen. Ejemplo: el rol `postgres` en lugar de `pmgm_app` (#1004).
3. **Sin capas huérfanas:** un rollback no puede dejar una capa Compose image-only colgando, como pasó con la capa 6eed4 en #525. El cambio de WEB se hace por digest con `compose up --no-build --pull never` y se verifica el estado después del rollback.
4. **Modos de archivo explícitos:** `sudo-rs` hereda `umask 077` (fallo de GO-H v6). Cada archivo que se escriba debe llevar `fchmod`/`O_CREAT` con su modo.
5. **Identidad de módulos:** cargar los módulos hermanos por hash fijado, no por `__file__` ni por rutas relativas (#1016).
6. **Una sola ruta:** no puede haber dos receptores ni dos actualizadores activos a la vez (#963/#966). `auto-web` comparte el plano de 5 locks y el launcher root.
7. **Higiene del repositorio:** nunca usar `git add -A` (se colaron `.pyc`); las clases de prueba vNext deben tener nombres únicos.
8. **La cuota de GitHub no gobierna nada:** en APPLY no hay red. La descarga ocurre antes y queda sellada.
