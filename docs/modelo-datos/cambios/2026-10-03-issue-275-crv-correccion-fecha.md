# Registro de cambio — corrección respaldada de fecha CRV

Issue #275 · PR draft #276 · rama `feature/admissions-residual-275`. Base propia `3204f9ea8c91ecd2861146c4d66a042bfd938604`, contiene dev `b36f0ff278853b01ce2748663db5c97dfdeac008`. SHA entregado y gates en recibo GitHub/Drive posterior. Modelo implementado en rama, no schema instalado.

Continúa [carta y firma vinculadas](2026-10-03-issue-275-crv-evidencia-vinculada.md). Fuente: regla Sponsor hasta tres meses inclusive simple/después activación; Protocolo 2026; Constitución; Línea Base; pendientes #275. No cambia el momento vigente de cálculo: fecha civil chilena de creación del expediente. Esta subsanación documental no decide derechos, comisión, traslado ni tarifas.

## Antes y después

Antes: una fecha declarada distinta de la carta aprobada bloqueaba firma/elegibilidad sin subsanación. Después: Gran Secretaría puede corregir fecha/modalidad desde la carta vigente aprobada y versionada, con fecha de revisión, fuente y motivo. El cliente no envía nueva fecha ni modalidad. Prohibido en incorporación, expediente fuera de under_review/observed, tras cualquier decisión no documental o solicitud de ceremonia. No registra cambios idénticos. Se preserva historia; una decisión de corrección obliga a verificar nuevamente firma. No se deduce fecha para legado sin carta.

## Diccionario del alcance

| Entidad/campo | Tipo físico / límite / nulabilidad | Significado y validación |
|---|---|---|
| core.admission_cases.Id | uuid PK, obligatorio | Expediente existente; no nueva identidad |
| AdmissionType | varchar(40), obligatorio | Sólo affiliation admite subsanación |
| Status | varchar(40), obligatorio | under_review/observed únicamente |
| CreatedAtUtc | timestamptz, obligatorio | Inmutable; convertir America/Santiago para cálculo |
| WithdrawalLetterGrantedDate | date, nullable | Sustituida por EvidenceDate acreditada; nunca inferida |
| AffiliationMode | varchar(40), nullable | Derivada simple/activation: alta ≤ otorgamiento.AddMonths(3), inclusive; no futura respecto del alta |
| core.admission_evidence.Id | uuid PK | Carta vigente: CreatedAtUtc descendente, Id descendente; sin fallback |
| AdmissionCaseId | uuid FK obligatorio | Expediente 1:N evidencias |
| DocumentVersionId | uuid nullable, índice | Requerido por operación; referencia lógica existente, sin nueva FK |
| EvidenceDate | date nullable | Requerida; anterior/igual a alta y no futura respecto de revisión |
| ReviewStatus / ReviewedAtUtc | varchar(40) obligatorio / timestamptz nullable | approved y revisión documentada; no se modifican al corregir fecha del expediente |
| core.admission_decisions.DecisionType | varchar(120), obligatorio | Familia nueva withdrawal_letter_date_correction:{EvidenceId:D}, 70 caracteres; ID lógico de evidencia |
| Status / AsOfDate | varchar(40) obligatorio / date obligatorio | approved indica subsanación técnica, no aceptación del hermano; revisión ≥ otorgamiento y ≤ hoy Chile |
| SourceReference / Notes | varchar(500)/varchar(4000), nullable en schema | Fuente y motivo obligatorios en contrato; trim, límites; sin truncar |
| RecordedBySubject / RecordedAtUtc | varchar(320)/timestamptz, obligatorios | Actor autenticado y hora de servidor; default CLR UTC existente |
| core.audit_events.MetadataJson | jsonb nullable | Antes/después fecha/modalidad, evidencia/versión, decisión y fecha revisión; sin nombres, RUT ni motivo libre |

Datos de expediente, evidencia y decisiones: acceso institucional restringido, sensibles por afiliación; no publicar filas reales. Auditoría conserva sanitización vigente. No nuevas políticas de retención, borrado ni permisos.

## Estructuras, relaciones y transacción

Sin migración: tablas/columnas/PK/FK/índices existentes. AdmissionCase 1:N AdmissionDecision, FK AdmissionCaseId, cascade existente; índice (AdmissionCaseId,DecisionType,RecordedAtUtc). Evidencia conserva índices por expediente/tipo/fecha y DocumentVersionId. Corrección → evidencia mediante sufijo lógico, no FK física nueva. No unicidad nueva; repetir datos idénticos retorna 409 sin nueva fila.

Operación nueva comparte conexión y transacción Serializable entre AdmissionsDbContext y PmgmDbContext: expediente + decisión + auditoría se confirman juntos. SQLSTATE40001, incluso envuelto por EF, devuelve409 tras rollback. No certifica atomicidad del resto del circuito. También comprueba CeremonyRequests por AdmissionCaseId para impedir corrección cuando falta el marcador histórico de solicitud.

Firma acreditada debe ser posterior a la última corrección; misma política en elegibilidad, fases y materialización. No borra aprobaciones previas ni altera la revisión del documento.

## Contrato y demo

POST `/api/admisiones/expedientes/{caseId}/carta-retiro/correccion-fecha`: EvidenceId UUID, AsOfDate date, SourceReference string≤500, Reason string≤4000; todos requeridos. Respuesta id, withdrawalLetterGrantedDate, affiliationMode, evidenceId, decisionId, signatureReviewRequired=true. 400 contrato inválido;403 autoridad;404 expediente;409 respaldo/etapa/concurrencia/sin cambio. Private/no-store. Mismo permiso vigente CanManageGrandSecretariat, ninguna atribución nueva.

Adaptador sintético con mismas reglas y contrato, fixture explícito demo-crv-review. Sigue parcial: sin panel operativo de carga/revisión, sin persistencia institucional ni análisis automático de original. Ningún reporte/exportación nuevo. Las pruebas/ZIP del nuevo SHA se consignan en recibo, no heredan certificación del corte anterior.

## Pendientes

Panel documental, demo integral, comisión/cronología/autoridades, duplicación de expedientes, traslado/materialización idempotente y atomicidad transversal; QA visual/UAT. #116 restringida; #276 draft. Main congelada, srv01 pausado; despliegue QA pendiente.
