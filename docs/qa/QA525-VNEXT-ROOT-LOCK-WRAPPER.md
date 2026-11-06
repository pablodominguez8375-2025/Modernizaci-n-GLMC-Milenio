# QA525 vNext – contrato del wrapper de 5 locks + lease raíz (INACTIVO)

Estado: biblioteca inactiva `scripts/qa/qa525-vnext-root-locks.py`. No accede a
ningún lock real, no tiene CLI ni puerto por defecto. No autoriza instalar,
aplicar ni operar en `glmdch`. Requiere revisión del puerto físico y GO del Sponsor.

## Qué fija
- Los 5 locks legacy exactos y su orden (igual a `qa525-vnext-locks.py`; un test lo compara).
- Cada candado: archivo regular, uid 0, modo 0600, nlink 1, no symlink; ancestros
  directorios uid 0, no symlink, sin escritura de grupo/otros. Única excepción:
  `/run/lock` con modo exacto 0700/0755/1777 (como `lock_parent` del instalador legacy).
- Apertura de solo lectura, identidad (dev, ino, uid, gid, modo, nlink) antes/durante/después del flock.
- Después de los 5, `journal.begin` (lease raíz). Falla -> código estático y se sueltan los locks.
- Liberación: lease primero, luego los 5 en orden inverso, aun si el cierre del lease falla.
- Si cualquier `close` del puerto falla, se intentan igualmente todos los demás fd y el lease, y se emite `ROOTLOCK_CLOSE_FAILED` (código fijo, sin texto externo): un lock podría seguir tomado, no se reporta éxito. Prevalece sobre el motivo previo (p. ej. `ROOTLOCK_BUSY`).
- `assert_held()` revalida identidad de cada lock y `lease_is_held`.

## Puerto inyectado (`euid, lstat, open_ro, fstat, flock_nb, unlock, close`)
Todo efecto pasa por él. El puerto físico (pendiente, NO incluido) deberá usar `os.open`
con `O_RDONLY|O_NOFOLLOW|O_CLOEXEC` y `flock(LOCK_EX|LOCK_NB)`, corrida como root, sin
registrar rutas o contenido externos.

## No cubre
Puerto físico, confianza PRE-root, CLI, fetch real, GO físico. Códigos: `ROOTLOCK_*`.
