# Handoff — 28-10-2026 — Revalidación de sesiones Keycloak (#768)

Estado inicial: `dev@415d86b87f83705ad433f834f0fb46d9586c6565`, `main@6dfb9546a4873baff15955cf86abfd7d47e3d111`. Coordinación: ChatGPT, issue #768; fase posterior al PR #772.

## Brecha a cubrir

Una cuenta puede volver a habilitarse después de un cierre de sesiones, mientras un token viejo siga figurando `active=true` en alguna respuesta de introspección. La combinación `active=true` + `enabled=true` no acredita que la sesión de ese token siga vigente. Se verificará también el identificador de sesión contra la lista viva de Keycloak, sin usar tokens ni identidades reales durante el desarrollo.

## Alcance reservado

`backend/src/PMGM.Api/Modules/UserAccounts/AccountTokenIntrospection.cs`, `backend/tests/PMGM.Api.Tests/Authorization/AccountTokenIntrospectionTests.cs` y este handoff.

## Restricciones

Solo pruebas sintéticas. Mantener `RequireLiveTokenIntrospection=false` y `EnableLifecycleMutations=false` por defecto. No activar servicios, no desplegar en glmdch, no borrar identidades, no promover `main` ni modificar UI de Claude. Los gates deberán verificarse en el HEAD exacto antes de integrar a dev. La UAT real con Keycloak sintético sigue siendo requisito independiente.

## Incremento de implementación

- PR DRAFT #778 en rama `fix/account-session-revalidation-20261028-gpt`, issue #768.
- Backend `AccountTokenIntrospection.cs`: exige `active=true`, `sub` del token igual al UUID esperado, cuenta `enabled=true` y **sesión activa cuyo `id` coincida literalmente con `sid` o `session_state` de la introspección**. La lista se obtiene sin caché desde GET Keycloak `/admin/realms/{realm}/users/{uuid}/sessions`, usando credencial administrativa confidencial existente. Si el proveedor falla, faltan datos, la respuesta no es lista o hay dos claims de sesión contradictorios, el acceso queda denegado.
- El `sid` OIDC es opaco y se compara como cadena de longitud acotada, no se asume UUID. No se registra ni se devuelve en logs.
- Tests `AccountTokenIntrospectionTests.cs`: token inicial válido; desactivar ⇒ rechazo; reactivar con nueva sesión conservando token antiguo ⇒ rechazo; token emitido desde sesión nueva ⇒ aceptado; alternancia de `session_state`; datos ausentes/incompatibles, lista malformada y prohibición 403 de consulta de sesiones ⇒ denegar sin cachear.
- Justificación: la documentación oficial de Keycloak advierte que cerrar sesiones no revoca necesariamente los access tokens existentes; no puede usarse `active=true` y `enabled=true` como garantía de sesión actual. Fuente externa: https://www.keycloak.org/docs/latest/server_admin/ y contrato de API Keycloak https://www.keycloak.org/docs-api/latest/rest-api/.
- El cambio incrementa la carga por petición autenticada cuando `RequireLiveTokenIntrospection=true`: otra solicitud al Admin REST por petición. La bandera **permanece OFF por defecto** y el desempeño/permisos deben validarse en Keycloak QA sintético antes de habilitar en cualquier servidor.
- Mantener issue #768 abierto para validación UAT y verificación del caso de logout real. PR #771 de suministros QA sigue bloqueado; `main` intacta. No tocar UI Claude, cuentas, Keycloak físico ni deploy.
- Gates CI, Showcase e instalable: verificar contra HEAD final del PR. Tras merge autorizado a `dev`, registrar SHA en este handoff/issue/Drive; nunca inferir UAT física de pruebas sintéticas.
