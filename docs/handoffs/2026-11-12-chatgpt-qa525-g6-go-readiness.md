# QA525 — Handoff 2026-11-12 — ChatGPT — preparación de GO físico G6 (NO_GO)

## Fuentes verificadas al inicio
- GitHub `dev@5e09d7b5718309e31f639a1eb45890546e7b29cf`; `main@6dfb9546a4873baff15955cf86abfd7d47e3d111`, ambos consultados por endpoints de ramas; no usar SHAs documentales como estado vivo.
- Leídos en ese HEAD: `START-HERE.md`, `AGENTS.md`, `README.md`, `docs/PMGM-GOV-001-instrucciones-decisiones-consolidadas.md`, `docs/PMGM-GOV-003-coordinacion-multi-ia.md`, `docs/PMGM-BASE-001-estado-maestro.md` (consulta focal), `docs/handoffs/2026-11-09-chatgpt-qa525-g6-flight.md` y documentación G6.
- Issue #525 consultado hasta último comentario visible #6840091629; etiqueta `agente:chatgpt` verificada.
- Todos los PR abiertos detectados y sus rutas listadas (14: #1002, #1001, #1000, #843, #841, #805, #804, #771, #766, #765, #764, #737, #479, #1); ningún PR ajeno toca los dos archivos nuevos de #1002 ni este handoff.
- Google Drive carpeta Proyecto Centenario: Línea Base Maestra oficial, «00 START HERE», adenda 2026-11-12 de QA525/launcher y otras adendas recientes consultadas. Registro oficial QA525 en Issue #525 prevalece sobre checkpoints anteriores; metadata del Drive no equivale al host.

## Etapas diferenciadas
- **EN CÓDIGO:** PR draft #1002; dos archivos nuevos: `scripts/qa/qa525-g6-backup-finalize-guard.py` y `scripts/qa/test_qa525_g6_backup_finalize_guard.py`. Función pura para rechazar estados finales ambiguos MinIO, watchdog, custodia GO y hashes; NO integrada al GO-H.
- **CI:** al inspeccionar HEAD del PR `4ac7a2cee0de22e48dc51783c2a4c762c2ba80a1` varias checks estaban aún IN_PROGRESS; no concluir SUCCESS. Tests del archivo nuevo no acreditados aún por ejecución explícita.
- **RELEASE:** no existe paquete final de G6 backup confirmado ni firma/sha256 entregados en esta sesión.
- **QA FÍSICO:** sólo antecedente documental en Drive de `LAUNCHER_ONLY PASS`, G6 init y preflight MinIO: se requiere verificación de glmdch para cualquier nuevo GO. Ninguna operación realizada en glmdch en esta sesión.
- **UAT:** no ejecutado. **MAIN:** no modificado. **Despliegue QA pendiente** para cualquier versión nueva API/WEB.
- **Pages:** no se confirmó `qa-current.json` en esta sesión; SHA publicado = UNKNOWN.
- **Paquete QA:** sha256 = UNKNOWN/no generado por esta rama.
- **Restauración G6:** pendiente; `G6_BACKUP=NO_GO`.

## Coordinación
- Issue #525 reclamado por agente:chatgpt. PR #1002 titulado [CHATGPT] y registrado draft con Issue/alcance/archivos/cierre estimado.
- **No conformidad heredada:** nombre de rama `qa525/g6-backup-finalize-guard-20261112` no sigue `feature|fix|docs/<tema>-AAAAMMDD-gpt`. No fusionar #1002 sin resolver por flujo trazable, sin reescribir el trabajo de otro agente. PR #1001 de Claude se mantiene sin fusionar, conserva corte físico actual.
- Vigente una sola QA física: `glmdch`, ningún `srv01` ni segunda VM.

## Próximo paso técnico mínimo
1. Terminar caller de una sola sesión con comprobación real final `MinIO running+healthy` y timer `inactive` confirmada, sin éxito falso por fallo de `systemctl stop`.
2. Pruebas negativas sobre cleanup, `sudo -n`, SIGTERM/desconexión, errores Restic, custodia GO, timeout y reinicio independiente. No inventar resultados.
3. Completar revisión/firma exact-head, tests y paquete reproducible; mantener ramificación conforme a GOV-003 antes de merge. Obtener autorización escrita específica del Sponsor para parar MinIO en G6 **solo después** de disponer de artefacto verificable.
4. Posteriormente: respaldo cifrado, custodia fuera del host, readback hash, restauración aislada, revisión y recién entonces GO API/WEB independiente.

## No tocar / salvaguardas
- No ejecutar GO, sudo, systemd-run, stop/start MinIO, backup/restore, deploy, API/WEB, DB, identidad, cambios de `main` o datos reales en virtud de «continúa».
- No borrar prepared, journals, actas, backups o credenciales; no divulgar PIN, firma, IP ni secretos.

## Fuente de continuidad
- Issue https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/issues/525
- PR https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/pull/1002
- HEAD `dev` y `main` deben consultarse de nuevo al cierre, no presumirse.

## Corrección de coordinación posterior — PR sustituto (2026-11-12)

- **PR vigente:** [#1004](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/pull/1004), draft `[CHATGPT]`, rama conforme a GOV-003 `fix/g6-backup-guard-tests-20261112-gpt`; deriva del código de #1002, a su vez creado desde `dev` observado. **PR #1002 CLOSED sin merge** (conservado como historial, no continuar allí).
- **Cambio importante respecto al corte anterior:** los checks verdes generales de #1002 no probaban ejecutar sus tests G6 porque estaban bajo `scripts/qa/`. Se migraron a `tests/test_qa525_g6_backup_finalize_guard.py` y se incluyeron expresamente en la suite `tests/test_qa_readonly.py` que ejecuta workflow `QA readonly contract tests`. Se quitó solo la copia redundante del test en esta rama; Git conserva historia. El workflow no se modificó.
- **Archivos actuales:** `scripts/qa/qa525-g6-backup-finalize-guard.py`, `tests/test_qa525_g6_backup_finalize_guard.py`, `tests/test_qa_readonly.py`, este handoff. Solapamiento con PR #1 (promoción bloqueada) en tests/test_qa_readonly.py es no-hot y registrado; no tocarlo para merge a main.
- **CI:** verificar checks específicos del último HEAD de #1004. No atribuir PASS a ejecución de tests mientras el check `QA readonly contract tests` no finalice SUCCESS. Sin integración en GO-H ni prueba real de SIGTERM, watchdog o MinIO.
- **QA FÍSICO:** G6 backup NO_GO, no sudo ni parada. Sponsor autoriza específicamente la ventana sólo después de entrega autenticada revisada.
