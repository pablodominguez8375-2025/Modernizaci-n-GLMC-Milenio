# #588 — Fase 5B2: crear borrador de Revista con permiso delegado

Fecha: 2026-10-20; base `dev@032e314f5586a1944e893a42a37d52e8b6b21da1`. Reclamo exclusivo ChatGPT conforme GOV-003, draft PR antes de programar.

## Alcance
- `POST /api/biblioteca/editorial/revistas` crea EXCLUSIVAMENTE una revista en estado `draft`, Orden, `internal`, `library_authenticated`, sin versión ni publicación. Entrada: colección existente y título breve. La colección real se verifica en PostgreSQL: debe estar activa, Orden y sin Taller.
- Requiere autorización institucional de Orden + `library/create/magazine` vigente por sujeto/categoría o cargo Bibliotecario que respete restricciones técnicas. Un permiso de editar o leer no crea; Gestor Documental y Gran Archivero no heredan Biblioteca.
- Revisar filtro dinámico y revalidar permisos dentro del endpoint, auditar evento con mismo guardado.
- HTTP/PostgreSQL y tests unitarios para concesión, falta de permiso, categoría equivocada, revocación, colección de Taller/inactiva y ausencia de versiones/publicación.

## Archivos planificados
`backend/src/PMGM.Api/Modules/DocumentManagement/LibraryCatalogMetadataEndpoints.cs`, `backend/src/PMGM.Api/Modules/DocumentManagement/LibraryMagazineMetadataAuthorization.cs`, `backend/src/PMGM.Api/Modules/Authorization/DynamicViewAccess.cs`, `backend/tests/PMGM.Api.Tests/Authorization/LibraryMagazineMetadataAuthorizationTests.cs`, `backend/tests/PMGM.Api.Tests/Integration/DocumentAreaCollaboratorHttpTests.cs` y este handoff.

Sin migraciones, frontend, archivos calientes, despliegue QA ni promoción main. Cerrar sólo con CI/Showcase/Instalable 3/3 SUCCESS exact-head. QA/UAT físico posterior.
