# Handoff — 28-10-2026 — Revalidación de sesiones Keycloak (#768)

Estado inicial: `dev@415d86b87f83705ad433f834f0fb46d9586c6565`, `main@6dfb9546a4873baff15955cf86abfd7d47e3d111`. Coordinación: ChatGPT, issue #768; fase posterior al PR #772.

## Brecha a cubrir

Una cuenta puede volver a habilitarse después de un cierre de sesiones, mientras un token viejo siga figurando `active=true` en alguna respuesta de introspección. La combinación `active=true` + `enabled=true` no acredita que la sesión de ese token siga vigente. Se verificará también el identificador de sesión contra la lista viva de Keycloak, sin usar tokens ni identidades reales durante el desarrollo.

## Alcance reservado

`backend/src/PMGM.Api/Modules/UserAccounts/AccountTokenIntrospection.cs`, `backend/tests/PMGM.Api.Tests/Authorization/AccountTokenIntrospectionTests.cs` y este handoff.

## Restricciones

Solo pruebas sintéticas. Mantener `RequireLiveTokenIntrospection=false` y `EnableLifecycleMutations=false` por defecto. No activar servicios, no desplegar en glmdch, no borrar identidades, no promover `main` ni modificar UI de Claude. Los gates deberán verificarse en el HEAD exacto antes de integrar a dev. La UAT real con Keycloak sintético sigue siendo requisito independiente.
