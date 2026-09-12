# PMGM-ADR-005 — OIDC/PKCE en la SPA

Estado: aceptado para implementación, 2026-09-07. Complementa ADR-002; proveedor definitivo pendiente.

## Decisión

React usa un adaptador `OidcSession` desacoplado de las vistas y del cliente API. `oidc-client-ts` 3.5.0 implementa Authorization Code con PKCE S256, state de un solo uso y nonce aleatorio. Se registra un cliente público, sin client secret. El proveedor se selecciona mediante autoridad, client ID y scopes públicos de compilación.

El usuario y los tokens residen sólo en memoria. sessionStorage contiene exclusivamente transacciones temporales de protocolo (state, nonce, code verifier; vencimiento de diez minutos). No se usa localStorage para identidad. No se solicita offline_access, no se conservan refresh tokens y no hay renovación silenciosa. Recargar, expirar o recibir 401 exige ingreso interactivo; puede aprovechar la sesión SSO del proveedor. El cierre local precede al intento de cierre remoto, incluso si éste falla. Otras pestañas mantienen sus propias sesiones hasta expiración o rechazo de la API: no se afirma revocación instantánea de JWT.

El cliente API recibe sólo access tokens mediante una función inyectada, exige sesión y usa el mismo origen a través del proxy; no sigue redirecciones ni usa cookies o caché. Un 403 informa falta de permisos sin iniciar bucles de login. La autorización institucional sigue exclusivamente en la API, que valida firma, emisor, audiencia y vigencia del JWT y aplica pmgm_role, pmgm_org y pmgm_scope.

El modo demo requiere VITE_USE_MOCKS=true; no es alternativa automática ante errores. La configuración ausente bloquea el acceso real. Las rutas fijas /auth/callback y /auth/logout limpian parámetros del historial antes de cargar datos; Nginx deshabilita sus logs de acceso y caché. El proxy exterior debe aplicar las mismas restricciones.

## Límites y operación

Memoria reduce persistencia, pero no elimina el riesgo XSS. Servir por HTTPS, mantener dependencias, evitar scripts de terceros y configurar CSP en el ingreso de producción con connect-src del proveedor elegido. No publicar secretos en VITE_*. Las pruebas automatizadas cubren límites de sesión y API; la aceptación con un proveedor institucional real sigue pendiente de su selección y configuración.

Referencias: https://authts.github.io/oidc-client-ts/ y https://www.rfc-editor.org/rfc/rfc7636.
