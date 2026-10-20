# #588 — Fase 5A: autorización real, limitada, de metadatos de revistas

20-10-2026. Base `dev@39eacf7afd0c7116a44cf901479768f56d672124`. Issue #588, PR draft inmediato antes de código conforme GOV-003.

## Alcance comprometido

- Conectar el permiso dinámico `library/edit/magazine` al endpoint **existente** `PUT /api/documentos/{documentId}/metadatos-biblioteca`, con documento consultado en PostgreSQL, no tipo/alcance aportado por el cliente.
- Admitir únicamente una **revista de Biblioteca institucional de alcance Orden**, clasificación interna, política `library_authenticated`, colección Orden activa, sin permisos cruzados a Taller, libro, Gestión Documental ni documentos reservados.
- Los responsables institucionales de Biblioteca conservan la administración legítima de metadatos de estas revistas; los permisos tradicionales del Gestor Documental permanecen sin regresión. Las concesiones de colaboradores se comprueban por `sub` OIDC exacto, categoría, acción, vigencia y revocación inmediata.
- Añadir pruebas positivas/negativas HTTP con PostgreSQL, incluyendo Taller, otra categoría, otra área, sujeto diferente y revocación.

## Archivos previstos

`backend/src/PMGM.Api/Modules/Authorization/DynamicViewAccess.cs`, `backend/src/PMGM.Api/Modules/DocumentManagement/LibraryCatalogMetadataEndpoints.cs`, un pequeño evaluador de recursos en DocumentManagement, `backend/tests/PMGM.Api.Tests/Integration/DocumentAreaCollaboratorHttpTests.cs` y este handoff. Sin migrations, sin frontend, sin archivos calientes, sin QA físico.

## Límite

**No habilitar subida, versiones, publicación, retirada ni descargas administrativas a colaboradores en este incremento.** La fase 5B extenderá controles reales a cada operación, con tests de ciclo y anti-malware. No presentar CRUD documental completo en la UI de Claude. Sin cambios a main ni QA físico. Gates exact-head obligatorios antes de squash.

## Implementación propuesta PR #599

- Evaluador centralizado `LibraryMagazineMetadataAuthorization` valida una revista real (`magazine`, `magazines`, `revista`, `revistas`), Orden, colección activa, categoría interna, acceso de Biblioteca autenticada y `sub` con permiso `library/edit/magazine` vigente o Bibliotecario.
- Excepción puntual del filtro dinámico sólo sobre `PUT /api/documentos/{documentId}/metadatos-biblioteca`, con comprobación previa de recurso; el endpoint valida otra vez de forma independiente. Toda otra operación conserva filtros/roles existentes.
- Pruebas unitarias incluyen categoría, Taller, clasificación confidencial, política privada, colección inactiva, revocación, vencimiento y actor diferente.
- CI exact-head y compatibilidad con funciones previas: pendiente de verificación. No desplegado en QA, no integrado en dev.
