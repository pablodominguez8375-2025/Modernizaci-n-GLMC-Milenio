# #768 — Handoff inicial inventario de cuentas preexistentes QA

- Sponsor aprobó activar, desactivar y eliminar definitivamente cuentas de acceso, con Máster `admin` único protegido.
- Fase A reclamada ChatGPT: inventario backend de cuentas provisionadas previamente (incluida `qa.admin`) visible **solo al Máster**, clasificación protegida sin habilitar acciones de mutación ni cambiar roles de Keycloak.
- Base `dev@e3e90cbbc21d131a477445b3127b4ce96a7e3605`; `main@6dfb9546a4873baff15955cf86abfd7d47e3d111` sin tocar.
- Archivos reservados: `backend/src/PMGM.Api/Modules/UserAccounts/{UserAccountContracts,UserAccountEndpoints,KeycloakAccountIdentityProvider}.cs`, `backend/tests/PMGM.Api.Tests/Authorization/AccountIdentityProviderTests.cs`, `backend/tests/PMGM.Api.Tests/Integration/UserAccountWorkflowTests.cs`.
- UI `frontend/src/UserAccountsPanel.tsx` y demo reservadas para Claude según GOV-003. Fase B de mutaciones solo después de contrato y pruebas.
- Código/CI/QA físico todavía no completados al crear esta rama. No recrear `qa.admin`, no borrar cuentas, no cambiar `main` ni reiniciar QA operativo.
