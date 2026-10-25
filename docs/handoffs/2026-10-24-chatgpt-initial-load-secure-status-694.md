# PMGM #694 — fase I: consulta HTTP de estado de carga inicial protegida por Máster

Fecha: 24-10-2026. Agente ChatGPT. Base inicial: `dev@dece2d609b3ccd54dbd81d437163e11912ee5340`.

## Objetivo
Conectar lectura real de estado (fases F/G, PR #705/#706) a política de identidad única (fase H, PR #707) mediante **un GET de solo lectura**. La respuesta puede dar `not_opened` / `open` / `closed` / `unavailable` y versión, siempre `CanApply=false`, `WritesPerformed=0`. No expone digests jurídicos, IDs auditoría, claims, datos personales ni nombres.

## Seguridad de acceso
- Endpoint: `GET /api/platform/bootstrap/initial-load/status`, `RequireAuthorization()`, API previa de configuración.
- Reautenticar explícitamente JWT Bearer (`AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme)`) para que nunca se acepten identidades de otro esquema o claims suministrados por el cliente.
- Configuración **sólo servidor**, inicialmente ausente: `InitialLoad:MasterSubject` y `InitialLoad:MasterIssuer`; si falta, rechazar con 403 sin revelar configuración. En producción se deberá enlazar desde un registro confiable (fuente IdP, cuenta única `admin`), jamás query string, header, body o parámetro provisto por usuario. No almacenar secretos, no incorporar valores reales en repositorio.
- Verificar con `InitialLoadMasterIdentityPolicy.Verify` rol plataforma, scope `order`, sujeto único `sub` **o** `ClaimTypes.NameIdentifier` cuando JwtBearer realiza el mapeo estándar (si ambos aparecen o hay duplicados, denegar), `preferred_username=admin`, `iss` único del JWT validado, emisor coincidente, no impersonación `pmgm_support_operator`.
- `Cache-Control: private, no-store`, `Pragma: no-cache`. DB no disponible/inconsistente → respuesta fail-closed `unavailable`; sin stacktraces ni SQL, nunca apertura presunta. El controlador no ejecuta `MigrateAsync`.
- **No se crean** operaciones POST, upload, plan/apply ni transición persistente; Jurídico Ley 21.719 no ha autorizado datos reales. Tampoco valida que la cuenta de Keycloak ya esté registrada en este entorno; sin configuración confiable el endpoint será 403.

## Cambios propios
- Nuevo `backend/src/PMGM.Api/Modules/Bootstrap/InitialLoadWindowStatusEndpoints.cs`.
- Modificación de una línea de mapeo en `backend/src/PMGM.Api/Program.cs` (revisar solapes PR ajenos).
- Nuevo `backend/tests/PMGM.Api.Tests/InitialLoadWindowStatusEndpointsTests.cs` (pruebas sintéticas de registro JWT/issuer/privilegios/ausencia de config; no se requieren conexiones reales).
- Ajuste de compatibilidad seguro en `backend/src/PMGM.Api/Modules/Bootstrap/InitialLoadMasterIdentityPolicy.cs` y nuevas regresiones en `backend/tests/PMGM.Api.Tests/InitialLoadMasterIdentityPolicyTests.cs`: JwtBearer puede mapear el JWT `sub` a `ClaimTypes.NameIdentifier`; sólo se acepta una representación inequívoca del sujeto y siempre debe coincidir con la identidad server-side; jamás se relajan rol/emisor/scope.
- Este handoff.

## Integración
Issue #694 permanece abierto. Permisos y auditoría de futuras transiciones no están implementados. Tras 3/3 gates `PMGM CI / Showcase / QA Installable` SUCCESS del mismo HEAD, merge squash únicamente dev si compare behind=0 y no hay revisión UAT bloqueante. No tocar `main` ni desplegar glmdch físicamente. Registrar estado exacto en GitHub/Drive.
