# QA525 vNext — runbook del ensayo en `glmdch` (BORRADOR, INACTIVO)

**Estado:** documento de planificación. No instala, no activa y no autoriza nada. El permiso de desarrollo no es el GO físico: el GO lo da el Sponsor por separado. Operador y dueño del QA operativo: el PO. Claude no accede a `glmdch` ni ejecuta comandos root.

**Lo que este runbook NO inventa.** Hoy el motor vNext (`qa525-vnext-engine.py`) es una **biblioteca sintética**: no existe todavía un punto de entrada físico (CLI) ni el wrapper root de los 5 locks. Por eso las fases 3 y 4 están marcadas **BLOQUEADAS** y no contienen comandos. Solo la fase 1 tiene un comando real (el inventario de solo lectura, ya documentado en `QA525-GLMDCH-INVENTORY-READONLY.md`).

## Reglas de parada (valen en todas las fases)
1. Ante cualquier resultado ambiguo o inesperado: **parar**, conservar evidencia, no reintentar a ciegas.
2. No repetir `--review-and-run` ni el actualizador histórico `6eed4`; no borrar `prepared.json`.
3. No reutilizar actas, nonces ni autorizaciones vencidas o consumidas.
4. Si un journal queda `PENDING`, `APPLYING` o `RECOVERY_REQUIRED`: no tocarlo a mano; pedir revisión (un journal pendiente bloquea todo por diseño).
5. Nada se pega en chats, Git ni Drive: ni secretos, ni PAT, ni tokens, ni claves SSH.
6. No se toca `main`, la base de datos, la identidad (Keycloak) ni producción.

## Fase 0 — Precondiciones, en dos bloques
El inventario de solo lectura (fase 1) NO muta nada y no necesita el GO del Sponsor ni G6; no debe confundirse con la autorización de APPLY. El GO físico (launcher-only/API+WEB) aplica únicamente a las fases que mutan (3 y 4).

### Fase 0-A — antes de la fase 1 (solo lectura)
| # | Condición | Quién | Evidencia |
|---|---|---|---|
| A1 | Canal administrativo acreditado y host confirmado como `glmdch` | PO | Registro del canal, sin secretos |
| A2 | SHA del código revisado fijado (commit de `dev` + checksum) y copia de los scripts con propietario root, sin symlinks y sin escritor de grupo/otros (inspección del code pin previa a ejecutar Python como root) | PO + ChatGPT | SHA en #525 |
| A3 | Ventana de observación acordada con el PO como operador | PO | Comentario en #525 |

### Fase 0-B — antes de las fases 3 y 4 (mutan; NO se exigen para la fase 1)
| # | Condición | Quién | Evidencia |
|---|---|---|---|
| B1 | **GO físico del Sponsor** (aparte del permiso de desarrollo) y ventana | Sponsor / PO | Comentario expreso en #525 |
| B2 | **G6 vigente**: respaldo cifrado de `pmgm` y `pmgm_keycloak` fuera de `glmdch`, acta y restauración aislada real; edad < 48 h y cubre TODA la ventana | PO + ChatGPT | Acta nueva; el motor exige `checkpoint_expires_at ≥ valid_until` |
| B3 | Imágenes de rollback **retenidas** (API y WEB actuales), confirmadas con la evidencia de la fase 1 | PO | Salida de la fase 1 revisada |
| B4 | Bootstrap raíz autenticado OPS/APP (Sigstore) | ChatGPT | **PENDIENTE** (no existe) |
| B5 | Wrapper root de los 5 locks legacy + lease raíz | ChatGPT | **PENDIENTE** (no existe) |
| B6 | Chequeo de salud real HTTPS/OIDC | ChatGPT | **PENDIENTE** (no existe) |
| B7 | CLI físico con adaptadores reales, revisado exact-head | ChatGPT | **PENDIENTE** (no existe) |

## Fase 1 — Inventario de solo lectura (existe; la hace el PO como operador)
Solo después del bloque 0-A (no requiere el bloque 0-B). El único comando es el documentado:

