# #768 — Handoff inicial inventario de cuentas preexistentes QA

- Sponsor aprobó activar, desactivar y eliminar definitivamente cuentas de acceso, con Máster `admin` único protegido.
- Fase A reclamada ChatGPT: inventario backend de cuentas provisionadas previamente (incluida `qa.admin`) visible **solo al Máster**, clasificación protegida sin habilitar acciones de mutación ni cambiar roles de Keycloak.
- Base `dev@e3e90cbbc21d131a477445b3127b4ce96a7e3605`; `main@6dfb9546a4873baff15955cf86abfd7d47e3d111` sin tocar.
- Archivos reservados: `backend/src/PMGM.Api/Modules/UserAccounts/{UserAccountContracts,UserAccountEndpoints,KeycloakAccountIdentityProvider}.cs`, `backend/tests/PMGM.Api.Tests/Authorization/AccountIdentityProviderTests.cs`, `backend/tests/PMGM.Api.Tests/Integration/UserAccountWorkflowTests.cs`.
- UI `frontend/src/UserAccountsPanel.tsx` y demo reservadas para Claude según GOV-003. Fase B de mutaciones solo después de contrato y pruebas.
- Código/CI/QA físico todavía no completados al crear esta rama. No recrear `qa.admin`, no borrar cuentas, no cambiar `main` ni reiniciar QA operativo.

## Incremento en PR #769
- Rama base `dev@e3e90cbbc21d131a477445b3127b4ce96a7e3605`; HEAD de trabajo observado `8d4e0ecbe070c2fb00da568493418cab9d4cd7fd` (consultar HEAD vivo antes de fusionar).
- Backend `GET /api/system/user-accounts/inventory` solo Máster, autenticado, `no-store`: cuentas de Keycloak en inventario separado de `GET /api/system/user-accounts` (que conserva el contrato anterior).
- `qa.admin` y `admin` preexistentes son cuentas protegidas; origen `provisioned`, tipo `legacy_protected`. No modificar atributos antiguos ni heredar permisos. Los registros `pmgm_managed` correctamente clasificados conservan `member/superuser`.
- Protección de rol: superusuarios de soporte ven la lista gestionada normal pero no el inventario completo. Cuenta bootstrap de QA no es un segundo Máster por efecto del listado.
- Tests: `AccountIdentityProviderTests` inventario sintético y `UserAccountWorkflowTests` respuesta y permisos del endpoint. Estado ejecución local: SDK .NET no disponible en runtime; GitHub CI exact-head constituye la verificación compilada y aún está en curso.
- **NO** se implementaron todavía activar, desactivar o eliminar definitivamente; requieren API segura contra Keycloak, cierre de sesiones, conciliación, auditoría, rollback/error handling y pruebas negativas, más UI demo por Claude; #768 continúa OPEN.
- **NO** se desplegó en glmdch, no se alteró Keycloak, DB, WEB ni API física, `main` intacta; historial hotfix QA #754 preservado.
- Handoff de UI para Claude: consumir `GET /api/system/user-accounts/inventory` exclusivamente con sesión Máster; mostrar `username, name, kind, enabled, protected, origin`, marcar `qa.admin` «Cuenta preexistente · Protegida»; **no incluir botones de mutación en esta fase**. Mantener lista actual para superusuarios y otros operadores.
- Siguiente ChatGPT: comprobar Actions exact HEAD, revisar fallo si hay, registrar en GitHub/Drive, integrar a dev con gates SUCCESS y base sincronizada, después desarrollar fase B de las tres operaciones aprobadas con seguridad.

## Endurecimiento de autorización — Único Máster
El inventario no se habilita por el rol heredado platform_superadmin solamente. Se requiere adicionalmente el claim OIDC `preferred_username=admin`, de modo que `qa.admin` aunque tuviese ese rol en algún realm histórico NO obtiene inventario Máster. Test HTTP negativo con rol privilegiado y nombre `qa.admin`. Si OIDC del único Máster no trae ese claim estándar de Keycloak, el endpoint responde 403 (falla cerrada); la infraestructura debe verificar el mapper antes de desplegar, nunca saltarse esta condición.
