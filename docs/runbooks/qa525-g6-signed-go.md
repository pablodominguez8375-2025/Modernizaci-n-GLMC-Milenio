# QA525 — GO firmado de la ruta C (opción A, dictamen 7225915717 / 7225969321)

Estado: **código + pruebas. NO autoriza nada.** `G6_MODE_ENABLED=True` solo desde el commit de habilitación (revisado aparte; el modo sigue en STOP cerrado sin bytes pinados, host/root, GO firmado válido, locks y journal). El driver aún NO está instalado en glmdch (DRIVER-INSTALL pendiente). No se ejecuta init/G6.

## Qué exige ahora el driver (`--g6`)
`--g6 <comando> --authorization-sha256 <hex64> --go <ruta> --go-sig <ruta> [--opción valor]...`

| Requisito del dictamen | Dónde se cumple | Prueba |
|---|---|---|
| GO canónico de 5 campos exactos (`command, op_sha256, nonce, expires_at, driver_sha256`), sin extras | `g6_go_parse` (JSON re-serializado debe ser idéntico byte a byte: rechaza duplicados, espacios, saltos, escapes) | `test_signed_but_not_canonical_or_with_extra_missing_or_mistyped_fields` |
| Firma Ed25519 del Sponsor sobre los bytes canónicos; no se reimplementa Ed25519 | `_g6_verify_signature`: `ssh-keygen -Y verify` ejecutado por el FD del binario verificado por hash, `env={}`, dir 0700 transitorio, salida comparada exacta | `test_valid_signed_go…`, `test_wrong_key_wrong_namespace_and_unsigned_garbage` |
| Namespace nuevo y separado `qa525-go-g6-v1@pmgm` | `G6_GO_NAMESPACE`; un GO del corte (`qa525-go-cut-v1@pmgm`) no abre G6 | misma prueba |
| Clave OPS = huella fijada `SHA256:DN64Bjj/7SMjP7LX9Nwrcp3lPRBpT2cKPs1NmaL1Y5k` | `G6_OPS_FINGERPRINT`; `allowed_signers` debe ser exactamente una línea cuyo blob da esa huella | `test_signers_must_be_exactly_the_pinned_ops_key` |
| command / op_sha256 / driver_sha256 exactos | `g6_check_go` → `CUT_G6_GO_MISMATCH` | `test_signed_but_for_another_command_operation_or_driver` |
| nonce hex fresco, del GO, al journal y al O_EXCL | `main_g6`: `nonce = go["nonce"]` (el driver ya no genera nonce) | `test_valid_go_dispatches_and_its_nonce_is_what_the_journal_spends` |
| replay rechazado | `nonce-journal.consume` (O_EXCL) | `test_replayed_go_nonce_is_stop…` |
| `expires_at` entero UTC vigente, TTL ≤ 900 s contra el reloj de glmdch | `g6_check_go` + `run_g6(expires_at=go.expires_at)` (antes 3600) | `test_ttl_boundary_900_ok_901_rejected_and_past_rejected` |
| Se conservan 5 locks, `auth-<hex64>.spent`, journal antes de despachar, getpass doble | sin cambios | `test_bad_go_stops_before_any_marker_lock_or_dispatch` |

El GO se verifica ANTES de tomar locks, gastar autorización o consumir nonce. Un GO malo no deja ningún marcador.

## Pines (27-Nov): fijados con evidencia, no inventados
1. `G6_OPS_ALLOWED_SIGNERS`: línea **pública** entregada por el Sponsor desde Termux (`~/.ssh/qa525-ops.pub`); su huella calculada == `SHA256:DN64Bjj/7SMjP7LX9Nwrcp3lPRBpT2cKPs1NmaL1Y5k` (verificada con `ssh-keygen -l` y por el driver).
2. `G6_KEYGEN_SHA256 = sha256:2dd834413c9d3f4c0e3535b9fcf2e96e14c11f3d36fd23b6c5fc24e50411d051`: `/usr/bin/ssh-keygen` de glmdch, gate read-only PASS (#525 7226448792): no symlink, regular, root, 755, nlink 1, 457232 B. Va también en el manifest de DRIVER-INSTALL.
`G6_MODE_ENABLED=True` en el commit de habilitación; el hash final del driver sale de ese commit.

## Flujo del Sponsor (off-host) — actualizado 27-Nov (dictamen 7228721966)
1. En glmdch, justo antes: `date -u +%s` (el Sponsor pega el entero). El reloj válido es SIEMPRE el de glmdch.
2. `qa525-g6-go-build.py --command C --op-sha256 H --driver-sha256 H --host-epoch N [--ttl N≤900] [--out PATH]` (obligatorio `--host-epoch`; entero; construye los bytes canónicos con nonce fresco; no firma). `expires_at = N + ttl`.
3. Firma en Termux: `ssh-keygen -Y sign -f <clave OPS> -n qa525-go-g6-v1@pmgm <archivo>` → `<archivo>.sig`.
4. UNA subida SFTP de `go-<nonce8>.json` y `.sig` (nombres únicos) a /home/soporte y DOS `sudo install -o root -g root -m 0600` SIN sobrescritura hacia `/var/lib/pmgm-qa525-manual-cut-e5c40d3a/` (root 0700, HOST-PREP; separado de `journal/` y de los locks). El driver exige GO y firma DIRECTAMENTE en ese directorio (real, sin symlink, uid root, modo exacto 0700, nombre `[A-Za-z0-9][A-Za-z0-9._-]{0,79}`, nunca el journal), archivos root 0600/0400, nlink 1: `CUT_G6_GO_LOCATION_INVALID` en cualquier otro caso.
5. Si el GO expira, se DESCARTA y se construye uno nuevo (nonce fresco); un nonce nunca se reutiliza: replay = `CUT_G6_NONCE_REPLAY` (otros rechazos del journal = `CUT_G6_NONCE_REFUSED`), antes de cualquier dispatch.
6. Restic key (solo `init`): getpass doble en la Termius del Sponsor, después de todas las validaciones y antes del dispatch; nunca chat/argv/env/archivo/log/salida.
Presupuesto estimado: 6–8 intercambios (~6–10 min) dentro de 900 s.

## Qué NO hace este cambio
No instala el driver, no fija clave pública ni hash de ssh-keygen, no ejecuta nada en glmdch.
