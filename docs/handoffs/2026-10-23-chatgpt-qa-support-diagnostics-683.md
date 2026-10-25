# Handoff — Soporte: errores, advertencias y exportaciones de auditoría (#683)

Fecha: 23-10-2026. Origen: Sponsor solicita opción «Soporte» para obtener fallas/advertencias, tras #681 500 al seleccionar Taller Libertad N.º 23. Entorno QA operativo glmdch; datos personales controlados, no exponerlos en reportes.

## Estado fuentes
- Base de rama: `dev@5873fca871b02163c1e3ea06624274db2fd224da`; `main@6dfb9546a4873baff15955cf86abfd7d47e3d111` intacta.
- PR #684 `feature/support-diagnostics-readonly-683-20261023-gpt`, Issue #683; sólo `dev`. Estado inicial PR borrador.
- Registro de fallo QA #681 y pruebas de regresión PR #682 (3/3 al 23-10), **sin causa física identificada ni reparación del HTTP 500**.

## Implementación
- Middleware nuevo `SupportDiagnosticMiddleware` delante del manejador de excepciones, con buffer en memoria por proceso, máximo 500 metadatos de respuesta: fecha UTC, severidad HTTP (5xx error, 4xx warning), método, plantilla de ruta, status y correlationId.
- `GET /api/system/support/diagnostics` únicamente autenticado con `IInstitutionalAccessService.CanConfigureSystem`, filtros `severity`, `route` (plantilla, no ruta usuario), `from`, `to` y `limit` hasta 200. Sin nombres, cuerpos, querystring, tokens, IP, stacktrace ni hashes de clave.
- `Sistema → Soporte · errores y advertencias`, consulta y descarga CSV/JSON seguros; acceso desde pestaña a Auditoría institucional (consulta existente audit_events).
- Tests `SupportDiagnosticBufferTests` y `supportDiagnosticExport.test.ts`, QA ficticia.
- No modificar `App.tsx` (archivo caliente), esquema de BD, migraciones, Keycloak ni servidor; ningún dato real se escribió.

## Limitaciones y alcance
- **Sólo registra desde el arranque de una instancia API con la versión integrada**. Buffer volatile; reinicio/reemplazo elimina historia; no lee los registros históricos de Docker/journald ni correlaciona clases SQLSTATE internas. Si hay múltiples réplicas, la respuesta muestra sólo la instancia que atendió la consulta. No confundir con bitácora auditable permanente.
- Captura 5xx/4xx genérico de HTTP; no todos los `ILogger.LogWarning` internos ni alertas offline.
- El backend controla acceso, UI no confiere permiso. Permisos granulares futuros según #604/#523: `soporte.ver_registros`, separar registros técnicos y auditoría de acciones; confirmar política institucional antes de habilitar acceso a soporte delegado.
- El formulario y descargas incluyen explícitamente advertencias de temporalidad/cobertura.
- En implementación futura: persistir telemetría sanitizada mediante canal de colección protegido y cifrado con retención, múltiples instancias, niveles de aplicación, logs de infraestructura, y auditoría de exportaciones sin datos personales.

## QA y validación
1. Esperar CI / Showcase / QA Installable SUCCESS exact HEAD de #684, verificar mergeable y dev vigente antes de fusionar.
2. Aplicar corte firmado en QA autorizado, comprobar ruta/API con cuenta Sistema habilitada y prohibición 403 para usuarios Taller, 4xx/5xx generados por *datos ficticios*, sin secretos.
3. Probar filtros, correlación, CSV y JSON, móvil; obtener correlación nueva de #681 únicamente **después** de instalar. No repetir operaciones mutativas como diagnóstico.
4. Para raíz #681, consultar logs internos del host por correlationId (fuera de GitHub/Drive). Con evidencia, corregir backend o esquema, luego cerrar incidentes.
5. Registrar SHA instalado y UAT en #97/#683 y Drive. Ninguna promoción `main` sin Sponsor.

## Archivos
`backend/src/PMGM.Api/Modules/Audit/SupportDiagnosticBuffer.cs`, `SupportDiagnosticEndpoints.cs`, `backend/src/PMGM.Api/Program.cs`, `backend/tests/PMGM.Api.Tests/Audit/SupportDiagnosticBufferTests.cs`, `frontend/src/api/pmgmApi.ts`, `frontend/src/SupportDiagnosticsPanel.tsx`, `frontend/src/supportDiagnosticExport.ts`, `frontend/src/supportDiagnosticExport.test.ts`, `frontend/src/SystemConfigurationPage.tsx`, este handoff.
