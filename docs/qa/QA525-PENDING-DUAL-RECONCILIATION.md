# QA525 — Conciliación de intento PENDING no activado

Estado: propuesta implementada en PR842; NO es permiso de ejecución física.
Base: dev cfe4e6573ece2ae81414cc252047d36cc56e5c73. Host único glmdch.
Incidente: #525, evidencia #6602360065 y análisis Claude #6602216562.

## Alcance

El comando nuevo `pmgm-qa-update --reconcile-pending-dual --reconciliation-review /ruta/absoluta/acta.json` archiva cinco registros: prepared, revisión sensible, aprobación postDB, cut y journal. No recrea contenedores, no toca BD, no elimina ni renueva el nonce consumido. No concede apply. No es ejecutable desde el launcher cfe4 actualmente instalado.

Solo procede si el journal es PENDING, los IDs e imágenes de todos los servicios de baseline siguen idénticos, dependencias running, one-shots exitosos, capas reconocidas, checks de identidad vigentes y versión WEB anterior sana. Rechaza eventos del SHA, .next del journal y otros journals incompletos.

## Antes de ejecutar

1. Revisión independiente del HEAD final de PR842, incluyendo este procedimiento, módulos y pruebas.
2. Todos los gates exact-HEAD SUCCESS; integración controlada y nueva release firmada completa.
3. Revisar y autorizar específicamente instalación del nuevo operador por el instalador firmado existente. No editar el launcher ni ejecutar módulos raw.
4. Recoger hashes de prepared/journal sin copiar contenido privado. Crear acta root0600 desde revisión real y autorización Sponsor explícita de ARCHIVE_PENDING_DUAL_NO_DEPLOYMENT. No inferirla del permiso API+WEB anterior.
5. Acta de schema qa525-pending-reconciliation-v1, source_sha y release_asset_sha256 del intento pendiente (cfe4/fab78644), prepared_sha256 y journal_sha256 exactos, operator_name soporte@glmdch, operator_reviewed true, approved_at y expires_at enteros de una ventana NUEVA de máximo 900 segundos; sponsor_approval_reference y technical_review_reference son enlaces reales a comentarios de #525. No reutilizar ni cambiar fechas de actas antiguas.

El acta root es una atestación del operador: los enlaces no se autentican de nuevo por red en esta operación de archivo. El operador y revisor deben verificar su contenido. No crear actas automáticamente ni usar un campo reviewed=true como sustituto de revisión.

## Transacción de archivo

Bajo lock exclusivo del operador: valida acta y registros privados, consumido del nonce, hashes y baseline. Repite runtime/hashes inmediatamente antes de mover.
Crea directorio privado nuevo y marcador durable pending-dual-reconciliation.json ANTES de mover el primer registro. Sin rollback automático de evidencia tras interrupción.
Mueve cinco registros con fsync de ambos directorios, copia acta específica y escribe receipt privado durable. Solo después elimina marcador y sincroniza.
El nonce consumido permanece en su lugar y jamás se vuelve válido.

## Estados y fallos

| Resultado | Interpretación | Próxima acción |
|---|---|---|
| Validación rechazada antes de marcador | Sin mover los cinco registros | Investigar código seguro, sin retry ciego |
| Marcador presente | Archivo interrumpido o incierto | status bloquea actualizaciones; inspección manual preservando todos los bytes |
| Receipt PENDING_DUAL_ARCHIVED_BASELINE_UNCHANGED_NO_DEPLOYMENT | Archivo completo, baseline previo comprobado | Postflight de solo lectura; no significa deploy |
| Error tras eliminar marcador | Recibo completo puede existir | Leer recibo/hashes antes de toda acción; no recrear estado anterior |
| Drift o eventos del intento | No elegible | Recuperación distinta, no usar este comando |

Después: confirmar cinco archivos archivados con hashes, receipt, ausencia marcador/prepared/journal originales, nonce aún consumido, API/WEB IDs e imágenes previos, HTTPS/OIDC y servicios. No declarar archivado PASS por exit code solamente.

## Nueva actualización

Revisión del nuevo corte, G6 vigente y restauración aislada, firma, aprobación nueva y fuente/CI exactos. El intento nuevo usa nonce nuevo por el mecanismo existente. No repetir EF100 ni Taller00. Nuevo G6 si caducó el anterior (04-Nov-2026 08:05:45 Santiago). Sin main/producción/srv01.

## Pruebas y límites

21 pruebas locales: 9 de diagnóstico/errores y core real con transporte falso, 12 de conciliación incluyendo expiración, drift, hash, nonce, acción, estado y fallo en segundo rename. Un fixture inicial de rollback omitía status=running y fue corregido; 21/21 finales PASS. No hubo prueba física ni SSH; revisión adversarial y CI final pendientes.

La causa histórica del fallo sigue desconocida: la nueva instrumentación no recupera mensajes perdidos. Errores futuros llevan etapa PRE/ACTIVATION/POST y SOURCE/APPROVAL/REVIEW/DATABASE/G6; red solo expone vocabulario fijo, nunca mensajes privados.

La aprobación específica de archivado también se consume irreversiblemente mediante un recibo root0600 identificado por su SHA256 antes del marcador y del primer movimiento. Si falla la creación del marcador, se exige aprobación nueva; el consumo no se borra ni reutiliza. Se prueba rechazo de replay sin mover archivos.
