# Mis planchas de trabajo y publicación segura en Biblioteca Virtual

## Mejora autorizada

El Hermano puede consultar sus planchas de trabajo, subir una plancha y reemplazar cualquier versión de su propia autoría, incluso si la carga inicial la efectuó Secretaría. Secretaría del Taller puede cargar a nombre de un Hermano activo del mismo Taller y actualizar la misma plancha vinculada a Tenida, agregando una versión al historial en vez de duplicarla. No se concede acceso del Hermano a planchas ajenas ni se altera el flujo de documentos oficiales.

Cada carga incluye una descripción breve. Se conserva el historial; el grado efectivo del autor determina la clasificación de acceso. La nueva versión sólo queda vigente y entra a Biblioteca Virtual → «Planchas de Trabajo» tras validar firma/tipo e integridad y completar el escaneo antimalware sin hallazgos. Un rechazo conserva la versión anterior. La plancha puede relacionarse con una Tenida sin cambiar la privacidad del acta ni el filtro por grado de Biblioteca.

## Causa y solución técnica

El catálogo y los adjuntos de Tenida ya admitían el tipo `work_paper`, pero faltaba el flujo personal de autoría, historial y reemplazo. Además, las rutas genéricas de gestión documental podían omitir metadatos de autoría/grado o adelantar el ciclo documental. La solución añade rutas específicas autenticadas, metadatos versionados, verificación de autor y pertenencia activa, actualización de la publicación sólo al terminar un análisis limpio, y bloqueo de las rutas genéricas para manipular planchas. El grado efectivo también se valida en el listado legado, detalle por ID y descarga directa, para que no se pueda eludir el filtro del catálogo.

Se reutilizan el catálogo, el almacenamiento privado, la comprobación de firma, hash, ClamAV y descarga autenticada existentes. No se crean mocks de servicios paralelos.

## Verificación y estado

- Frontend local: `npm ci`, Vitest 208/208, lint sin errores y build productivo exitosos.
- Kit de regresión ampliado con QA-042, pendiente de ejecución; `srv01` sigue en pausa.
- Backend aún sin compilar localmente: el entorno no tiene .NET SDK; se requiere CI exact-head para validar compilación, migración y endpoints.
- Código comprometido en `feature/member-work-papers-20260927`, basada en `dev@8f41db9dc18e4cb5f1577d35c1af86a7c4b9b138`; PR aún no abierto y sin integración en `dev`.
- `main` permanece intacta. No se ha publicado Pages ni un nuevo artefacto QA. Este estado no implica instalación real, QA aceptada ni UAT.
