# PMGM-GOV-004 — salida de controles GET genéricos soporte #604

Fecha: 2026-10-23. Commit base `dev@eec77c53f3bd0c8ad5120f6fa97ca3af2c4c9c6a`.

## Cambios en estructuras y contratos

| Tabla / contrato | Antes | Después |
|---|---|---|
| `core.organizations` | `Type=support` reservado (#652); no hay instancia sembrada | **Sin cambio físico** |
| `core.memberships` y `core.members` | Relación institucional existente | **Sin migración, escritura ni nueva relación** |
| GET `/api/institutional/organizations/{id}/profile` | Permitía consultar perfiles y agregados por UUID soporte con alcance Orden | `404` para tipo soporte, antes de proyecciones de miembros, finanzas o historia |
| GET `/api/members?organizationId=...` | Permitía consultar un directorio sintético por UUID aun si no figuraba en selector | `404` para organización tipo soporte, antes de nombres |
| GET `/api/members/{id}/profile`, GET `/history` | Permitía datos de un miembro sintético con membresía soporte si se conocía su ID | `404` si el miembro tiene **cualquier** membresía ligada a organización tipo soporte, incluso vínculo mixto accidental |
| Listado de miembros de Taller real `workshop` | Incluía un miembro con vínculo mixto accidental `workshop`/`support` | Excluye al miembro sintético de filas y conteo total (fail closed); conserva miembros reales |
| Talleres verdaderos `workshop` | Rutas institucionales vigentes | Sin cambio para miembros sin vínculos `support`; sigue respondiendo |

Categorización por tipo exacto `support` sin distinguir capitalización; no depende del nombre «Taller 00» ni del número 00. Prueba HTTP/PostgreSQL `DynamicAccessHttpTests.Synthetic_support_organization_is_hidden_from_general_directory_even_for_order_administrator` amplía revisión de rutas por UUID y confirma control positivo para Taller real. Todas las identidades insertadas son ficticias y sin RUT ni correo.

**No se declara aislamiento global**: tesorería, Hospitalaria, padrones, reportes, documentos, alta/baja de membresías y otros endpoints deben revisarse por separado antes de sembrar datos ficticios. No se ha dado acceso ni se ha generado ningún Máster, usuario, superusuario, membresía real o identidad Keycloak. `main` y QA físico intactos.
