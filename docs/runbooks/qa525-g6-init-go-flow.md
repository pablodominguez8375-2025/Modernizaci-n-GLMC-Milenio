# QA525 — G6 `init` con GO firmado: flujo físico propuesto (CANDIDATO, NO AUTORIZA NADA)

Dictamen base: ChatGPT 7228721966 (diseño), 7231528897 (correcciones). Driver vigente en glmdch: `0ff26bf6c0637e9ea45e05086ba7744395c090aee3030d45018983e03f1fdccb`. Pin restic aceptado (gate PASS, #525 7231455677): `e0ba8fd58a7cb76707cbc064abb4ac72d5a91aa26041b9c4f763405684d00a41`. Operación init: `g6_operation_sha256=70da2dba…d817` (bytes `{"argv":["--restic-sha256","e0ba8fd5…0a41"],"g6_command":"init","schema":"qa525-g6-operation-v1"}`).

## Piezas
| Pieza | Dónde | Qué garantiza |
|---|---|---|
| `qa525-g6-go-build.py --host-epoch N` | off-host | bytes canónicos del GO (5 campos), nonce fresco, `expires_at = N + ttl` (≤900) con el reloj de glmdch |
| firma `ssh-keygen -Y sign -n qa525-go-g6-v1@pmgm` | Termux del Sponsor | clave OPS; Claude no la tiene |
| `qa525-g6-go-verify.py` | off-host (Claude) | formato canónico (parser del driver), huella OPS fijada, firma real, imprime `sha256` de `go.json` y `go.json.sig`, comprueba command/op/driver y TTL contra `--now` |
| `qa525-go-install.py install GO GOSHA SIG SIGSHA` | glmdch, `sudo` | copia exacta (hash verificado sobre los mismos bytes escritos) a `/var/lib/pmgm-qa525-manual-cut-e5c40d3a/` con `O_CREAT\|O_EXCL\|O_NOFOLLOW` por FD, 0600, nlink 1; destino existente = STOP; nunca borra/renombra/sobrescribe |
| driver `--g6 init …` | glmdch | revalida regular/root/0600/nlink 1, ubicación 0700, firma, hashes, TTL, nonce O_EXCL (replay = `CUT_G6_NONCE_REPLAY`) antes de cualquier dispatch |

## Secuencia (cada paso con PASS/STOP; un comando a la vez; `sudo -k` al final)
1. Autorización literal del Sponsor registrada en #525 (texto con `<HASH_DE_ESTE_TEXTO>` literal; hash = sha256 de los bytes UTF-8 exactos del texto registrado, sin salto final y sin vallas Markdown; ChatGPT lo recalcula antes de ejecutar).
2. Transferencia SFTP de `qa525-go-install.py` (hash verificado) — una sola vez.
3. `sudo -v`; `date -u +%s` en glmdch (N); Claude construye el GO (TTL ≤ 900) y el Sponsor lo firma en Termux.
4. El Sponsor adjunta `go.json.sig`; Claude ejecuta `qa525-g6-go-verify.py`, publica `sha256(go.json)`/`sha256(go.json.sig)`; ChatGPT coteja.
5. SFTP de `go-<nonce8>.json` + `.sig` a `/home/soporte`; `sudo python3 -I qa525-go-install.py install …` (O_EXCL); lectura de metadatos y hashes.
6. `init` (getpass doble de la clave; nunca por chat/argv/env/archivo/log/salida); salida íntegra sin clave; lecturas posteriores (`stat` de `key-v2` sin leer ni hashear; `ls -A`); `sudo -k`.
Si el GO expira: se descarta completo, nonce nuevo, se rehace desde el paso 3. Recibo `restic_repo_id`/`key_epoch` = recibo inicial, no pines definitivos de backup hasta registrar evidencia posterior y vínculo off-host.

## Siguiente tramo: gate RO de backup (candidato; no autoriza nada)
`scripts/qa/operators/qa525-g6-backup-gate-ro.py --repo-id H --key-epoch H --restic-sha256 H` (root, solo lectura; sin Docker ni restic; no lee `key-v2`): comprueba `key-v2` (40 B, 0600, nlink 1), `repo-v2`/`state-v2` (0700), `identity.json` canónico igual a los pines, `/usr/bin/restic` (regular, root, sin escritura g/o, nlink 1, sha256 = pin), Silo (dir real, dev:ino, owner/modo, archivos y bytes exactos, espacio libre ≥ 3×Silo + 2 GiB) y ausencia del watchdog anterior `pmgm-g6-minio-restart.{timer,service}`. Complementa al `preflight` G6 del driver (identidad de contenedores y bind del Silo). No cambia el driver ni las libs instaladas.
