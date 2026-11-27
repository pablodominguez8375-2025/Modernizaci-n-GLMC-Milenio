# QA525 — Gate HOST-PREP (ruta C) y evidencia PRE-4 de la ruta manual

Estado: CANDIDATO. No autorizado, no ejecutado. Dictamen de ruta: PR #1095 7218335393 (ruta C manual del driver, sin launcher; `g6_pins=ABSENT` de da274d65 = N/A y vuelve a ser STOP si algún comando intenta ejecutar G6 vía launcher).

## Qué hace
`scripts/qa/operators/qa525-host-prep.py` (generado por `qa525-host-prep-build.py` desde `qa525-host-prep.template.py`; reproducible, test lo comprueba) lleva EMBEBIDAS las 11 librerías pinadas (7 `LIB_PINS` + 4 `G6_PINS`, idénticas byte a byte a dev@e5c40d3a): una transferencia = un hash.
- Crea `/opt/centenario/qa525-manual-cut-e5c40d3a/lib` (root 0755; 11 archivos 0644, nlink 1) y `/var/lib/pmgm-qa525-manual-cut-e5c40d3a/journal` (root 0700, vacío). NO instala el driver (su hash final se fija en su propio gate: hoy `G6_MODE_ENABLED=False`).
- Modos `preflight | apply <bind> | rollback <bind>`; bind = `dev:ino,dev:ino` de `/opt/centenario` y `/var/lib` impresos por `preflight` (just-in-time; cualquier cambio = STOP).
- FD-relativo con `O_NOFOLLOW`; `renameat2(RENAME_NOREPLACE)` con firma y errno cerrados. Nunca sobrescribe ni borra: todo destino debe estar ausente; apply fallido y rollback RENAMEAN lo creado a `.apply-failed-20261127` / `.rolledback-20261127` (nombre ocupado = STOP). Rollback exige conjunto instalado exacto + journal VACÍO (un journal usado nunca se toca).
- No ejecuta G6, no lanza procesos ni abre sockets (test AST de imports/llamadas).

## Salida cerrada
preflight: `G6HP_BIND=…`, `G6HP_PARENTS=…`, `G6HP_BUNDLE=files=11 hash=OK manifest_sha256=…`, `G6HP_TARGETS=ABSENT`, `G6HP_PREFLIGHT=PASS`.
apply: `G6HP_APPLIED=1`, `G6HP_LIB_DIR=dir uid=0 mode=0755 files=11`, `G6HP_JOURNAL_ROOT=dir uid=0 mode=0700 entries=0`, `G6HP_MANIFEST_SHA256=…`. Cualquier otra cosa: `G6HP_STOP:<CODE>` y rc 1.

## PRE-4 para ruta C: elemento → prueba (tests/test_qa525_manual_cut_g6_main.py, test_qa525_manual_cut_g6_adapter.py)
| Elemento exigido por el dictamen | Dónde se demuestra |
|---|---|
| bytes pinados (`G6_PINS`/`LIB_PINS`) | `test_host_root_and_pin_and_unexpected_failures_never_traceback` (pin distinto = STOP) + `Manifest.test_bundle_equals_driver_pins_and_repo_bytes` |
| hash de admisión driver\|policy\|auth\|op | `test_full_cycle_one_authorization_per_command_and_params_stage` |
| checks host/root | `test_host_root_and_pin_and_unexpected_failures_never_traceback` |
| cinco locks | `test_full_cycle_…` (5 locks tomados antes del despacho) |
| marcador `auth-<hex64>.spent` | `test_same_authorization_text_cannot_be_reused`, `test_full_cycle_…` (5 marcadores) |
| nonce O_EXCL fresco | `test_full_cycle_…` (5 nonces) |
| journal escrito antes de despachar | `test_full_cycle_…` y tests del adaptador (journal antes del runner) |
| modo apagado por defecto, gramática cerrada | `test_off_by_default_and_flag_is_false`, `test_grammar_is_closed` |

Palabra `Verified`/bootstrap: no usada; ninguna equivalencia implícita (doc `qa525-manual-cut-g6-trust-boundary.md`).
