# QA525 — Inspección previa del lanzador root-owned (#822)

Estado: **fases 1–2 de inspección + ensayo 3b exclusivamente temporal**. El inspector aislado no verifica firma, pero el preflight de fase 2 añade Sigstore y manifiesto. Ninguno instala, cambia Docker, ni habilita la actualización física QA525.
Un examen satisfactorio de un lanzador antiguo **no** autoriza reemplazarlo.

## Riesgo real

En glmdch existe `/usr/local/sbin/pmgm-qa-update`; su plantilla fijó el
código Python del operador en un directorio root-owned. El comando actual
`--install-launcher` utiliza `O_EXCL`, por lo que no sustituye el archivo
existente. La PR #820 introduce el archivado con marcador de recuperación
pero ese código NO se activa al hacer commit en GitHub.

## Herramienta propuesta para comprobación inicial

`scripts/qa/operators/qa525-launcher-upgrade-inspect.py` inspecciona
**únicamente** el estado actual. Antes de leer exige UID 0 en hostname glmdch;
verifica propiedad root, sin symlinks, padres no escribibles por terceros,
archivo regular único, modo launcher 0700, lectura sin seguir enlaces, identidad
de inode y tamaño limitado. Rechaza cualquier lanzador que no coincida
exactamente con la plantilla Python históricamente instalada; extrae su ruta
inmutable usando `ast.literal_eval`, **sin ejecutarla**. Comprueba por separado
la fuente antigua root-owned y emite sus hashes SHA-256 como evidencia local,
no como prueba de firma ni de correspondencia con un commit GitHub.

Salida informativa:
`LAUNCHER_OBSERVED_UPGRADE_NOT_AUTHORIZED`,
`signed_release_verified=false`, `rollback_validated=false`,
`may_install=false`, `may_apply=false`. En fallos devuelve
`BLOCKED_QA525_LAUNCHER_INSPECTION` y código estable no sensible.

