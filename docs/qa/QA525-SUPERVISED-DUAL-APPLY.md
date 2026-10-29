# QA525 — Aplicador API+WEB supervisado, firma, G6 y rollback dual

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
