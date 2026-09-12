# Acceso institucional OIDC

## Desarrollo

Copiar `.env.example` a `.env.local`. `VITE_USE_MOCKS=true` habilita únicamente datos demostrativos y muestra una etiqueta visible. Para acceso real usar `false`, configurar `VITE_OIDC_AUTHORITY`, `VITE_OIDC_CLIENT_ID` y `VITE_OIDC_SCOPE` (incluyendo openid y el scope de API requerido por el proveedor). Mantener `VITE_API_BASE_URL` vacío: Vite/Nginx reenvía `/api` al backend y evita CORS o envío de tokens a otros orígenes.

Registrar en el IdP un cliente público SPA con Authorization Code + PKCE S256, sin secreto, implicit ni password grant. Permitir CORS para discovery y token endpoint desde el origen del frontend. Registrar exactamente:

- Desarrollo Vite: `http://localhost:5173/auth/callback` y retorno de logout `http://localhost:5173/auth/logout`.
- Compose: `http://localhost:8081/auth/callback` y `http://localhost:8081/auth/logout`.
- Producción: las mismas rutas bajo el origen HTTPS institucional, sin comodines.

El proveedor debe emitir un access token JWT para la audiencia del backend, con los claims institucionales `pmgm_role`, `pmgm_org`, `pmgm_scope`. Configurar `Authentication__Authority` y `Authentication__Audience` en la API. El scope de API y la audiencia no son necesariamente el client ID de la SPA. Configurar MFA y revocación central en el IdP según ADR-002. No se necesita entregar credenciales al frontend ni a esta tarea.

## Docker Compose

Definir `VITE_USE_MOCKS=false`, `OIDC_AUTHORITY`, `OIDC_CLIENT_ID`, `OIDC_SCOPE` y `OIDC_API_AUDIENCE` en el entorno que ejecuta Compose. Reconstruir la imagen web al cambiarlos: Vite los incorpora al compilar, no al arrancar Nginx. La autoridad HTTPS debe ser accesible tanto desde el navegador como desde el contenedor API y conservar el mismo issuer. La excepción HTTP de autoridad sólo se permite en Vite DEV para loopback, no en imágenes compiladas.

## Sesión y diagnóstico

Los tokens sólo viven en memoria. Recargar exige volver a ingresar; la sesión SSO del IdP puede evitar pedir credenciales. Expiración y 401 cierran el acceso local. Un 403 conserva la sesión y muestra falta de permiso. Logout elimina tokens locales aunque el endpoint del proveedor no esté disponible; en ese caso se informa que la sesión remota puede continuar. No hay renovación automática ni almacenamiento de refresh tokens.

Nunca registrar tokens, código, state o respuestas completas del IdP. Nginx omite logs de las rutas de callback y aplica no-store/no-referrer; configurar también el balanceador o proxy exterior. No activar analytics/scripts externos en callbacks. Para producción aplicar HTTPS y una CSP compatible con el proveedor; el almacenamiento en memoria no protege frente a XSS activo.

## Verificación

`npm ci`, `npm run lint`, `npm test`, `npm run build`. CI ejecuta lo anterior, construye la imagen, valida Nginx en la red con alias api y prueba backend/PostgreSQL.

Aceptación con el IdP elegido: ingresar y comprobar `/api` con Bearer access token, denegar consentimiento y reintentar, alterar/repetir state y comprobar rechazo, recargar, dejar expirar, probar cuenta sin permisos y cerrar sesión con y sin conexión al IdP. Esa aceptación requiere el proveedor real y no se considera cubierta por los dobles de prueba automatizados.
