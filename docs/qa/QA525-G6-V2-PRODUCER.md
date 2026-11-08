# QA525 G6 v2 — productor (biblioteca INACTIVA, sin ejecución)

Implementación nueva desde el contrato de `QA525-G6-RESTIC-ROTATION.md`; no deriva de los scripts root v1 del host. Nada aquí se instala, ejecuta ni toca glmdch. Siempre NO-GO.

## Modos
- **INIT** (una vez): clave, repo y `state-v2` deben no existir; clave `0600`, archivos con `O_EXCL|O_NOFOLLOW`; identidad canónica `identity.json`.
- **BACKUP**: exige repo+clave+identidad existentes, dueño/modo seguros, sin symlink (`fstat` = `lstat`), y **iguales a dos pines independientes** más el id vivo de Restic. No acepta rutas arbitrarias: un *captor* inyectado deja en `stages-v2/<ts>` exactamente `pmgm.dump`, `pmgm_keycloak.dump`, `globals.sql` y `silo/`; el productor verifica allowlist, solo ficheros regulares, no vacíos, 0600, hashea, escribe `manifest.json` (mismo corte) y solo entonces hace snapshot de ese stage (se obtiene el `snapshot_id`), `check`, y transmite en streaming (tope incremental, `O_EXCL`, limpieza ante fallo) el repo CIFRADO a `exports-v2`. El stage en claro se borra siempre. Registro privado `capture-<ts>.json` con el snapshot.
- **RESTORE**: parte **solo** de la copia **descargada** (`downloads-v2/<archivo>`), abierta con fd retenido, SHA pinneado, extraída por un extractor propio (solo ficheros y directorios, nombres validados, sin enlaces, `O_EXCL`, topes) a un repositorio privado `restores-v2/<ts>/repo`; luego `restic check` y `restic restore <snapshot pinneado>` contra ese repo extraído. **El repositorio vivo nunca se usa.** El hash se recalcula durante la extracción (cierra el cambio entre hash y uso). Ventana: corte ≤ ahora < corte + 48 h.
- **REVIEW** (`finalize_review`): hashea aquí el archivo descargado, exige receipt y reporte de restore existentes, canónicos y coherentes (hash, repo, epoch), y solo entonces escribe el review.

## Rutas y esquemas
Solo nombres v2 bajo una raíz inyectada (la ruta no puede pasar por symlinks). Esquemas `qa525-g6-v2-{review,receipt,restore}` con los campos exactos del contrato de #941; internos: `-identity`, `-capture-record`, `-capture`. Nombres `centenario-qa-g6-v2-CIFRADO-<ts>.tar.gz` / `isolated-restore-v2-<ts>.report.json`.

## Qué NO es
`ResticCli` fija el binario por SHA-256 (se re-hashea en cada llamada; no elimina la carrera hash→exec, el bootstrap root debe autenticarlo) y pasa la clave solo por ruta. El **captor** (pg_dump x2 + globals + silo) y el **verificador** (Postgres/silo reales) son adaptadores inyectados NO autenticados aquí: el reporte `PASS` solo prueba que el archivo descargado contiene un repo Restic restaurable, no que las bases sean las de producción. Eso es el adaptador físico root (bajo #936/#822). Hash de Drive y Sponsor no se autentican aquí.
