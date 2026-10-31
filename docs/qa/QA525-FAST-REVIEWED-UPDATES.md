# Actualizaciones QA glmdch — operación reutilizable

Estado de esta entrega: código y pruebas en PR815; NO instalado ni activado
en glmdch hasta CI exact-head, merge, release firmada e instalación supervisada.
Último corte físico comprobado725bc319; EF99 y Taller00 pendiente.

## Uso habitual después de la instalación

Un revisor técnico examina el delta del nuevo SHA y deja una revisión exacta
en un único archivo root:root0600. El operador único sigue siendo Pablo; una
revisión IA se registra como IA, sin inventar un DBA humano independiente.

```bash
sudo pmgm-qa-update --status
sudo pmgm-qa-update --run-reviewed /etc/pmgm/qa525-reviewed-update.json
```

El segundo comando verifica fuente/CI/digest actuales, revisión sensible y G6,
archiva actas anteriores conservando sus bytes, emite un nonce nuevo, construye
API+WEB firmadas sin tocar servicios y después aplica ambas con el entrypoint
existente. Este revalida firma, OCI, EF/esquema, G6, expiración, identidad y estado
bajo los locks, comprueba HTTPS/OIDC/versión y revierte ambas imágenes si falla.
No hay que pegar de nuevo bloques Python largos ni editar tres actas a mano.

La modalidad recomendada evita incluso confeccionar ese archivo de revisión.
Después de que la revisión técnica del SHA exacto esté registrada en #525:

```text
sudo pmgm-qa-update --review-and-run --source <SHA40> --digest <sha256:HASH64> \
  --technical-review-reference <URL-comentario525> \
  --g6-review /etc/pmgm/qa525-g6-reviewed.json --operator-name "<operador>"
```

Ese es UN comando explícito del operador: verifica el comentario público de
revisión, el SHA/digest, CI, esquema y G6; genera las actas con identidad IA
veraz, las valida físicamente y encadena preparación/aplicación. No convierte
un comentario cualquiera o una aprobación antigua en revisión del corte nuevo.
`--g6-review` contiene las cinco referencias g6_* del formato de revisión,
root0600, con copia externa y restauración realmente comprobadas. Puede ser
el acta sensible anterior si TODAS esas evidencias siguen siendo aplicables;
para el primer corte EF100 necesita evidencia posterior a la migración.

El comentario se publica por el revisor solo después de revisar el código y
las condiciones reales, como JSON completo (sin fences), autor de la cuenta
del proyecto. Campos exigidos: schema=`qa525-reviewed-cut-v1`, source_sha,
release_asset_sha256, review_kind=`automated_ai`, review_mode=`single_operator_qa`,
reviewed_sections con las cinco secciones ordenadas del acta y
status=`REVIEWED_FOR_QA_UPDATE`. Referenciar el comentario exacto creado, nunca
inventar un número de comentario. No usar este formato para afirmar revisión
humana/DBA ni para adelantar una revisión que todavía está pendiente.

La fuente se obtiene de gates completos, y su reutilización para emitir actas
solo ocurre dentro de ese mismo comando bloqueado, sin cache persistente.
Preparación y aplicación siguen descargando/verificando la firma de forma
independiente y ejecutando sus gates críticos. Los defaults de appsettings
de cuentas se comprueban además en el paquete firmado antes de build y apply.

No se renuevan fechas ni se autoaprueban revisiones. La vigencia efectiva sigue
siendo la menor de las dos revisiones +30min. Si caduca durante build, se detiene.
Un backup existente solo se reutiliza si su evidencia sigue vigente y cubre el
estado a proteger; no se rehace por rutina ni se marca verificado sin probarlo.

Si se completó la preparación pero una consulta de GitHub impidió la aplicación,
se puede intentar explícitamente el paso separado, con toda la evidencia aún
vigente y el journal sin consumir:

```bash
sudo pmgm-qa-update --apply-prepared
```

Esto no acepta una operación PENDING/ROLLED_BACK ni recicla un nonce consumido.
Ante una transacción interrumpida se inspecciona el estado real y se reconcilia;
no borrar journals para reintentar. No ejecutar un segundo actualizador paralelo.

## Instalación única del acceso corto

Después de verificar la firma del paquete que contiene estas herramientas y
comprobar sus gates, desde ESE paquete root-owned, ejecutar como root en glmdch:

