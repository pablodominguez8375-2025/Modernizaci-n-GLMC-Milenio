# QA525 vNext — inspección de metadatos G6 anterior al APPLY (pieza 5)

**Estado:** biblioteca y tests herméticos **INACTIVOS**, PR #880 a `dev`. No se instala ni ejecuta en `glmdch`, y no constituye autorización física.

## Motivo y alcance

El motor sintético vNext (PR #876, integrado en `dev`) necesita vincular el estado de respaldo G6 al mismo vuelo antes de habilitar cualquier mutación. El operador legacy `scripts/qa/operators/qa525-supervised-dual-entrypoint.py::validate_g6` ya valida el archivo cifrado, su recibo y una restauración **aislada en el mismo host**, con vencimiento estricto de **48 horas**. Este módulo solo anticipa la porción **pura de coherencia documental** y preserva los nombres/estados del legacy, sin ejecutarlo ni sustituirlo.

`scripts/qa/qa525-vnext-g6-metadata.py::inspect` recibe por **bytes** el registro revisado, recibo local y reporte de restauración, más SHA-256 fijados externamente para cada documento, SHA-256 observado del archivo cifrado y hora del control. Comprueba:

- coincidencia byte-a-byte contra los tres digests esperados; límites de 64 KiB y JSON sin claves repetidas ni constantes NaN/Infinity;
- nombre del respaldo con fecha UTC estricta, ni futura ni con **48 h o más** de antigüedad;
- SHA-256 del backup revisado igual al hash *observado proporcionado*, y coincidencia exacta de nombre y hash del recibo local;
- reclamo explícito `g6_offhost_sha256_verified=true` en la revisión, pero **no lo autentica**;
- reporte `G6_SAME_HOST_ISOLATED_RESTORE_PASS_NOT_DISASTER_RECOVERY` para **pmgm y pmgm_keycloak**, recuentos positivos estrictos, Silo con tipo entero, sin contacto a producción.

**NO certifica** que el backup exista, que el SHA observado proceda de lectura física confiable, que los tres pins sean auténticos, que Drive contenga custodia offhost independiente, que el restore esté firmado ni que G6 esté realmente vigente en `glmdch`. Solo constata la coherencia de metadatos de entrada. Los booleanos de retorno `may_apply`, `may_install`, `signature_verified_here`, `physical_archive_verified_here`, `offhost_custody_authenticated_here`, `independent_restore_verified_here` y `sponsor_approved_here` siempre son `false`.

## Precondiciones para integrar a un ejecutor físico futuro

1. El **bootstrap OPS root firmado** debe validar Sigstore, procedencia y política del componente, aprobar fuentes OPS/APP y no ejecutar software de rama mutable.
2. El mismo operador debe leer cada archivo desde descriptores en rutas protegidas, verificar owners/ancestros/links, hashes reales y **custodia externa** (no solo claim), efectuar restore drill acorde con la excepción documentada y comprobar la aprobación Sponsor exacta.
3. Debe obtener y conservar los **cinco locks legacy más el lease raíz** en una sola ventana y vincular bytes de admisión/autorización mediante la pieza 3 de Claude; revalidar G6 inmediatamente antes de API, WEB y rollback.
4. Si hay vencimiento, drift, archivo sustituido, falta de restore independiente, caída de locks, excepción o estado ambiguo: **STOP sin reintento ni limpieza**, conservar journals y evidencias. El resultado de este módulo jamás se interpreta como un GO.
5. Antes de cualquier instalación: nueva CI exact-head, revisión adversarial independiente, nuevo G6 real, restauración y rollback verificados y autorización separada para `glmdch` (#525/#822).

## Pruebas y coordinación

`python3 -m unittest discover -s tests -p 'test_qa525_vnext_g6_metadata.py' -v` sobre una copia de código del PR. La suite cubre edad en límites, digest adulterado, JSON duplicado, registros incompatibles, falta de BD, valores falsos/booleanos, nombres fuera de alcance y ausencia de efectos host/red. No son ensayos de restauración física.

Claude trabaja piezas 3, 4 y 6; ChatGPT conserva 5 y 7. PR #880 solo añade estos tres archivos y permanece DRAFT hasta CI, cocontrol y rebase si `dev` cambia. No modifica `main`, glmdch, API/WEB, PostgreSQL, identidad, Compose, actas o producción.

Referencia: Issue #525 y PR #876; la información física de `glmdch` debe ser reinspeccionada desde canal administrativo autorizado.
