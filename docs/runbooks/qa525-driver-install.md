# QA525 — gate DRIVER-INSTALL (ruta C) — CANDIDATO, NO AUTORIZA NADA

Instala **un solo archivo**: `/opt/centenario/qa525-manual-cut-e5c40d3a/qa525-manual-cut-e5c40d3a.py` (root, 0644, nlink 1), con los bytes del driver final EMBEBIDOS en `scripts/qa/operators/qa525-driver-install.py` (generado por `qa525-driver-install-build.py`; reproducible; una transferencia = un hash). El script no importa ni ejecuta el driver, no lanza procesos, no abre sockets y no ejecuta G6.

## Modos (uno por proceso)
- `preflight` (solo lectura): imprime 6 líneas `G6DI_BIND=dev:ino` (identidad del dir base), `G6DI_PARENTS`, `G6DI_HOSTPREP` (base 0755 con `lib` + 11 libs por hash, journal root 0700 vacío), `G6DI_DRIVER=sha256=… size=…`, `G6DI_TARGET=ABSENT`, `G6DI_PREFLIGHT=PASS`.
- `apply dev:ino`: re-ejecuta preflight con ese bind (cualquier divergencia = STOP), crea el archivo O_EXCL|O_NOFOLLOW por FD del dir base, escribe, 0644, fsync, verifica bytes/uid/modo/nlink; si falla lo RENOMBRA a `.apply-failed-20261127` (renameat2 NOREPLACE) y lo informa. Salida: `G6DI_APPLIED=1`, `G6DI_DRIVER_FILE=…`, `G6DI_DRIVER_SHA256=…`.
- `rollback dev:ino`: solo si el archivo instalado tiene EXACTAMENTE los bytes embebidos; lo RENOMBRA a `.rolledback-20261127`. Nunca borra.

## Garantías (cada una con prueba en `tests/test_qa525_driver_install.py`)
Bytes embebidos == driver del repo; tabla de 11 libs == pins del driver == bundle HOST-PREP; HOST-PREP debe estar exacto (lib alterada/extra/faltante/modo, base con extras, journal no vacío = STOP); destino o nombres sobrantes ocupados = STOP; base symlink no se sigue; falla en medio = renombre (nunca borrado); rollback con bytes alterados = STOP; sin imports de proceso/red; sin exec/eval.

## Lo que NO es
No es una autorización. Antes de cualquier ejecución física: CI exact-head 6/6 del commit con el hash final del driver, dictamen de ChatGPT, texto de autorización revisado y autorización literal del Sponsor registrada en #525 (una transferencia, un preflight; luego apply con el bind JIT; `sudo -k`). No habilita init/G6: el driver instalado exige además GO firmado (OPS), nonce, TTL ≤ 900 s, locks y journal.

## Nota 27-Nov (cambio de driver tras DRIVER-INSTALL)
El driver instalado en glmdch (sha256 c9cd98c7…1962) fue superado en el repo por el cambio revisado (código CUT_G6_NONCE_REPLAY, comprobación de ubicación del GO, `--host-epoch`): nuevo hash del driver y nuevo instalador. Reemplazar requiere un camino sin borrado (renombrar el driver anterior verificado por hash a un nombre `.superseded-…` y crear el nuevo) con autorización propia; el preflight actual exige la base exacta, así que NO sirve tal cual. Pendiente de dictamen de ChatGPT.
