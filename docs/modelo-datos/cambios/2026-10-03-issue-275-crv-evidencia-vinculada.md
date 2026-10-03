# Registro parcial — revisión CRV vinculada a evidencia

Issue #275 / PR draft #276. Continúa [alta externa](2026-10-03-issue-275-alta-externa.md). Base propia `8b1ce37f99f4da6959837d1a24bfe55d79f82b0e`; dev al iniciar `1c2162760f1202f0292341f631698d5aa5f69e7c`, avance Claude #299 `ade3f2e280299056bc2d85f55bdc802fbecdc214` a incorporar. Reserva previa comentario 5971386995.

## Antes / después y fuente

Antes: la firma se aprobaba por expediente, sin identificar la carta; los selectores podían ignorar una carta nueva rechazada y recuperar una anterior aprobada. Después: la última carta por CreatedAtUtc/Id es la vigente aunque esté rechazada/pendiente; sólo su revisión expresa puede acreditar firma. No se convierte una decisión antigua sin referencia en aprobación de un documento nuevo.

Fuente: protocolo 2026 de Drive, obligatorio original de CRV firmado de puño y letra; firmas digitalizadas/imágenes insertadas no cumplen. Revisión humana, no verificación criptográfica ni aprobación automática de un archivo. Decisión PO: hasta tres meses calendario inclusive, afiliación simple; después con activación. Se conserva cálculo al crear expediente, no 90 días ni fecha de carga.

## Diccionario y estructuras del alcance

| Dato | Tipo/tamaño/nulabilidad | Regla nueva / clasificación |
|---|---|---|
| POST firma: evidenceId | UUID; campo opcional para deserialización, obligatorio en validación | Debe identificar la carta más reciente del mismo expediente; metadato privado institucional |
| POST firma: status | string approved/observed/rejected | Permiso Gran Secretaría vigente; no nuevo perfil |
| POST firma: asOfDate | DateOnly nullable en DTO, obligatorio al validar | Fecha no futura; aprobación no anterior a otorgamiento |
| POST firma: sourceReference | string hasta 500, obligatorio | Fuente de inspección del original, no firma digital inventada |
| POST firma: notes | string nullable hasta 4000 | Notas privadas, límites de columna existentes |
| admission_evidence.EvidenceDate | date nullable existente | Para acreditar CRV debe constar fecha de otorgamiento no futura; para afiliación coincide con WithdrawalLetterGrantedDate |
| admission_evidence.DocumentVersionId | UUID nullable, referencia lógica existente | No se acredita carta sin versión trazable; no nueva FK física |
| admission_evidence.ReviewStatus/ReviewedAtUtc | varchar(40)/timestamptz nullable existentes | Carta aprobada con revisión registrada; una revisión posterior exige nueva verificación de firma |
| admission_decisions.DecisionType | varchar(120) requerido existente | Familia withdrawal_letter_handwritten_signature:{EvidenceId:D}; 76 caracteres. Vinculación lógica, mismo patrón que evidence_review:{UUID}; sin nueva FK física |
| admission_decisions.AsOfDate/RecordedAtUtc | date/timestamptz existentes | Última decisión de esa carta; rechazo/observación posterior no recupera aprobación anterior |
| core.audit_events metadata | JSON existente | evidenceId/documentVersionId/status/fecha, sin nombres/RUT/contactos |

PK y relaciones existentes: expediente Id UUID; evidencia Id UUID → AdmissionCaseId FK cascade; decisiones Id UUID → AdmissionCaseId FK cascade. Expediente 1:N evidencias y 1:N decisiones. EvidenceId en el tipo de decisión referencia lógicamente una evidencia del mismo expediente. Índices/unicidad/borrado sin cambios. Sin nuevas columnas/tablas/índices/migración SQL; no transformar decisiones legadas ni inferir fechas. Modelo implementado, schema instalado no certificado.

Tres consumidores comparten AdmissionWithdrawalEvidencePolicy: elegibilidad general, habilitación de procedimiento y AdmissionCaseEligibilityProjector utilizado para materialización. Fecha/modo se validan contra fecha civil chilena de creación del expediente; no recalcular por el tiempo de tramitación ni alterar automáticamente modalidad histórica. Si fecha/modalidad no coincide, queda no acreditado y requiere corrección controlada posterior (este corte no crea endpoint de corrección).

Contrato de revisión de firma conserva ruta/DTO de respuesta, agrega referencia obligatoria al enviar. Clientes antiguos sin evidenceId reciben 400; historial se preserva. 409 para carta distinta/no trazable/no aprobada/fecha incompatible o expediente resuelto; 403 sin autoridad. Revisión general de evidencia ahora rechaza expediente resuelto y fechas futuras. Sin nuevos permisos/tarifas/autoridades/retención. Auditoría y decisión de revisión mantienen guardados existentes en dos contextos: atomicidad transversal del circuito pendiente, no afirmar que esta intervención la incorpora.

## Demo y pruebas

API frontend transporta el contrato con no-store. Adaptador y proyección sintéticos prueban identidad de carta, reemplazo, rechazo, nueva revisión y fechas/modalidad; fixture explícito demo-crv-review. No habilita una autoridad real: selector/gestor de evidencias y panel operativo de Gran Secretaría siguen pendientes; fuera del fixture no simula revisión de expedientes arbitrarios. No cambios visuales en este corte.

Frontend antes de incorporar #299: 373/373 PASS, lint/build PASS. Catorce escenarios unitarios backend y una prueba HTTP PostgreSQL nueva; CI/SHA final/resultados de lectura/QA se completan en recibos de PR/Drive. Prueba real: permisos, contrato, revisión pending bloqueada, carta aprobada vinculada, reemplazo rechazado sin fallback en ambos GET, historial/auditoría conservados y caso resuelto bloqueado. No SDK .NET local.

Extensión documental operativa: la vista de Secretaría reutiliza el contrato de documentos privados, crea/encuentra una colección de Taller con política management_only, registra documento y versión, carga el binario, ejecuta análisis de seguridad y sólo después vincula DocumentVersionId mediante el endpoint existente de evidencias. No expone claves de almacenamiento ni acepta una versión no disponible. No hay tablas, campos ni migraciones nuevas; se reutilizan AdmissionEvidence, Document y DocumentVersion. El adaptador demo sólo muestra demo-crv-review y evidencia sintética.

Pendientes: corrección controlada de fecha/modalidad posterior al alta; comisión/autoridades/cronología, duplicación de expedientes reutilizados, materialización/traslado idempotente y atomicidad transversal, demo completa y UAT. #276 draft; #116 NO FUSIONAR TODAVÍA; main congelado, srv01 pausado, despliegue QA pendiente.
