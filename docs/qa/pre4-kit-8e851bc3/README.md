# Kit PRE-4 — corte 8e851bc3 (LAUNCHER_ONLY) — AUTHORITY=NONE, NO_GO_ROOT_APPLY

Nada de este kit firma, admite, autoriza ni gasta un GO. Cada hash que imprime es CANDIDATO hasta que el Sponsor lo recompute en otro dispositivo y lo fije fuera de banda (Bitwarden). Ensayo off-host sobre el release firmado real: `RESULT PASS (18/18)` (`python3 rehearse.py outer.zip`, contenedor desechable, NUNCA en glmdch).

## Contenido (ver SHA256SUMS)
- `qa525-vnext-installer-manifest-build.py` — generador offline (#1022, sha256 `fd678da2…2839aa`; el kit se niega a usarlo si el hash difiere).
- `pre4_kit.py` — `facts`, `check-config`, `verify-manifest` (re-derivación INDEPENDIENTE, código propio), `stage`.
- `pre4_install.py` — crea el INSTALL_DIR nuevo (root). Una sola cosa, sin red, sin borrar nada.
- `launch-config.template.json` — las 17 claves; 14 campos `@@REQUIRED:…@@` que solo una autoridad puede llenar.
- `rehearse.py` — ensayo con llave y valores desechables.

## Campos que NO puedo ni debo inventar (config privado, no subir a GitHub/Drive)
| Campo | Quién |
|---|---|
| `pins.admission_sha256` | PO (admisión auténtica y vigente; se emite ANTES del manifiesto) |
| `app_subject_sha256` | PO (sha256 del `app-subject.json` auténtico) |
| `allowed_signers`, `go_fingerprint` | PO (llave GO pública y su huella desde Bitwarden) |
| `authenticity.ops_allowed_signers` | PO (llave pública OPS; el kit exige huella `SHA256:DN64Bjj/7SMjP7LX9Nwrcp3lPRBpT2cKPs1NmaL1Y5k`) |
| `inbox` (dir NUEVO, no `inbox-launcher`), `temp_root`, `socket_path`, `journal_root`, `lease_dir`, `app_archive_path` | ChatGPT/PO tras lectura read-only de glmdch |
| `peer_uid`, `authenticity.drop_uid`, `drop_gid` | PO (drop_uid ≠ peer_uid, > 0) |
El resto (13 valores públicos: SHAs del corte, plan `769c53b8…6063`, política, asset, launcher viejo, operador candidato, OPS `3771195a…`, firma `36875914…`, bundle `6da41f30…`, trusted-root `65ca537f…`, repo id) ya viene fijado y `check-config` los compara (MATCH/MISMATCH).

## Procedimiento único (un comando a la vez; STOP ante cualquier otra salida)
**0. Dispositivo independiente A (Sponsor, sin root, fuera de glmdch)**
1. `sha256sum outer.zip` = `f0a018c8…0176`; Sigstore `gh attestation verify … --source-digest 8e851bc3…` exit 0 (comando del runbook).
2. Llenar `launch-config.json` (copia del template) con los campos de la tabla. `python3 -I pre4_kit.py check-config launch-config.json` → `CONFIG_OK keys=17`.
3. `python3 -I qa525-vnext-installer-manifest-build.py --asset outer.zip --asset-sha256 f0a018c8…0176 --source-sha 8e851bc3d2f1615a94009591f0781f88d6d3fe6f --config /ruta/abs/launch-config.json --interpreter /usr/bin/python3.14=52e0a13e…30cb41 --helper gh=/usr/bin/gh=7469124f…4c9b --helper ssh-keygen=/usr/bin/ssh-keygen=2dd83441…d051 --output /ruta/abs/manifest-launcher.json` (rutas ABSOLUTAS; rutas de host por confirmar con inventario read-only).
4. `python3 -I pre4_kit.py verify-manifest manifest-launcher.json launch-config.json outer.zip f0a018c8…0176` → `MANIFEST_INDEPENDENT_OK … MANIFEST_SHA256=<PIN>`.
5. **Dispositivo B (otro):** repetir 3–4 y comparar a ojo los 64 hex. Iguales ⇒ el Sponsor fija el PIN en Bitwarden. Ese acto, y solo ese, es la autenticación del manifiesto.

**1. Staging en glmdch (soporte, sin sudo)**
6. Copiar al directorio privado `0700` de soporte: kit, `outer.zip`, `launch-config.json`, `manifest-launcher.json` (SCP/transporte rootless; no `inbox-launcher`).
7. `python3 -I pre4_kit.py stage outer.zip f0a018c8…0176 manifest-launcher.json launch-config.json /home/soporte/pre4-stage` → `STAGE_OK files=51`.

**2. Instalación (root, el Sponsor teclea; requiere TTY; aprobación explícita)**
8. `sudo python3 -I -S pre4_install.py /home/soporte/pre4-stage` → pide la frase `AUTORIZO INSTALL_DIR 8e851bc3d2f1615a94009591f0781f88d6d3fe6f` y el PIN oculto. Crea `/opt/centenario/qa525-root-source/8e851bc3…/` (`mkdir` sin reemplazo, 51 archivos `root:root 0644`, dir `0755`), leyendo cada archivo UNA vez y escribiendo esos mismos bytes. Si falla, deja el directorio parcial (0700) y NO borra nada.
9. Como soporte, sin sudo: `python3 -I -S <DEST>/qa525-vnext-root-bootstrap.py check <DEST> <DEST>/manifest-launcher.json <PIN>` → `BOOT_CHECK_OK … files=48`. No es GO.

**3. GO (fuera del kit)**
10. Sender (soporte) primero; GO `LAUNCHER_ONLY` TTL ≤ 900 s emitido último; luego `sudo /bin/sh -c 'umask 022; exec "$@"' sh /usr/bin/python3.14 -I -S -u <DEST>/qa525-vnext-root-bootstrap.py run <DEST> <DEST>/manifest-launcher.json` y el PIN por TTY. Luego `--prepare`/`--capture`, G6 v2 vigente y GO API_WEB (fases aparte).

## Qué prueba el ensayo (18/18) y qué no
Prueba: template rechazado con placeholders; config con hecho público erróneo o llave OPS no fijada rechazado; manifiesto de 48 archivos determinista y recomputado por código independiente; manifiesto alterado, PIN erróneo, frase errónea, archivo extra, symlink y módulo modificado en staging → STOP sin crear nada; instalación feliz; segunda instalación sobre directorio existente → STOP; `bootstrap check` real = `BOOT_CHECK_OK files=48` y con PIN erróneo se niega.
No prueba (bloqueos reales): admisión, APP subject, llaves/huella GO, rutas/uids del host, pins reales del intérprete (en el ensayo se usan los de este contenedor), GO, `run` root, G6/API_WEB. No se ejecutó nada en glmdch.
