# PMGM-ARCH-002 — Arquitectura de Dominios Documentales

**Estado:** Propuesta aprobada para desarrollo incremental  
**Fecha:** 2026-09-08  
**Relacionados:** PMGM-REQ-027, PMGM-REQ-030, PMGM-REQ-031, PMGM-ADR-003

## 1. Propósito
Definir límites técnicos claros entre **Gestión Documental Operativa**, **Biblioteca Virtual** y **Gran Archivo / Gran Archivero**, evitando un módulo documental monolítico y evitando reintroducir CENDOC como concepto técnico o funcional.

## 2. Principio rector
Los tres dominios pueden compartir infraestructura de archivos, auditoría, identidad y autorización, pero **no comparten responsabilidad funcional**.

- Gestión Documental Operativa responde a procesos vigentes de negocio.
- Biblioteca Virtual responde a publicación y consulta.
- Gran Archivo responde a custodia, clasificación y preservación histórica.

## 3. Componentes lógicos

### 3.1 Document Storage Core
Componente transversal de infraestructura, no visible como módulo funcional.

Responsabilidades:
- almacenamiento binario en Object Storage;
- hash SHA-256;
- validación MIME/extensión/tamaño;
- análisis antimalware;
- generación de URL temporal autorizada;
- estado de procesamiento;
- retención física;
- legal hold;
- integridad y verificaciones técnicas.

No decide por sí mismo quién puede leer un documento: la autorización funcional pertenece al dominio dueño.

### 3.2 Gestión Documental Operativa
Responsabilidades:
- documento de trabajo o expediente vigente;
- versiones operativas;
- clasificación funcional;
- relación con Gran Secretaría, Talleres, ceremonias, tenidas u otros procesos;
- cierre administrativo;
- propuesta de transferencia histórica cuando corresponda.

### 3.3 Biblioteca Virtual
Responsabilidades:
- registro bibliográfico/editorial;
- colecciones y taxonomías;
- workflow editorial;
- publicación/retiro;
- búsquedas y navegación;
- política de lectura;
- referencia a una versión o derivado autorizado.

La Biblioteca no es propietaria del original histórico cuando éste proviene de Gran Archivo.

### 3.4 Gran Archivo / Gran Archivero
Responsabilidades:
- estructura Fondo/Sección/Serie/Subserie/Expediente/Pieza;
- procedencia;
- transferencias;
- custodia;
- ubicación física;
- restricciones archivísticas;
- digitalización;
- preservación;
- préstamos;
- cadena de custodia;
- generación/autorización de derivados históricos para consulta.

## 4. Regla de no duplicación
Cuando un binario ya existe en el núcleo seguro, los módulos deberán preferir **referencias** antes que duplicaciones.

Duplicar un archivo sólo se justificará cuando:
- exista un derivado de consulta;
- exista una conversión de formato;
- exista una versión de preservación distinta;
- la política de seguridad requiera separación física/lógica;
- exista una razón técnica o jurídica documentada.

## 5. Entidades técnicas compartidas
El núcleo transversal podrá exponer conceptos como:

### StoredObject
- Id
- StorageProvider
- BucketOrContainer
- ObjectKey
- ContentType
- SizeBytes
- Sha256
- ProcessingStatus
- MalwareScanStatus
- CreatedAtUtc
- RetentionPolicyId opcional
- LegalHoldState

### StoredObjectDerivative
- Id
- SourceStoredObjectId
- DerivativeStoredObjectId
- Purpose (`consultation`, `thumbnail`, `ocr`, `preservation_migration`, `publication`)
- CreatedAtUtc
- CreatedBySubject

Los módulos funcionales no deberán exponer `ObjectKey` a clientes finales.

## 6. Entidades de Biblioteca

### LibraryResource
- Id
- Title
- Subtitle opcional
- ResourceType
- Language
- Description
- PublisherOrResponsibleEntity
- PublicationYear opcional
- EditorialStatus
- AccessPolicyId
- CurrentPublishedEditionId opcional
- CreatedAtUtc

### LibraryEdition
- Id
- LibraryResourceId
- EditionLabel
- SourceType (`native`, `operational_document`, `archive_derivative`)
- SourceReferenceId opcional
- StoredObjectId
- PublishedAtUtc opcional
- WithdrawnAtUtc opcional
- ApprovedBySubject opcional

### LibraryCollection
- Id
- ParentCollectionId opcional
- Code
- Name
- Description
- Status

### LibrarySubject / LibraryKeyword / LibraryAuthor
Catálogos relacionados N:N con `LibraryResource`.

## 7. Entidades de Gran Archivo

### ArchiveFund
- Id
- Code
- Title
- Producer
- Description
- DateFrom opcional
- DateTo opcional

### ArchiveSection
- Id
- ArchiveFundId
- ParentSectionId opcional
- Code
- Title

### ArchiveSeries
- Id
- ArchiveSectionId
- ParentSeriesId opcional
- Code
- Title
- RetentionRuleId opcional

