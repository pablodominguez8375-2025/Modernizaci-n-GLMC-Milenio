# QA525 — Aplicador API+WEB supervisado, firma, G6 y rollback dual

## Actualización aprobada 30-10-2026: operador único post-PR801 (PR #806)

Esta sección prevalece para el modo explícito de operador único sobre las
referencias históricas a revisión humana independiente de este documento.
No constituye ejecución ni UAT. El PO aprobó la propuesta #525/6527378960;
registro de decisión y reconciliación: [#525/6528024401](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/issues/525#issuecomment-6528024401).
La migración ya había sido autorizada con revisión técnica **automatizada de
ChatGPT**, sin afirmar DBA humano, en [#525/6512522202](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/issues/525#issuecomment-6512522202).

El entrypoint conserva la ruta DBA independiente original. Solo al añadir
`--single-operator-qa` después de `--prepare-reviewed` o `--apply-supervised`
selecciona el modo aprobado de un operador. No existe fallback por acta ausente.
Este modo solo admite el esquema conocido PR801, ya aplicado y verificado en
vivo, en `glmdch`/`QA_SYNTHETIC`, para preparación/aplicación binaria API+WEB.

En lugar de un acta DBA humana usa exclusivamente
`/etc/pmgm/qa525-single-operator-postdb-approved.json`, root/0600. Mantiene todos
los campos comunes del acta post-DB descrita en
[contrato PR801](QA525-PR801-PR802-SCHEMA-CUT.md), **excepto**
`dba_independently_reviewed`, que está prohibido en este modo. Añade exactamente:

| Campo | Contrato |
| --- | --- |
| `review_mode` | `single_operator_qa` |
| `environment` / `host` | `QA_SYNTHETIC` / `glmdch` |
| `scope` | `POST_PR801_API_WEB_ONLY_NO_MIGRATION` |
| `sponsor_approval_reference` | URL exacta de #525/6528024401 |
| `sponsor_accepts_single_operator_risk` | `true`, solo tras aceptación real del PO |
| `independent_human_review` | `false`; no afirmar segunda persona |
| `technical_review_kind` | `automated_ai` |
| `technical_review_reference` | URL de comentario real #525 con revisión técnica del corte |
| `operator_name` | Nombre del operador real, sin caracteres de control |

La revisión sensible y aprobación local temporal de uso único siguen siendo
obligatorias, por separado y con sus campos anteriores. No generar ninguno de
estos registros como plantilla preaprobada; fechas/identidad/hashes deben provenir
de la evidencia real. El nuevo acta queda ligada a SHA/digest, blobs EF, G6
postmigración y revisión actual de máximo 30 minutos, igual que la ruta DBA.
El G6 mantiene su ventana de 48 horas y la comprobación de restauración y hash.

Se exigen explícitamente las dos variables de migración en `false`. Introspección
y mutaciones de cuentas deben estar ausentes (defaults del corte revisado) o en
`false`; el modo rechaza valores activados o ambiguos y no cambia configuración.
La ausencia en el entorno no prueba otras fuentes de configuración: revisar
también la configuración del paquete firmado antes de atestiguar el acta sensible.
El recibo de preparación registra `review_mode`; no se puede aplicar una
preparación bajo otro modo. Los recibos históricos sin ese campo solo pertenecen
a la ruta DBA original. Los controles de firma, HEAD, manifiesto, locks, capas
Compose/identidad, imágenes anteriores retenidas y rollback dual se conservan.

Antes de usar el modo: PR806/CI exact-head y nuevo corte firmado aprobados;
revisión técnica y aceptación real del operador; crear registros privados solo
tras comprobar evidencias; preparar imágenes y luego aplicar supervisado con
UAT sintética. No reutilizar el código de `ceaca327` como si ya tuviera el modo
nuevo, no repetir EF99/G6 válidos ni tocar PR804/805, main, producción o srv01.

El resto del documento describe la ruta original y sus antecedentes históricos.

**Estado: PR #800 DRAFT / DESARROLLO. NO instalado ni probado en glmdch.**
Objetivo exclusivo: QA sintético en la ÚNICA VM \`glmdch\`. No tocar \`main\`, producción, \`srv01\`, datos reales, claves institucionales, Keycloak/SMTP ni migraciones. NO ejecutar operadores obsoletos de PR #771/#737/#766 ni scripts \`backup-srv01*\`.

## Antecedentes físicos verificados

- GitHub #525 comentario **6504066245**: G6 de **2026-10-29 17:24:34 UTC** restauró **pmgm**, **pmgm_keycloak**, roles y objetos Silo en contenedores efímeros de la misma VM. Exit code **0**; red/recursos descartables eliminados; sin DR por pérdida completa de host.
- Drive carpeta privada \`QA glmdch - Respaldos cifrados\`, archivo \`centenario-qa-g6-CIFRADO-20261029T172434Z.tar.gz\`, ID \`1TErsKdCZd7LzJD8aST6NV0iiqlUeNYT9\`, SHA256 \`8dd3303da2feab945b30051763f523e957803e61e4779f558e93acefefe7c245\` contrastado con el host.
- \`dev@0bd14cb81a1c0e607e04283083f77a5ddf36b339\`; release histórica \`qa-signed-<SHA>\`, asset SHA256 \`aae33a8da6776e55f689d7f1b6269c66f5a9bd185a035b661d595cfa72ce3dd4\`. **No asumir** que la publicación sigue siendo HEAD cuando se ejecute: el gate en vivo aborta si cambió.
- El G6 es un *checkpoint*; política de máximo **48 horas** para aplicar. Una nueva copia fresca debe pasar copia Drive+hash y restore aislado por snapshot si el tiempo vence. No usar por inercia la copia del 29-oct una vez caducada.

## Revisión de diferencias sensibles (código)

Comparación GitHub exacta \`6ff4ae0d092d4214df4ea31af484efbc969fde54...0bd14cb81a1c0e607e04283083f77a5ddf36b339\`: **39 commits / 160 archivos**, con cambio sensible en:
- \`backend/src/PMGM.Api/Program.cs\`: +24 líneas. Incorpora introspección de tokens opcional, inyección de cliente y guardias de arranque. El mismo fichero contiene ruta \`Database:ApplyMigrationsOnStartup\` capaz de ejecutar EF; debe permanecer explícitamente desactivada para la API runtime, al igual que \`Database:RunMigrationsAndExit\`. La herramienta comprueba las variables efectivas y **no llama** a migrator.
- \`Modules/UserAccounts/UserAccountEndpoints.cs\`: alta, inventario, activación, desactivación y eliminación; exige verificación real del Master único y protección de cuentas especiales, no asumir autorización por menú.
- \`Modules/UserAccounts/KeycloakAccountIdentityProvider.cs\`: +190/-2; inventario y mutaciones de identidades, suspensiones/sesiones. Revisar la revocación de JWT tras desactivar/eliminar y relación usuario/miembro antes de prueba autenticada.
- \`AccountLifecyclePolicy.cs\` y \`AccountTokenIntrospection.cs\` nuevos. Revisión manual de políticas de Master y \`qa.admin\`, revocación, verificación de identidad y seguridad de rol; flags de introspección/mutación deben conservar el valor aprobado en QA.
- \`infrastructure/keycloak/pmgm-qa-public-realm.json\` y \`pmgm-realm.json\` modificados: **NO reimportar realm** ni recrear cuentas. El operador solo recrea API/WEB.
- \`UserAccounts:Mail\` / SMTP y cifrado de correo: aunque no se observa fichero SystemMail modificado en esta comparación, revisar credenciales/URLs **en privado**, nunca registrar valores; los bytes y parámetros efectivos deben mantenerse. El operador compara hashes del entorno de API/WEB antes/después sin imprimir sus secretos.
- **EF/migraciones:** el inventario de 160 rutas no registra ficheros \`/Migrations/\` ni \`DbContext\` modificados; el aplicador repite una comparación de GitHub en vivo y BLOQUEA si aparece alguno. Esto no reemplaza inspección DBA del esquema/EF de QA, ni autoriza migraciones destructivas.

**La revisión anterior es técnica y preliminar; no es aceptación funcional de mutaciones de usuarios.** Antes del primer apply debe revisarse en privado evidencia exacta de Keycloak, correo, roles y banderas, luego UAT autenticada con usuarios sintéticos. El resultado de CI no sustituye esa revisión.

## Diseño implementado en el PR #800

- \`qa525-supervised-dual-transaction.py\`: núcleo con contratos puros de baseline 7 servicios + 3 one-shots, mismas imágenes de dependencias, capas API (base + override + \`qa754\` identidad) y WEB (base + override), overrides adicionales **inmutables** que solo fijan \`services.api.image\` / \`services.web.image\`. No ejecuta \`down\`, \`rm\`, migrator, rebuild, pull ni \`up\` de dependencias. Primero API y después WEB, con \`--no-deps --no-build --pull never --force-recreate\`; revisión de salud HTTPS y OIDC. Ante cualquier fallo intenta revertir **ambos** servicios, WEB y API, a IDs de imagen anteriores. Una caída brusca o fallo de rollback deja journal persistente \`RECOVERY_MANUALLY_REQUIRED\`, no reintenta ciegamente.
- \`qa525-supervised-dual-entrypoint.py\`: comandos de uso manual **\`--prepare-reviewed\`** (revalida release/firma Sigstore y manifiesto, prepara ambas imágenes sin cambiar contenedores) y **\`--apply-supervised\`** (exige recibo de preparación, verificación posterior de firma, G6 restaurado, diferenciales sensibles, aprobación actual y lock existente de QA). Solo root en \`glmdch\`. Consume nonce local irreversible para impedir reuso.
- Reutiliza validadores firmados de \`qa525-stage-current-release.py\`, imágenes \`qa525-current-image-preparation.py\`, política \`qa525-current-change-policy.py\`, aprobación \`qa525-supervised-cut-approval.py\` y transporte Docker saneado \`qa-lite-compose-adapter.py\`. No introduce un segundo sistema de firma.
- Prohíbe cambios de contenedores/volúmenes/puertos/redes o variables de identidad sin rollback. Los únicos componentes que pueden recrearse son API y WEB. Estado y logs root/0600, sin contraseñas ni dumps.
- CI hermético y adversarial **NO acredita ejecución real en glmdch**; antes del uso físico faltan revisión independiente de código PR, puertas CI exact-head, prueba de Docker/Compose en entorno sintético, y control de compatibilidad del instalador en esa única VM.

## Condiciones obligatorias de operación (NO un comando de instalación)

1. Revisar/fusionar PR únicamente hacia \`dev\` tras gates; instalar fuentes **de confianza** en árbol root-owned, no desde scripts copiados de un chat. Verificar sha256 del código instalado contra commit aprobado. El PR en sí no instala código.
2. Crear administrativamente el directorio \`/opt/centenario/qa525-signed-prepare\` root/0700 tras verificar ancestros, y preparar el acta de revisión sensible *fuera del repositorio* en \`/etc/pmgm/qa525-sensitive-reviewed.json\`, root/0600, más el artefacto de aprobación de corte \`/etc/pmgm/qa525-supervised-cut.json\` conforme al contrato ya existente de PR #793. **Nunca subirlos a GitHub/Drive ni compartirlos por chat.**
3. El acta sensible requiere los campos exactos: \`source_sha\`, \`release_asset_sha256\`, \`baseline_sha\`, \`reviewed_sections\` (exactamente \`Program.cs\`, \`UserAccounts\`, \`Keycloak/SMTP\`, \`EF/migrations\`, \`Compose/identity\`), \`sensitive_reasons\` (clasificación exacta del gate dinámico), \`no_new_ef_migrations\`, \`runtime_migration_flags_disabled\`, \`legacy_master_and_smtp_preserved\`, \`reviewed_at\` (Unix UTC dentro de 30 min), \`g6_archive_name\`, \`g6_archive_sha256\`, \`g6_drive_file_id\`, \`g6_offhost_sha256_verified\` y \`g6_restore_report_name\`. Son **afirmaciones del revisor**, no pruebas criptográficas por sí mismas. No rellenar \`true\` por defecto; comprobar cada una.
4. Tras la aprobación local y primera revisión, el administrador autorizado puede invocar **\`--prepare-reviewed\`** con el script root verificado. Solo construye imágenes desde el ZIP firmado y escribe \`prepared.json\`. No toca contenedores. No se ejecutará en este chat.
5. Tras nuevas verificaciones, con aprobación local aún vigente, el administrador autorizado invoca **\`--apply-supervised\`**. El operador adquiere locks concurrentes, verifica que el SHA objetivo siga siendo HEAD, vuelve a descargar y comprobar firma, valida \`archive.receipt.json\` + \`isolated-restore-<UTC>.report.json\`, valida que los diez componentes Docker estén exactamente como en baseline y consume autorización de uso único.
6. Verificar después desde sesión real sintética (sin contraseñas en reportes): Home, \`/health/web\`, \`/health/ready\`, discovery HTTPS de Keycloak, Master único, \`qa.admin\`, lifecycle de cuentas, SMTP y permisos por rol/taller. Registrar SHAs físicos y estado de rollback. Si no se cumple un control, **no avanzar a despliegue**. El operador NO ejecuta UAT autenticada por sí solo.

## Límites y riesgos explícitos

- Las firmas Sigstore verifican procedencia del paquete, **no garantizan** que Program.cs o políticas de usuarios sean correctos. La evidencia de revisión sensible debe ser auténtica y distinta del desarrollador.
- Sin API autenticada para Drive desde \`glmdch\`, el hash de copia externa se constata documentalmente por revisión humana/IA autorizada y queda en acta local; el host comprueba SHA del TAR local y reporte de restauración. **No afirmar** que el host verificó la copia remota directamente.
- Si cambian las capas físicas de Compose (por ejemplo tras una activación anterior que agregó un override), esta versión de **primer corte** BLOQUEA y requiere actualizar contrato y nuevo PR. No suprimir archivos de configuración ni sobrescribir overrides antiguos.
- Reversión de imágenes API+WEB no revierte migraciones de DB: por eso se PROHÍBE ejecutar migraciones en esta primera aplicación y se preserva estado PostgreSQL/Keycloak/Silo. Fallas de energía/SIGKILL y reconciliación tras caída requieren procedimiento administrativo separado de recuperación de journal; no hay un "rollback probado" para esos casos.
- La prueba G6 same-host no demuestra DR ante destrucción de la única VM; limitación aceptada para QA sintético, no para operación productiva.

## Trazabilidad

Seguimiento [Issue #525](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/issues/525), [Issue #97](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/issues/97), comentario [6504066245](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/issues/525#issuecomment-6504066245), Estado Ejecutivo Drive \`1rqG3YAi_csweWyxT4362Wzf3guJRdzy3foqn-Pvizf0\`. No se modifica \`main\` ni se ejecuta despliegue por crear este PR.


## Auditoría de hardening — continuidad y rollback verificable

Correcciones incorporadas tras la primera revisión del PR:

- **Fuente dinámica, autorización específica**: no se fija \`dev@0bd14...\` dentro del aplicador. El SHA objetivo procede del selector de **HEAD vigente firmado**; el acta de revisión sensible, la aprobación temporal y el recibo de imágenes deben coincidir exactamente. Integrar este PR en \`dev\` cambia el HEAD: exige nueva release firmada y nueva aprobación, **nunca se reutiliza** la release del SHA antiguo.
- **Capas inmutables**: el operador obtiene las cuatro rutas de Compose **directamente de las etiquetas físicas** API/WEB; exige base compartida, override API/WEB e identidad \`qa754\` en el orden observado. Calcula SHA-256 de los cuatro archivos root-owned, vuelve a comprobarlos en cada snapshot, y verifica que después de cada recreación solo exista un override adicional \`qa525-<servicio>-<source>-<imagen>.json\` con contenido exacto \`services.<servicio>.image\`. También coteja hash de entorno (sin mostrar secretos), montajes, redes y puertos.
- **Retención de dos imágenes anteriores**: antes de mutar contenedores, dentro de los mismos locks, verifica los IDs OCI activos y les añade etiquetas \`pmgm-qa525-rollback:<servicio>-<source>-<old-image>\`. Si una etiqueta apunta a otro ID, deniega; después confirma que ambas etiquetas siguen resolviendo a sus respectivos hashes. Previene el borrado accidental de imágenes *dangling* ordinarias (no protege ante borrado forzado por otro administrador).
- **Journal de energía**: la transacción sincroniza el archivo \`dual-<sha>.json\` y su directorio antes del primer Compose up. Un journal anterior \`PENDING\`, \`APPLIED\` o \`RECOVERY_MANUALLY_REQUIRED\` deniega el reintento silencioso; requiere conciliación manual si hubo interrupción.
- **G6**: confirma hash del archivo cifrado **local**, acta administrativa de verificación off-host, reporte de restauración aislada por nombre+SHA y edad inferior a 48 horas. No verifica Drive desde el host en tiempo real; la verificación externa sigue siendo evidencia humana cotejable. La herramienta no expone claves, env, manifiestos privados ni datos personales.
- **No sustituir UAT**: flags de introspección y mutaciones de cuentas siguen OFF por defecto en el código; antes de activarlos se requieren pruebas reales con cuentas sintéticas, verificación del único Master, recuperación SMTP y garantías de revocación de JWT. Una versión de API nueva no concede derecho automático a eliminar usuarios.

**Gates actuales de desarrollo:** pruebas herméticas de transacción, revisión sensible, cadena Compose y etiquetas rollback en GitHub Actions; **ningún test con fake Docker equivale a un ensayo físico del aplicador**. Mantener PR como draft hasta revisión independiente y controles exact-head. La única VM sigue siendo \`glmdch\`; no provisionar otra.



## Tercera auditoría: contexto Compose y EF físico

Se cotejó la llamada Docker Compose original con la línea base read-only `scripts/qa/operators/qa525-host-baseline-6ff4-readonly.py` ya fusionada. El contrato de `glmdch` exige `--env-file /opt/centenario/qa-auto-lite/empty.env` en el **mismo** contexto de API y WEB. El operador QA525 conserva esa opción tanto en apply como en rollback; antes de recrear contenedores comprueba que sea un archivo root/0600, sin asignaciones (solo comentarios/blancos), y fija su SHA-256 para detectar cambios durante la transacción. Si falla, se cancela sin ejecutar Compose. No se sustituye por variables exportadas en la terminal.

La revisión sensible de EF se reforzó con consulta **solo lectura** al PostgreSQL activo: `BEGIN READ ONLY; SELECT "MigrationId" FROM public."__EFMigrationsHistory" ORDER BY 1; COMMIT;`. Compara estrictamente el conjunto de las 98 migraciones esperadas en el recibo root de API `/opt/centenario/qa-api-refresh/installed.json`; cualquier migración agregada, eliminada, faltante o duplicada bloquea el primer apply y requiere plan DBA separado. Este control complementa, sin reemplazar, la comparación de archivos GitHub y los flags de migración desactivados. No se tocan dos bases ni se ejecuta migrator.

El código mantiene bloqueo sin autorización física vigente del operador y revisión humana de Program.cs/UserAccounts/Keycloak-SMTP. No afirmar que pruebas herméticas de GitHub demuestren que la configuración Compose física está lista. PR #800 permanece DRAFT hasta validación independiente de ese diseño; solo existe `glmdch`.

## Adenda PR #801/#802 — excepción aditiva pos-DB cerrada por defecto (29-10)

La primera versión del documento describía un aplicador **sin migraciones**: esa regla sigue vigente para el operador API+WEB, que nunca ejecuta el migrador ni `Down`. La implementación actual en PR #800 añade una ruta de **comprobación previa de migración ya ejecutada por un DBA mediante un procedimiento separado**. No es permiso implícito de migrar ni de desplegar.

El enlace compara HEAD firmado con los blobs exactos PR #801, exige acta root/0600 de un revisor/DBA independiente (archivo `/etc/pmgm/qa525-member-history-dba-approved.json`) y otra acta sensible vigente. La prueba física `qa525-post-db-readonly.py --inspect-readonly`, que solo funciona en `glmdch` root, verifica la historia EF original de 98 IDs contra 99 IDs en PostgreSQL y la existencia de dos tablas y tres índices esperados. El respaldo cifrado G6, copia privada externa y restore aislado deben ser **posteriores a la intervención DBA** y quedar vinculados a las actas y a la release exacta.

El campo `no_new_ef_migrations` del acta sensible debe ser `false` **solo** cuando la excepción conocida pasa todos los controles físicos con aprobación DBA. Cambiar la bandera sin migración y prueba real produce bloqueo. Cualquier cambio EF adicional, diferencia de blob, otra release, DBA sin aprobar, G6 obsoleto o esquema inesperado mantiene fail-closed. Sigue prohibido manipular el recibo histórico del API instalado para fingir 99; la comparación usa lectura efectiva del contenedor PostgreSQL. Detalles y campos del acta DBA se enumeran en `docs/qa/QA525-PR801-PR802-SCHEMA-CUT.md`.

**Aún faltan antes del uso físico:** revisión de seguridad independiente del PR #800 (incluida la nueva excepción), CI exact HEAD y merge autorizado a `dev`, instalador con archivo/hashes root, procedimiento DBA de uso único y prueba controlada real de Docker/Compose. Ninguno de esos pasos se acredita por los tests herméticos. Solo después corresponde operar desde el hilo QA y cerrar UAT sintética. Hasta ese momento, no iniciar `--prepare-reviewed` ni `--apply-supervised`.
