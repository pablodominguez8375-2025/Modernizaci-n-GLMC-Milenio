# QA525 B7 — entrega del PLAN REAL al factory runner (sin host GO)

Fecha 07-11-2026. Issue #922, PR DRAFT inicial, agente ChatGPT. HEAD dev inicial 91a19ea3bc53b3eb01edd8c92a800a56cb4388b6. main 6dfb9546a4873baff15955cf86abfd7d47e3d111 intocada.

Hallazgo de integración #917 + #920 + #921: el motor ya valida y conserva la línea base real dentro de un RootPlane, pero su factory recibía solo (plane,boundary,report) y no el plan. El runner inferior y el guard superior necesitan exactamente la misma estructura para mantener allowlist, layer hashes, scopes, journal y rollback; no se permite recapturar fuera del lock ni inventar un plan propio.

Se modificará solo el bridge vNext existente y su test, y este handoff. Se entregará una COPIA PROFUNDA del plan inicial validado como cuarto argumento para no compartir la misma estructura mutable. El motor debe detectar mutación posterior del plan original o intento de alterar la copia dentro del factory; se evitará ejecución antes de un intent persistido.

Biblioteca INACTIVA, no autentica OPS/APP/Sponsor ni ejecuta host. Cualquier conexión a #920/#921 está restringida a un punto de entrada físico firmado y validado externamente. #822 NO-GO físico. Cierre requiere CI exact-head, revisión externa y handoff Drive/GitHub.

## Implementación PR #923 — corte y pruebas

Contrato actualizado en bridge físico INACTIVO: runner_factory recibe 4 argumentos: (plane, held_boundary, b7_report, DEEPCOPY(plan inicial real)). El plan se generó dentro de la misma ventana RootPlane y lease tras validar bytes G6+B7, nonce/digest, baseline_hash, y el manifest de 2 scopes. El bridge verifica flight.plan_mod.validate(plan) == APPLYING, compara el plan original con la copia tras ejecutar factory, rechaza cualquier mutación de la copia y revalida el reporte B7 y G6 una vez más. Un callback de 3 argumentos antiguo se bloquea antes de ejecutar el primer child; no se reconstruye el plan por lectura secundaria.

Pruebas ampliadas: factory recibe el mismo nonce, hash y capas baseline/target que el journal; lector de baseline sólo permite UNA lectura; mutación de la copia o reporte tras factory -> STOP sin child; cambios G6 durante creación -> STOP; firma vieja de factory -> STOP; rollback original y gates siguen cubiertos. Nuevo CI exact-head en curso sobre este PR, review Claude pendiente. No es GO, no autentica firmas OPS/APP ni acceso glmdch. #920 y #921 deben enlazar sus propias capas desde esa COPIA VALIDADA, sin reconsultar Compose fuera del lock.
