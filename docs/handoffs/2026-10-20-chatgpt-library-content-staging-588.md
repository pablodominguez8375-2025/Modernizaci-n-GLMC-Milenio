# #588 Fase 5B3 — reserva, carga y escaneo de versiones de Revista

Fecha 2026-10-20. Fuente `dev@27af98f835edbc0320b535dd6855ab0e71117cad`, `main` estable fuera de alcance. PR draft reclamada antes de código.

## Contrato
- Bibliotecario puede delegar `library/view+write/magazine` exclusivamente para revistas, no para libros, otra área, Taller o tipos reservados. La acción `write` no delega `create`, `edit`, `delete` ni `publish`.
- POST `/api/documentos/{documentId}/versiones` reserva una versión solo en revistas Orden internas de colección activa y `draft`, con permiso vigente `write`. Se mantiene validación existente de nombre, MIME, tamaño, secuencia y auditoría.
- PUT `/api/documentos/versiones/{versionId}/contenido` permite subir **solo la versión que creó ese mismo sujeto OIDC**, con concesión activa, sin sobrescribir, con firma MIME real, bytes exactos, integridad SHA-256, almacenamiento objeto existente y política de tamaño.
- POST `/api/documentos/versiones/{versionId}/analizar` permite solicitar el servicio antimalware real **solo al autor de la versión**, bajo concesión aún activa. El servidor, no el colaborador, genera ScanReference y transición a disponible/rechazado; fallos quedan fail-closed. La ruta administrativa `/estado` NO se delega.
- Nunca autoriza descarga administrativa, publicar, retirar, cambiar visibilidad ni modificar documentos Taller. Las comprobaciones del filtro técnico y endpoints se aplican por recurso en BD. No relajar privilegios del Gestor Documental preexistente.
- Pruebas unitarias y HTTP/PostgreSQL con objeto y scanner sintéticos (nunca credenciales/datos reales), revocación, otros sujetos, áreas y controles maliciosos.

## Archivos previstos
`DocumentAreaCollaboratorEndpoints.cs`, `LibraryMagazineMetadataAuthorization.cs`, `DynamicViewAccess.cs`, `DocumentManagementEndpoints.cs`, `DocumentContentEndpoints.cs`, tests de autorización e integración del área, este handoff. Sin migraciones ni frontend ni modificación de main/servidor QA.
