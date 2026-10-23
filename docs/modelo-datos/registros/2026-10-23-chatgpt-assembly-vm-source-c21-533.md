# GOV-004 — Asamblea #533 C21, trazabilidad V.M. acreditada
Fecha: 23-10-2026. Sin migración ni modificación de tablas o datos personales.

## Contrato de salida (servidor, no API pública)
`AssemblyMasterTermEvidenceResult.AccreditedSource` (nullable):
- `OfficeAssignmentId` GUID: identidad del cargo V.M. acreditado en fuente existente.
- `OrganizationId` GUID: Taller del período V.M. de origen, que puede diferir del Taller actual.
- `EvidenceReference` texto: referencia de acta institucional de certificación de dicho período, no una nueva aprobación.
- `StartDate`, `EndDate`: período exacto acreditado.

Selección permanente: uno de los períodos íntegramente cumplidos y certificados, primero por EndDate, StartDate e Id; representante: período actual certificado (StartDate descendente, Id ascendente). Sin `Category.Qualifies` o acreditación incompleta => `AccreditedSource=null`. Este contrato no almacena ni expone RUT, nombres, votos o restricciones. No crea padrón por sí solo, no sustituye auditoría/GT/Tribunal de Honor. Para materialización futura se deben exigir constancias oficiales obtenidas exclusivamente por backend confiable, estado de membresía y controles por Asamblea; la referencia no equivale a autorización de voto.
