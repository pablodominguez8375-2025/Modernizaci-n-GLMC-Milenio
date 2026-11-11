# QA525 — Contrato de aceptación para GO launcher-only (BORRADOR / NO_GO)
Fecha: 2026-11-11. Base: rama `dev` al crear `docs/qa525-go-contract-20261111`.
Issue de seguimiento: #525. Este archivo NO constituye acta GO, plan firmado, admisión autorizada ni permiso de ejecución.

## 1. Alcance invariable
- Host único `glmdch`, stack sintético. **El acta launcher-only usa literalmente `environment=QA_GLMDCH_ONLY`**; `QA_SYNTHETIC` es la descripción operativa del entorno, no el valor de ese campo firmado.
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

## 3. Plan canónico — compromiso de bytes pendiente
Revisión independiente de Claude, #525 comentario 6795237959: en el flujo LAUNCHER_ONLY actual **no hay verificador que lea e interprete el contenido de plan o admisión**. `plan_sha256` y `admission_sha256` se comparan contra los pines de config y el acta GO y se vinculan al nonce del journal, **pero esa comparación no certifica la validez semántica** de los documentos.

Para evitar una confianza ficticia: preparar un JSON breve, legible, con esquema/versionado propio de evidencia operativa (no afirmar que es formato exigido por el launcher), con corte SHA40, host, `environment=QA_GLMDCH_ONLY`, scope launcher-only, operador, política, release, launcher viejo/candidato, hashes OPS/APP, restricciones y rollback. Serializar en formato explícito estable; calcular SHA256 sobre esos bytes reales, conservarlos en custodia verificada y publicarlos sin secretos. El Sponsor y revisión independiente deben aprobar los contenidos **fuera de banda**, no por el mero hecho de que `check_cut` acepte el digest.

## 4. Admisión canónica — compromiso de bytes pendiente
Documento separado que enumere hash del plan, inventario/pines, verificación independiente del corte y G6 Restic B actual con evidencias de backup off-host/restauración aislada. Marcar estado PENDING hasta tener pruebas efectivas. Su digest se calcula únicamente con sus bytes finales exactos y se fija en config/GO. El hash por sí solo no es autorización, GO ni verificador de contenido.

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
- [ ] Alterar 1 byte de OPS o APP => STOP en verificación de autenticidad. Alterar sólo el archivo *externo* de plan/admisión NO genera STOP automático si no hay verificador de su contenido; exigir rehash/readback de custodia y comprobación humana independiente antes del GO. La cadena valida sólo sus digests fijados en configuración/acta.
- [ ] Desajuste SHA source, release, política, launcher anterior o candidato => STOP.
- [ ] Ruta symlink, ancestro mutable, archivo preexistente o cambio de inode => STOP.
- [ ] GO expirado, adelantado, ausente, firmado con otro namespace, nonce repetido => STOP.
- [ ] Drift de `dev`/corte o pins del host => STOP y revisión. **No fusionar este PR a dev antes del primer GO**, porque cambiaría dev y desalinearía la evidencia del corte firmado.
- [ ] G6 sin copia off-host o restore aislado => STOP.
- [ ] Fallo del reemplazo o de los postchecks => rollback acotado, evidencia.
- [ ] Ejecución positiva en entorno de ensayo compatible con Python 3.14/sudo-rs, sin recursos externos; revisar logs sanitizados.
- [ ] Revisión independiente y firma GO Sponsor específica, emitida solamente en la ventana final, TTL <= 900 s.
- [ ] Recibos de POST-estado y cierre documentados antes de declarar `GO_FISICO=PASS`.

## 8. Estado de este incremento
`SPEC_DRAFT_ONLY / NO_GO`. Esta rama contiene documentación propuesta; falta revisar contra código fuente/verificadores reales, generar los dos documentos canónicos y hashes, implementar/verificar PRE-root y G6. No se ha ejecutado ningún comando en glmdch ni cambiado launcher/API/WEB/BD/identidad/producción. No hacer merge a dev **antes del GO del corte 5e09d7b5**, incluso si CI y revisión pasan; mantener el PR en draft.

