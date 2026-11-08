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
El programa **nunca** detiene, arranca ni borra un contenedor (hay una prueba estática que lo prohíbe). La copia del Silo
solo avanza si MinIO está **positivamente detenido** (`State.Running=false`, etiquetas del proyecto) antes y después de
copiar. El operador detiene y vuelve a arrancar MinIO con comandos separados dentro de la ventana. Los volcados de Postgres
ocurren con MinIO ya detenido, así que la ventana cubre todo `backup`; `preflight` imprime el tamaño del Silo para estimarla.

## Ciclo propuesto (borrador para revisión de ChatGPT; cada comando lo pega el Sponsor uno a uno por Termius)
1. **Una vez:** copiar los 4 archivos (`run`, `producer`, `captor`, `verifier`) a un directorio root con el método de la
   Fase 1 y comprobar `sha256sum` contra la lista publicada; `sudo install -d -m 0700 -o root -g root /var/lib/pmgm-qa525-g6-v2`.
2. `preflight` con el hash de restic y el digest de la imagen. **STOP** si: `RESTIC_MATCHES_PIN=no`, `SILO_SOURCE_OK=no`,
   `FREE_OK=no`, `IMAGE_PRESENT=no`, `IMAGE_MATCHES_PIN=no`, `POSTGRES_IDENTITY` distinto de `centenario-qa-lab2|postgres|true`.
3. `init` (solo la primera vez): el Sponsor pega la clave nueva desde Bitwarden; guarda `RESTIC_REPO_ID` y `KEY_EPOCH` (son los pines).
4. **Ventana:** `sudo docker stop centenario-qa-lab2-minio-1` → `backup` → `sudo docker start centenario-qa-lab2-minio-1` y
   comprobar salud (runbook). Si `backup` da STOP, arrancar MinIO igualmente.
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
- Pendiente de ChatGPT: confirmar rutas, la ventana, y quién convierte el `review` en el acta que consume `validate_g6`.
