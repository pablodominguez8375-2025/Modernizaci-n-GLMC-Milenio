# QA525 — Contrato de evidencia G6 → A0 y matriz de parada (borrador para dictamen)

Derivado del productor `scripts/qa/qa525-vnext-g6-v2-producer.py` (constantes `REPO, KEY, STATE, EXPORTS, RESTORES, DOWNLOADS, STAGES`) y del
runner `qa525-vnext-g6-v2-run.py` (`STATE_ROOT=/var/lib/pmgm-qa525-g6-v2`). Fixture real: `tests/fixtures/qa525-a0-g6-inventory-v3-real-20261126.txt`.
Ningún gate certifica al siguiente ni cambia sus condiciones. Tras una ejecución física no se modifica el inventario para «hacer pasar» su salida.

## 1. Tabla de contrato (constante → tipo real → comprobación del productor → salida A0 v3 → estado observado)
| Constante | Tipo real | Comprobación del productor | Salida A0 v3 | Observado 2026-11-26 |
|---|---|---|---|---|
| STATE_ROOT | dir | `_layout`: ruta absoluta canónica, `_safe_dir` (dir, uid, sin 0o077) | `A0G6_STATE_ROOT=… SAFE` | dir uid0 0700 nlink 8 (árbol antiguo) |
| `key-v2` (KEY) | **archivo** | `_open_regular`: regular, uid, **modo exacto 0600**, nlink 1, fstat=lstat | `A0G6_KEYFILE_key-v2=…` (solo stat; nunca contenido) | file uid0 0600 nlink1 |
| `repo-v2` (REPO) | dir | `_safe_dir`; init lo crea 0700 y exige que NO exista | `A0G6_DIR_repo-v2=…` | dir 0700 nlink 7 (existe → bloquea init) |
| `state-v2` (STATE) | dir | `_ensure_dir`; init exige que NO exista | `A0G6_DIR_state-v2=…` + `_COUNT` | dir 0700, 5 entradas |
| `exports-v2` | dir | `_ensure_dir` | `A0G6_DIR_exports-v2` + `_COUNT` | 1 archivo cifrado (262909 B, 0600) |
| `restores-v2` | dir | `_ensure_dir`; destino de restore no debe existir | `A0G6_DIR_restores-v2_COUNT` | 0 |
| `downloads-v2` | dir | `_ensure_dir` | `A0G6_DIR_downloads-v2_COUNT` | 1 |
| `stages-v2` | dir | `_ensure_dir`; stage por backup no debe existir (`G6P_TARGET_EXISTS`) | `A0G6_DIR_stages-v2_COUNT` | 0 |
| `identity.json` | archivo en state-v2 | `_create_excl` 0600; contiene repo_id + key_epoch | `A0G6_EVIDENCE_identity.json=sha256… repo_id key_epoch` | repo_id b4df75c5…, epoch 75870a7a… |
| `receipt-<stamp>.json`, `review-<stamp>.json`, `isolated-restore-v2-<stamp>.report.json` | archivos en state-v2 | `_create_excl`; ventana G6 48 h | `A0G6_EVIDENCE_*` | stamp 20261113T165400Z (ventana vencida → G6 nuevo obligatorio) |

Conclusión: `init` exige `repo-v2`, `key-v2`, `state-v2` inexistentes; con el árbol 13-nov presente es imposible → gate G6-0 (retiro reversible por `rename` del árbol completo, raíz nueva vacía 0700). Con raíz nueva vacía `_layout` pasa (`_safe_dir`) y `init` crea key/state/repo; los demás directorios los crea `_ensure_dir`.

## 2. Separación de gates
- Código: parser, checklist, fixture, CI exact-head. – A0: inventario físico solo lectura (ejecutado una vez, v3, PASS; congelado).
- G6-0: retiro reversible (autorización propia; `preflight` imprime `G6_0_ROOT_BIND=dev:ino` just-in-time, exigido por `apply`/`rollback`).
- G6-1 init (clave nueva del Sponsor), G6-2 backup (ventana MinIO ≤600 s), G6-3 restore aislado, G6-4 review, … : cada uno con GO/nonce/TTL/autorización literal y evidencia propia.

## 3. Matriz de parada (cualquier dato desconocido = STOP; un STOP no se reintenta sin dictamen y autorización nueva)
| Gate | STOP | Causa | Acción |
|---|---|---|---|
| A0 | `A0G6_STOP:STATE_DIR_UNSAFE` | raíz/subdir ausente, symlink, uid/modo | conservar salida, no reparar en caliente |
| A0 | campo duplicado/malformado/contaminado (checklist `Anomaly`) | salida no cerrada | reprocesar fixture offline; no repetir A0 |
| G6-0 | `ROOT_LAYOUT_UNEXPECTED`, `KEY_METADATA_UNEXPECTED`, `SUBDIR_METADATA_UNEXPECTED`, `TREE_NOT_THE_INVENTORIED_ONE`, `RETIRED_TARGET_EXISTS`, `RENAME_REFUSED_ERRNO_*`, `MOVED_TREE_NOT_BOUND_ROLLED_BACK`, `APPLY_FAILED_*` | divergencia de árbol/carrera | sin cambios o revertido; nueva autorización |
| G6-0 | `…ROLLBACK_FAILED`, `ROLLBACK_VERIFY_FAILED` | recuperación incompleta | STOP duro, intervención del Sponsor con ChatGPT |
| G6-1 init | `G6P_INIT_TARGET_EXISTS`, `G6P_KEY_INVALID` (<32 B o NUL) | árbol previo / clave inválida | no crear nada |
| G6-2 backup | `G6P_TARGET_EXISTS` (stage), ventana MinIO >600 s | colisión / watchdog | restablecer MinIO por watchdog, evidencia |
| G6-3/4 | `G6P_RESTORE_TARGET_FORBIDDEN`, hash/ventana 48 h | restore fuera de `restores-v2` o ventana vencida | repetir ciclo con nonce nuevo |
