# GOV-004 — Salida de contrato: alta reservada al Máster (#604)

Corte 23-10-2026. Base anterior `dev@eec77c53f3bd0c8ad5120f6fa97ca3af2c4c9c6a`. Se reconcilió con `dev@18fbbfed7cede2823c0f3fec2760f2aa528b5f48` (PR #654) sin sobrescribir ningún blob modificado en dev. Sin migración ni cambio de esquema.

| Contrato | Antes | Después | Control |
|---|---|---|---|
| POST /api/system/user-accounts | `CanConfigureSystem`; GranLogiaAdmin podía crear usuarios sin ser Máster | Exige `CanConfigureSystem && IsPlatformSuperAdmin` a **todas** las altas de usuario y superusuario | Respuesta 403 previa a identidad, correo, consulta de miembro o transacción |
| `PlatformAdministrator=true` | Denegado incluso al Máster | Se mantiene **denegado** | Nunca existe alta/duplicación de segundo Máster |
| GET /api/system/user-accounts/eligible-members | `canCreatePlatformAdministrator=false` y `canCreateSuperuser` | Añade `canCreateUser` booleano desde `IsPlatformSuperAdmin` | Cliente UI puede ocultar alta; servidor sigue siendo autoridad |
| Cuentas existentes / roles Keycloak | Sin cambio por este PR | Sin cambio | No amplía roles, perfiles, sesiones, correos ni MFA |

## Pruebas

`UserAccountWorkflowTests` (HTTP + PostgreSQL): Máster puede crear usuario normal y superusuario limitado; GranLogiaAdmin y Tesorería reciben 403 para crear usuario, sin efectos `identity.Prepares=0`, sin cuenta habilitada y sin correo; GranLogiaAdmin conserva acceso de lectura a elegibles, con `canCreateUser=false` y `canCreateSuperuser=false`; Máster tiene ambas capacidades verdaderas. Segunda cuenta Máster sigue prohibida y auditada.

## Invariantes y alcance pendiente

El único rol global que satisface `IsPlatformSuperAdmin` **debe estar vinculado exclusivamente a la cuenta Máster `admin` en identidad**; este incremento no verifica ni crea bootstrap y no basta por sí solo para asegurar unicidad Keycloak. Falta MFA, inventario único de rol, perfiles de soporte dinámicos, flujo «Ver como» #574, aislamiento completo de Taller 00, QA/UAT. No presentar #604 como cerrado hasta evidencias. No se tocan `main`, producción ni QA físico. Handoff: `docs/handoffs/2026-10-23-chatgpt-master-only-user-creation-604.md`.
