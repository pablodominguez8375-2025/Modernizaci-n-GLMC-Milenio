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
| B2 | **G6 vigente, alternativa A** (aprobada por el PO el 07-11 11:18 Santiago solo para QA SINTÉTICO, #525 6708081930): respaldo cifrado nuevo con las herramientas G6 ya instaladas (Restic: 2 BD + globals + Silo) → solo el ciphertext a una carpeta privada de Drive, SHA-256 de los **bytes descargados** idéntico → restauración aislada en el mismo host → acta con el formato exacto que consume `validate_g6`; edad < 48 h y cubre TODA la ventana | PO + ChatGPT | Acta nueva; el motor exige `checkpoint_expires_at ≥ valid_until`. El informe debe conservar `G6_SAME_HOST_ISOLATED_RESTORE_PASS_NOT_DISASTER_RECOVERY`: **nunca declararlo OFFHOST**. Riesgo aceptado: sin DR de host perdido |
| B3 | Imágenes de rollback **retenidas** (API y WEB actuales), confirmadas con la evidencia de la fase 1 | PO | Salida de la fase 1 revisada |
| B4 | Bootstrap raíz autenticado OPS/APP (Sigstore) | ChatGPT + Sponsor | **PARCIAL**: contrato inactivo de firma OPS Ed25519 separada de APP (#902, integrado a `dev` 07-11, inactivo) y recibo de 7 campos; custodia de la clave OPS decidida por el Sponsor (único dueño: la guarda en su gestor de contraseñas, generada en su PC; no es GO); **pendiente** generar la clave y fijar su huella por fuente independiente del repositorio, autenticar la identidad APP (Sigstore/OIDC) por vía externa e integración PRE-root real (#822 puntos 6/7) |
| B5 | Wrapper root de los 5 locks legacy + lease raíz | ChatGPT | **PARCIAL**: contrato (#898) y puerto POSIX (#899) en `dev`, inactivos; **pendiente** composición en el punto de entrada y prueba real en el host |
| B6 | Chequeo de salud real HTTPS/OIDC | ChatGPT | **PARCIAL**: proveedor (#898) y `fetch` HTTPS con plazo duro (#900) en `dev`; hijo acotado con plazo externo para el DNS (#905, borrador); **pendiente** probar CA, nombre y respuestas reales desde `glmdch` |
| B7 | CLI físico con adaptadores reales, revisado exact-head | ChatGPT | **PENDIENTE**: puente de solo lectura de 10 contenedores (#904) es insumo; falta la composición B7, su `scope` cgroup (un grupo de procesos no contiene `setsid`) y la autenticación de rutas/hash de `python` y del hijo |
| B8 | Línea base física de **10** contenedores: los 3 oneshots (`db-grants`, `migrator`, `minio-init`) `exited/0` leídos en `glmdch` | PO + ChatGPT | **PENDIENTE**: la fase 1 del 06-11 solo cubrió 7 servicios; lectura complementaria de solo lectura con método revisado |

## Fase 1 — Inventario de solo lectura (existe; la hace el PO como operador)
**HECHA el 06-11 21:47–21:57 (Santiago)** con código `dev@95ffc073`: exit 0, `PHYSICAL_METADATA_CAPTURED_NO_CHANGE_AUTHORITY`, 7 servicios running, capas API=6/WEB=5 (registro en #525, comentarios 6699949734 y 6699974381). **No repetir** salvo cambio de código fijado o de host. Solo después del bloque 0-A (no requiere el bloque 0-B). El único comando es el documentado:

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
Del bloque 0-B: integración real de la firma OPS y APP (Sigstore) con custodia decidida por el Sponsor (B4), composición del punto de entrada con locks, salud y límite externo de DNS (B5–B7), línea base de 10 contenedores leída en el host (B8), pieza 7 (retirar duplicados legacy), G6 vigente según la alternativa A (B2) y GO del Sponsor (B1).

## G6 en el ensayo: dos alternativas y la elegida
- **A (elegida, QA sintético):** ver B2. Pendientes antes de usarla: custodia externa de la clave Restic (si solo vive en `glmdch`, perder el host deja inútil el ciphertext de Drive), revalidar byte/hash/dueño de `g6-local.py` y `g6-restore.py` (pines 5bbf092e… / 514a482e… leídos el 04-11; no están en GitHub) y no reescribir el recibo `archive.receipt.json` (`status` exacto `ENCRYPTED_LOCAL_ARCHIVE_READY_OFFHOST_PENDING`).
- **B (hito previo a datos personales reales, #694 / Ley 21.719):** `qa525-dual-db-age-backup` + restauración en un laboratorio descartable externo (estación del PO con WSL2/VM). Cubre solo las 2 BD, no es consumida por `validate_g6` y exige código nuevo revisado antes de contar como G6.
La alternativa A no autoriza ejecutar respaldo, restauración ni instalación: cada paso necesita autorización específica del PO.
