# QA525 vNext — Contrato de los cinco locks legacy y un lease raíz (INACTIVO)

Fuente del requisito: `docs/qa/qa525-single-plane-v1.json`, función `all_five_locks` del instalador firmado y GitHub Issue #525, comentarios 6683302736 / 6683399994 / 6683481392. Diseño complementario al PR de Claude #865, no implica permiso de host.

**Objetivo:** introducir un contexto offline de prueba que tome **en este orden** las cinco exclusiones `flock EX|NB` existentes y, sin liberarlas, cree el `Lease` propio de `qa525-vnext-journal.py` con su root lock. Retenerlos conjuntamente hasta salir del contexto, liberar en orden inverso, y bloquear de forma conservadora ante intento concurrente, path sustituido, symlink, hardlink, archivo ausente o modo/propietario inesperado. No crear locks que falten silenciosamente.

**Activación prohibida:** este módulo NO es un CLI, NO configura sudoers ni instala. El contexto de pruebas marca `may_apply=False`, `may_install=False`, `external_runtime_verified=False`, `signature_verified_here=False`. El futuro root entrypoint debe validar por una vía independiente todas las rutas y ancestros (incluida la excepción auditada /run/lock), ownership root, origen de scripts, políticas y firmas de OPS/APP/G6/Sponsor antes de usarlo. En tests, las rutas son privadas bajo tempfile y se declaran explícitamente `synthetic_only=True`; esto **nunca** es autoridad para `glmdch`.

**Peligros que no resuelve:** flock es advisory; el viejo entrypoint debe estar realmente deshabilitado después de validar la conmutación, sin segundo escritor; el cgroup del controlador no prueba detención de Docker daemon. El módulo no autentica baseline, no ejecuta Compose, no reintenta APPLY, no modifica Keycloak ni base de datos. El nivel de verificación físico requiere además backup G6 offhost reciente + restauración aislada, guardas de identidad y rollback, autorización separada y revisión de la versión firmada en el host.

**Gates:** PR draft, tests herméticos inactivos y CI exact-head; revisión adversarial independiente, merge únicamente a dev. Prohibido promover main o desplegar en glmdch. 
