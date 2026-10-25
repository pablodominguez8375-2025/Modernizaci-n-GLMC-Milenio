# GOV-004 — #523 S1, acción Buscar separada de Leer

Fecha 20-10-2026; Issue #523, PR #616. Base dev@b966ac6249e0eea4d4fd7aa38f13d495b41c7a67. Rama `feature/dynamic-search-action-523-20261020-gpt`. Revisor Sponsor, estado en PR hasta verificación de gates y merge.

## Diccionario vigente y cambio

| Contrato/campo | Antes | Después | Validación / clasificación |
|---|---|---|---|
| `DynamicAccessCatalog.Actions[]` | `view,create,write,edit,delete,print` | agrega `search` sin cambiar otras acciones | lista de códigos técnicos, sin dato personal |
| `DynamicGrant.Actions[]` | acciones configuradas en vista padre | admite `search` solamente junto a `view`; `view` solo NO incluye búsqueda | ámbito/taller/fecha/sujeto OIDC exactos |
| `AccessAction` (TS) | unión de 6 códigos | unión de 7 con `search`; etiqueta «Buscar» | frontend catálogo, sin permisos automáticos |
| `dynamicMethodPolicies.pmgmApi.searchAdmissionPeople.action` | `view` | `search` | contrato de llamada a búsqueda, no otros métodos |
| `DynamicViewAccess.ActionFor` | GET genérico `view` | ruta exacta `GET/HEAD /api/admisiones/personas-busqueda` => `search` | conserva autorización de dominio independiente por rol/Taller de `AdmissionPersonLookup.SearchAsync` |
| `core.dynamic_access_snapshots` | JSON serializado de catálogo, perfiles, grants y asignaciones | mismo esquema JSON; nueva cadena de acción permitida en grants futuros | no migración ni nuevos grants automáticamente; versión incrementa solo al crear/revisar perfiles |

## Estructuras
No cambian PK/FK, índices, cascadas, tablas ni columnas. El catálogo JSON versionado continúa en `DynamicAccessSnapshots`; un grant `search` requiere `view` en misma vista. `DynamicViewAccess.Allows` mantiene compatibilidad cuando no hay asignaciones (las reglas institucionales del endpoint aplican siempre); con asignaciones técnicas, `view` no proporciona `search`. Subvistas restantes todavía NO tienen acciones independientes.

## Impacto / privacidad
Las búsquedas de personas pueden revelar nombre e identificadores internos, por lo que se conserva control de roles `CanManageOrganization` y alcance de Taller, límites de 3..80 caracteres y 20 resultados, auditoría y no-store existentes. Este registro NO autoriza búsqueda inter-Taller por el solo perfil técnico. Se conserva la separación de Secretaría y Tesorería y las reglas de confidencialidad existentes.

## Pruebas y gates
Tests: `DynamicAccessPolicyTests` (view-only deny, search+view allow, sujeto/Taller/vigencia y dependencia view), `DynamicViewAccessTests` (ruta específica); frontend `dynamicAccess.test.ts` (UI y mock). CI/Showcase/QA Installable y SHA final pendientes al crear este registro.
No deployment en QA físico, sin actualización de main, sin habilitar otras búsquedas ni actuar sobre la planilla de 5.037 cruces sin aprobación Sponsor.
