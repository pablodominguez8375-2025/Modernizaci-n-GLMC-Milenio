# QA525 — Journal durable vNext (fase sin ejecución) — 05-11-2026

**Issue #525; nueva rama desde `dev@b2034ccf57bfc3219c504be6482995d5ca50b903`.** Este documento reserva la tarea para ChatGPT y habilita revisión multi-IA mediante PR draft. Sucesor de PR #845 ya integrado como código INACTIVO.

## Alcance de este incremento

- Diseñar e implementar **solo** el componente de journal y el contrato de máquina de estados, probado en directorios descartables con fallos/crash simulados, nunca sobre `glmdch`.
- Estados: `ADMITTED → APPLYING → APPLIED|ROLLED_BACK|RECOVERY_REQUIRED`. `CACHED_VERIFIED` es un gate externo de SYNC; no es un evento de despliegue.
- Al ingresar a `APPLYING`: un único nonce `O_EXCL` con hash del recibo y ruta esperada del journal, `fsync`, journal durable y verificación de falta de recuperación pendiente. No se invoca Docker, SSH, `pmgm-qa-update`, bash ni red.
- El resultado exitoso **NO** puede ser informado por la biblioteca sin el futuro verificador externo `verify_state` de API/WEB, oneshots, dependencias, imágenes, etiquetas, health y OIDC; la biblioteca no implementa ni suplanta esa verificación.
- Un crash entre nonce y journal o una mutación dudosa se clasifica `RECOVERY_REQUIRED`, bloqueando reutilización; recuperación/rollback requerirá otra tarea firmada.
- Conservar registro GitHub+Drive y solicitar revisión Claude exact-head, sin instalar el componente.

## Guardas

- QA físico: último `glmdch@6eed4f00b7917da3cca36bbda70e16d16328648d` APPLIED según operador anterior; no reingresar a su actualización.
- `dev@b2034ccf57bfc3219c504be6482995d5ca50b903` ya contiene el código inactivo de #845; `main@6dfb9546a4873baff15955cf86abfd7d47e3d111` no se modifica.
- Sin uso de PAT, secretos, credenciales, migraciones, Keycloak, servidor, timers, despliegue físico ni promoción a main.
- El contrato de pruebas no autoriza un APPLY real. Hacer revisión de firma OPS/Sponsor/gh/transitivas y ancla externa antes de cualquier instalador.

## Prototipo implementado en el PR draft #846

Archivos agregados: `scripts/qa/qa525-vnext-journal.py` y `tests/test_qa525_vnext_journal.py`. `tests/test_qa_agent_readiness.py` incorpora la clase de regresión en el job CI protegido. **No hay CLI, servicio, timer, API Docker, SSH, subprocess, red ni integración con el lanzador**. En un directorio privado existente del usuario que ejecuta el proceso:

1. `begin(root, admission_path, expected_sha256, now)` abre la admisión root/user 0600 por `O_NOFOLLOW`, verifica SHA-256 esperado, esquema, campos, ventanas `valid_until` y `g6_expires_at`. El SHA esperado **debe proceder de un origen externo autenticado**, no de un valor proporcionado por el mismo archivo. Las verificaciones físicas de backup, aprobación Sponsor y baseline **NO** las implementa este módulo.
2. Obtiene un lock `flock LOCK_EX|LOCK_NB` que permanece retenido en el objeto `Lease` hasta el cierre explícito o muerte del proceso. Bajo lock, exige nonce no consumido y crea `nonce-consumed-<nonce>.json` por `O_EXCL` + `fsync(fd)` + `fsync(dir)`; el marcador contiene digest de admisión, SHA OPS/APP, policy y ruta absoluta del journal esperado.
3. Solo después del marcador escribe `journal-<nonce>.json` en estado `APPLYING` usando archivo temporal `0600`, `fsync`, `os.replace`, `fsync(dir)`. Un temporal abandonado permanece como evidencia; no se borra automáticamente.
4. `reconcile_after_crash` exige lock disponible (si está ocupado: `JOURNAL_OPERATION_ACTIVE`), lee el marcador/journal y crea evidencia `recovery-<nonce>.json` con `RECOVERY_REQUIRED`, `retry_allowed=false`, `external_verified_rollback=false`. No consume una segunda autorización ni reintenta la operación. Es idempotente. El journal `APPLYING` previo se conserva.
5. El módulo no expone ninguna transición a `APPLIED` ni `ROLLED_BACK`, ni pretende verificar salud, imágenes, etiquetas, dependencias, oneshots u OIDC. Esos resultados requieren un ejecutor firmado y un verificador externo futuro, con gates independientes.