```bash
python3 /ruta-root-verificada/scripts/qa/operators/qa525-host-inventory-readonly.py --inventory
```

(La ruta es ilustrativa: no es un ejecutor raw ni salta el bloqueo físico #822; se usa solo desde una copia ya inspeccionada según A2.) Esperado: `PHYSICAL_METADATA_CAPTURED_NO_CHANGE_AUTHORITY`. Los siete servicios (API, WEB, Keycloak, PostgreSQL, MinIO, gateway, ClamAV) son el contrato TEÓRICO del inventario, no siete servicios ya comprobados en `glmdch`; más SHA-256 de las capas Compose de API/WEB. No imprime variables de entorno. Compartir solo el mínimo técnico, nunca el JSON crudo sin revisión.
**Parar si:** falta un servicio, hay discrepancia con el Compose real o aparece cualquier restante de un vuelo anterior. Ante diferencias: STOP y conciliar con revisión; no «reparar» a ciegas.

## Fase 2 — Ensayo sintético fuera de `glmdch` (no toca el host)
Se ejecuta en CI o en una máquina de desarrollo, nunca en `glmdch`, y es independiente de toda precondición física (sin G6 real, sin GO). Sus pruebas son herméticas. Objetivo: demostrar el comportamiento del motor con adaptadores simulados.
- `pytest tests/test_qa525_vnext_*.py tests/test_qa_agent_readiness.py` en verde sobre el SHA fijado.
- Escenarios que deben quedar cubiertos: vuelo APPLIED; fallo de WEB con rollback verificado (ROLLED_BACK); gate denegado antes de cualquier efecto; ventana vencida tras tomar los locks; G6 caducado antes del intent WEB; cierre del lease fallido tras el vuelo (`ENGINE_CLOSE_FAILED_AFTER_*`); caída a mitad del vuelo con `RECOVERY_REQUIRED`.
**Criterio de salida:** todo en verde y cero P0/P1 en la última revisión exact-head.

## Fase 3 — Ensayo físico acotado (**BLOQUEADA**: falta todo el bloque 0-B)
No hay comandos hasta que ChatGPT entregue y se revise: el wrapper root de locks, el punto de entrada físico con adaptadores reales (`held_runner`, lector Docker de solo lectura, salud) y el bootstrap de confianza. Cuando existan, esta fase deberá definir, antes de ejecutarse: orden exacto de pasos, qué se observa en cada punto de parada, quién autoriza cada gate (`gate(stage)` para API y WEB) y el tiempo máximo de la ventana.

## Fase 4 — Caída y recuperación (**BLOQUEADA**, mismo bloque 0-B)
Ensayar, antes de cualquier actualización real, que una caída a mitad del vuelo deja el journal en un estado recuperable y que el rollback con las imágenes retenidas devuelve API y WEB a la línea base con salud y OIDC en verde. Sin esta evidencia no hay GO.

## Criterios de rollback
- Cualquier gate denegado, verificación fallida, salud/OIDC rojo o ventana/G6 vencida durante un avance → rollback **verificado** a la línea base retenida (un solo rollback; si no se prueba, `RECOVERY_REQUIRED`).
- El rollback no depende de que G6 siga vigente, pero sí de los locks y del gate externo.

## Registro de evidencia (para cualquier IA que continúe)
Por cada fase: fecha y hora (America/Santiago), SHA del código, resultado y código de parada si lo hubo, y enlace al comentario en #525. Actualizar el Estado Maestro de Drive y la adenda de la sesión. No pegar salidas crudas.

## Qué falta para poder ejecutar la fase 3
Del bloque 0-B: bootstrap raíz OPS/APP (Sigstore), wrapper root de los 5 locks + lease raíz, salud HTTPS/OIDC real, piezas 5 (respaldo previo) y 7 (retirar duplicados legacy), G6 vigente y GO del Sponsor.
