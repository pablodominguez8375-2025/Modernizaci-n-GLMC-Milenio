# QA525 G6 v2 — Punto de entrada root (`qa525-vnext-g6-v2-run.py`, INACTIVO en dev)

`scripts/qa/qa525-vnext-g6-v2-run.py` une el productor (#942), el captor (#952) y el verificador (#955) en un solo
comando para el operador. **No es un GO, no autoriza respaldo ni restore y no se ejecutó contra glmdch.** CI solo
corre sus pruebas herméticas (docker y restic falsos, directorios temporales).

## Subcomandos (gramática cerrada: sin abreviaturas, sin argumentos posicionales, sin variables de entorno)
| Comando | Qué hace | Escribe |
|---|---|---|
| `preflight [--restic-sha256 H] [--image I]` | Solo lectura: hash de docker y restic, identidad de `postgres` y `minio`, si MinIO está detenido, censo del Silo (sin enlaces, topes), espacio libre, imagen local vs digest, constantes `fcntl` de sellado, hash de los 4 archivos | nada |
| `init --restic-sha256 H` | Crea repo y clave Restic nuevos (una sola vez). La clave se pide **dos veces sin eco**, nunca por argumento | `STATE_ROOT/{repo,key,state}` |
| `backup --restic-sha256 --repo-id --key-epoch` | Volcado de `pmgm` y `pmgm_keycloak` + globals + copia del Silo → snapshot Restic → archivo cifrado `exports-v2/` + recibo | stage temporal (se borra), `exports-v2`, `state-v2` |
| `restore --restic-sha256 --repo-id --key-epoch --image --archive-name --archive-sha256 --snapshot-id --manifest-sha256` | Restore **del archivo descargado** (`downloads-v2/`) en un Postgres desechable sin red; el reporte solo se escribe tras borrar el plaintext | `state-v2` |
| `review --archive-name --drive-file-id --repo-id --key-epoch` | Re-hashea el archivo descargado y exige recibo y reporte de restore coincidentes | `state-v2` |

Salida: líneas `CLAVE=valor` con identificadores y hashes públicos. Un fallo imprime una sola línea `G6RUN_STOP=<CÓDIGO>` y
sale con 1; nunca un traceback, ruta ni salida de un comando.

## Constantes fijas (se verifican con `preflight`, no se aceptan por línea de comandos)
- Estado: `/var/lib/pmgm-qa525-g6-v2` (debe existir, root, 0700). Restic: `/usr/bin/restic`. Docker: `/usr/bin/docker`.
- Silo: `/var/lib/docker/volumes/centenario-qa-lab2_pmgm_srv01_minio_data/_data` (supone la raíz de datos por defecto de Docker).
- Contenedores: `centenario-qa-lab2-postgres-1` y `centenario-qa-lab2-minio-1`, verificados por etiquetas de compose.
- Prefijos prohibidos para el restore: `/var/lib/docker`, `/opt/centenario`, `/srv`.

## Quiesce del Silo (decisión del Sponsor 08-11 00:55: ventana de 5–10 min, solo durante el respaldo G6)
El programa **nunca** detiene, arranca ni borra un contenedor (hay una prueba estática que lo prohíbe). Revisión ChatGPT 08-11
(P1-2/P1-3) aplicada:
- **Ligadura:** antes de la ventana se inspecciona `Mounts` de MinIO: debe haber exactamente un montaje con `Destination=/data`,
  `Type=volume` y `Source` idéntico a la ruta del Silo, sin symlinks. Si no, `G6RUN_SILO_UNBOUND` y no se pide parar nada.
- **Ventana medida y acotada:** MinIO debe estar **en marcha** cuando se llega al Silo (los volcados de Postgres ocurren antes, con
  MinIO activo; si ya está parado, `G6RUN_MINIO_STOPPED_EARLY`). Entonces imprime `G6RUN_ACTION=STOP_MINIO_NOW` y espera hasta
  180 s a que el operador lo pare. La ventana se mide desde que se observa parado hasta terminar la copia; al terminar imprime
  `G6RUN_ACTION=START_MINIO_NOW` y `MINIO_WINDOW_SECONDS` **antes** de que Restic y el archivo cifrado sigan. Tope duro 600 s:
  si se supera, `G6RUN_WINDOW_EXCEEDED` y la copia no es válida. Ante cualquier error tras la parada también imprime
  `START_MINIO_NOW` (`REASON=ABORTED`).
- **Lo que el programa NO garantiza:** si la terminal móvil se desconecta, nadie imprime ni ejecuta el arranque. Por eso el operador
  arma **antes de parar** un temporizador independiente del terminal (paso 4) que arranca MinIO pase lo que pase.
- Sesgo conocido: los volcados de Postgres y la copia del Silo no son atómicos entre sí (BD antes que Silo).

## Ciclo propuesto (borrador para revisión de ChatGPT; cada comando lo pega el Sponsor uno a uno por Termius)
1. **Una vez:** copiar los 4 archivos (`run`, `producer`, `captor`, `verifier`) a un directorio root con el método de la
   Fase 1 y comprobar `sha256sum` contra la lista publicada; `sudo install -d -m 0700 -o root -g root /var/lib/pmgm-qa525-g6-v2`.
2. `preflight` con el hash de restic y el digest de la imagen. Sale con código 1 y `PREFLIGHT_READY=no` si algo falla. **STOP** si: `RESTIC_MATCHES_PIN=no`, `SILO_BOUND_TO_MOUNT=no`, `SILO_SOURCE_OK=no`, `PYTHON_FCNTL_SEALS=no`,
   `FREE_OK=no`, `IMAGE_PRESENT=no`, `IMAGE_MATCHES_PIN=no`, `POSTGRES_IDENTITY` distinto de `centenario-qa-lab2|postgres|true`.
3. `init` (solo la primera vez): el Sponsor pega la clave nueva desde Bitwarden; guarda `RESTIC_REPO_ID` y `KEY_EPOCH` (son los pines).
4. **Ventana** (MinIO sigue en marcha al empezar `backup`): en una segunda sesión el Sponsor arma el temporizador independiente
   `sudo systemd-run --on-active=11min docker start centenario-qa-lab2-minio-1` y lanza `backup`. Al ver `G6RUN_ACTION=STOP_MINIO_NOW`
   ejecuta `sudo docker stop centenario-qa-lab2-minio-1`; al ver `G6RUN_ACTION=START_MINIO_NOW` ejecuta
   `sudo docker start centenario-qa-lab2-minio-1` y comprueba salud (runbook). Si no ve nada o `backup` da STOP, arranca MinIO igualmente.
   Requiere la aprobación expresa del Sponsor para ese día; **no** se extrapola la del 08-11 00:55 si no se puede garantizar el temporizador.
5. Descargar `exports-v2/<archivo>` por SFTP, subirlo a la carpeta Drive, bajarlo de nuevo, comprobar que su SHA-256 es
   `ARCHIVE_SHA256`, y subirlo por SFTP a `downloads-v2/` (0600).
6. `restore` con los valores de `backup` → debe imprimir `RESULT=G6_SAME_HOST_ISOLATED_RESTORE_PASS_NOT_DISASTER_RECOVERY`.
7. `review` con el id del archivo en Drive → deja el `review-<ts>.json` que consume el validador de evidencia G6 v2.

## Límites y puntos abiertos
- **No autenticado aquí:** este archivo y sus hermanos (hay que fijarlos por hash fuera de banda), el binario de restic, el daemon de
  Docker, el host. El hash de restic y el digest de la imagen se pasan como pines que el Sponsor copia de su propio registro.
- `--no-role-passwords`, `--no-owner --no-acl` y el volcado no atómico entre bases y Silo siguen como en `QA525-G6-V2-CAPTOR.md`; el
  restore aislado **no** prueba roles, ACL ni que MinIO lea el Silo restaurado.
- Se asume Python ≥ 3.9 en el host y raíz de datos de Docker por defecto; `preflight` informa lo que difiera.
- La clave la lee `getpass` del terminal: debe ser ASCII imprimible, sin espacios, ≥ 32 caracteres.
- **Primer byte root (P1-1) sigue ABIERTO:** el ejecutor carga hermanos con `exec_module` bajo UID 0 sin ancla por FD ni bootstrap
  autenticado. Se resuelve con el contrato de #936/#822 (bootstrap autenticado desde un pin independiente antes de privilegiar). Hasta entonces
  **NO-GO para `sudo` de este entrypoint**, también `preflight`.
- **Capacidades de Python en glmdch (evidencia del Sponsor 08-11, solo lectura, sin sudo):** el `python3` del usuario `soporte` es 3.14.4 y trae
  `os.memfd_create` y las seis constantes de sellado (`F_ADD_SEALS`, `F_GET_SEALS`, `F_SEAL_WRITE`, `F_SEAL_SEAL`, `F_SEAL_SHRINK`,
  `F_SEAL_GROW`). Eso prueba **símbolos presentes** (`hasattr`), no que se pueda crear, sellar y bloquear `pwrite` en un memfd real, ni que root use
  el mismo intérprete, ni su procedencia. Queda **un** test funcional rootless de sellado (una vez) y no se repite la comprobación de símbolos.
  Nada de esto concede autorización física: `PYTHON_FCNTL_SEALS=yes` en `preflight` sigue siendo informativo.
- `preflight` con `PREFLIGHT_READY=yes` es solo lectura, no una puerta de paso validada.
- **Contrato con `validate_g6` (hallazgo, sin cerrar aquí):** el validador actual lee `/home/soporte/<archivo>`, `/opt/centenario/qa-g6/archive.receipt.json`
  y el informe de restore bajo `/opt/centenario/qa-g6/`, todo propiedad de root y 0600, mientras que `review` deja los documentos en
  `/var/lib/pmgm-qa525-g6-v2/`. Falta un paso de puente (o adaptar el validador) y una prueba de contrato; no se afirma compatibilidad.
- Pendiente de ChatGPT: confirmar rutas, la ventana, y quién convierte el `review` en el acta que consume `validate_g6`.

## Temporizador independiente (obligatorio, verificado por el programa) y guard de host

Antes de `backup` el Sponsor arma, con el nombre EXACTO, un reinicio independiente de la terminal:

    sudo systemd-run --on-active=11min --unit=pmgm-g6-minio-restart --collect \
        /usr/bin/docker start centenario-qa-lab2-minio-1

`backup` consulta SOLO LECTURA a systemd (`systemctl show`) ANTES de imprimir `STOP_MINIO_NOW` y se
detiene si: el temporizador no está `active/waiting` (`G6RUN_WATCHDOG_MISSING`); la unidad que
dispara no ejecuta exactamente `docker start centenario-qa-lab2-minio-1` (`G6RUN_WATCHDOG_WRONG`);
o no vence entre 600 y 900 s (`G6RUN_WATCHDOG_TIMING`). Si al terminar la copia el reloj ya
superó el vencimiento, la ventana se invalida (`G6RUN_WINDOW_EXCEEDED`). Como el reinicio vive en
systemd, cubre desconexión de Termius o SIGKILL del programa. Tras la ventana cancelar si sigue
armado: `sudo systemctl stop pmgm-g6-minio-restart.timer`.
Probado en glmdch (08-11-2026, 21:39Z): `systemd-run --on-active=60s` ejecuta y termina bien.
PENDIENTE: confirmar en glmdch el formato real de `NextElapseUSecMonotonic` con el ensayo de 11 min.

`real_env` se niega (`G6RUN_HOST_REFUSED`) si `hostname != glmdch`, ANTES de cargar módulos. Es una
defensa adicional, NO prueba procedencia ni autorización. P1-1 (cargar módulos como root sin ancla
autenticada) sigue abierto y pertenece al contrato #936/#822: este entrypoint NO es apto para sudo.

## Corte real de la copia antes del temporizador independiente (P1 de ChatGPT)

- Al abrirse la ventana, el presupuesto de copia es `min(apertura + 600 s, deadline_del_temporizador − 30 s) − ahora`.
- `copy_silo` corre bajo `real_deadline(presupuesto)` (SIGALRM → `G6RUN_WINDOW_EXCEEDED`): la copia se interrumpe de verdad, no solo se verifica después.
- Si no queda presupuesto, no se copia nada. La ventana autorizada (≤10 min) no se amplía.
- El gate post-copia también exige `ahora < deadline − 30 s`.
- Caso SIGKILL/reinicio automático: el temporizador vive en systemd, así que el reinicio ocurre igual; el backup ya está invalidado y se imprime `START_MINIO_NOW`. Nunca se declara G6 coherente.
- Tests nuevos: temporizador 660 s + espera 120 s + copia 590 s, corte real de un bucle, y sin presupuesto no se copia.
