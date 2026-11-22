# QA525 — Transición autenticada 7→6→7 del flight API/WEB tras el corte de esquema (DISEÑO)

Estado: PROPUESTA para revisión de ChatGPT; no modifica código. NO_GO físico. Responde al hallazgo de #1073 (comentario 7080807250).

## Problema
`make_quiesce_writes` detiene el contenedor API antes del DDL (invariante: escrituras cerradas durante 100→104). El flight reutilizado (`root-schema-flight` → `apiweb-host.build` → `held-flight-bridge.run_held`) abre con B7 (`StateReader.capture_baseline`), que exige 7 servicios running y `baseline_sha256` == pin; con la API detenida falla `BASELINE_TARGET_NOT_RUNNING_API`. Además `run_held` arma el rollback con `ServiceSwitcher.for_plan(plan, "baseline")`, que reaplica las capas/imágenes BASELINE: reiniciaría la API VIEJA contra EF104.

## Invariantes (no negociables)
- I1 Nunca código de API viejo contra EF104 (ni por rollback automático ni por reinicio). El retroceso de esquema es SOLO restauración G6 (sin EF Down).
- I2 B7 no se omite ni se relaja en el flight API_WEB normal; el cambio es un PERFIL adicional, cerrado y autenticado, solo alcanzable desde el GO SCHEMA_API_WEB_QA_ONLY.
- I3 Un solo RootPlane/journal para quiesce → esquema → flight (mismo lock class y módulos).
- I4 Fallo tras APP_STARTED ⇒ terminal APP_PARTIAL_STOP con API DETENIDA (nunca API vieja).
- I5 Nada se infiere por nombre: el contenedor es el pin `api_container_id` del plan autenticado.

## Estados
- S7 (baseline): 7 running, EF100, imágenes viejas. B7 normal, hash H0 = `authority.baseline_sha256` (ya pinneado).
- S6q (quiesced): API detenida POR EL CORTE (id == pin del plan, imagen == `api_image_id` baseline), 6 servicios restantes idénticos a S7, EF100→EF104 tras el esquema.
- S7′ (target): imágenes nuevas, 7 running, EF104, health y SHA servido == destino.

## Mecanismo propuesto (3 ganchos aditivos pequeños)
1. **Perfil B7 `SCHEMA_POSTQUIESCE`** (b7-held-readonly + state-reader): acepta SOLO: `running_services_expected == 6`; API `Running=false` con id == `api_container_id` del plan e imagen == baseline API; los otros 6 servicios con la huella de S7 (misma H0 sin la fila de API); `baseline_sha256` = `sha256("QA525-SCHEMA-POSTQUIESCE-v1\n" + H0 + "\n" + api_container_id)` recomputado por el verificador (no pinneado aparte: derivado de H0 + pin del plan). Cualquier otra combinación (API running, otro servicio cambiado, id distinto, imagen vieja corriendo) ⇒ `HELDFLIGHT_B7_DENIED`.
2. **Gancho `rollback_factory` opcional en `run_held`** (default = comportamiento actual `ServiceSwitcher.for_plan(plan,"baseline").switch_all`). El schema-flight lo fija a `hold_stopped`: detiene (docker stop exacto por id/nombre de scope, mismo plane) cualquier API nueva y deja WEB en baseline; NO invoca `up` con capas baseline para la API. `terminal_outcome` ROLLED_BACK en modo esquema = FAILED (ya lo trata así `root-schema-flight`) ⇒ el secuenciador persiste APP_PARTIAL_STOP.
3. **Preflight B7 previo (read-only)** en `make_preflight`/effects: antes del quiesce, capturar con el perfil normal S7 y exigir H0; así el quiesce parte de un baseline autenticado y el perfil S6q solo puede derivarse de él.

Flujo: preflight(B7 normal==H0) → RESERVED → BACKUP_VERIFIED → quiesce (stop API id del plan; 0 sesiones) → esquema (4 fases) → revalidate (EF104 + imágenes) → APP_STARTED → flight con perfil SCHEMA_POSTQUIESCE: B7(S6q) → plan baseline→target → forward `switch_api` (up --no-deps de la imagen NUEVA desde API detenida) → `switch_web` → observe → health/SHA → APP_APPLIED → UAT.

## Matriz de fallos
| Punto | Resultado | Terminal | Recuperación |
|---|---|---|---|
| B7(S6q) deniega | sin mutación del flight | APP_PARTIAL_STOP (API detenida) | Sponsor: reintentar con GO nuevo o G6 restore |
| falla switch_api | `hold_stopped` (API nueva detenida, WEB baseline) | APP_PARTIAL_STOP | G6 restore; jamás API vieja |
| falla switch_web / observe / health | `hold_stopped` | APP_PARTIAL_STOP | idem |
| plano perdido | nada se escribe | (journal sin terminal) | operador, por journal |
| crash a mitad | journal en APP_STARTED sin APP_APPLIED | APP_PARTIAL_STOP al recuperar | idem; sin auto-start |

## Alternativas descartadas
- Cerrar escrituras por rol (`CONNECTION LIMIT`/REVOKE CONNECT) sin detener la API: `pmgm_app` es superusuario (riesgo aceptado solo sintético) y los superusuarios ignoran límites ⇒ no garantiza cero escritores.
- Saltar o parametrizar B7 desde el GO: viola I2.
- Rollback del flight reiniciando la API baseline: viola I1.

## Pruebas exigidas (offline, sin host)
Fake docker/state-reader: (a) S6q exacto ⇒ perfil acepta; (b) API running / otro servicio distinto / id distinto / imagen vieja corriendo ⇒ deniega; (c) `hold_stopped` nunca emite `up` con capas baseline (aserción sobre argv); (d) forward desde API detenida llega a APPLIED con servido==destino; (e) cada fallo de la matriz deja el journal en APP_PARTIAL_STOP con el almacén real; (f) default de `run_held` sin gancho conserva el rollback actual (regresión API_WEB normal).

## Qué toca a cada quien
ChatGPT: perfil B7 + gancho `run_held` + wiring en `root-schema-flight` (archivos suyos). Claude: pruebas (b)(c)(e) con el contrato/almacén reales, `hold_stopped` y el preflight S7 en effects si se asigna.
