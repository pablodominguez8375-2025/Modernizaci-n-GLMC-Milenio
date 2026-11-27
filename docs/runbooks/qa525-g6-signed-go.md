# QA525 — GO firmado de la ruta C (opción A, dictamen 7225915717 / 7225969321)

Estado: **candidato de código + pruebas. NO autoriza nada.** `G6_MODE_ENABLED=False` (sin cambio). No se ejecuta init/G6; no hay cambios en glmdch.

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

## Pines que faltan (producción = STOP `CUT_G6_GO_VERIFIER_NOT_PINNED`; no se inventan)
1. `G6_OPS_ALLOWED_SIGNERS`: línea **pública** `qa525-ops namespaces="qa525-go-g6-v1@pmgm" ssh-ed25519 <KEY>\n` (la entrega el Sponsor o se lee con un gate read-only; su huella debe ser exactamente la fijada).
2. `G6_KEYGEN_SHA256`: sha256 del `/usr/bin/ssh-keygen` real de glmdch, demostrado en un gate read-only. Va también en el manifest de DRIVER-INSTALL.
Ambos se rellenan en un cambio revisado posterior; hasta entonces el driver final está bloqueado.

## Flujo del Sponsor (off-host) y límite práctico
1. `qa525-g6-go-build.py --command C --op-sha256 H --driver-sha256 H [--ttl N≤900]` (construye bytes canónicos, nonce fresco; no firma).
2. Firma en Termux: `ssh-keygen -Y sign -f <clave OPS> -n qa525-go-g6-v1@pmgm <archivo>` → `<archivo>.sig`.
3. Subir `go.json` y `go.json.sig` a glmdch; el driver las exige `root`, modo 0600/0400, nlink 1 (cada GO = comando por GO, TTL ≤ 900 s). **Riesgo operativo a decidir:** subir por SFTP a `/home/soporte` y pasarlas a root dentro de 900 s es ajustado; el paso de instalación a root es un comando privilegiado adicional por GO.

## Qué NO hace este cambio
No habilita `G6_MODE_ENABLED`, no instala el driver, no fija clave pública ni hash de ssh-keygen, no ejecuta nada en glmdch.
