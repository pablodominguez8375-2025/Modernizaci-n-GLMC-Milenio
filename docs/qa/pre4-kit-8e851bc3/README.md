# Kit PRE-4 — corte 8e851bc3 (LAUNCHER_ONLY) — AUTHORITY=NONE, NO_GO_ROOT_APPLY

Nada de este kit firma, admite, autoriza ni gasta un GO. Cada hash que imprime es CANDIDATO hasta que el Sponsor lo recompute en otro dispositivo y lo fije fuera de banda (Bitwarden). Ensayo off-host sobre el release firmado real: `RESULT PASS (35/35)` (`python3 rehearse.py outer.zip`, contenedor desechable, NUNCA en glmdch).

## Contenido (ver SHA256SUMS)
- `qa525-vnext-installer-manifest-build.py` — generador offline (#1022, sha256 `fd678da2…2839aa`; el kit se niega a usarlo si el hash difiere).
- `pre4_kit.py` — `facts`, `check-config`, `verify-manifest` (re-derivación INDEPENDIENTE, código propio), `stage`.
- `pre4_install.py` — crea el INSTALL_DIR nuevo (root, solo desde copia root-owned verificada por hash). Sin red, sin borrar nada.
- `pre4_check.py` — chequeo post-instalación con PIN oculto (sin argv).
- `launch-config.template.json` — las 17 claves; 6 campos `@@REQUIRED:…@@` que solo una autoridad puede llenar.
- `PATHS-PROPOSAL.md` — rutas/uids propuestos con su requisito de código y comandos de provisión (no ejecutados).
- `rehearse.py` — ensayo con llave y valores desechables.

## Campos que NO puedo ni debo inventar (config privado, no subir a GitHub/Drive)
| Campo | Quién |
|---|---|
| `pins.admission_sha256` | PO: acta canónica (`check-admission`) **firmada** por la llave Sponsor fijada: `ssh-keygen -Y sign -f <llave> -n qa525-launcher-admission-v1@pmgm acta.json` (en su dispositivo, p. ej. Termux) ⇒ `acta.json.sig`. `fill-config` verifica esa firma (origen) y exige `VALID_NOW` antes de calcular el hash. |
| `allowed_signers`, `authenticity.ops_allowed_signers` | PÚBLICA, derivada: el kit las ARMA con el formato exacto del verificador (`qa525-ops namespaces="<ns>" ssh-ed25519 <blob>`, ns GO `qa525-go-cut-v1@pmgm` y OPS `qa525-ops-v2@pmgm`) desde el archivo de llave pública que entregue el Sponsor (`~/.ssh/qa525-ops.pub` en Termux). Se NIEGA si su huella no es `SHA256:DN64Bjj/7SMjP7LX9Nwrcp3lPRBpT2cKPs1NmaL1Y5k` (la que ya verificó el recibo OPS). |
| `go_fingerprint` | **Prellenado = `SHA256:DN64Bjj…Y5k`.** El código (`root-go-verify`) compara AMBAS líneas de signers (GO y OPS) con este mismo valor ⇒ la llave GO y la OPS son la MISMA llave del Sponsor, en namespaces distintos. El PO lo confirma contra su Bitwarden. |
| `authenticity.drop_uid`, `drop_gid` | **Físico PASS, prellenado 65534/65534** (`getent`: nobody/nogroup, #6887553491; ≠ peer_uid 1000). Ya no es entrada de autoridad. |

`python3 -I pre4_kit.py fill-config launch-config.template.json /ruta/abs/launch-config.json --pub qa525-ops.pub --admission acta.json --admission-sig acta.json.sig` llena SOLO esas 3 entradas (admisión, 2 líneas de signers; `go_fingerprint` y la identidad de caída vienen del template) con esas fuentes explícitas (nada inventado) y escribe un archivo 0600 nuevo; luego `check-config` debe dar `CONFIG_OK keys=17`.

Rutas (`inbox`, `temp_root`, `journal_root`, `lease_dir`, `socket_path`, `app_archive_path`) y `peer_uid=1000` vienen PREllenados desde `PATHS-PROPOSAL.md` (propuesta con origen en código/hechos físicos; la aprueban ChatGPT/PO; provisión solo con root tras la compuerta).
`app_subject_sha256` = `9b47ac0d…2e58` ya vino prellenado: coincide con el `app-subject.json` REAL de glmdch (317 B, físico PASS); el Sponsor lo aprueba. El resto (13 valores públicos: SHAs del corte, plan `769c53b8…6063`, política, asset, launcher viejo, operador candidato, OPS `3771195a…`, firma `36875914…`, bundle `6da41f30…`, trusted-root `65ca537f…`, repo id) ya viene fijado y `check-config` los compara (MATCH/MISMATCH).

## Admisión LAUNCHER_ONLY (PROPUESTA, pendiente de dictamen ChatGPT/PO; `check-admission` solo valida forma)
El launcher NO abre este documento para LAUNCHER_ONLY: solo compara su sha256 con `pins.admission_sha256` (el valor entra al config => manifiesto => PIN). Por eso su vigencia es un contrato humano, no código: debe cubrir el tiempo de config+manifiesto+PIN+instalación (el TTL ≤ 900 s es del GO, que se emite al final). Una admisión de 900 s obligaría a repetir config/manifiesto/PIN si algo tarda. Forma canónica (JSON compacto, claves ordenadas, LF final, ≤16 KiB): `schema=qa525-launcher-admission-v1, purpose=LAUNCHER_ONLY, source_sha, release_asset_sha256, app_archive_sha256, plan_sha256, ops_v3_sha256, issuer (texto, p. ej. PO), issued_at, expires_at` (enteros UTC; 900 s ≤ vigencia ≤ 24 h). El PO la emite y fija su hash; `python3 -I pre4_kit.py check-admission FILE` imprime `ADMISSION_SHAPE_OK … admission_sha256=…` (candidato).

## Atajo rootless de un solo comando (v2.7, AUTHORITY=NONE)
`python3 -I pre4_run.py --work /home/soporte/pre4-work --outer outer.zip --admission acta.json --admission-sig acta.json.sig --pub qa525-ops.pub --python-sha <64hex> --gh-sha <64hex> --ssh-keygen-sha <64hex>` encadena los pasos 0-7 de abajo (integridad del kit, hash del asset, `check-admission`, `fill-config` que verifica la firma del acta, `check-config`, generador de manifiesto con pins del host, `verify-manifest` independiente y `stage` de 51 archivos) y se detiene en el primer error. Rechaza root, rechaza un `--work` existente y exige que los 3 SHA coincidan con los extremos publicados en el paso 3. NO sustituye al dispositivo B: el `PIN_CANDIDATE` impreso se recomputa allí y se compara a ojo antes de fijarlo en Bitwarden. No emite GO ni usa sudo. Ensayo off-host con llave desechable: PASS; negativos (ancla errónea, work existente, acta alterada, firma alterada) se detienen.

## Hoja del GO LAUNCHER_ONLY (v2.8, rootless, AUTHORITY=NONE)
`python3 -I pre4_gosheet.py --outer outer.zip --config launch-config.json --out /ruta/abs/NUEVO` arma SOLO los bytes canónicos del GO LAUNCHER_ONLY (módulo sponsor-signing tomado del release con hash fijado; campos pinneados desde el config instalado, cuyo sha256 `7a059c56…c98` se exige; nonce hex64 fresco; ventana ≤ 900 s que empieza AHORA). No firma, no usa llaves, no emite GO. Imprime la tarjeta de revisión y el código corto para comparar en dos dispositivos; el Sponsor firma en Termux con `ssh-keygen -Y sign -f ~/.ssh/qa525-ops -n qa525-go-cut-v1@pmgm go.json`. Ejecutarlo inmediatamente antes de firmar y copiar `go.json`+`go.json.sig` al inbox como lo ÚLTIMO.

## GO LAUNCHER_ONLY en dos pegadas (v2.9, AUTHORITY=NONE hasta que el Sponsor firma)
Requiere inbox con los 5 archivos pinneados (PRE_GO_INBOX_5=PASS), provisión PASS y `~/qa525-deviceB-a3451816/{outer.zip,launch-config.json}` en Termux.
1. **Termux:** `sh pre4_go_sign.sh` (arma el GO con `pre4_gosheet.py`, muestra tarjeta y SHORT_CODE, pide `FIRMO`, firma con la llave del Sponsor y verifica; envía `go.json`+`go.json.sig` por scp a un directorio NUEVO `go-in-<ts>` de glmdch). La ventana de 900 s empieza al armarlo.
2. **Termius (glmdch, soporte):** `sh pre4_go_launch.sh` (exige exactamente un `go-in-*`, verifica la firma y que queden >= 240 s, `check-admission`, pide `LANZAR`, instala `go.json`/`go.json.sig` en el inbox como root 0600 sin sobrescribir, arranca `pre4_sender.py` en segundo plano y ejecuta el `run` del bootstrap; el PIN se teclea oculto en el TTY).
`pre4_sender.py` envía el ZIP interno como memfd sellado por SCM_RIGHTS con el nonce y sha del GO; el receptor compara con sus propios valores verificados, así que no otorga nada. Ensayo off-host con el receptor real: entrega OK, nonce erróneo rechazado, sin socket → STOP. NO probado en glmdch.

## Procedimiento único (un comando a la vez; STOP ante cualquier otra salida)
**0. Dispositivo independiente A (Sponsor, sin root, fuera de glmdch)**
1. `sha256sum outer.zip` = `f0a018c8…0176`; Sigstore `gh attestation verify … --source-digest 8e851bc3…` exit 0 (comando del runbook).
2. `fill-config` (ver arriba) ⇒ `launch-config.json`; `python3 -I pre4_kit.py check-config launch-config.json` → `CONFIG_OK keys=17` (incluye: formato exacto de signers, misma huella en GO y OPS, hechos públicos coincidentes).
3. `python3 -I qa525-vnext-installer-manifest-build.py --asset outer.zip --asset-sha256 f0a018c8…0176 --source-sha 8e851bc3d2f1615a94009591f0781f88d6d3fe6f --config /ruta/abs/launch-config.json --interpreter /usr/bin/python3.14=52e0a13e…30cb41 --helper gh=/usr/bin/gh=7469124f…4c9b --helper ssh-keygen=/usr/bin/ssh-keygen=2dd83441…d051 --output /ruta/abs/manifest-launcher.json` (rutas ABSOLUTAS). **Pins del host: físicos PASS en glmdch (soporte, rootless, ChatGPT #6887458024): `/usr/bin/python3.14`=`52e0a13e…30cb41`, `/usr/bin/gh`=`7469124f…4c9b`, `/usr/bin/ssh-keygen`=`2dd83441…d051`; nunca los del contenedor de ensayo.**
4. `python3 -I pre4_kit.py verify-manifest manifest-launcher.json launch-config.json outer.zip f0a018c8…0176` → `MANIFEST_INDEPENDENT_OK … MANIFEST_SHA256=<PIN>`.
5. **Dispositivo B (otro):** repetir 3–4 y comparar a ojo los 64 hex. Iguales ⇒ el Sponsor fija el PIN en Bitwarden. Ese acto, y solo ese, es la autenticación del manifiesto.

**1. Staging en glmdch (soporte, sin sudo)**
6. Copiar al directorio privado `0700` de soporte: kit, `outer.zip`, `launch-config.json`, `manifest-launcher.json` (SCP/transporte rootless; no `inbox-launcher`).
7. `python3 -I pre4_kit.py stage outer.zip f0a018c8…0176 manifest-launcher.json launch-config.json /home/soporte/pre4-stage` → `STAGE_OK files=51`.

**2. Instalación (root, el Sponsor teclea; requiere TTY; aprobación explícita)**
0'. **Revalidación de la admisión INMEDIATAMENTE antes de cada paso privilegiado** (8b, provisión de rutas, emisión del GO): `python3 -I pre4_kit.py check-admission <acta>` debe imprimir `ADMISSION_SHAPE_OK VALID_NOW`; `ADMISSION_NOT_VALID_NOW` es **STOP**, nunca PASS. Ese resultado y el hash NO son permiso para sudo: la autorización humana del PO se confirma aparte (el launcher no abre ni verifica este documento).
8a. **Compuerta PRE-ROOT (solo utilidades root-owned; el instalador NO corre aún).** El hash de `pre4_install.py` (`sha256sum`, también en `SHA256SUMS` del commit del PR #1027) se obtiene en el dispositivo independiente A y se TECLEA aquí; no se lee de glmdch:
`sudo install -d -m 0700 -o root -g root /root/qa525-pre4-8e851bc3`
`sudo install -m 0600 -o root -g root -T /home/soporte/pre4-kit/pre4_install.py /root/qa525-pre4-8e851bc3/pre4_install.py`
`echo "<SHA256_DISPOSITIVO_A>  /root/qa525-pre4-8e851bc3/pre4_install.py" | sudo sha256sum -c -` → debe imprimir `OK`; cualquier otra salida = STOP (el archivo ya es copia root 0600 en dir root 0700: no hay TOCTOU entre verificar y ejecutar). Idem `pre4_check.py` (misma copia root 0600 + `sha256sum -c -` con su hash del dispositivo A) e intérprete: `echo "52e0a13e60a981d8c4b6478be2ba5176f69da07948a056bf49cf6f077e30cb41  /usr/bin/python3.14" | sudo sha256sum -c -`.
8b. `sudo /usr/bin/python3.14 -I -S /root/qa525-pre4-8e851bc3/pre4_install.py /home/soporte/pre4-stage` → pide la frase `AUTORIZO INSTALL_DIR 8e851bc3d2f1615a94009591f0781f88d6d3fe6f` y el PIN oculto (nunca argv). El script se niega a correr si no es una copia root-owned en directorio seguro (`INSTALLER_NOT_ROOT_OWNED_COPY`; guardia contra error del operador, no ancla de confianza). Crea `/opt/centenario/qa525-root-source/8e851bc3…/` (`mkdir` sin reemplazo, 51 archivos `root:root 0644`, dir `0755`), leyendo cada archivo UNA vez y escribiendo esos mismos bytes. Si falla, deja el directorio parcial (0700) y NO borra nada.
9. **Como root** (el padre `/opt/centenario/qa525-root-source` es 0:0 0700 —hecho físico— y NO se afloja: `soporte` no puede recorrerlo), con PIN OCULTO (no argv, no historial): `sudo /usr/bin/python3.14 -I -S /root/qa525-pre4-8e851bc3/pre4_check.py <DEST>` → `BOOT_CHECK_OK … files=48`. Ejecuta el mismo gate del bootstrap (`verify_install` + `materialize_config`) sobre los bytes del bootstrap con hash fijado, sin PIN en argv. (El `check` nativo exige el PIN en argv: NO usarlo.) No es GO.

**3. GO (fuera del kit)**
10. Sender (soporte) primero; GO `LAUNCHER_ONLY` TTL ≤ 900 s emitido último; luego `sudo /bin/sh -c 'umask 022; exec "$@"' sh /usr/bin/python3.14 -I -S -u <DEST>/qa525-vnext-root-bootstrap.py run <DEST> <DEST>/manifest-launcher.json` y el PIN por TTY. Luego `--prepare`/`--capture`, G6 v2 vigente y GO API_WEB (fases aparte).

## Qué prueba el ensayo (35/35) y qué no
Prueba: template rechazado con placeholders; config con hecho público erróneo o llave OPS no fijada rechazado; manifiesto de 48 archivos determinista y recomputado por código independiente; manifiesto alterado, PIN erróneo, frase errónea, archivo extra, symlink y módulo modificado en staging → STOP sin crear nada; instalador ejecutado como root desde ruta escribible por usuarios → STOP antes de leer staging; compuerta pre-root: instalador alterado NO pasa `sha256sum -c` y nunca se ejecuta (canario ausente), copia genuina pasa; instalación feliz; segunda instalación sobre directorio existente → STOP; `pre4_check` real = `BOOT_CHECK_OK files=48`, PIN erróneo se niega y el PIN nunca aparece en el argv del proceso.
No prueba (bloqueos reales): admisión, APP subject, llaves/huella GO, rutas/uids del host, pins reales del intérprete (en el ensayo se usan los de este contenedor), GO, `run` root, G6/API_WEB. No se ejecutó nada en glmdch.
