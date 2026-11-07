# QA525 vNext – contrato de firma OPS (INACTIVO, #822 puntos 6/7)

`scripts/qa/qa525-vnext-ops-signature.py` es una biblioteca sin efectos: no abre archivos, no ejecuta procesos, no usa red. No autoriza instalar ni aplicar.

## Problema
`qa525-vnext-cache.py::load_verified_ops` confía en un recibo root 0600 (`OPS_SIGNED_VERIFIED`) sin probar **quién** lo firmó, y OPS y APP no son identidades distintas.

## Qué verifica
Una firma separada Ed25519 de `ssh-keygen -Y` sobre los **bytes exactos** del recibo (namespace `qa525-ops-v2@pmgm`, principal `qa525-ops`):
- El archivo `allowed_signers` tiene exactamente una línea, con namespace fijado y clave `ssh-ed25519` de 32 bytes, sin comentario.
- Su huella SHA256 coincide con la huella que el Sponsor fija fuera de banda, y **no** está entre las huellas APP (Sigstore/GitHub) entregadas.
- El verificador inyectado (futuro `ssh-keygen -Y verify`, recibo por stdin; ver `ssh_keygen_argv`) devuelve código 0 y la salida exacta `Good "…" signature for qa525-ops with ED25519 key SHA256:…`.
- El recibo es JSON estricto (sin claves duplicadas ni NaN) con **exactamente** los siete campos que consume `load_verified_ops`: `schema=qa525-ops-v2`, `status=OPS_SIGNED_VERIFIED`, `ops_source_sha` (40 hex), `program_sha256`/`readiness_sha256`/`staging_sha256` (`sha256:` + 64 hex) y `policy_hash` igual al valor esperado que entrega el llamador. La firma válida sola no basta: si la forma no es exacta, `OPSSIG_RECEIPT_INVALID`.

La respuesta siempre trae `may_apply=False` y marca como NO verificados aquí: procedencia de la huella, identidad APP, custodia de la clave y aprobación del Sponsor.

## Qué NO hace
Quién guarda la clave privada OPS es decisión del Sponsor (fuera de línea, nunca en glmdch/GitHub/Drive/chat). La huella fijada debe llegar por un canal autenticado independiente. Conectar este módulo a `load_verified_ops` y al bootstrap root es trabajo futuro con revisión y GO físico separados.

## Pruebas
`tests/test_qa525_vnext_ops_signature.py`: caminos con `ssh-keygen` real (firma buena, byte alterado, namespace distinto, otra clave, huella APP reutilizada, huella no coincidente; se omiten si no hay `ssh-keygen`) y casos herméticos de entrada, salida exacta, JSON y ausencia de superficie de procesos/archivos/red.

## Recibo v3: vínculo OPS ↔ APP (cierra #928 P1-A)
Sin vínculo, un recibo OPS válido de un programa X y una atestación APP válida de un archivo Y pasan juntos. `verify_ops_receipt(..., expected_app_archive_sha256=<digest>)` exige un recibo **v3**: los siete campos de v2 más `app_archive_sha256`, con `schema=qa525-ops-v3`. El digest debe ser igual al del archivo que APP atestó (lo entrega el llamador desde la prueba APP ya verificada); si no, `OPSSIG_APP_ARCHIVE_MISMATCH`.
- Sin ese argumento el contrato v2 queda idéntico (siete campos, `schema=qa525-ops-v2`), así que nada existente cambia.
- No hay degradación: con el argumento, un recibo v2 se rechaza; sin él, un recibo v3 se rechaza (`OPSSIG_RECEIPT_INVALID`). El esquema va dentro de los bytes firmados, no en un parámetro.
- El namespace y la huella OPS son los mismos que ya fijó el Sponsor (`qa525-ops-v2@pmgm`); no hay que regenerar la clave ni el `allowed_signers`.
- Quien firma el recibo (el Sponsor, con su clave OPS) debe incluir el digest del archivo que APP atestó. **Qué digest es `app_archive_sha256` (definición de contrato, a pedido de ChatGPT en #932):** es el digest del **sujeto exacto que Sigstore atestó** (el miembro ZIP interno, `signed_installable_sha256` / `signed_installable_digest()` de `qa525-vnext-cache.py`), corroborado por el sujeto DSSE de la atestación (`sigstore_dsse_subject_digest()`) con workflow y SHA fuente exactos. **No** es el `artifact_sha256` del ZIP externo (wrapper) de Actions, salvo que el auditor pruebe explícitamente que Sigstore atestó ese wrapper. Tampoco se acepta el SHA tomado del nombre del release, de un manifiesto autodeclarado ni de un callback que devuelva `True` sin procedencia. Este módulo solo compara el valor que le entrega el llamador; que ese valor provenga del resultado real del auditor Sigstore rootless es obligación del gate (#928) y del orden de autenticación pre-root (#936).
- De dónde sale ese digest y cómo se fija `ops_source_sha` frente a `app_source_sha` (P1-B) lo define el gate de #928; este módulo no lo decide.
- La respuesta añade `app_archive_bound` y mantiene `may_apply=False` y los demás indicadores en `False`.
