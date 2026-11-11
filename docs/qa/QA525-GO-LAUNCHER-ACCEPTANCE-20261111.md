# QA525 — Contrato de aceptación para GO launcher-only (BORRADOR / NO_GO)
Fecha: 2026-11-11. Base: rama `dev` al crear `docs/qa525-go-contract-20261111`.
Issue de seguimiento: #525. Este archivo NO constituye acta GO, plan firmado, admisión autorizada ni permiso de ejecución.

## 1. Alcance invariable
- Host único `glmdch`; entorno `QA_SYNTHETIC`.
- Única operación potencial (posterior, con GO distinto): reemplazar el launcher `pmgm-qa-update`, y verificar su postestado.
- Excluidos: ejecución de despliegue API/WEB, Docker Compose, mutaciones de BD, migraciones, identidad, `main`, producción, `srv01`, secretos, cambios de red y agentes de ejecución remota arbitraria.
- La instalación real no debe ejecutarse a partir de este borrador.

## 2. Constantes observadas; verificar de nuevo inmediatamente antes de GO
```text
SOURCE_SHA=5e09d7b5718309e31f639a1eb45890546e7b29cf
APP_ARCHIVE_SHA256=fed841dfcce436cdfed0a21e8e7af8de220974b7296f906c90107dbedeacac5c
CANDIDATE_OPERATOR_SHA256=69fddd49bde49d0335cbfb22dfbfa4e729328c018bfdbb75c2fa709df38ce99d
OLD_LAUNCHER_SHA256=61e3f1f7e7dd561567e5fac415a6fa9695c2f74fa86adde21dd8ed0330d03cd3
OPS_RECEIPT_SHA256=37a22f19ee2a56ad778ba609adb0059b728c0e0615d42d837d9ef356a63df4ed
OPS_SIGNATURE_SHA256=a8a568803a5ee00443afb1f94d77fb551a83511115e41d74d4ba2d855a1c091f
PYTHON_PATH=/usr/bin/python3.14
PYTHON_SHA256=52e0a13e60a981d8c4b6478be2ba5176f69da07948a056bf49cf6f077e30cb41
SSH_KEYGEN_SHA256=2dd834413c9d3f4c0e3535b9fcf2e96e14c11f3d36fd23b6c5fc24e50411d051
GH_SHA256=7469124f706944133d6a169691dd1c6c3511b12e85878d255e044e2948df4c9b
PEER_UID_GID=1000:1000
DROP_UID_GID=65534:65534
```
Estas constantes proceden de evidencia del operador en #525 (comentarios 6785172603 y 6784949386), NO de una medición nueva de este borrador.

## 3. Plan canónico — contrato pendiente de implementación
El generador debe adoptar el esquema REAL del verificador en el corte firmado; no improvisar campos, orden ni algoritmo de serialización. Mínimos semánticos que deben quedar vinculados en bytes canónicos:
1. `schema`, versión y finalidad `QA525_LAUNCHER_ONLY_NO_DOCKER_NO_IDENTITY_NO_MIGRATION`.
2. Identidad host, operador, entorno sintético y alcance temporal.
3. `SOURCE_SHA`, `APP_ARCHIVE_SHA256`, `CANDIDATE_OPERATOR_SHA256`, `OLD_LAUNCHER_SHA256` exactos.
4. Hash de política revisada; referencias de release y firma APP/OPS; rutas de custodia root existentes.
5. Precondiciones read-only, backup/restore G6 validado, stop conditions, rollback de launcher, postchecks y registro de resultado.
6. Dependencias incompatibles con este alcance deben producir rechazo, no ampliación implícita del permiso.
Prohibido declarar `PLAN_SHA256` antes de generar/verificar bytes con el módulo validador real.

## 4. Admisión canónica — contrato pendiente de implementación
Debe apuntar al hash del plan, al corte y al mismo alcance; marcar estado `PENDING` hasta verificar PRE-root/G6 y revisar propuesta de operador. Exigir hashes release/política APP/OPS, pins host y evidencias G6 verificables; enumerar verificaciones positivas y negativas. `ADMISSION_SHA256` no existe hasta que su archivo real sea serializado y validado.

## 5. PRE-root v3 — condiciones de veto
- Verificar fuera de root el paquete firmado; dentro de la frontera root, verificar de nuevo APP/OPS antes de confiar en contenido, configuración o primer byte del proyecto.
- Ancla independiente del Sponsor; no aceptar cambios de clave, firma ni configuración sólo porque el JSON local lo diga.
- Verificar rutas ancestro, propietarios, permisos, tipo regular, symlinks/hardlinks, `O_NOFOLLOW` o equivalentes, exclusividad de creación y protección de intercambio TOCTOU.
- Hashes de binarios `python3.14`, `ssh-keygen` y `gh` deben coincidir con observación física vigente; no confiar en rutas sin rehash.
- Verificar contexto de autoridad, scope, TTL <= 900 s, reloj UTC, nonce único consumido de forma atómica y antirreplay persistente; cualquier duda STOP.
- No usar `pkill -f` por patrón amplio; gestionar sólo PID/cgroup del proceso propio.
- El uso de `memfd`/socket debe verificar identidad y autorización de emisor, integridad de los bytes y cierre, no sólo `exit 0`.
- Registrar preestado y postestado del launcher con hash e inventario, recibo auditable sin secretos y rollback seguro del cambio exclusivo.

## 6. G6 Restic B — condición bloqueante separada
Exigir evidencia reciente: 2 bases de datos + globals + Silo; copia cifrada fuera del host con hash comprobado en destino y restauración aislada satisfactoria; verificación de watchdog MinIO. Sin resultado verificable o ante evidencia obsoleta: STOP. No inferir PASS de respaldos históricos.

## 7. Matriz mínima de pruebas negativas y criterios de aceptación
- [ ] Alterar 1 byte de plan, admisión, OPS y APP => STOP antes de root.
- [ ] Desajuste SHA source, release, política, launcher anterior o candidato => STOP.
- [ ] Ruta symlink, ancestro mutable, archivo preexistente o cambio de inode => STOP.
- [ ] GO expirado, adelantado, ausente, firmado con otro namespace, nonce repetido => STOP.
- [ ] Drift de `dev`/corte o pins del host => STOP y revisión.
- [ ] G6 sin copia off-host o restore aislado => STOP.
- [ ] Fallo del reemplazo o de los postchecks => rollback acotado, evidencia.
- [ ] Ejecución positiva en entorno de ensayo compatible con Python 3.14/sudo-rs, sin recursos externos; revisar logs sanitizados.
- [ ] Revisión independiente y firma GO Sponsor específica, emitida solamente en la ventana final, TTL <= 900 s.
- [ ] Recibos de POST-estado y cierre documentados antes de declarar `GO_FISICO=PASS`.

## 8. Estado de este incremento
`SPEC_DRAFT_ONLY / NO_GO`. Esta rama contiene documentación propuesta; falta revisar contra código fuente/verificadores reales, generar los dos documentos canónicos y hashes, implementar/verificar PRE-root y G6. No se ha ejecutado ningún comando en glmdch ni cambiado launcher/API/WEB/BD/identidad/producción. No hacer merge a dev sin revisión y CI.