```text
python3 -B <paquete-firmado>/scripts/qa/operators/qa525-update.py --install-launcher
```

Es una creación O_EXCL de `/usr/local/sbin/pmgm-qa-update` root0700. No reemplaza
un lanzador existente ni inicia una actualización. El lanzador usa la ruta
inmutable instalada. Al cambiar el propio operador, revisar y sustituir el
lanzador de forma controlada; no apuntarlo a código raw o una rama mutable.
Un cambio de aplicación sin nuevo esquema puede usar la ruta ya revisada.
Un nuevo esquema fuera de PR801/PR813 siempre requiere otro contrato y revisión.

## Archivo único de revisión

Plantilla: `scripts/qa/operators/templates/qa525-update-review-packet.example.json`.
**Deliberadamente inválida:** fechas0, referencias vacías y aprobaciones false.
No copiarla y cambiar todas las banderas a true. El revisor completa únicamente
hechos comprobados y referencias de revisión; contiene referencias/hash del
backup, nunca claves, contraseñas, conexión de BD ni contenido personal.

`review` es el acta sensible completa; `post_db` es el acta de esquema completo.
`schema_mode=member_history_postdb` conserva EF99/PR801;
`schema_mode=support00_postdb` exige EF100/PR813, su blob exacto y scope
`POST_PR813_API_WEB_ONLY_NO_MIGRATION`. Deben concordar fuente, digest, línea
base, respaldo y revisión. La firma y la sonda física siguen siendo obligatorias;
un archivo de revisión por sí solo no autoriza ni prueba el despliegue.

El estado histórico `archive.receipt.json` no se reescribe para fingir una copia
externa. La copia cifrada debe verificarse de verdad en Drive, junto con el hash
y la restauración del mismo corte; registrar la evidencia en #525 y Drive.

## Primera incorporación de Taller00: excepción explícita

El migrador está separado del comando habitual, y solo permite EF99→100 para
`20261030223000_SeedSupportWorkshop00`. No ejecuta Program.cs, bootstrap, otras
migraciones, contextos o Down. Su consola usa EF IMigrator con objetivo único;
antes exige historial99 exacto y pending={soloPR813}; después exige100 exacto.

1. Verificar corte firmado y preflight99, soporte0 y conflictos00=0.
2. Comprobar recuperación G6 actual de ambas BD+Silo, copia cifrada externa y
   restauración aislada. Para esta intervención DB el checkpoint debe tener
   menos de30min; el plazo48h del aplicador binario no basta. Esta es una causa
   real para renovar un respaldo que ya no cubra el estado, no una repetición
   de PR801 ni de la instalación original.
3. Crear root0600 `/etc/pmgm/qa525-support00-migration-approved.json` usando la
   plantilla del mismo nombre. Registrar el corte/digest, nonce, alcance,
   revisión técnica y referencia PO indicados por el contrato, sin secretos.
   Su referencia técnica debe ser el comentario estructurado del mismo
   SHA/digest descrito arriba; el operador también lo verifica en GitHub.
4. Del paquete firmado actual, ejecutar por separado:

```text
python3 -B <paquete-firmado>/scripts/qa/operators/qa525-support00-migration.py --status
python3 -B <paquete-firmado>/scripts/qa/operators/qa525-support00-migration.py --prepare-reviewed
python3 -B <paquete-firmado>/scripts/qa/operators/qa525-support00-migration.py --migrate-reviewed
```

La preparación construye el runner de ese mismo paquete firmado; no cambia la
BD. La migración usa un contenedor efímero sin puertos ni bind mounts, rootfs
de solo lectura, capacidades eliminadas y la única red ya compartida con API.
Toma la conexión DDL solo del contenedor migrador original verificado, mediante
archivo temporal root0600 eliminado al finalizar; no modifica API/WEB ni sus
flags. La consola solo admite Host=postgres, puerto5432, Database=pmgm.

El nonce se consume y el inicio PENDING se persiste ANTES de escribir. Una
excepción exige reconciliación de PostgreSQL, nunca retry ni Down automático.
Una ejecución exitosa deja recibo `support00-completed.json`, EF100 y
esquema exacto, y devuelve `SUPPORT00_EF100_VERIFIED_POST_DB_RECOVERY_REQUIRED`.
La hora real de ese recibo debe usarse como migration_completed_at; no inventarla.
El registro PENDING inicial se conserva como evidencia de inicio, acompañado
por el recibo separado de terminación. Sin recibo, PENDING es indeterminado.
El aplicador comprueba el recibo de terminación real (blob, identidad y hora),
además del acta y de la base viva; una fecha escrita a mano no lo sustituye.
Ese recibo histórico se preserva para las siguientes actualizaciones binarias.
Tras una interrupción abrupta pueden permanecer un contenedor efímero y un
archivo temporal root0600. Inspeccionar/reconciliar primero; no repetir la
migración ni borrar el journal para ocultar el estado.

