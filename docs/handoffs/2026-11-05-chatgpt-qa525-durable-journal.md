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
