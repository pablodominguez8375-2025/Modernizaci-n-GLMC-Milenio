# QA525 — Ruta mínima y segura para incorporar PR #801 y #802 a QA

**Estado: procedimiento supervisado propuesto, NO ejecutado.** Una sola VM `glmdch`, exclusivamente QA sintético; `main`, producción y `srv01` fuera de alcance. No usar aplicadores descartados de PR #771/#737/#766.

## Fuente y cambio exactos

- **dev observado 29-oct-2026:** `b27509d935516c6e7aa08a7f3cb76dc731b97e72`, PR #802 (ayuda del módulo Historia del hermano) fusionado encima del PR #801.
- PR #801: única migración EF nueva identificada en comparación de `6ff4ae0d092d4214df4ea31af484efbc969fde54...b27509d935516c6e7aa08a7f3cb76dc731b97e72`:
  - `backend/src/PMGM.Api/Migrations/20261029120000_AddMemberHistoryProposals.cs`, Git blob `f3d29c00fb8b153a3dee4a2aa954a70879826f23`.
  - `backend/src/PMGM.Api/Data/PmgmDbContext.cs`, Git blob `9de850e92d9addce7f47de99c67fa052b99408c5`.
- El `Up` revisado contiene dos `CreateTable` (`core.member_history_proposals`, `core.member_history_proposal_items`) y tres `CreateIndex`. El `Down` contiene `DropTable`: **no ejecutar Down como rollback automático**, pues eliminaría datos capturados después de la migración.
- PR #802 solo cambia cuatro archivos de ayuda/interfaz/pruebas, sin migraciones nuevas. Los blobs anteriores están vinculados a este corte: si dev o la fuente firmada cambian, volver a revisar.

## Gates, en orden estricto

1. **Liberación firmada al HEAD vivo:** congelar un SHA exacto de `dev` y comprobar firma Sigstore, tag, paquete, manifiesto, CI y revisión independiente. No reutilizar el ZIP del SHA `0bd14c...` si el HEAD vigente es otro. Preparar imágenes API+WEB solo desde ese paquete verificado, sin activar contenedores.
2. **Respaldo G6 renovado antes de la modificación de DB:** ambos PostgreSQL (pmgm, pmgm_keycloak), roles y objetos Silo con Restic cifrado. Subir SOLO archivo cifrado a la carpeta privada Drive y verificar hash/bytes independientes; ensayo de restauración aislada del MISMO corte con `G6_RESTORE_EXIT_CODE=0`. El ensayo histórico del 29-oct se conserva pero puede caducar para nuevas operaciones (48h).
3. **DBA/supervisión de migración SEPARADA:** bajo locks y autorización documental de alcance único, verificar primero historial EF de 98 IDs del recibo API instalado. Comparar blobs de migración/modelo con los fijados arriba y revisar SQL/DDL generado mediante cadena de confianza. Ejecutar únicamente el procedimiento institucional de migrador EF *de un solo uso* y con identidad DDL controlada. Mantener API runtime `Database__ApplyMigrationsOnStartup=false` y `Database__RunMigrationsAndExit=false`. No inventar ni ejecutar comandos Compose, scripts `srv01`, `Down` ni ALTER/DROP a mano. Si falla el migrador, **detenerse** y reconciliar bajo el respaldo; no repetir a ciegas.
4. **Post-DB solo lectura:** `public.__EFMigrationsHistory` debe contener **exactamente** los 98 IDs antiguos más `20261029120000_AddMemberHistoryProposals` (99 únicos), y `pg_catalog` debe reflejar ambas tablas y tres índices esperados. Rechazar cambios de historia, modelos, columnas o índices no revisados. Crear un nuevo G6 tras confirmar el estado migrado si se requiere continuidad/rollback sobre datos nuevos; comprobar hash externo/restore del corte según puerta de despliegue.
5. **Aplicación API+WEB dual, solo tras DBA y revisión del PR #800:** revisar `Program.cs`, `UserAccounts`, Keycloak/SMTP y roles; aprobación root temporal de uso único, cinco locks QA, capas Compose originales y `qa754` identidad, `--env-file empty.env`, imágenes OCI antiguas retenidas, transacción de API y WEB con rollback de **ambas**. NUNCA se habilita este paso desde el gate offline adjunto, que devuelve `may_apply:false`.
6. **UAT de sintéticos:** /, /health/web, /health/ready, OIDC, /qa-version.json coincidente con SHA WEB firmado; crear cuenta/SMTP, visibilidad Máster y qa.admin, activar/desactivar/eliminar solo con política aprobada, y roles por taller; nueva historia de hermano, propuesta Secretaría, aprobación/observación/rechazo Régimen Interior, ayuda del #802 y auditoría. Registrar hashes físicos y evidencias sanitizadas.
7. **Rollback**: si la WEB/API falla, revertir imágenes de ambas con journal y locks; **conservar las tablas aditivas** mientras el API viejo sea compatible. Un rollback de esquema requiere una decisión DBA distinta tras reconciliar datos y nunca se automatiza mediante `Down`.

## Herramienta para cotejo antes de autorizar

`scripts/qa/operators/qa525-member-history-schema-gate.py` más `tests/test_qa525_member_history_schema_gate.py` implementan verificaciones **puras y herméticas** (sin CLI, SSH, credenciales, PostgreSQL ni acciones Docker): un único cambio de migración/modelo, blobs exactos revisados, solo operaciones CreateTable/CreateIndex del Up, inventario de dos tablas/tres índices, y lista EF 98→99. Todos sus resultados dicen **`may_apply:false`**: pruebas de contrato, nunca permiso de despliegue.

## Bloqueos restantes

