# Issue #588 — Biblioteca Virtual: categoría Revista (backend parcial)

Fecha: 2026-10-20. Responsable: ChatGPT. Base: dev@19485cd9dd2f6105c4179a24cc5b81ea3a7f2525.

## Contrato de API propuesto para Clau (sujeto a CI/merge en dev)

- `PUT /api/documentos/{documentId}/metadatos-biblioteca`: documentos tipo `magazine`, `magazines`, `revista` o `revistas` reciben `catalogKind: "Magazine"`. Pueden declarar `documentDate`, `authorName`, `topic`, `edition` (número), `shortDescription`, `abstractText`, `minimumDegreeRequired`. `officialDocumentType` no se aplica.
- `GET /api/biblioteca/buscar?documentType=magazine`: filtra las cuatro grafías, conservando exactamente las verificaciones vigentes de publicación, grado y visibilidad. También se admiten como filtro `revista`, `revistas`, `magazines`.
- `GET /api/biblioteca/facetas`: los tipos revista se agrupan en una faceta `{ "value": "magazine", "label": "Revistas", "count": N }`, contando sólo elementos visibles para quien consulta. No se altera la faceta de otros tipos.
- No se modifican tablas, migraciones, autorización, roles, interfaces ni `main`.

## Verificación

Prueba PostgreSQL/HTTP `Magazine_aliases_have_distinct_kind_search_and_canonical_facets` cubre tipos/alias, persistencia editorial, filtros y agrupación. Ejecutar gates exact-head PMGM CI, Showcase e instalable. En este entorno no hay SDK .NET para pruebas locales; el resultado de CI determina si es integrable.

## Fuera de alcance — sigue abierto #588

**NO se habilitan privilegios adicionales** por el mero tipo `Magazine`. Falta separar `librarian` (Biblioteca) y `grand_archivist` (Gestor documental), sustituir la autorización compartida `CanManageDocuments` en rutas de escritura, ampliar el catálogo dinámico con categoría/acciones/vigencia, crear endpoints auditables de colaboradores y pruebas negativas. Clau deberá conectar la interfaz solo luego de aprobarse el contrato definitivo.

Mantener toda publicación QA en el otro hilo (#525/#97). No usar datos personales reales ni tocar `main`, producción o `srv01`.
