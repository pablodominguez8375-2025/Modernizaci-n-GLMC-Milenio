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