## 9. Contrastación directa de módulos dev@5e09d7b5 (revisión del 11-11)
Se leyeron los archivos reales `scripts/qa/qa525-vnext-root-bootstrap.py`, `qa525-vnext-root-launcher.py`, `qa525-vnext-root-launch-config.py`, `qa525-vnext-sponsor-signing.py` y `qa525-vnext-go-cut-contract.py`. Estas son restricciones del código existente, no un esquema inventado:

- PRE-root `check_manifest_pin()` exige JSON canónico **sin salto final**: `json.dumps(...,sort_keys=True,separators=(',',':')).encode()`; claves exactas del v3: `config`, `files`, `helpers`, `interpreter`. `files` asocia nombre de módulo `.py` con SHA256 **hex sin prefijo**; `config` tiene sólo `name` y `sha256` (hex sin prefijo); `interpreter` y `helpers` tienen `path` y `sha256` (hex sin prefijo). La firma APP/OPS no reemplaza este PIN externo.
- `qa525-vnext-root-launch-config.py` fija top-level exactamente `pins`, `launcher_pins`, `allowed_signers`, `go_fingerprint`, `ops_v3_sha256`, `app_subject_sha256`, `inbox`, `temp_root`, `socket_path`, `journal_root`, `lease_dir`, `peer_uid`, `timeout`, `children`, `g6_pins`, `authenticity`. `g6_pins=null` es la opción segura si se prepara exclusivamente LAUNCHER_ONLY. Los bloques `pins`, `launcher_pins` y `authenticity` tienen listas cerradas en el módulo: no añadir campos. `allowed_signers` y `authenticity.ops_allowed_signers` son texto ASCII en JSON, convertido a bytes por bootstrap.
- `qa525-vnext-root-launcher.py`: `children` es tabla **cerrada** `CHILD_MODULES`; `timeout` mayor que 60 es inválido. Se exige `ssh-keygen` y `gh` con sus SHA en `helpers`, OPS y APP verificadas por autenticidad antes del nonce. `ops_v3_sha256` es SHA256 del **recibo firmado OPS** (no el SHA40 de source).
- GO `qa525-go-launcher-v1` es JSON canónico **con salto final**: `(json.dumps(...,sort_keys=True,separators=(',',':'),ensure_ascii=True)+'\\n').encode('ascii')`, con campos `GO_FIELDS` más `operator`, `release_asset_sha256`, `old_launcher_sha256`, `candidate_operator_sha256`; TTL máximo **900 segundos**. Son formatos distintos: no copiar mecánicamente la serialización del manifiesto al GO.
- En PR #999 (ya integrado) se corrigió el binding del journal del nonce, los límites de miembros del ZIP y la idempotencia segura del recibo PRE-root. Esos parches deben quedar en los bytes firmados del mismo source; no usar los módulos del corte anterior.
- **Pendientes reales:** origen/autenticidad de los bytes finales de `plan_sha256` y `admission_sha256`; rutas/archivos de custodia host y configuración del inbox; resultado G6 off-host y restore; no existe un manifiesto v3 medido físicamente en esta revisión.

Esta contrastación **no certifica** plan/admisión ni PRE-root PASS. Requiere revisión independiente y pruebas negativas antes del GO.

## 10. Respuesta de Clau / estado CI
Claude revisó el commit documental `7cf3ce8a` (comentarios #525 6795237959 y PR #1000 6795238377) y reportó 0 P0/P1, con cuatro observaciones: preservar dev hasta GO, naturaleza opaca de PLAN/ADMISSION, entorno GO `QA_GLMDCH_ONLY` y ensayo rootless con Python3.14 del host. Se aplicaron las tres correcciones **documentales** anteriores; la validación de pruebas sigue separada. Los tests `tests/test_qa525_go_contract_format.py` en el commit 4acc0c1d deben ser corridos en checkout/CI y verificados antes de declararlos PASS. Ninguna evidencia de este documento concede autoridad root.
