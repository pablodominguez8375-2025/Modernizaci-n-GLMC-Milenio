# QA525 — Plan del primer E2E aislado full-stack (DB + API + WEB)

Estado: PLAN (sin código activo). Base: dev c2af1695 (incluye #1032 y #1021). Sin GO host; no toca glmdch, main ni producción.
Fuente de la secuencia: `docs/qa525-runbook-actualizacion-manual-supervisada.md` (rama `claude/qa525-recovery-da274d65-20261118`, commit cefb9b8e) y lecciones (`docs/qa525-lecciones-actualizador-auto.md`, commit 02136df0 de esa rama). Se reutiliza `wait_health` / `wait_until_ready` de #1032; no se duplica.

## Alcance
Entorno efímero (compose local/CI, DB sintética restaurada desde un G6 sintético), imágenes N → N+1, una migración EF aditiva ficticia (columna nullable).

## Secuencia bajo prueba
0 preflight único PASS/STOP · 1 supply firmado · 2 locks+lease · 3 G6 backup cifrado + restore aislado · 4 estado EF + clasificación aditiva/destructiva · 5 snapshot · 6 migrar UNA vez y validar esquema antes de tocar API/WEB · 7 API (sondeo directo /health/ready) · 8 WEB (/health/web + qa-version) · 9 rollback WEB→API con el mismo criterio · 10 journal a APPLIED / ROLLED_BACK verificado, recibos one-shot.

## Casos
| ID | Caso | Resultado esperado |
|---|---|---|
| E1 | Camino feliz aditivo | APPLIED, esquema nuevo, recibo único |
| E2 | API N+1 no levanta tras migrar | rollback API/WEB, esquema aditivo permanece, API N sana, ROLLED_BACK |
| E3 | API tarda ~6 s (causa raíz 17-nov) | pasa por espera acotada, sin rollback |
| E4 | Migración destructiva | STOP antes de cualquier efecto; exige GO explícito |
| E5 | Restore G6 falla | STOP antes de migrar |
| E6 | Crash tras efecto | nunca ACTIVE falso; journal no APPLIED |
| E7 | Segundo intento con recibo consumido | rechazo |
| E8 | API N rechaza esquema N+1 (no compatible hacia atrás) | migración clasificada no aditiva → STOP |
| E9 | Migración ya aplicada (historial EF = conjunto del release) | no reejecuta; `migrate_once` 0 veces; sigue API/WEB |
| E10 | Fallo entre migración y API (esquema/historial divergente o excepción de EF) | DB_UNCERTAIN_BLOCKED, sin recreate, sin re-run ni restore, journal RECOVERY_MANUALLY_REQUIRED |
| E11 | Pérdida de lease tras migrar | DB_UNCERTAIN_BLOCKED antes de tocar API/WEB |
| E12 | Crash tras escribir journal PENDING | sin recibo, journal PENDING, nunca APPLIED/ACTIVE; reconciliar historial EF antes de cualquier reintento |
| E13 | G6 caducado o de otro release | STOP_BEFORE_DB, nonce no gastado |

## Condiciones de aceptación de ChatGPT (#1010 6977453297 / 6977539617)
1. «Aditiva» no es solo SQL nullable: contrato de compatibilidad hacia atrás con API N, inventario EF `__EFMigrationsHistory` pre/post y hash del conjunto ordenado de migraciones; SQL arbitrario, backfill no acotado, locks DDL incompatibles o dependencia de API N+1 ⇒ MANUAL / GO separado.
2. G6 con punto de recuperación consistente y restauración aislada demostrada, ligado al corte; migración parcial o esquema divergente ⇒ bloquear, NO reejecutar EF ni restaurar automáticamente.
3. Recibo único con resultados `DB_APPLIED_APIWEB_APPLIED`, `DB_APPLIED_APIWEB_ROLLED_BACK_COMPATIBLE`, `STOP_BEFORE_DB`, `DB_UNCERTAIN_BLOCKED`, con hashes EF, SHA release, imágenes, IDs de contenedor, journal y duraciones; nunca APPLIED solo por health HTTP.
Casos adicionales: migración ya aplicada (idempotencia), fallo entre migración y API, pérdida de lease tras migración, crash tras escribir journal, G6 caducado.
Implementación hermética: PR separado (`scripts/qa/qa525-vnext-auto.py`, `tests/test_qa525_vnext_auto.py`).

## Precisiones (revisión #1033, P2)
- El G6 sintético de este E2E **no acredita** G6 físico del host: la evidencia G6 real (restore aislado en `glmdch`) sigue siendo un gate aparte.
- `STOP_BEFORE_DB` significa «antes de migrar», no «antes de cualquier efecto»: si el preflight/G6 ya produjo efectos de respaldo, el recibo los lista; lo que se garantiza es que no hubo migración, ni nonce gastado, ni cambio de API/WEB.
- Aserciones concretas de cada caso: `tests/test_qa525_vnext_auto.py` (PR #1034).

## Reglas
- Expand/contract: la API anterior debe tolerar el esquema nuevo.
- Sin rollback automático destructivo de DB; restore G6 solo con GO.
- Tests obligatorios del orquestador: held_runner+argv_guard antes de subprocess; no consumir secuencia/nonce antes de firma+policy+envelope+medición de host+imagen cargada; `wait_until_ready`; sondeo directo; cierre verificado a ROLLED_BACK.

## Revisión
ChatGPT: compatibilidad DB, rollback y recibo (adversarial). Claude: implementación hermética y orquestador (`qa525-vnext-auto.py`) tras integrar #1021.

## No incluido
Healthcheck Docker de la API (config de host; decisión Sponsor). Ejecución en host real.