### ArchiveFile
- Id
- ArchiveSeriesId
- Code
- Title
- ProducerOrganizationId opcional
- DateFrom opcional
- DateTo opcional
- AccessRestrictionId
- PhysicalLocationId opcional
- CustodyStatus

### ArchiveItem
- Id
- ArchiveFileId
- ItemType
- Title
- OriginalSupport
- DateCreated opcional
- AccessRestrictionId
- PhysicalLocationId opcional

### ArchiveDigitalRepresentation
- Id
- ArchiveItemId
- StoredObjectId
- RepresentationType (`preservation_master`, `consultation_copy`, `thumbnail`, `transcription`)
- DigitizedAtUtc
- DigitizedBySubject
- QualityControlStatus

### ArchiveTransfer
- Id
- ProducerOrganizationId
- SourceModule
- SourceReferenceId
- SubmittedAtUtc
- SubmittedBySubject
- ReceivedAtUtc opcional
- ReceivedBySubject opcional
- Status
- InventoryReference

### ArchiveLoan
- Id
- ArchiveItemId
- RequestedBySubject
- Purpose
- ApprovedBySubject opcional
- CheckedOutAtUtc opcional
- DueAtUtc opcional
- ReturnedAtUtc opcional
- ConditionOut
- ConditionReturn opcional
- Status

## 8. Relaciones entre dominios

### Operativo → Gran Archivo
Un expediente cerrado puede generar `ArchiveTransfer` con referencia al origen. Una transferencia aceptada agrega metadata archivística y custodia histórica sin borrar la historia operativa.

### Gran Archivo → Biblioteca
Gran Archivo autoriza un `ArchiveDigitalRepresentation` de consulta/publicación. Biblioteca crea `LibraryEdition` con `SourceType=archive_derivative` y conserva `SourceReferenceId` al registro archivístico.

### Operativo → Biblioteca
Un documento autorizado puede convertirse en una edición de Biblioteca sin alterar el expediente original.

## 9. Autorización
La autorización se resolverá por dominio y backend.

Ejemplos:
- `GRAN_ARCHIVERO` no implica lectura automática de toda Tesorería, Hospitalaria o Régimen Interior.
- `BIBLIOTECA_EDITOR` no puede modificar un registro archivístico original.
- `BIBLIOTECA_LECTOR` nunca obtiene acceso directo al Object Storage.
- un Secretario de Taller no puede alterar fondos archivísticos globales salvo permiso explícito.

## 10. Auditoría mínima
Eventos auditables transversales:
- `document.object.uploaded`
- `document.object.scanned`
- `document.object.download_authorized`
- `archive.transfer.submitted`
- `archive.transfer.accepted`
- `archive.classification.changed`
- `archive.loan.checked_out`
- `archive.loan.returned`
- `archive.restriction.changed`
- `library.resource.created`
- `library.resource.approved`
- `library.resource.published`
- `library.resource.withdrawn`
- `library.restricted_download.authorized`

Los eventos de auditoría deberán evitar incluir contenido del documento o secretos de almacenamiento.

## 11. Persistencia
Para el primer despliegue se utilizará PostgreSQL compartido dentro del monolito modular, pero con:
- esquemas o convenciones de tabla por módulo;
- DbContext/configuraciones separables por dominio;
- claves foráneas sólo donde no rompan autonomía funcional;
- servicios de aplicación para operaciones entre módulos;
- prohibición de escritura cruzada directa desde un módulo a tablas de otro.

## 12. APIs sugeridas

### Biblioteca
- `GET /api/library/resources`
- `GET /api/library/resources/{id}`
- `POST /api/library/resources`
- `POST /api/library/resources/{id}/submit-review`
- `POST /api/library/resources/{id}/approve`
- `POST /api/library/resources/{id}/publish`
- `POST /api/library/resources/{id}/withdraw`
- `POST /api/library/resources/{id}/download`

### Gran Archivo
- `GET /api/archive/funds`
- `GET /api/archive/series`
- `GET /api/archive/files`
- `GET /api/archive/items/{id}`
- `POST /api/archive/transfers`
- `POST /api/archive/transfers/{id}/accept`
- `POST /api/archive/transfers/{id}/reject`
- `POST /api/archive/items/{id}/digital-representations`
- `POST /api/archive/items/{id}/loans`
- `POST /api/archive/loans/{id}/return`
- `POST /api/archive/items/{id}/publish-to-library`

## 13. Orden de implementación recomendado
1. Object Storage y `StoredObject`.
2. Autorización de descarga + malware scan + integridad.
3. Biblioteca Virtual básica sobre el núcleo seguro.
4. Estructura archivística y rol Gran Archivero.
5. Transferencias y custodia.
6. Digitalización/preservación.
7. Préstamos y cadena de custodia.
8. Integración Gran Archivo → Biblioteca.

## 14. Criterio arquitectónico de aceptación
La arquitectura se considera respetada cuando ningún flujo requiere fusionar Biblioteca Virtual y Gran Archivo, ningún cliente accede directamente al Object Storage, las referencias entre dominios conservan procedencia y cada dominio puede aplicar su propia autorización, retención y auditoría.