# GOV-004 — #523 S2, «Buscar» separado en Biblioteca Virtual

Fecha 2026-10-21, PR #618, rama `feature/library-search-action-523-20261021-gpt`, base dev@28779aa4853897467c14e6e8fb0097ff03d68b35 (incluye #616 y UI #617). Estado: en revisión hasta CI y merge.

## Diccionario de permisos y contratos

| Contrato | Antes | Después | Seguridad |
|---|---|---|---|
| `DynamicAccessCatalog` / `DynamicGrant` | Acción `search` incorporada previamente en PR #616, sin cambio de esquema | Se reutiliza la misma acción `search`; **ningún campo, FK, tabla o índice cambia** | El permiso debe acompañarse de `view` en un perfil asignado |
| `DynamicViewAccess.ActionFor` | `GET /api/biblioteca/buscar` se resolvía como `library/view` | Resolución exacta `library/search` en GET/HEAD; únicamente esa ruta | Por perfiles asignados, un grant view-only es insuficiente para buscar |
| `frontend/src/api/dynamicMethodPolicies.ts` | `documentApi.searchLibrary` requería `library/view` | Requiere `library/search` | Las operaciones `getLibrary` y `getLibraryFacets` continúan requiriendo `view` |
| `/api/biblioteca/buscar` | Búsqueda HTTP ya existente, requiere autenticación y filtro de visibilidad documental | Misma respuesta, filtros, búsqueda y límites, sin modificar DTO | Mantiene únicamente documentos publicados y autorizados según clasificación, alcance y grado |

No se crean automáticamente grants de Search ni se reasignan sujetos. El contrato de autenticación y aplicación de dominio permanece sin cambio. El filtro técnico mantiene compatibilidad para sujetos sin asignaciones dinámicas, **nunca** sustituye la autorización de documento, taller, grado, publicación ni protección de objeto almacenado. No se cambia retención ni se genera material personal adicional; las respuestas siguen sujetas a no-store y minimización existente.

## Estructura y pruebas

Sin migraciones ni modificación de `core.dynamic_access_snapshots`, PK/FK, índices, cascadas ni esquema físico. La fuente de permisos es el catálogo ya persistido; única nueva asociación código-ruta de aplicación.

Pruebas: `DynamicViewAccessTests` confirma ruta exacta (y preserva portada/facetas en view), `DynamicAccessPolicyTests` comprueba view-only, view+search y revocación; `dynamicAccess.test.ts` comprueba contrato de métodos frontend. Se añadió prueba HTTP/PostgreSQL en `DynamicViewAccessHttpTests`: perfil asignado con `library/view` conserva acceso a facetas pero recibe 403 en `/api/biblioteca/buscar`; habilitar `library/search` concede 200 sin ampliar visibilidad de dominio y revocar la asignación restablece 403. Exige aprobación exact-head en CI antes de integrar.

Gates exact-HEAD, merge SHA y QA físico: pendientes de evaluación. `main` no se modifica.