5. Verificar evidencia G6 posterior al esquema100 para el acta postDB y su
   restauración. El comando de aplicación comprueba que ese checkpoint sea
   posterior a la migración; no permite sustituirlo por el snapshot preDB.
6. Completar el paquete de revisión binaria100 y usar `pmgm-qa-update --run-reviewed`.
7. UAT con usuario sintético: login, Mi Ficha/Secretaría y «Ver como» Taller/Orden;
   comprobar ausencia de415, contexto00 técnico, solo lectura, auditoría y bloqueo
   cruzado. Un health verde no sustituye esta aceptación funcional.

## Recuperación de preparado vencido y no aplicado (#819) — solo cuando el parche esté firmado e instalado

Caso real 31-10-2026 en `glmdch`: después de preparar las imágenes para
`a184936d78e6e9ee0fa85f7be060b13a5280710e`, la cuota pública GitHub
impidió aplicar; `prepared.json` siguió presente con estado
`PREPARED_API_WEB_SIGNATURE_VERIFIED_NO_DEPLOYMENT`, sin journal target,
mientras API/WEB anteriores `41d9c490d138419c7286445f37e7aeddf0d1b8d5`
continuaron activas. El control del operador abortó con
`VIGENCIA_RESTANTE_SEGUNDOS=-567`, `NO_APLICAR=VIGENCIA_INSUFICIENTE`.
No es un éxito ni un rollback de una operación iniciada.

**Prohibido** reescribir fechas (`reviewed_at`, `approved_at`, `expires_at`),
reutilizar un nonce, borrar/mover manualmente `prepared.json`, modificar
journals o recurrir a `--apply-prepared` cuando las actas han caducado.
Tampoco ejecutar `--review-and-run` de nuevo: esa ruta no archiva
una preparación anterior SIN journal APPLIED.

La nueva operación explícita `--archive-expired-prepared` **no aplica
ninguna imagen, no renueva actas ni toca PostgreSQL/EF100**. Solo existe
en una versión posterior del código. **No está instalada en el lanzador
actual de glmdch** por el hecho de existir en dev/PR; antes de cualquier
uso, pasar por PR, CI exact-head, publicación firmada y procedimiento
aprobado de instalación del operador actualizado desde paquete firmado
root-owned. No descargar ni ejecutar scripts raw de una rama.

Una vez que el propio operador revisado esté instalado y con la cuota y
runtime recuperados, el paso de archivo requiere un comando explícito,
SHA y digest del `prepared` caducado y operador real:

```bash
sudo pmgm-qa-update --archive-expired-prepared \
  --source <SHA_PREPARED_CADUCADO> \
  --digest <DIGEST_PREPARED_CADUCADO> \
  --operator-name 'soporte@glmdch'
```

Bajo el lock del QA, exige el preparado/las tres actas root0600, SHA y digest,
`schema_mode=support00_postdb`, actas **todas expiradas**, nonce de la
autorización sin consumir, journal de target ausente, journals anteriores solo
APPLIED, imágenes construidas existentes con identidad OCI exacta, snapshot
de servicios idéntico al preparado, estado y salud del WEB anterior,
capas Compose revisadas y guardas del operador único. Si no se cumplen,
aborta sin cambiar el preparado. Solo conserva los cuatro originales en
`review-archive-<id>`, con un manifiesto root0600 de hashes y estado
`EXPIRED_PREPARED_ARCHIVED_NO_DEPLOYMENT`. Los contenedores y la BD siguen
intactos; el archivo no equivale a revisión nueva ni permiso de aplicar.

**Después** de comprobar el recibo archivado (sin volcar datos privados),
el próximo corte debe obtener revisión técnica REAL nueva, source/CI exactos,
release Sigstore firmada y evidencia G6 vigente; usar una sola vez el
procedimiento normal `--review-and-run` con las referencias nuevas.
Un parche que cambia `dev` cambia el HEAD: las imágenes preparadas para
el SHA anterior NO deben aplicarse saltándose el gate `dev HEAD == release`.
Se reconstruyen las imágenes para el SHA nuevo y la misma EF100 verificada,
sin correr de nuevo la migración.

