# QA525 — ensayo completo fuera del host (10-11-2026)

**Qué es.** Recorrido real de la cadena `bootstrap run` (manifiesto v3 + config privada + PIN) → launcher →
autenticidad (OPS v3 con `ssh-keygen`, APP con `gh attestation verify` offline como uid sin privilegios) →
plano de 5 locks + lease → verificación del GO → journal de nonce → receptor del FD sellado → hijo, con los
**módulos reales** y el **ZIP real** de la release `qa-signed-c952a4a0…` (interior `ce080632…`, bundle
`0a8dc30e…`). Se hace en un contenedor desechable, como root, con una clave Ed25519 **desechable**. No es un
GO, no toca glmdch y no usa ninguna clave ni PIN del Sponsor.

**Por qué.** Las suites unitarias usan dobles en las uniones entre módulos. Este ensayo encontró tres fallos
que habrían detenido el primer GO en glmdch (dos de ellos después de la ceremonia completa del Sponsor):

| # | Dónde | Síntoma con el corte c952a4a0 | Corrección |
|---|-------|-------------------------------|------------|
| 1 | launcher → journal de nonce | `NONCE_BINDING_INVALID` en **todo** GO real: el launcher pasaba `ops_source_sha` (40 hex) y el journal solo acepta `sha256:<64>` | el binding usa `ops_v3_sha256` (digest pineado del recibo OPS firmado) |
| 2 | hijo del launcher | `CHILD_ARCHIVE_UNSAFE` **con el nonce ya gastado**: el ZIP atestado trae `storage-bundle/silo-qa-review-only.tar` de 229 MiB y el tope por miembro era 128 MiB | topes iguales a `qa-signed-staging` (1 GiB por miembro, 2 GiB total) |
| 3 | bootstrap, recibo `pre-root-trust` | `BOOT_IO_ERROR` en cualquier **segundo** `run` con el mismo manifiesto/PIN (el nombre del recibo es el hash de su contenido y se creaba con `O_EXCL`) | idempotente si el archivo existente es seguro y byte a byte igual; si no, `BOOT_RECORD_CONFLICT` |

**Resultado con las correcciones (sin parches en el arnés):**
1. `run` sin emisor: `LAUNCH_SOCKET_FAILED` a los 60 s, nonce sin gastar.
2. Segundo `run`, mismo manifiesto, PIN y GO, con emisor: `LAUNCH_DONE` / `BOOT_RUN_DONE`; launcher reemplazado por
   el shim que ejecuta el operador retenido en `/opt/centenario/qa525-signed-stage/<sha>/`; recibo, intent
   archivado y copia del launcher viejo en `qa525-launcher-replacement`.
3. Tercer `run`: `NONCE_REPLAY`.
4. G6 `preflight` por la misma cadena (GO G6 + `g6-operation.json`): llega al `g6-v2-run.py` real; fuera de glmdch
   se detiene en `G6RUN_HOST_REFUSED`, y con el hostname puesto imprime el informe de preflight.

**Hallazgos operativos (no son defectos de código):**
- La release no trae el **emisor sin privilegios** que entrega el ZIP interior por el socket (memfd sellado +
  marco `QA525-BND-V1:<nonce>:<sha>`). El arnés tiene uno de 20 líneas (`peer()`), reutilizable como bloque.
- El launcher espera al emisor **60 s** como máximo: hacen falta dos sesiones (root y `soporte`) y arrancar
  primero el emisor en bucle de reconexión.
- El GO LAUNCHER_ONLY y el de G6 valen **15 minutos** desde `issued_at`.
- `/var/lib/pmgm-qa525/pre-root-trust` debe existir (root 0700) antes del primer `run`.
- `preflight` devuelve 1 si el host no está listo (por ejemplo MinIO en marcha): gasta el nonce igualmente.

**Cómo repetirlo.** `docs/qa/qa525-offhost-rehearsal.py.txt` (renombrar a `.py`): necesita root, Python 3.12+
con `fcntl.F_SEAL_*`, `gh` con `attestation`, `ssh-keygen`, dos usuarios sin privilegios y el ZIP de la release
extraído. `prepare` arma instalación, inbox, config, manifiesto y PIN; `peer` es el emisor. SOLO en un
contenedor desechable: crea rutas reales (`/usr/local/sbin/pmgm-qa-update`, `/opt/centenario/...`).
