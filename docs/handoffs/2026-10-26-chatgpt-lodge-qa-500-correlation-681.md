# #681 — Recurrencia HTTP 500 en Supervisión de Talleres con qa.admin; corrección de diagnósticos

Fecha: 2026-10-26. Rama desde dev@cb94ee2290d62f4bc73f01512abfb0c54f3809d5. Main protegida.

## Evidencia
Sponsor aporta `soporte-diagnosticos-qa (1).csv`, panel de QA (NO se copia CSV a GitHub/Drive). 28 eventos, de los cuales 1 GET HTTP500, 16 HTTP403, 11 HTTP404. Incidente 2026-10-26T11:31:28.6953605Z (08:31 Chile), correlationId `0HNPJ9BI34MHQ:00000001`, ruta `(sin ruta)`. Reproducción declarada con cuenta histórica `qa.admin`, distinta de Máster único `admin`. Los 403/404 de permisos/fixtures no acreditan la causa del GET 500.

## Diagnóstico verificable
`SupportDiagnosticMiddleware` lee `HttpContext.GetEndpoint()` después de `UseExceptionHandler`; un handler puede borrar la selección del endpoint. En vez de almacenar Request.Path (con ID real del Taller), el middleware conserva el **RoutePattern.RawText** definido por ASP.NET una vez seleccionado. Se añade `app.UseRouting()` antes del middleware para garantizar que exista metadato del endpoint, y un snapshot ANTES del handler con fallback seguro `(sin ruta)`. Ningún valor de ruta, GUID, headers, tokens, excepciones o PII se almacena. La corrección solamente sirve para incidentes FUTUROS; no recupera la ruta perdida en el CSV histórico, no repara el HTTP 500, ni modifica privilegios de qa.admin.

## Pruebas
Regresión unitaria para retorno 500 con borrado de endpoint por manejador, excepción thrown sin texto, y ruta desconocida; sin DB, fixtures personales ni datos QA. Tres gates CI/Showcase/QA Installable exact-head obligatorios, PR solo a dev. No desplegar ni hacer DDL/GRANT sin causa raíz y backup.

## Operación posterior
Primero usar logs privados host para correlationId citado; comprobar SHA QA API/WEB + cinco checks de metadatos `/api/system/support/lodge-data-health` con usuario CanConfigureSystem cuando esté disponible; reproducir GET `tenidas`, `miembros/opciones`, `instrucciones` bajo qa.admin con datos sintéticos y guardar solo route template, método, status, correlationId. Cuando se identifique el GET/código DB/excepción, abrir parche real con pruebas, luego deploy firmado con rollback y UAT. Nunca confundir la creación del Máster activo con arreglo de los permisos de qa.admin.
