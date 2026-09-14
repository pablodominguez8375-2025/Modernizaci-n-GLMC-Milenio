# PMGM-DB-003 — Modelo de datos Gran Archivo / Gran Archivero

**Estado:** Propuesto para implementación  
**Requisito:** PMGM-REQ-030  
**Dependencias:** PMGM-REQ-026, PMGM-REQ-027, PMGM-ADR-003

## 1. Principios
- Gran Archivo y Biblioteca Virtual son dominios separados.
- CENDOC no forma parte del modelo.
- El registro archivístico conserva procedencia, contexto y cadena de custodia.
- El archivo histórico puede referenciar un documento ya existente en Gestor Documental sin duplicar el binario.
- Las restricciones de privacidad y acceso se aplican en backend.
- Ninguna reclasificación destruye el historial anterior.

## 2. Entidades principales

### `archive_units`
Representa Fondo, Sección, Serie, Subserie, Expediente o Pieza.

Campos mínimos:
- `id uuid PK`
- `parent_id uuid nullable FK archive_units`
- `unit_type varchar` (`fund`, `section`, `series`, `subseries`, `file`, `item`)
- `code varchar`
- `title varchar`
- `description text nullable`
- `producer_organization_id uuid nullable`
- `producer_member_id uuid nullable`
- `date_from date nullable`
- `date_to date nullable`
- `extent varchar nullable`
- `support_type varchar`
- `classification varchar`
- `access_policy varchar`
- `restriction_until timestamptz nullable`
- `retention_policy_id uuid nullable`
- `physical_location_id uuid nullable`
- `institutional_document_id uuid nullable`
- `published_version_id uuid nullable`
- `status varchar`
- `created_at_utc timestamptz`
- `created_by uuid`
- `updated_at_utc timestamptz`

Restricciones:
- `code` único dentro del mismo `parent_id`.
- impedir ciclos jerárquicos.
- `restriction_until` no reduce otras restricciones existentes.

### `archive_transfers`
Cabecera de transferencia documental.

Campos:
- `id uuid PK`
- `source_organization_id uuid`
- `source_area varchar`
- `delivery_actor_id uuid`
- `receiving_actor_id uuid nullable`
- `requested_at_utc timestamptz`
- `received_at_utc timestamptz nullable`
- `period_from date nullable`
- `period_to date nullable`
- `support_summary varchar nullable`
- `item_count int`
- `classification varchar`
- `inventory_reference varchar nullable`
- `transfer_record_document_id uuid nullable`
- `status varchar` (`draft`, `submitted`, `observed`, `accepted`, `rejected`, `cancelled`)
- `observations text nullable`
- `correlation_id uuid`

### `archive_transfer_items`
Relaciona una transferencia con documentos/unidades.

Campos:
- `id uuid PK`
- `transfer_id uuid FK archive_transfers`
- `archive_unit_id uuid nullable`
- `institutional_document_id uuid nullable`
- `document_version_id uuid nullable`
- `temporary_description text nullable`
- `checksum_sha256 char(64) nullable`
- `condition_note text nullable`
- `accepted boolean nullable`
- `rejection_reason text nullable`

Al menos uno de `archive_unit_id` o `institutional_document_id` deberá existir.

### `archive_locations`
Ubicación física jerárquica.

Campos:
- `id uuid PK`
- `parent_id uuid nullable FK archive_locations`
- `location_type varchar` (`site`, `repository`, `room`, `shelf`, `module`, `box`, `folder`, `unit`)
- `code varchar`
- `name varchar`
- `security_classification varchar`
- `active boolean`

### `archive_custody_events`
Cadena de custodia append-only.

Campos:
- `id uuid PK`
- `archive_unit_id uuid`
- `event_type varchar` (`received`, `relocated`, `loaned`, `returned`, `digitized`, `restricted`, `released`, `disposed`)
- `from_location_id uuid nullable`
- `to_location_id uuid nullable`
- `actor_id uuid`
- `occurred_at_utc timestamptz`
- `reason text nullable`
- `reference_document_id uuid nullable`
- `correlation_id uuid`

No se permite UPDATE/DELETE ordinario sobre eventos de custodia.

### `archive_digitizations`
Registra original y derivados digitales.

Campos:
- `id uuid PK`
- `archive_unit_id uuid`
- `master_document_version_id uuid`
- `consultation_document_version_id uuid nullable`
- `digitized_at_utc timestamptz`
- `operator_id uuid`
- `format varchar`
- `resolution varchar nullable`
- `sha256 char(64)`
- `quality_status varchar`
- `quality_reviewed_by uuid nullable`
- `quality_reviewed_at_utc timestamptz nullable`
- `notes text nullable`

Un maestro de preservación publicado no se sobrescribe; toda corrección genera una nueva versión.

