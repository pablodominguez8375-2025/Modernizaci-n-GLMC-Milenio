# QA525 vNext — Registro durable de etapas (biblioteca inactiva)

**Corte fuente:** tren #862, HEAD `5d969ea939471a60acee86086a023496e4fa3313`. Reclamo de carril registrado en Issue #525 comentario 6675673575. Esta entrega NO instala ni aplica QA; el permiso de desarrollo no es una aprobación física de `glmdch`.

## Alcance del componente

`scripts/qa/qa525-vnext-stage-store.py` persiste un snapshot JSON **por etapa**, empezando en `APPLYING` (índice 0000), con archivos `stage-<nonce>-NNNN.json`. Usa el `Lease` **real y retenido** emitido por `stage_store.journal.begin()` de la *misma instancia de módulo*; rechaza objetos fabricados, leases cerrados, fd sustituidos o emitidos desde otro import. Comprueba que `journal.inspect` continúa exactamente en `APPLYING` y que el marker del nonce liga los bytes de admisión.

Cada archivo registra el plan completo validado por `baseline-plan.validate`, índice, etapa, nonce, admission_sha256, baseline_sha256, target_sha256, checksum del plan y SHA256 del snapshot **anterior**. Los archivos se crean con NOREPLACE, permisos 0600, fsync del archivo, enlace atómico y fsync del directorio mediante el helper existente del journal. No se sobrescribe ni se repara silenciosamente un registro. Se rechazan saltos de etapas, modificaciones del origen, replay, gaps, archivos de otros nonces, enlaces inseguros, JSON inválido y restos temporales tras una caída.

**Regla esencial:** para cualquier etapa que implique mutación posterior (API_SWITCHING, WEB_SWITCHING, ROLLING_BACK), el futuro motor debe persistir **antes** el intent de etapa y comprobar fsync completo. Este módulo **no ejecuta** esas mutaciones, no expone método APPLY y no certifica salud/OIDC.

## Límites de confianza que siguen bloqueando el uso físico

- Un digest SHA256 del plan o snapshot es una cadena de integridad **no autenticada**: no sustituye firmas ni prueba que la medición provino del runtime real. El motor futuro debe invocar `verify.require_ok` bajo sus locks y obtener resultados de un adaptador propio y confiable; no aceptar resultados JSON de terceros.
- Un plan con `VERIFYING` o `ROLLBACK_VERIFYING` es una **afirmación estructural**, no evidencia de que API y WEB estén saludables. El escritor terminal #854 NO puede inferir APPLIED solo de este registro.
- El chequeo de lease conserva solamente el bloqueo del journal vNext. No acredita por sí solo los **cinco locks legacy + root**, deshabilitación del lanzador legacy, contención de hijos Docker/cgroup ni origen confiable del directorio y sus ancestros.
- El futuro bootstrap firmado independiente debe autenticar OPS/APP, Sponsor, G6/offhost, recibos, política y nonce. Nunca reutilizar una autorización consumida. Ante un snapshot faltante, temporal remanente o crash ambiguo: **STOP / RECOVERY_REQUIRED** con intervención controlada; nunca repetir APPLY.
- Tests herméticos con `tempfile`, sin red, host, Keycloak, Docker ni SSH. Los tests nuevos están **escritos, NO se declaran PASS hasta ejecutarse en CI**.

## Continuidad

La nueva rama queda separada y apilada sobre el HEAD congelado del PR #862 para no reiniciar su CI. Cuando #862 esté integrado a `dev`, rebasar este PR a la nueva cabeza, conservar únicamente estos tres archivos, ejecutar CI exact-head, pedir revisión adversarial independiente y fusionar **solo código inactivo** si procede. Mantener `main`, `glmdch`, base de datos e identidad intactos.
