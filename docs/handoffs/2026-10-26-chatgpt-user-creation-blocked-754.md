# #754 — Alta de usuario QA sin respuesta (26-10-2026)

Inicio: dev@6ff4ae0d092d4214df4ea31af484efbc969fde54; main@6dfb9546a4873baff15955cf86abfd7d47e3d111, protegida. Issue #754, PR draft #755, ChatGPT, trabajo principal backend/QA con ajuste UI puntual según GOV-003. Reporte Sponsor: pulsar «Crear usuario y enviar clave inicial» en tipo Superusuario no produce respuesta. Captura contiene PII: **no se exporta ni se copia a GitHub o Drive**.

## Causa verificada en código
El botón en `UserAccountsPanel.tsx` estaba `disabled` cuando `deliveryConfigured=false`, sin indicación en el drawer y con apariencia de botón activo. La pantalla ya mostraba advertencia fuera del panel. `GetEligibleMembers` calcula `identity.Configured && mail.Configured`. Keycloak requiere ServerUrl/Issuer/ClientSecret; el envío inicial requiere SMTP legacy o vault de credencial Google y además `UserAccounts:Mail:LoginUrl` HTTPS. El SMTP institucional «ready» NO certifica este circuito de alta ni la configuración Keycloak.

## Cambio propuesto y pruebas
- `UserAccountEndpoints.cs`: nuevas señales booleanas `identityConfigured`, `mailConfigured` en ruta ya autorizada sin divulgar secretos ni opciones de entorno, conserva `deliveryConfigured`.
- `UserAccountWorkflowTests.cs`: matriz 4 combinaciones PostgreSQL; si falla cualquiera, backend retorna 503 sin creación, envío ni credenciales.
- `frontend/src/api/userAccounts.ts`: contrato con flags opcionales, compatible con runtime anterior.
- `frontend/src/UserAccountsPanel.tsx`: advertencia y motivo dentro del drawer, botón de explicación si bloqueado, ningún POST, mensajes de resultado dentro y fuera del drawer.
- `frontend/src/UserAccountsPanel.test.tsx`: helper seguro, cuatro escenarios, compatibilidad con servidor antiguo y SSR.
- No se cambia Master único ni permisos de creación; no se suministran credenciales.

## Requisito operacional para funcionamiento real de creación en QA (PENDIENTE)
Verificar vía operador autorizado, solo flags (nunca valores de secretos): `UserAccounts__Identity__ServerUrl`, `Issuer`, `ClientSecret` bajo rol Keycloak de gestión de usuarios; `UserAccounts__Mail__LoginUrl` URL **HTTPS** y SMTP vault/legacy. Solo la lectura de `GET /api/system/user-accounts/eligible-members` con cuenta Master y respuesta autenticada permitirá conocer `identityConfigured` y `mailConfigured`. Si están a false, hay que configurar en entorno seguro y probar envío sintético controlado con el Master, sin duplicarlo. No reemplazar cuenta Master, no usar datos reales en test, ni deshabilitar SMTP o bypass.

**QA físico no actualizado en este cambio**, git merge dev requiere 3 gates exact-head; `main` no se promueve, no reenviar claves ni modificar el Taller Libertad 23. No atribuir 500 #752. Drive continuidad se actualizará.
