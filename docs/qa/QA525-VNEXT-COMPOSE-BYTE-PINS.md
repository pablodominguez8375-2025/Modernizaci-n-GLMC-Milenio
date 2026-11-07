# QA525 B7 — Integridad de archivos Compose con descriptor (INACTIVO)

Issue #907. Biblioteca `scripts/qa/qa525-vnext-compose-byte-pins.py`.
Complementa el puente read-only #904: el puente compara la **lista y
orden** de capas de los diez servicios; esta biblioteca comprueba los **bytes
SHA-256** de cada archivo Compose referenciado en API/WEB. No reemplaza G6.

## Contrato de integración

`inspect(layers={"api":[...],"web":[...]}, pinned_sha256={"/ruta":"sha256:..."}, port=...)`

- Las listas API/WEB tienen al menos el corte histórico 6/5 y hasta 64
  componentes por servicio, sin elementos duplicados. El primer archivo
  Compose es compartido. Permite crecimiento futuro API7/WEB6 **únicamente**
  cuando todos los nuevos archivos aparecen en los pins del corte revisado.
- El conjunto de pins es **exactamente** la unión de todos los archivos
  Compose; cualquier pin faltante, excedente, ruta no absoluta, `..`,
  huella inválida o symlink se rechaza.
- El puerto inyectado exige `euid/lstat/open_ro/fstat/read/close` y UID 0.
  El port físico futuro debe usar `O_RDONLY|O_NOFOLLOW|O_CLOEXEC|O_NONBLOCK`
  y no permitir abrir archivos fuera del conjunto firmado. Esta biblioteca
  **no construye un puerto físico ni lo autentica**.
- Se comprueba propietario UID/GID 0 y sin escritura de grupo/otros para
  archivo y ancestros; archivo regular, `nlink=1`, longitud en (0, 4 MiB].
  Tras abrir, `fstat(fd)` debe coincidir con la identidad inicial; se lee
  EXCLUSIVAMENTE desde el descriptor, se vuelve a verificar `fstat` +
  `lstat(path)` y se compara SHA-256. Un cambio de bytes, inode,
  propietario, permisos o tamaño detiene en cerrado.
- No lee contenedores, secretos, variables, logs, red ni bases de datos.
  Devuelve solo un hash público del manifiesto ordenado, conteos y
  `may_apply=false`, `may_install=false`,
  `pins_authenticated_here=false`, `port_authenticated_here=false`.

## Puerto POSIX específico (también inactivo)

`scripts/qa/qa525-vnext-compose-posix-port.py::PosixReadPort(exact_files)`
construye un puerto de lectura real, sin CLI ni instancia predeterminada.
Admite solamente rutas concretas bajo `/opt/centenario/` y limita la
operación `lstat` a esos archivos y sus ancestros. `open_ro` solo abre las
rutas fijadas con `O_RDONLY|O_NOFOLLOW|O_CLOEXEC|O_NONBLOCK` y `fstat/read/close`
aceptan únicamente los descriptores creados por el puerto. Nunca escribe,
reinicia servicios ni llama a Docker. La lista `exact_files` debe provenir
del pin firmado y revisado de B7, no de un parámetro arbitrario del usuario.

La prueba `tests/test_qa525_vnext_compose_posix_port.py` ejecuta la ruta
completa de 10 archivos sobre syscalls POSIX simuladas y casos negativos
de rutas externas, FDs desconocidos, reuso y errores. **No acredita
ejecución como root ni en glmdch**. El puerto devuelve UID real al inspector,
que rechaza todo caso distinto de 0. Ninguna parte hace `sudo`.

## Precondiciones del futuro B7 real

La autenticación de estos hashes **no ocurre aquí**: B7 debe obtener el
manifiesto desde una autoridad OPS/APP independiente (PR #902), verificar
identidad de la fuente antes de root y fijar su SHA en el mismo scope de locks
y lease de la transacción. Debe comprobar concordancia entre estas listas
pinneadas y el inventario del puerto #904, repetida en cada frontera de
mutación y rollback. La comparación de hash con un pin proporcionado por un
atacante NO autentica el archivo.

El enfoque de `qa-lite-compose-adapter.ComposeWebAdapter.check_files` ya
verifica propietario+SHA, pero utiliza `read_bytes` del nombre y tiene un
adaptador de mutación WEB-only con límite de una capa extra. El nuevo contrato
se limita a lectura por descriptor, igualdad del inode antes/después y
pilas completas API/WEB, sin tocar el antiguo adaptador mutante.

**Advertencia:** la comparación de `lstat` y descriptor no reemplaza la
custodia de anclas y la política root. Otros procesos root pueden alterar
archivos; la integración física necesita anclas fuera de banda, locks y
relectura bajo la misma ventana. No declara recuperación de host perdido
ni GO del Sponsor. #822 permanece STOP hasta B4–B8 físicos, G6 vigente y
autorizaciones explícitas. Sin despliegue físico, `main` o producción.
