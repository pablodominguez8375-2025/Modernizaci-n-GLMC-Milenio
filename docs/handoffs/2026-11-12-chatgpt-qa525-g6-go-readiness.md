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

## Continuación 12-11-2026 CLT — supervisor de sesión, aún sin kit físico

- Rama propia de #1004 retomada desde `8a191376e01654289f7eb127fd7b0e0e8e4ec5d4`; `dev=5e09d7b5718309e31f639a1eb45890546e7b29cf`, `main=6dfb9546a4873baff15955cf86abfd7d47e3d111` revalidados. Reclamo #525 comentario 6841132944. PR abiertos/archivos y adenda Drive 13c3kg6uKWZoOwEt2BufNeP3LnMrsbr97SUkaqnb2sMM consultados; sus pendientes se mantienen. No se observó novedad normativa aplicable.
- Evidencia del operador: inventario PASS_READ_ONLY_NO_GO; runner root:root 0600; inbox-g6-backup ausente. No crear un inbox vacío ni repetir init: aún falta KIT-A/B de backup.
- Nuevo `scripts/qa/qa525-g6-backup-session.py`: ciclo one-shot preflight → watchdog transitorio → comprobar con **validador real existente** → instalar GO mediante callback externo → bootstrap con stdout vigilado y terminal conservada → recuperación/readback MinIO healthy → desarmar/readback timer **y service** inactivos → custodia externa → guard final existente. Binarios del sistema por sudo -n, nombres y argv cerrados; no Python nuevo bajo root. Import/CLI no ejecutan operación física.
- La bandera de propiedad del timer se fija antes del intento de armado (timeout con éxito remoto ambiguo exige reconciliación). Rechaza unidad preexistente activa/desconocida; nunca cancela un servicio de rescate en curso. Recomprueba el watchdog inmediatamente antes del stop, tras PIN/volcados.
- `BootstrapProcess`: transporta stdout del comando PRE-root fijo, topes de línea/plazo, sin PIN/key en argumentos/log; espera terminación antes de recuperación. Si sudo no confirma terminar, conserva watchdog y GO, devuelve STOP_WORKER_UNSETTLED y retiene handle para incidente; nunca mata sudo a la fuerza y deja huérfano un copier root.
- `tests/test_qa525_vnext_g6_backup_session.py`: **18 tests locales PASS**, incluyendo 24 escenarios de fallo en subtests, señales HUP/INT/TERM reales en hijos desechables, stdout real de un hijo sintético, timeout, salida truncada y proceso que ignora TERM. Host/systemd/Docker son dobles: NO prueba física, ni SIGKILL real del bootstrap root, ni sudo-rs, ni ensayo completo firmado.
- Guard anterior en harness readonly: **37 tests PASS**. Harness general `test_qa_agent_readiness.py`: intentado, no PASS local. Python predeterminado 3.12 carece de F_SEAL_WRITE; con `/usr/bin/python3` 3.13 se ejecutan 1212 tests y falla por confianza de padres/interprete (entorno: `/` uid1000, `/usr` uid65534, frente a root requerido). No relajar checks ni alterar entorno para fingir verde. CI Ubuntu exact-head debe acreditar el resultado.
- Tests nuevos se auto-descubren en `test_qa_agent_readiness.py` por prefijo vnext, sin editar workflows ni harness compartido. Pending: comprobar logs CI que acrediten clases G6BackupSessionTests, G6BackupFixedHostPortTests y G6BackupBootstrapProcessTests.
- **Pendiente concreto:** ensamblar GO-H/KIT-A/B/GO-T con las funciones ya existentes de firma/verificación/publicación/custodia, pines y manifiestos del corte; autenticar también estos bytes no privilegiados; ensayar cadena completa con release real y TTY antes de entregarla al Sponsor. Los callbacks son frontera pendiente, no evidencia de firma o custodia física. Mantener #1004 DRAFT/no merge durante el corte físico. No se solicita autorización de ventana sobre un kit todavía sin ensamblar.
- Pages SHA no revalidado; sin cambio visible. Paquete nuevo no publicado; despliegue QA pendiente. G6_BACKUP=NO_GO; restore/offhost/review pendientes. glmdch, dev/main, API/WEB, BD e identidad sin cambios por esta intervención.
