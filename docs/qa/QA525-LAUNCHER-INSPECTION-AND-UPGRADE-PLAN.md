# QA525 — Inspección previa del lanzador root-owned (#822)

Estado: **fase 1 / solo lectura**. No actualiza, no instala, no valida la
firma de un paquete, no hace cambios en Docker ni desbloquea la PR #820.
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

**NO usar esta herramienta desde código crudo de GitHub en glmdch.**
Solo después de CI exact-head, revisión independiente y publicación firmada,
el inspector podrá incorporarse a una instalación autorizada. Ningún comando
del proyecto cambia aún al launcher existente.

## Contrato de fase 2 (futuro, NO implementado aquí)

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

Continuidad: Issues #525, #819, #821, #822; PR #820 permanece draft.

## Fase 2 — Candidato firmado, comprobación previa sin instalar

La PR #823 incorpora también `scripts/qa/operators/qa525-launcher-signed-preflight.py`. Sólo después de publicar, aprobar y entregar su paquete firmado al host, el comando previsto para el operador supervisado será:

```text
sudo python3 -B <RUTA_INMUTABLE_Y_VERIFICADA>/scripts/qa/operators/qa525-launcher-signed-preflight.py --preflight
```

**No ejecutar esta ruta a partir de una copia no verificada de GitHub.** El paquete y todos sus módulos deben estar previamente acreditados como root-owned, con identidad exacta y permisos privados; el comando no es una autorización implícita de instalación. La rama PR aún no es una publicación firmada utilizable en glmdch.

El preflight exige UID 0 y host glmdch, origen del propio código y dependencias root-owned sin symlink ni padres escribibles; inspecciona dos veces el launcher antiguo y sus hashes; reutiliza `qa525-stage-current-release.py` para revalidar el `dev` vivo antes y después de la descarga, verificar hash de la release y autenticidad **Sigstore** del instalable. Repite la comprobación del `MANIFEST.sha256` del paquete, compara explícitamente el hash del archivo `scripts/qa/operators/qa525-update.py` contra su entrada firmada, y rechaza candidatos que no incluyan los comandos de recuperación segura.

Su salida `SIGNED_LAUNCHER_CANDIDATE_VERIFIED_NO_INSTALL` no es un acta de aprobación ni la prueba de que el host fue actualizado. Los archivos descargados se eliminan al terminar la comprobación: por diseño no deja ninguna credencial, paquete temporal reutilizable ni autorización para reemplazar el launcher. Todos los resultados declaran `replacement_applied=false`, `may_install=false`, `may_apply=false`. Ningún archivo API/WEB ni instalación existente se modifica.

Pruebas herméticas en `tests/test_qa525_launcher_signed_preflight.py`: rechazo por host incorrecto, launcher antiguo alterado, origen sin permisos, manifest manipulado, ausencia de recuperación segura, firma no verificada, source SHA o digest inválidos, cambio del launcher durante la descarga, symlinks/hardlinks, y ausencia de instalador en este módulo. El workflow QA525 las ejecuta en PR exact-HEAD.

**Fase 3 aún pendiente:** diseñar e integrar la sustitución atómica del launcher con respaldo completo, aprobación específica, journal/lock/fsync, pruebas de SIGKILL y rollback. Antes de ese paso no existe comando seguro autorizado para escribir `/usr/local/sbin/pmgm-qa-update`.