Nunca utilizar el archivo como bypass de una transacción PENDING/ROLLED_BACK:
esa condición exige reconciliación especializada. Ante cualquier `BLOCKED`,
registrar código, runtime y archivo sin modificar el host a mano. Las
pruebas QA/UAT autenticadas continúan pendientes aunque el controlador
informe `DUAL_API_WEB_APPLIED`.

## Recuperación y evidencia para la siguiente sesión

Conservar el recibo API histórico de98 migraciones, manifests y paquetes firmados,
prepared anteriores en `review-archive-*`, actas, journals duales, nonces consumidos,
`dual-events.log`, recibos de migración y G6 cifrado/restauración. No publicar
entornos, logs con datos personales ni conexiones. Registrar SHA físico, resultado
real, pruebas y pendiente en GitHub #525 y Estado Ejecutivo de Drive.

Las capas Compose originales (base, API/WEB e identidad) se preservan en orden.
Se aceptan capas de imagen QA525 anteriores SOLO con JSON exacto image-only,
root0600, nombre/OCI fijados y journal APPLIED correspondiente. No se compactan
ni se borran archivos históricos. Máximo128 capas: al alcanzar el límite se
requiere una compactación revisada, nunca descartar identidad silenciosamente.

## Cuota pública de GitHub y tiempos

El controlador conserva las consultas críticas de firma/fuente/CI, compartiendo
solo el resultado recién comprobado dentro de la misma operación. No utiliza
tokens personales ni cache persistente como sustituto de una comprobación viva.
La cuota pública de la IP puede agotarse por varias operaciones en la misma
ventana. Ante GITHUB_API_RATE_LIMITED/RETRY_LATER no repetir comandos en bucle:
esperar el reinicio informado por GitHub, inspeccionar el estado y conservar
journals. Si vencieron las actas, una revisión real nueva debe preceder al
reintento; nunca editar solo expires_at. Preparación, migración, G6 y publicación
pueden seguir requiriendo tiempo de CI/proveedor; el comando único reduce las
intervenciones manuales, no elimina esas comprobaciones ni garantiza duración.

No se instala un timer que despliegue arbitrariamente ni se habilitan migraciones
al iniciar API. El monitoreo puede ser automático; la escritura sigue atada al
corte revisado, evidencia vigente y acciones explícitas. No main, srv01,
producción, PR771/737/766 ni datos reales.


## Seguridad ante corte abrupto del archivado de preparados vencidos — 31-10-2026

El operador del PR #820 crea el marcador privado `expired-prepared-archive-pending.json` en el directorio QA525 **antes** de mover `prepared.json` y las tres actas a `review-archive-*`. Lo escribe con `root:root 0600`, incluye SHA, digest, nombre del archivo e hashes de las evidencias, y sincroniza a disco. Al finalizar el archivo completo y su recibo, sincroniza los directorios y quita el marcador. Si ocurre una excepción Python normal durante los movimientos, revierte los archivos; una prueba hermética inyecta un error en el segundo renombrado y confirma integridad y limpieza. Si hay `SIGKILL`, caída de energía o rollback incompleto, debe quedar el marcador y el próximo intento `--review-and-run`, `--run-reviewed`, `--apply-prepared` o `--archive-expired-prepared` **se bloquea** con `RECOVERY_ARCHIVE_PARTIAL_REQUIRES_RECONCILIATION` mientras el marcador esté presente. `--status` expone `archive_reconciliation_required` sin cambiar la máquina.

**El marcador NO es una aprobación, un journal APPLIED ni un permiso para continuar.** Jamás borrar manualmente ese marcador, usar otro launcher o fingir un prepared exitoso: inspeccionar originales y archivo parcial, validar hashes y journals bajo el procedimiento de recuperación previamente revisado. El PR todavía está DRAFT. La prueba de excepción no equivale a simulación real de pérdida súbita de energía ni prueba el funcionamiento en glmdch; se exige revisión adversarial independiente y liberación firmada antes del uso físico. La nueva actualización de QA también requiere resolver el reemplazo firmado del launcher inmutable actualmente fijado (O_EXCL), verificación de G6 y nueva aprobación válida.