**Estado de continuidad legible (PR #830, fase read-only):** el inspector
emite además `launcher_transition=INSTALLER_UPDATE_REQUIRED` **solo** cuando
la plantilla de launcher heredado, su origen root-owned y los hashes superan
la inspección; en cualquier fallo controlado emite
`launcher_transition=LAUNCHER_INSPECTION_BLOCKED`. Se conserva el campo
`status=LAUNCHER_OBSERVED_UPGRADE_NOT_AUTHORIZED` requerido por el
preflight firmado previo. Estos estados no verifican que exista un release
firmado, no acreditan una instalación, no renuevan aprobaciones y no permiten
`may_install` ni `may_apply`. Tampoco comparan el launcher con el HEAD actual
de `dev`: `INSTALLER_UPDATE_REQUIRED` significa que **para sustituir este
launcher heredado se necesita el instalador firmado**, no que ya exista una
nueva versión instalable ni que el host haya quedado atrasado. Tras instalar
el reemplazo, usar diagnóstico post-instalación específico, no este indicador.

**NO usar esta herramienta desde código crudo de GitHub en glmdch.**
Solo después de CI exact-head, revisión independiente y publicación firmada,
el inspector podrá incorporarse a una instalación autorizada. Ningún comando
del proyecto cambia aún al launcher existente.

## Contrato general para una instalación física futura (especificación)

1. Inspeccionar estado antiguo y registrar hash, propietario, permisos,
   ubicación exacta, ausencia de symlink y ruta efectiva. Bloquear si difiere.
2. Construir release exacta nueva desde dev integrado y exigir workflow,
   tag, hash del artefacto, firma Sigstore y manifest íntegro; no aceptar
   un digest suministrado por el propio candidato como prueba externa.
3. Staging verificable en ruta inmutable root-owned; verificar cada módulo
   de importación antes de ejecutar Python, y no usar rutas raw mutables.
4. Aprobar una operación específica de sustitución del launcher, con lock,
   copia íntegra del antiguo, journal de reemplazo duradero, fsync,
   sustitución atómica y rollback seguro incluso tras interrupción.
5. Validar que el launcher nuevo solo hace `--status` y que no ha cambiado
   API/WEB, identidad, Compose, EF100, Keycloak ni SMTP. Solo entonces se
   puede considerar el paso explícito de archivar el preparado caducado,
   con aprobación nueva para otro SHA y G6 vigente.
6. No reintentar automáticamente operaciones incompletas; un marcador
   parcial exige reconciliación manual auditada. No usar producción/main,
   segunda VM, srv01 ni PR #771/#737/#766.

## Pruebas iniciales

`tests/test_qa525_launcher_upgrade_inspect.py` cubre la plantilla
histórica válida, inyección de comandos, intérprete alterado, fuente relativa,
parent traversal, enlaces simbólicos, UID/hostname incompatible, ausencia de
capacidades de instalación y distinción inequívoca entre inspección y firma.

**Limitaciones de esta entrega:** no hay evidencia física reciente de glmdch,
no se probó apagón real, no hay firma nueva para la PR #820, no se ha instalado
el operador reemplazante ni se ha completado la UAT. Los hashes locales
sólo sirven para inventario, no para validación de integridad con Sigstore.

Continuidad: Issues #525, #819, #821, #822. PR #820 integrada exclusivamente a `dev` mediante squash `6b7b6a3c34d0d3d309dd12d7217f9725a00d95fe`; recuperación disponible **en código**, NO instalada en `glmdch`.

## Fase 2 — Candidato firmado, comprobación previa sin instalar

La PR #823 incorpora también `scripts/qa/operators/qa525-launcher-signed-preflight.py`. Sólo después de publicar, aprobar y entregar su paquete firmado al host, el comando previsto para el operador supervisado será:

```text
sudo python3 -B <RUTA_INMUTABLE_Y_VERIFICADA>/scripts/qa/operators/qa525-launcher-signed-preflight.py --preflight
```

**No ejecutar esta ruta a partir de una copia no verificada de GitHub.** El paquete y todos sus módulos deben estar previamente acreditados como root-owned, con identidad exacta y permisos privados; el comando no es una autorización implícita de instalación. La rama PR aún no es una publicación firmada utilizable en glmdch.

El preflight exige UID 0 y host glmdch, origen del propio código y dependencias root-owned sin symlink ni padres escribibles; inspecciona dos veces el launcher antiguo y sus hashes; reutiliza `qa525-stage-current-release.py` para revalidar el `dev` vivo antes y después de la descarga, verificar hash de la release y autenticidad **Sigstore** del instalable. Repite la comprobación del `MANIFEST.sha256` del paquete, compara explícitamente el hash del archivo `scripts/qa/operators/qa525-update.py` contra su entrada firmada, y rechaza candidatos que no incluyan los comandos de recuperación segura.

Su salida `SIGNED_LAUNCHER_CANDIDATE_VERIFIED_NO_INSTALL` no es un acta de aprobación ni la prueba de que el host fue actualizado. Los archivos descargados se eliminan al terminar la comprobación: por diseño no deja ninguna credencial, paquete temporal reutilizable ni autorización para reemplazar el launcher. Todos los resultados declaran `replacement_applied=false`, `may_install=false`, `may_apply=false`. Ningún archivo API/WEB ni instalación existente se modifica.

Pruebas herméticas en `tests/test_qa525_launcher_signed_preflight.py`: rechazo por host incorrecto, launcher antiguo alterado, origen sin permisos, manifest manipulado, ausencia de recuperación segura, firma no verificada, source SHA o digest inválidos, cambio del launcher durante la descarga, symlinks/hardlinks, y ausencia de instalador en este módulo. El workflow QA525 las ejecuta en PR exact-HEAD.

**Fase 3 de instalador físico aún pendiente:** integrar la sustitución atómica del launcher con respaldo completo, aprobación específica, journal/lock/fsync, pruebas de SIGKILL y rollback. Antes de ese paso no existe comando seguro autorizado para escribir `/usr/local/sbin/pmgm-qa-update`.

## Fase 3a — Contrato auditable previo al reemplazo (NO instalador)

Se añadió `scripts/qa/operators/qa525-launcher-replacement-contract.py` junto a `tests/test_qa525_launcher_replacement_contract.py`, con su ejecución explícita dentro del CI hermético de QA525. Es un **contrato puro**, sin acceso a host, filesystem, Docker ni red; **no reemplaza el launcher** y no convierte un informe de preflight en autorización.

`assess_review()` comprueba el informe efímero de firma/manifest del paso anterior, coincidencia exacta del SHA del release, digest, hash del launcher viejo y del operador nuevo con una aprobación explícita de operación `replace-root-launcher-supervised` en QA glmdch, emitida por el operador autorizado. Exige nonce SHA256 no reutilizado, emisión no futura, TTL máximo 900 segundos, inexistencia de journal pendiente y todos los campos de ámbito exactos. Incluso en el resultado positivo devuelve `may_install=false`, `may_apply=false`, `rollback_complete=false`: **ninguna aprobación de este contrato reemplaza firma, revisión del host o ejecución supervisionada**.

`assess_interrupted_swap()` define estados de diagnóstico para crash antes o después del reemplazo: marcador pendiente con launcher viejo, con launcher nuevo o desconocido, receipt completo con nuevo hash correcto, hash de backup divergente y receipt inconsistente. Todos los resultados prohíben auto-reparación y despliegue. Sus pruebas cubren caducidad, scopes alterados, nonce repetido, cambios de hashes, inyección de receipt y las fronteras de confirmación.

**Hallazgo de auditoría:** una versión inicial duplicó una barra de escape en los patrones regex de hash; el CI hermético lo detectó (no se acepta como verificación). La corrección simplifica las expresiones porque `re.fullmatch()` ya obliga a coincidir con toda la cadena. Solo el **nuevo HEAD exacto** y su CI determinan si la corrección quedó validada.

**Coordinación recuperada:** después del intento previo de construir un árbol bloqueado, GitHub generó su commit de prueba de fusión con dos padres (`dev@d5f828642120de539ace8795904d3b60c35d9d42` y el HEAD de PR #823). Se verificaron los hashes de archivos críticos y se avanzó la rama con `update_ref` fast-forward `force=false`, con SHA esperado, a `2f94cfbf2ae7c65ef8bbf6d3c12e1c3216128a68`. No se reescribió historial, no se fusionó a `dev` y no se desplegó: la ronda de CI de este nuevo HEAD debe ser `SUCCESS` antes de integrar.

**Fase 3b pendiente:** implementar el instalador físico transaccional en paquete firmado inmutable, con adquisición comprobada, lock compartido de cinco recursos, evidencia/aprobación root0600, respaldo antiguo con SHA, journal de intención durable, sustitución atómica `os.replace`, fsync, recibo final y reconciliación manual obligatoria de interrupciones. Revalidar no alteración de Compose/identidad, G6 y API/WEB separadamente. Sin esta fase, **NO ejecutar reemplazo root ni archivo de preparado**.

## Mejora adversarial — validación del operador como AST, no cadenas

El verificador de la fase 2 ya no considera suficiente que el archivo firmado contenga palabras como `--archive-expired-prepared` en comentarios. Utiliza `ast.parse` **sin ejecutar el módulo** para comprobar definiciones Python únicas de las funciones de recuperación, registro real mediante `choice.add_argument()` de cinco opciones del CLI, invocaciones del guard `require_reconciled_archive()` en `install_review_packet`, `archive_expired_prepared` y `main`, y la existencia de códigos de error de recuperación fail-closed.

Pruebas adversariales nuevas prueban comentarios falsos, guard comentado, código Python inválido y bytes UTF-8 incorrectos. Se valida sobre **bytes del operador extraídos del paquete Sigstore, atados al MANIFEST SHA-256**, no GitHub raw. Este análisis estático es defensa adicional de coherencia, NO prueba formal de que el programa sea seguro ni autoriza instalarlo.

Todo cambio al verificador requiere un HEAD exacto nuevo de la PR #823 y que los tests del workflow QA525 estén realmente en SUCCESS. Si hay un falso positivo por diferencias legítimas de sintaxis, registrar la causa y cambiar el test con criterio de aceptación; nunca saltar la firma ni la comparación del manifiesto.

## Fase 3b-ensayo — journal, sustitución atómica y cortes abruptos EN SANDBOX

Se agregó `scripts/qa/operators/qa525-launcher-atomic-sandbox.py`, una **simulación cerrada a un directorio temporal privado** cuyo nombre empieza `qa525-launcher-swap-test-`. No es un instalador de producción; su CLI se niega explícitamente a ejecutarse y la función exige rutas exclusivas debajo del temporal del proceso. No puede usar `/usr/local/sbin` ni `/opt/centenario`, modificar Docker o desplegar API/WEB. No confundir sus resultados con una prueba en glmdch.

La simulación verifica hashes SHA256 del launcher viejo y de un candidato preparado, crea un **intento root-style 0600** sincronizado a disco antes de los cambios, copia y verifica respaldo 0700, prepara archivo nuevo 0700, ejecuta una única sustitución atómica `os.replace` dentro del sandbox, sincroniza directorios, escribe recibo durable 0600 y quita marcador de intención. Rechaza repetir nonce, colisiones, enlaces simbólicos, hardlinks, permisos inseguros, cambios de bytes y evidencia falsificada. Inspección posterior es **sólo lectura**, sin reparación automática.

Diez tests en `tests/test_qa525_launcher_atomic_sandbox.py` prueban finalización normal, siete puntos de interrupción, tampering, nonce repetido, rutas no autorizadas y modo inseguro. Un test ejecuta tres procesos hijos y los termina mediante **SIGKILL real** en los checkpoints tras marcador, respaldo y sustitución; se inspecciona el estado persistido luego de la muerte del proceso. Esto permite demostrar propiedades de fsync y transiciones en entorno efímero, pero **no simula una pérdida real de alimentación eléctrica ni garantiza persistencia hardware**.

**Restricciones del ensayo:** sólo muestra el algoritmo. Antes de usarlo físicamente hace falta desarrollar por separado la interfaz firmada root-only, integrar el bloqueo exclusivo del operador real (cinco locks compartidos), aceptar únicamente el paquete verificado de SHA exacto, comprobar las aprobaciones/root0600 y nonce al instante del corte, almacenar respaldo/journal en rutas reales protegidas, diseñar recuperación manual revisada y probar en un entorno de restauración autorizado. Las pruebas de sandbox no conceden `may_install` ni permiso a glmdch. El primer código firmado nunca debe ejecutarse desde raw GitHub.

**Estado de coordinación:** PR #823 sigue en borrador y basada en SHA anterior a la fusión de PR #820; debe sincronizarse con dev vivo, repetir CI exact-head y preservar los aportes de Claude antes del merge. Hasta entonces **NO hay transición a montaje QA físico**.

**Control adicional de identidad de corte (31-10-2026):** la inspección de journal/recibo del ensayo exige ahora que tanto el marcador de intención como el comprobante durable coincidan no solo en `nonce`, `old_launcher_sha256` y `new_launcher_sha256`, sino también en `source_sha` y `release_asset_sha256` exactos. Se añadieron dos regresiones que alteran esos campos después de una operación comprometida o interrumpida y exigen bloqueo (`SANDBOX_COMMIT_RECEIPT_INVALID`, `SANDBOX_INTENT_IDENTITY_INVALID`). Es una defensa del modelo de recuperación, no la autenticación real de Sigstore ni autorización para instalar.
