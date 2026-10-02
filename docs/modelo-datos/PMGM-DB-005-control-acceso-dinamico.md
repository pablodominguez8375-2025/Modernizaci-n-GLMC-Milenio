# PMGM-DB-005 — Control de acceso dinámico

## Diccionario y decisión de persistencia

Issue #266 / PR #267. El catálogo se conserva en el almacén ya versionado `core.institutional_rule_settings`, bajo el código `system.access.dynamic_catalog`. No se añade una tabla ni una migración SQL en este corte: se reutiliza el versionado/evidencia existente para evitar dos fuentes de verdad y permitir rollback por versión.

| Estructura | Tipo | Campos | Regla |
|---|---|---|---|
| `DynamicAccessCatalog` | JSON raíz | `version`, `menus`, `profiles`, `assignments` | deny-by-default; catálogo ficticio para demo |
| `DynamicMenu` | JSON | `code`, `name`, `isActive`, `views[]` | código único dentro del catálogo |
| `DynamicView` | JSON | `code`, `name`, `route`, `isActive` | una vista pertenece a un menú |
| `DynamicProfile` | JSON | `id`, `code`, `name`, `scope`, `description`, `isSystem`, `isActive`, `menuCodes[]`, `grants[]` | perfiles de sistema protegidos |
| `DynamicGrant` | JSON | `viewCode`, `actions[]` | acciones válidas: `view`, `create`, `write`, `edit`, `delete`, `print` |
| `DynamicAssignment` | JSON | `id`, `subject`, `profileCode`, `organizationId`, `effectiveFrom`, `effectiveTo`, `isActive` | asignación revocable, nunca se elimina físicamente |

## Antes / después

- Antes: `system.access.profile_definitions` y `system.access.user_assignments` eran blobs consumidos sólo por la UI; no existían menú/vista/grant ni evaluación backend.
- Después: `system.access.dynamic_catalog` es un contrato único administrado por API; cada mutación deja auditoría y el endpoint `/api/system/access/evaluate` responde sólo cuando existe una asignación vigente y un grant explícito.

## Migración e impacto

Migración SQL: **no aplica**. La estructura existente `institutional_rule_settings` ya soporta el nuevo código y su historial por fecha. El primer guardado crea una versión activa; las operaciones posteriores del mismo día actualizan el snapshot y conservan la evidencia de cada cambio en `audit_events`. No se migran ni reinterpretan tarifas, cargos, atribuciones ni antecedentes institucionales.

## Pruebas requeridas

- catálogo protegido para administradores del sistema;
- creación, edición y baja lógica; perfiles de sistema no eliminables;
- validación de scope y acciones, incluyendo `print` explícito;
- asignación/revocación y evaluación deny-by-default;
- auditoría de cada mutación;
- serialización compatible con la demo sintética.
