# #604 — Alta de usuarios reservada al Máster único

Fecha 2026-10-23. Reclamado por ChatGPT según PMGM-GOV-003, issue #604 `agente:chatgpt`. Rama `fix/604-master-only-account-creation-20261023-gpt` basada en `dev@eec77c53f3bd0c8ad5120f6fa97ca3af2c4c9c6a`, main `b77de59e170ebfa2abec71b418f6d8b0407ba7ad`.

## Hallazgo
En `backend/src/PMGM.Api/Modules/UserAccounts/UserAccountEndpoints.cs`, `POST /api/system/user-accounts` solo exigía `CanConfigureSystem`. Administrador de Gran Logia con permisos de Sistema podía dar de alta usuarios normales, aunque el Sponsor aprobó que **solo el Máster único admin** cree usuarios y superusuarios. El Master no puede crear un segundo Máster, restricción implementada con `PlatformAdministrator` y `KeycloakAccountIdentityProvider`.

## Contrato de incremento
- Exigir `IsPlatformSuperAdmin` en el propio endpoint POST antes de preparar credenciales, enviar correo, iniciar transacción o consultar miembros. No basta con habilitación de menú o permisos dinámicos de lectura.
- Crear usuarios activos sigue empleando ficha y correo del hermano; superusuarios limitados siguen sin ficha institucional ni rol global de Máster. No cambiar autorización de GET ni de recuperación de credenciales.
- Tests HTTP/PostgreSQL: GranLogiaAdmin y roles ordinarios denegados al intentar ambas clases de cuenta, con `identity.Prepares == 0` y `mail` sin invocaciones, Máster permite ambas, creación de Máster nunca autorizada. Datos ficticios.
- UI de Clau permanece intacta: su siguiente cambio puede ocultar botón crear usuario a quien no sea Máster, sin depender de seguridad visual.
- No implica que exista bootstrap único, inventario Keycloak ni MFA de producción: `#604` sigue abierto hasta esos hitos y QA/UAT. No generar credenciales reales ni crear Taller 00.

## Puertas
3/3 GitHub Actions SUCCESS exact SHA y behind 0. Solo fusionar a dev, no main, producción ni QA físico; conservar comentario en #604 y espejo Drive.

## Incremento aplicado y reconciliado

- POST `/api/system/user-accounts`: `CanConfigureSystem` **y** `IsPlatformSuperAdmin`, antes de cualquier identidad, SMTP o BD. No se amplía `GET`; el Máster continúa imposibilitado de crear otro Máster.
- GET `/api/system/user-accounts/eligible-members` añade `canCreateUser:boolean` junto a `canCreateSuperuser`, ambos solo verdaderos para el Máster. Clau puede usar este flag en la UI; el backend sigue verificando POST.
- `UserAccountWorkflowTests`: permisos negativos de GranLogiaAdmin/Tesorería con cero llamadas a identidad/correo, capacidades de GranLogiaAdmin falsas y de Máster verdaderas, y los casos existentes de creación e invalidación con actor Máster.
- La rama preserva el árbol de `dev@18fbbfed7cede2823c0f3fec2760f2aa528b5f48` (PR #654) y modifica solo `UserAccountEndpoints.cs`, `UserAccountWorkflowTests.cs`, este handoff y el registro GOV-004 nuevo. Documentación: `docs/modelo-datos/registros/2026-10-23-chatgpt-master-only-create-authorization-604.md`.
- No se dio de alta cuenta, no hubo secretos, no se creó Taller 00. Falta bootstrap/MFA/QA/UAT para cierre de #604. CI/Showcase/Instalable tienen que ser 3/3 SUCCESS sobre HEAD final y behind dev=0 antes de merge; `main` congelada.
