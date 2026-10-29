# QA525 — ensayo de restauración integral de dos bases, fuera de glmdch

**Estado:** PR #798, código sujeto a revisión, sin ejecución física ni evidencia de datos reales. Este operador solamente debe instalarse en **un laboratorio descartable separado**, nunca en el QA físico ni en producción.

## Arquitectura mínima de laboratorio

Crear fuera de `glmdch` un host aislado, con Docker/age y un contenedor PostgreSQL 17 del proyecto Compose `qa525-restore-lab`, servicio `postgres`, nombre exacto `qa525-restore-lab-postgres-1`. La configuración debe utilizar `network_mode: none`, **sin bind mounts**, sin contenedor privilegiado, sin modo PID/IPC del host, sin dispositivos de host ni puertos publicados. El único volumen admitido es `qa525-restore-lab_pgdata`, montado en `/var/lib/postgresql/data` con etiqueta Docker Compose de proyecto `qa525-restore-lab` y volumen `pgdata`; no puede estar asociado a ningún otro contenedor, aunque esté detenido. Preparar manualmente **dos bases VACÍAS** `qa525_verify_pmgm` y `qa525_verify_pmgm_keycloak`. No conectar ningún contenedor a redes de QA, producción o Internet, y no utilizar volúmenes existentes del Proyecto Centenario.

El operador `qa525-offhost-dual-restore-drill.py` verifica etiquetas de proyecto/servicio, estado y `NetworkMode=none` junto con mounts de volumen nombrado. Además comprueba que la referencia de imagen del contenedor sea `postgres:17@sha256:<digest>` y que su ID Docker sea un digest OCI completo: el nombre del contenedor por sí solo no basta, y esta verificación sintáctica tampoco acredita firma ni autenticidad del digest. También verifica aislamiento antes de cada lectura de catálogo y restauración, y al finalizar. Para impedir concurrencia, el custodio debe **preaprovisionar** el archivo `/var/lib/qa525-restore-lab/restore.lock` propiedad de root, modo `0600`, sin enlaces simbólicos ni hardlinks y con todos sus ancestros bajo control root; su bloqueo exclusivo `flock` es obligatorio durante todo el ensayo. No crea bases ni cambia configuración Docker: si el laboratorio no está aprovisionado correctamente, BLOQUEA. Su ejecución escribe SOLO en las dos bases de laboratorio, que deben estar vacías, y no intenta limpiarlas después: para repetir el ensayo se provisiona laboratorio NUEVO.

## Entrega cifrada y protección de claves

Desde el operador de respaldo #797 (si está integrado y autorizado para el QA físico) se generan dos archivos cifrados age y un `manifest.json` protegido. Transferir ambos `.age` y manifest a `/var/lib/qa525-restore-lab/incoming/backup-<UTC>-<token>` del host de restauración, directorios root/0700 y archivos root/0600. Validar permisos y checksum durante el traslado, sin subir datos ni claves a GitHub/Drive.

La **clave privada age** solo debe existir en el laboratorio/custodio externo, en `/etc/pmgm/qa525-restore-lab-age-identity.txt`, root/0600, sin symlinks ni hardlinks. No debe instalarse en `glmdch`. Conservar acceso del laboratorio restringido a personal autorizado, porque la restauración permite leer datos QA (que deben ser ficticios).

## Qué hace y qué NO certifica

Invocación futura, exclusivamente con autorización para el laboratorio descartable:

```bash
python3 /ruta-codigo-root-verificada/scripts/qa/operators/qa525-offhost-dual-restore-drill.py --restore-drill backup-AAAAmmddTHHMMSSZ-<token>
```

La ruta de código es ilustrativa. El proceso valida: recibo de origen marcado `ENCRYPTED_LOCAL...`, pareja exacta de archivos, tamaños y SHA-256 ciphertext; bases de laboratorio vacías, contenedor sin red y volúmenes propios. Luego **descifra en memoria por tubería**, ejecuta `pg_restore --list` para cada archivo y realiza `pg_restore --exit-on-error --no-owner --no-acl` en cada una de las dos bases del laboratorio. Nunca escribe un dump descifrado en archivos ni imprime catálogos/secretos. Requiere que aparezcan tablas de usuario en cada BD restaurada; no afirma equivalencia de negocio o permisos Keycloak completos.

Salida positiva: `OFFHOST_DUAL_RESTORE_DRILL_LOCAL_EVIDENCE_ONLY`, `may_apply:false`, `independent_offhost_custody_attested:false` y `repository_or_host_signed_evidence:false`. **No transforma una prueba de laboratorio en autorización automática del despliegue.** El operador responsable debe registrar evidencia independiente y sanitizada (hashes, estado del lab, fecha, tablas contadas, proceso de custodia, aprobación formal), **nunca** dump descifrado, contraseñas, keyfile, tablas de usuarios ni datos personales.

## Bloqueos y siguientes pasos

El primer apply real API+WEB aún exige revisión humana de Program.cs/UserAccounts/Keycloak, migraciones y Compose, release Sigstore exact-SHA, copia nueva de ambas BD con offhost/restore verificados, selección de imágenes y rollback real bajo locks, pruebas autenticadas y gobernanza #97/#525. Los antiguos operadores SHA fijos PR #771/#737/#766 y scripts `backup-srv01-qa.sh` no son aptos para el stack `centenario-qa-lab2`.

CI en `tests/test_qa525_offhost_dual_restore_drill.py` simula Docker/age y ensaya integridad, scope, copia ausente, modificación, no-emptiness, lab sin red, no-bind y rechazo de ejecución en glmdch. **CI NO realiza restauración real**, por lo que este paso seguirá pendiente hasta ejecutar el operador revisado en un laboratorio real aislado. No cambiar `main`.