### `archive_integrity_checks`
Campos:
- `id uuid PK`
- `document_version_id uuid`
- `checked_at_utc timestamptz`
- `expected_sha256 char(64)`
- `actual_sha256 char(64) nullable`
- `result varchar` (`ok`, `missing`, `mismatch`, `error`)
- `executed_by varchar`
- `incident_id uuid nullable`

### `archive_preservation_actions`
Campos:
- `id uuid PK`
- `archive_unit_id uuid`
- `document_version_id uuid nullable`
- `action_type varchar` (`format_migration`, `backup_validation`, `metadata_fix`, `physical_conservation`, `rehousing`, `restoration`)
- `performed_at_utc timestamptz`
- `actor_id uuid nullable`
- `details text`
- `source_format varchar nullable`
- `target_format varchar nullable`
- `result_document_version_id uuid nullable`

### `archive_access_requests`
Campos:
- `id uuid PK`
- `archive_unit_id uuid`
- `requester_id uuid`
- `purpose text`
- `requested_at_utc timestamptz`
- `status varchar` (`pending`, `approved`, `rejected`, `expired`, `cancelled`)
- `decided_by uuid nullable`
- `decided_at_utc timestamptz nullable`
- `decision_reason text nullable`
- `valid_from_utc timestamptz nullable`
- `valid_until_utc timestamptz nullable`

### `archive_loans`
Campos:
- `id uuid PK`
- `archive_unit_id uuid`
- `requester_id uuid`
- `purpose text`
- `authorized_by uuid`
- `checked_out_at_utc timestamptz`
- `due_at_utc timestamptz`
- `returned_at_utc timestamptz nullable`
- `checked_out_condition text`
- `returned_condition text nullable`
- `temporary_location varchar nullable`
- `status varchar` (`active`, `returned`, `overdue`, `lost`, `damaged`)

### `archive_relations`
Relaciones contextuales.

Campos:
- `id uuid PK`
- `archive_unit_id uuid`
- `relation_type varchar` (`person`, `organization`, `lodge`, `meeting`, `ceremony`, `decree`, `office_term`, `document`, `archive_unit`)
- `target_id uuid`
- `description text nullable`

### `archive_library_publications`
Enlace controlado hacia Biblioteca Virtual.

Campos:
- `id uuid PK`
- `archive_unit_id uuid`
- `library_document_id uuid`
- `derived_document_version_id uuid`
- `authorized_by uuid`
- `authorized_at_utc timestamptz`
- `publication_scope varchar`
- `status varchar`

La publicación no cambia la custodia del original ni del maestro de preservación.

## 3. Capacidades de autorización
Capacidades sugeridas:
- `archive.catalog.read`
- `archive.description.manage`
- `archive.transfer.submit`
- `archive.transfer.receive`
- `archive.restriction.manage`
- `archive.location.manage`
- `archive.digitization.manage`
- `archive.preservation.manage`
- `archive.access.decide`
- `archive.loan.manage`
- `archive.report.read`
- `archive.library.publish`

Rol inicial `GRAN_ARCHIVERO`: todas las capacidades anteriores dentro del alcance institucional autorizado, sin anular restricciones de datos sensibles.

## 4. Índices mínimos
- `archive_units(parent_id, code)` unique.
- `archive_units(unit_type, status)`.
- `archive_units(producer_organization_id, date_from, date_to)`.
- `archive_units(classification, access_policy)`.
- `archive_transfers(status, requested_at_utc)`.
- `archive_custody_events(archive_unit_id, occurred_at_utc desc)`.
- `archive_access_requests(status, requested_at_utc)`.
- `archive_loans(status, due_at_utc)`.
- `archive_integrity_checks(document_version_id, checked_at_utc desc)`.

## 5. Reglas de integridad
1. No eliminar físicamente una unidad archivística con eventos de custodia sin proceso formal de disposición.
2. Una transferencia aceptada requiere receptor y fecha de recepción.
3. Un préstamo devuelto requiere `returned_at_utc` y condición de retorno.
4. La ubicación de una pieza restringida no se expone en endpoints públicos.
5. Todo acceso a material restringido debe generar evento de auditoría.
6. La copia publicada en Biblioteca Virtual debe ser derivada o autorizada explícitamente; nunca se expone directamente el maestro de preservación.
7. Un documento bajo legal hold no puede pasar a disposición física.
8. Los hash de preservación se almacenan como metadata y se verifican periódicamente.

## 6. Relaciones con módulos existentes
- **Gran Secretaría:** origen de expedientes cerrados y actas de transferencia.
- **Talleres / Gestión Logial:** productores de documentación histórica transferible.
- **Gestor Documental:** fuente de documentos/versiones digitales ya existentes.
- **Biblioteca Virtual:** destino opcional de copias de consulta autorizadas.
- **Privacidad:** retención, legal hold, derechos de titulares e incidentes.
- **Auditoría:** actor, acción, resultado y correlation ID.

## 7. Exclusión de CENDOC
No se crea tabla, módulo, servicio, rol, ruta ni dependencia denominada CENDOC dentro del Proyecto Milenio.
