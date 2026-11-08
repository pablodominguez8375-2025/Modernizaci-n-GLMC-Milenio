# QA525 G6 v2 — productor (biblioteca INACTIVA, sin ejecución)

Implementación nueva desde el contrato de `QA525-G6-RESTIC-ROTATION.md`; no deriva de los scripts root v1 del host. Nada aquí se instala, ejecuta ni toca glmdch.

## Modos
- **INIT** (una vez): clave, repo y `state-v2` deben no existir; clave `0600` y archivos con `O_EXCL|O_NOFOLLOW`; identidad canónica `identity.json`.
- **BACKUP** (posterior): exige repo+clave+identidad existentes, dueño/modo seguros, sin symlink, y **iguales a dos pines independientes** (`restic_repo_id`, `key_epoch`) más el id vivo de Restic. Nunca inicia el repo ni sobrescribe. Exporta solo el repositorio CIFRADO como tar.gz determinista `centenario-qa-g6-v2-CIFRADO-<ts>.tar.gz`; receipt canónico `receipt-<ts>.json`.
- **RESTORE**: directorio nuevo y aislado, bajo prefijos prohibidos controlados por el llamador; el verificador (Postgres/silo) se **inyecta**; report canónico `isolated-restore-v2-<ts>.report.json`.
- **REVIEW** (`finalize_review`): solo si el SHA del archivo **descargado de Drive** es igual al del archivo local.

## Rutas y esquemas
Solo nombres v2 bajo una raíz inyectada (`repo-v2`, `key-v2`, `state-v2`, `exports-v2`, `restores-v2`). Esquemas `qa525-g6-v2-{review,receipt,restore}` con los campos exactos del contrato de #941 (más `qa525-g6-v2-identity`). Los nombres usan el prefijo `-v2-`; #941 aún valida patrones v1, hay que alinear al converger.

## Límites
`ResticCli` pasa la clave solo por ruta (`RESTIC_PASSWORD_FILE`), entorno vacío salvo lo mínimo, sin stdin, sin stderr, argv fijo. No hay entrada física: la raíz, el binario, uid, fuentes y el verificador real los entrega el entorno de instalación bajo su propio GO. No autentica Drive, Sponsor ni APP/OPS (#936). Siempre NO-GO.
