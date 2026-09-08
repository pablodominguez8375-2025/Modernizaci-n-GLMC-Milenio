# PMGM-BLG-050 — Gestor Documental y Biblioteca Virtual

**Estado:** En implementación  
**Prioridad:** P1  
**Fecha:** 2026-09-08  
**ADR relacionado:** PMGM-ADR-006

## Objetivo
Incorporar un repositorio documental institucional versionado y una Biblioteca Virtual como proyección controlada del repositorio interno, manteniendo separación entre metadata transaccional y archivos binarios.

## Alcance del incremento
- Documento lógico con alcance institucional.
- Colecciones/documentotecas.
- Versiones documentales inmutables.
- Clasificación de acceso y sensibilidad.
- Hash SHA-256 por versión.
- Estado de procesamiento del archivo.
- Política de publicación en Biblioteca.
- Contrato abstracto `IDocumentObjectStore`.
- Auditoría de altas, nuevas versiones, publicación, retiro y descarga autorizada.
- API para consulta de documentos y versiones.
- Proyección minimizada de Biblioteca.

## Reglas funcionales
1. Un documento puede pertenecer a la Orden completa o a un Taller.
2. Una nueva versión nunca sobrescribe la anterior.
3. Sólo una versión con estado `available` puede publicarse.
4. Una publicación de Biblioteca expone únicamente metadata necesaria para localizar y comprender el documento.
5. Los binarios no se almacenan en PostgreSQL.
6. La clave física del objeto no puede contener nombres, RUT, correos ni títulos sensibles.
7. La descarga requiere autorización en API aun cuando se conozca el ID o la clave lógica.
8. El repositorio interno puede contener documentos que nunca se publiquen en Biblioteca.
9. El contenido del archivo y la clave física no se copian a metadata de auditoría.
10. Legal hold y retención deben bloquear eliminación física mientras corresponda.

## Modelo inicial
### DocumentCollection
- Id
- Code
- Name
- Description
- Scope (`order`, `organization`)
- OrganizationId opcional
- Status
- CreatedAtUtc

### InstitutionalDocument
- Id
- CollectionId
- OrganizationId opcional
- Title
- DocumentType
- Classification
- AccessPolicy
- Status
- PublishedVersionId opcional
- CreatedAtUtc
- CreatedBySubject

### DocumentVersion
- Id
- DocumentId
- VersionNumber
- OriginalFileName
- ContentType
- SizeBytes
- Sha256
- ObjectKey
- ProcessingStatus (`pending_upload`, `uploaded`, `scanning`, `available`, `rejected`)
- ScanReference opcional
- CreatedAtUtc
- CreatedBySubject

## Acceso inicial
- Administración de Gran Logia: gestión global.
- Roles documentales de Orden: gestión de colecciones/documentos según finalidad.
- Administración/Secretaría de Taller: gestión documental sólo dentro de su Taller cuando la política lo permita.
- Usuario autenticado: sólo lectura de publicaciones de Biblioteca autorizadas.
- Régimen Interior, Tesorería, Hospitalaria u otros órganos no reciben acceso documental general por pertenecer a un órgano central.

## Privacidad y seguridad
- Añadir clasificación explícita de metadata documental al catálogo PMGM.
- No registrar título sensible, nombre de archivo, ObjectKey, hash o contenido en logs generales.
- Validar MIME, tamaño y extensión en el pipeline de carga.
- Escaneo antimalware antes de `available`.
- Buckets privados y credenciales sólo del backend.
- Descargar mediante API o URL firmada temporal posterior a autorización.
- Toda publicación/retiro debe quedar auditada.

## Entregables de este incremento
- [ ] Entidades y `DbContext`.
- [ ] Migración PostgreSQL.
- [ ] Códigos y estados de dominio.
- [ ] Política de acceso documental.
- [ ] Endpoints de colecciones, documentos y versiones.
- [ ] Proyección Biblioteca.
- [ ] `IDocumentObjectStore` proveedor-agnóstico.
- [ ] Servicio de integridad SHA-256.
- [ ] Clasificación de datos.
- [ ] Pruebas unitarias y PostgreSQL.
- [ ] UI Gestor Documental.
- [ ] UI Biblioteca Virtual.

## Fuera de alcance inmediato
- Elección definitiva de proveedor S3-compatible.
- OCR/indexación full-text del contenido.
- CENDOC avanzado.
- Firma electrónica avanzada.
- Workflow editorial complejo.

## Criterio de aceptación del primer corte
El primer corte queda listo cuando sea posible crear una colección, registrar un documento y múltiples versiones, comprobar que el historial es inmutable, impedir publicación de una versión no disponible, publicar una versión disponible y consultar desde Biblioteca únicamente la metadata permitida por su política de acceso.
