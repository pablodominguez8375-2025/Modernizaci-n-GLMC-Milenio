# Handoff — Claude — revisión de seguridad y correcciones inmediatas — 05-10-2026

- **Issue:** #326 (`agente:claude`). Pedido del PO: «revisa vulnerabilidades y posibles mejoras».
- **Revisión previa de Drive:** solo carpeta Proyecto Centenario. ChatGPT ejecutó y consolidó las instrucciones v2 del tarifario.
- **Base:** `dev@1216e15`. **Versión:** UI QA v0.89.

## Revisión realizada

| Área | Resultado |
|---|---|
| Dependencias npm (prod y dev) | **0 vulnerabilidades** (`npm audit`). |
| Dependencias NuGet | No verificado: el entorno no tiene dotnet. Se delega su revisión en CI. |
| Secretos en el repositorio | No hay credenciales versionadas. Solo existen `.env.example`. |
| Sesión OIDC | Correcta: los tokens viven **en memoria** (`InMemoryWebStorage`), se usa PKCE y en `sessionStorage` solo queda el *state*. |
| XSS | No se usa `dangerouslySetInnerHTML` ni `innerHTML`. |
| Autorización de la API | Correcta: los **48 grupos de endpoints** exigen `RequireAuthorization`. Solo `/health/*` y `/api/system/info` son anónimos. |
| Datos de demostración | Producción bloquea `DemoData:Enabled`. |

## Corregido en este PR

1. **Inyección de fórmulas en exportaciones CSV (severidad media).** Un valor que empieza con `=`, `+`, `-` o `@` podía ejecutarse como fórmula al abrirse en Excel. Se neutraliza con `'`:
   - `listing.tsx` → `buildCsv`: Fichas de miembros y Gestor Documental. Los valores numéricos no se modifican.
   - `AuditableEventLog.tsx` (componente de ChatGPT): bitácora de eventos.
   - Tesorería y Revisión de accesos ya contaban con esta protección.
2. **Límite de carga en nginx (severidad media, falla funcional).** No había `client_max_body_size`, por lo que regía el valor por defecto de 1 MB. Con eso, en un despliegue real cualquier foto, acta o plancha de más de 1 MB fallaba con error 413, aunque la API acepta hasta 10 MB. Ahora el límite es `client_max_body_size 12m`.
3. **Cabeceras de nginx.** Se agregaron `server_tokens off` y `Permissions-Policy` (cámara, micrófono, ubicación, pagos y USB desactivados).

Tests nuevos: `csv-safety.test.ts` y `nginx-security.test.ts`. Total: 420 en verde.

## Delegado a ChatGPT (infraestructura y backend)

Ver «INSTRUCCIONES PARA CHATGPT — 2026-10-05 — Endurecimiento de seguridad» en Drive.
