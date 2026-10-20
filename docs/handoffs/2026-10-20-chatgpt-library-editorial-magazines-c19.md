# #588 Fase 5B1 — listado editorial de Revistas para colaboradores

Fecha: 2026-10-20. Base `dev@76ea32af02eab84616068fc00640fb2404a7eb50`, `main` sin promoción. PR draft previo a código, conforme GOV-003.

## Contrato estricto

GET `/api/biblioteca/editorial/revistas` con autorización personal por categoría `library/view/magazine` vigente, o Bibliotecario institucional sujeto a restricciones técnicas explícitas. Solo retorna revistas institucionales de Orden con colección activa, acceso de Biblioteca autenticada, clasificación interna, nunca de Taller ni tipo no autorizado. Resultados limitados y con `Cache-Control: private, no-store`. Solo metadatos editoriales mínimos, sin contenido, causa reservada, listas de Taller, secretos ni descargas. Un usuario sin grant falla cerrado (403); sujeto distinto o revocado no hereda grant; no aumenta permisos por acceso a biblioteca público.

## Archivos de fase

`backend/src/PMGM.Api/Modules/DocumentManagement/LibraryCatalogMetadataEndpoints.cs`, `backend/src/PMGM.Api/Modules/DocumentManagement/LibraryMagazineMetadataAuthorization.cs`, `backend/tests/PMGM.Api.Tests/Authorization/LibraryMagazineMetadataAuthorizationTests.cs`, `backend/tests/PMGM.Api.Tests/Integration/DocumentAreaCollaboratorHttpTests.cs`, este archivo.

## Fuera de alcance

Aún no permite crear publicaciones, versionar, cargar archivos, publicar o retirar. Frontend UI de Claude separado. CI exact-head 3/3 previo a integrar dev. QA físico y main intocados.