**Pruebas locales:** 10 casos `unittest` PASS en directorios temporales; 7 puntos distintos de crash-injection (antes/después de marcador, temp, rename y fsync), reutilización del nonce, lock activo, archivo de admisión alterado/expirado/symlink, directorio débil, retención del temporal, ausencia de red por instrumentación de sockets y ausencia de API de éxito. El job `QA auto-agent readiness contract` verificó SUCCESS en commit `e85c1d0` (otros CI exact-HEAD aún pendientes al registrar). **La prueba con sockets interceptados NO sustituye el aislamiento real de namespace de red**, requerido antes de conectar al launcher.

**Riesgos/bloqueos que quedan abiertos antes de cualquier instalación:** verificar toda la cadena de ancestros de `root` por un instalador externo firmado (la biblioteca comprueba solo el directorio inmediato); autenticar el SHA de admisión y la procedencia OPS; revalidar G6 físico/approval y baseline *dentro* de la sesión con lock; resolver un único journal autoritativo compartido con launcher existente, incluyendo otro tipo de eventos `dual-*.json`; reconciliar marcadores truncados y errores de fsync; completar transiciones terminales solo con `verify_state` real; crash injection multiproceso (no solo excepciones) y prueba sandbox sin red. Hasta entonces NO-GO host, PR #846 DRAFT e Issue #525 OPEN.

## Protección multi-nonce y crash real (revisión adversarial de Claude #6660281690)

Tras la primera revisión exact-head de Claude `d143777` (0 P0/P1, 7/7 workflows SUCCESS), se corrigieron los riesgos P2 **antes** de conectar el motor a cualquier lanzador:

- `_guard_no_other_flights` recorre, bajo `flock`, `nonce-consumed-*.json`, `journal-*.json`, `recovery-*.json` y journals legacy `dual-<sha>.json` en el **mismo directorio privado**. Un segundo nonce queda bloqueado siempre que exista cualquier nonce vNext anterior porque aún no hay escritor terminal externamente verificado. Un legacy `dual` con estado diferente de `APPLIED` bloquea; `APPLIED` solo es una condición mecánica, **no** reemplaza `verify_state`. Nombres sospechosos o archivos temporales huérfanos bloquean; no se borran. El futuro instalador debe fijar el `root` autoritativo a `/opt/centenario/qa525-signed-prepare`, no cualquier ruta CLI; la biblioteca no está instalada.
- El journal se publica por `os.link(temp, journal)` (creación **NO REPLACE**, sin clobber de otro escritor) y solo después se elimina el temporal. Una interrupción entre ambas operaciones deja dos nombres del mismo inode; se tolera `nlink=2` **únicamente para journal** cuando hay exactamente un temporal privado `.qa525-next-*` del mismo inode, dueño, grupo y modo 0600. Los demás hardlinks/symlinks siguen rechazados. El marcador nonce está durable antes de ese punto, por lo que se declara `RECOVERY_REQUIRED` sin reintentos.
- Las pruebas adversariales cubren dos nonces distintos, `RECOVERY_REQUIRED` ajeno, `dual` legacy `PENDING/RECOVERY_REQUIRED/APPLIED`, nombres ilegales, carrera con escritor que intenta imponer otro journal, y SIGKILL real de un hijo de prueba (`os.fork`) después de fsync del nonce, después de link y después de fsync de journal. Los hijos son procesos de fixture descartable sin Docker/SSH/red.

**Correcciones CI observadas:** el HEAD intermedio `b46d827` falló readiness porque `inspect` rechazaba un journal con `nlink=2` generado por un crash simulado justo después de `os.link`; la comprobación estricta del temporal hermano se implementó en `c23f6bc`, sin debilitar admisiones ni marcadores. La prueba SIGKILL real se agregó en `20c70ce`. CI sobre el HEAD definitivo debe revisarse de nuevo antes de integrar.

**Pendientes bloqueantes de instalación y cualquier motor APPLY:** separar ancla externa firmada de bytes OPS y herramientas `gh`, recibo de autorización Sponsor/G6 y hash confiable de admisión, no confiar solo en el formato de campos ni en `now` provisto por caller; exigir cadena de ancestros confiable y root real, anclar lock único con lanzador legacy, detectar/esperar cgroup de hijos después de caída del orquestador antes de recuperación, snapshot dual de imágenes/oneshots/dependencias previo a mutación, `advance` monotónico, verificador externo de estado (con rollback) y ensayos en namespace sin red. Ninguno se elude por CI verde de este componente inactivo.