El aplicador del PR #800 continúa intencionalmente **fail-closed** si detecta un `/Migrations/` o `DbContext` nuevo; NO abrir mediante modificación directa de banderas. Se requiere integrar el gate de migración posterior a una intervención DBA real y una aprobación específica, revisar con segunda persona/IA y pasar un ensayo seguro del Compose físico. El respaldo aislado no garantiza DR completo ante pérdida de la única VM. Registrar cada evidencia en GitHub #525 y Estado Ejecutivo Drive.

**Ruta de ejecución:** las escrituras y CLI físicas se llevan únicamente en el hilo **«Montaje QA operativo»** una vez cumplidos los pasos de desarrollo/validación; este documento no autoriza por sí solo una operación.
## Verificación física posmigración, solo lectura (añadida 29-10)

Se preparó `scripts/qa/operators/qa525-post-db-readonly.py` con invocación explícita `--inspect-readonly`, únicamente como root verificado en la VM `glmdch`, desde código instalado root-owned y revisado. Usa la adaptación Docker local existente; **solo** ejecuta tres consultas SQL de literal fijo dentro de `BEGIN READ ONLY` sobre `pmgm`: IDs EF, nombres de las dos tablas y nombres de índices del módulo. Coteja los 98 IDs históricos del recibo API instalado con los 99 IDs de la base tras la intervención DBA, y exige las dos tablas y los tres índices revisados. Salida solo de contadores/estado, nunca IDs, filas, SQL de respuesta ni secretos; rechazo seguro ante error. No ejecuta migrador, reinicios, Compose, `Down`, escritura o despliegue.

**Importante:** el recibo `installed.json` original de 98 migraciones se mantiene **solo como línea base**; no falsificar ni editar ese recibo para simular 99. La prueba de 99 debe provenir exclusivamente de PostgreSQL en vivo después del migrador separado y aprobado. La herramienta devuelve `may_apply:false` incluso con PASS; no abre el gate del entrypoint API+WEB, que seguirá bloqueando el cambio EF hasta una integración supervisada específica revisada y nueva aprobación de uso único. Verificar controles DBA, snapshot G6 posterior, columnas/constraints/índices con revisión independiente y rollback binario compatible antes de cualquier apply. La existencia de esta herramienta y CI verde **no autoriza** entrar a ejecutar una migración sin el plan/acta correspondiente.

Pruebas adversariales: `tests/test_qa525_post_db_readonly.py`, incluidas en el workflow QA525 (fake Docker), con rechazo de migraciones ausentes/extra/duplicadas, tabla/índice faltantes, SQL no permitido, parámetros de aplicación y ejecución no-root. No se ha ejecutado la sonda en glmdch ni aplicado la migración. La próxima fase precisa intervención física en el hilo `Montaje QA operativo` **solo tras** revisión/plan DBA y sin saltar el aplicador fail-closed.

## Contrato de enlace DBA → aplicador (desarrollo, revisión obligatoria)

El entrypoint supervisado PR #800 ahora contempla una **excepción estricta**, exclusiva para PR #801. Su secuencia bajo locks: obtiene el HEAD vigente y release firmada; exige que los blobs Git de la única migración `20261029120000_AddMemberHistoryProposals.cs` y `PmgmDbContext.cs` coincidan con las dos huellas revisadas; lee el acta privada del DBA; coteja respaldo G6 cifrado y restauración aislada **posteriores** a la intervención; consulta en `pmgm` con transacciones `BEGIN READ ONLY` el historial exacto 99 y objetos esperados; revalida durante preparación/aplicación. Si aparece otra migración, otro blob, falta índice, acta o G6, debe DENEGAR. La excepción no ejecuta migraciones y no habilita `Down`.

El administrador y revisor independiente, **después de ejecutar de manera autorizada la migración por vía DBA separada**, deberán mantener un acta fuera de GitHub/Drive/chat, en `/etc/pmgm/qa525-member-history-dba-approved.json` (root/0600), con campos exactos:

- `source_sha`, `release_asset_sha256`, `baseline_sha`;
- `migration_id`, `migration_blob_sha`, `dbcontext_blob_sha`;
- `migration_completed_at`, `approved_at` (Unix UTC; aprobación fresca de máximo 30 minutos);
- `dba_independently_reviewed`, `additive_up_only`, `previous_api_forward_compatible` (solo `true` tras evidencia real, no por defecto);
- `post_db_g6_archive_name`, `post_db_g6_archive_sha256`, `post_db_g6_restore_report_name`.

El acta DBA debe coincidir con el acta sensible independiente de API/WEB `qa525-sensitive-reviewed.json` y con el respaldo G6 posterior a la migración; esta última acta debe reconocer **`no_new_ef_migrations:false`** solamente cuando el gate conocido y el cotejo físico han concluido PASS. Si no, falla. **Prohibido** editar el recibo `installed.json` que describe el API anterior (98 migraciones) o declarar `false` para burlar el guard. La herramienta física de lectura `qa525-post-db-readonly.py --inspect-readonly` siempre emite `may_apply:false`. Solo el flujo integral, revisado y aprobado puede proseguir con preparación/activación; en ningún caso un JSON privado es prueba suficiente por sí solo.

**Límites:** la inspección automática del esquema comprueba historial, existencia de tablas e índices esperados, no garantiza por sí misma equivalencia total de columnas, constraints, datos, compatibilidad del API anterior, custodia externa de clave ni autorización institucional. Para ello exige revisión humana/DBA independiente y UAT sintética. Antes de pasar a la activación física, cerrar código+CI exact-head y la revisión externa de este cambio; no interpretar pruebas fake Docker como rollback físico.
