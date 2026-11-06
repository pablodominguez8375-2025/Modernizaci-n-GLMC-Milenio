# QA525 vNext — enlace G6 ↔ ancla de aprobación (código INACTIVO)

**Estado:** pieza de continuidad de ChatGPT, en PR apilado sobre #884. No autoriza instalar ni aplicar en glmdch.

## Propósito

El motor vNext ya dispone de puente `admission+approval` (#878/#879) y de inspector puramente documental G6 (#880). **Todavía falta relacionar las dos evidencias**: el `g6_review_sha256` incluido en el ancla externa debe corresponder exactamente a los bytes del registro G6 inspeccionado; el respaldo debe conservar vigencia durante toda la ventana de admisión, no solo en el instante de inspección.

`scripts/qa/qa525-vnext-g6-anchor.py::compare` recibe bytes de admisión, ancla, review G6, recibo local y reporte de restore, más cinco pins `sha256:`, hash cifrado observado, policy hash y hora. Reutiliza el binding de admisión y el inspector G6, compara cada hash contra sus bytes, exige igualdad de review SHA entre ancla y G6 e impone `g6_expires_at ≥ valid_until`. Los errores son códigos fijos y nunca incluyen secretos.

**NO** autentica los pins, firma Sigstore ni ancla Sponsor; **NO** lee disco/Drive, ni comprueba cifrado real, custodia offhost, restauración física, identidad OCI, locks o salud; cualquier caller podría inyectar datos falsos. Por eso el resultado siempre marca `may_apply=false`, `may_install=false` y todas las verificaciones físicas/autoridad en `false`. La presencia de `G6_METADATA_COHERENT` no habilita ninguna transición.

## Condiciones no cubiertas

El futuro bootstrap root firmado verificará físicamente ficheros con descriptores confiables, hash real del archive, proveedor externo y restauración aislada, firma OPS/APP y aprobación del Sponsor; revalidará reloj confiable después de cinco locks y antes de cada mutación, con rollback dual y recuperaciones. Una prueba offline no equivale a GO operacional.

## Pruebas

`python3 -m unittest discover -s tests -p 'test_qa525_vnext_g6_anchor.py' -v` exige coincidencia positiva no-autoritativa; rechazo por hash de ancla distinto, expiración antes de admisión, byte/pin adulterado, política errónea, ancla malformada, restore o recibo incorrectos y JSON repetido. No ejecuta Docker, red, procesos, cambios BD ni host.

## Coordinación

Claude mantiene P1/P2 del motor tras #884; ChatGPT conserva exclusivamente este enlace G6. La rama se apila sobre el HEAD exacto #884 para no rebasar antes de tener un `dev` integrado. Tras el cierre de #884: retarget/reconstrucción sobre `dev` vivo y CI exact-head nueva, luego revisión adversarial independiente; no mezclar con el PR de Claude, no tocar `main` o QA físico.
