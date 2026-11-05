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
