# QA525 — Corte de esquema Taller 00 (#812/#813)

## Estado de partida comprobado

Fuente de aplicación integrada: `02271268d3957d3aa669f8cef38e29e40c2b9298`.
En glmdch se verificó la firma del ZIP con digest
`sha256:270d3e2c9698e94ed381fb171413f2cb1fd700b46d82c009c976ecb1887a2c8c`.
La consulta del operador devolvió EF99, PR801=1, PR813=0, support=0,
conflictos de número00=0. API+WEB siguen con última evidencia aplicada725bc319.
No se ha ejecutado la migración nueva ni desplegado02271268.

## Contrato de esta entrega

La ruta original de PR801/EF99 continúa intacta por omisión. La ruta nueva
requiere `--single-operator-qa --support00-postdb` después de la acción explícita
`--prepare-reviewed` o `--apply-supervised`. No es un modo de migración.

El nuevo contrato coteja exactamente los tres blobs acumulados desde la línea
base6ff4: migración PR801, PmgmDbContext y migración PR813
`20261030223000_SeedSupportWorkshop00` (blob8eb86e632009acd0bf252e8ea8abee1eabee6c6b).
Otro cambio de migración/modelo, renombre o huella debe bloquear. El historial
debe ser la unión exacta de los98 IDs originales del recibo más PR801 y PR813,
sin editar el recibo histórico.

La sonda `qa525-support00-post-db-readonly.py --inspect-readonly` conserva las
comprobaciones PR801 de22 columnas, tres índices y tres constraints. Añade una
consulta fija `BEGIN READ ONLY` que exige una sola fila support/00 con UUID
canónico b12c0000-0000-4000-8000-000000000000, cero conflictos institucionales,
índice parcial único válido/listo con expresión/predicado exactos y CHECK
validado con definición exacta. Esta exigencia de UUID corresponde al caso
físico observado support=0; no adjudica silenciosamente otro UUID existente.
Solo devuelve contadores y `may_apply:false,may_migrate:false`.

El acta postDB de operador único conserva el formato existente, pero debe
identificar migration_id y blob PR813, contexto fijado y scope
`POST_PR813_API_WEB_ONLY_NO_MIGRATION`. No acepta actas PR801. Exige G6 posterior
a la intervención PR813, restore/hash y revisión vigentes, fuente/digest nuevo,
nonce temporal, locks y rollback binario dual. El prepared registra schema_mode;
no puede cruzarse entre las rutas99 y100. No se cambian flags, identidad o SMTP.

## Intervención de migración separada: implementada, pendiente de validación/ejecución

La sola publicación de esta entrega **no ejecuta EF99→100 ni concede autoridad
mediante un JSON**. El migrador separado `qa525-support00-migration.py` exige
identidad DDL del contenedor original, fuente firmada y objetivo EF exacto PR813.
Comprueba pending migrations={solo PR813} y los99 IDs previos, conserva
servicios/identidad y no ejecuta Program.cs con sus inicializaciones.
Sus modos de preparación y migración son acciones explícitas separadas; exige
checkpoint G6 de menos de30min, referencia PO/revisión, nonce y journal durable.
El manual [Actualizaciones rápidas revisadas](QA525-FAST-REVIEWED-UPDATES.md)
describe los comandos, plantillas, instalación y recuperación.
No usar INSERT/ALTER manual ni Down. Si la ejecución falla, conservar journal
y detenerse; no repetir automáticamente ni restaurar destructivamente.

Preparar recuperación G6 para el estado previo conforme al runbook vigente;
verificar ambas BD, Silo, copia cifrada externa y restauración aislada del mismo
corte. Un archivo dentro de48h no demuestra por sí solo que incluya los cambios
posteriores. Tras migración exitosa, comprobar EF100/esquema, dejar evidencia de
hora real y preparar evidencia G6 postDB antes del enlace binario si lo exige el
acta. No reusar fechas, hashes o flags de una evidencia anterior.

Tras CI completo: nueva release firmada del operador ya integrado, actas exactas del
nuevo SHA, preparación y aplicación dual, health/version/OIDC y UAT de «Ver como».
El defecto Content-Type se corrige en PR814; no confundir415 con falta de00.
main, srv01, PR771/737/766 y datos reales permanecen fuera de alcance.

## Validación

Pruebas Python adversariales: deriva de blobs, cambios EF desconocidos, EF99
reutilizado, receipt adulterado, índice/check ausentes, identidad técnica
incorrecta, acta de scope anterior, cambio de schema_mode y CLI inválida.
Prueba C# con PostgreSQL real verifica las MISMAS consultas de catálogo sobre
la migración real en CI. No sustituir esa evidencia por una respuesta fake.
