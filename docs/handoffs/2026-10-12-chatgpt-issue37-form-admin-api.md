# Handoff — Issue #37 — API administrativa de formularios (fase 2b)

**Corte 12-10-2026 (Chile), integración funcional:** [PR #456](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/pull/456) fusionada por squash a `dev@af736027b1005feb5c2bff7dce4f3eca3641bdf2` desde HEAD exacto `8bbee8d0d94f4c878190cd077c5ccfc258ab3223`. La rama `main@6dfb9546a4873baff15955cf86abfd7d47e3d111` permanece estable y sin promoción. Issue [#37](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/issues/37) sigue **OPEN**; `srv01` y UAT física suspendidos por [#97](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/issues/97).

## Entregado

1. **API real, sin UI todavía** `/api/system/forms`, integrada en `Program.cs` y DI `FormSchemaVersionStore`:
   - `GET /catalog`: catálogo de formularios núcleo institucionales soportados.
   - `GET /{formCode}/versions?organizationId=<uuid-opcional>`: versiones del ámbito solicitado.
   - `GET /{formCode}/versions/{version}?organizationId=<uuid-opcional>`: recuperar definición exacta de una versión histórica.
   - `POST /{formCode}/versions?organizationId=<uuid-opcional>`: publicar con body `{ version, expectedPreviousVersion, effectiveFromUtc, fields }`.
2. **Acceso restrictivo:** `RequireAuthorization` + `IInstitutionalAccessService.CanConfigureSystem` vigente; solo Superadministración o Administración de Gran Logia con ámbito `order`. Se exige sujeto OIDC para publicar. No hay ampliación de atribuciones a Secretaría/Taller ni modificación de la política de acceso actual.
3. **Catálogo confiable del servidor:** únicamente `candidate_profile` (Ficha privada de Insinuado) en esta fase, con claves núcleo `candidate_id`, `organization_id`, `first_names`, `paternal_surname`, `maternal_surname`, `rut_or_institutional_id`, `process_status`. El cliente NO define claves núcleo, su clasificación ni el ámbito de seguridad como cadena libre.
4. **Privacidad y no regresión:** no ocultar/desactivar/degradar núcleo; campos de expediente no pueden ser `public`; foto/documento requieren `restricted` y correo/teléfono `sensitive`; se rechazan campos nulos, cambio silencioso de tipo, supresión de campos históricos y vigencias retroactivas.
5. **Concurrencia/auditoría:** `ExpectedPreviousVersion` se verifica en PostgreSQL **después** de adquirir advisory lock por ámbito/código, con `409 Conflict` para escritor obsoleto; la publicación exitosa conserva `FormSchemaVersion` inmutable y su evento `AuditEvent` atómico. GET privado `Cache-Control: private, no-store`. Taller inexistente se rechaza.
6. **Pruebas .NET**: unitarias de campos protegidos/privacidad/formulario desconocido/campos nulos; HTTP PostgreSQL de catálogo, creación/lectura exacta, ámbito de Taller, auditoría, replay 409, autorizaciones 401/403, backdating y privacidad. Nueva clase registrada exactamente una vez en shard A.

## Archivos modificados en PR #456

- `backend/src/PMGM.Api/Modules/FormConfiguration/FormSchemaInstitutionalCatalog.cs`
- `backend/src/PMGM.Api/Modules/FormConfiguration/FormSchemaAdministrationEndpoints.cs`
- `backend/src/PMGM.Api/Modules/FormConfiguration/FormSchemaVersionStore.cs`
- `backend/src/PMGM.Api/Program.cs`
- `backend/tests/PMGM.Api.Tests/Integration/FormSchemaAdministrationHttpTests.cs`
- `backend/tests/PMGM.Api.Tests/Unit/FormSchemaInstitutionalCatalogTests.cs`
- `.github/backend-integration-shards.json`.

**Sin migración nueva:** reutiliza la tabla, audit log y trigger de la PR #454. No modifica `FormSchemaPolicy` ni `FormSubmissionPolicy` ni archivos de UI de Claude.

## Verificaciones y estados exactos

- HEAD PR #456 `8bbee8d0d94f4c878190cd077c5ccfc258ab3223`:
  - **PMGM CI #38280661301 SUCCESS** (unit, frontend, HTTPS/OIDC, smoke y ambas baterías PostgreSQL).
  - **Showcase #38280661231 SUCCESS**.
  - **QA Installable #38280661270 SUCCESS**.
- Merge a `dev@af736027b1005feb5c2bff7dce4f3eca3641bdf2` comprobado por lectura de rama/PR.
- Gates **postmerge** del SHA integrado estaban en ejecución al registrar: CI #38281278009, Showcase #38281278079, QA #38281277939, Pre-UAT #38281277916 y poda #38281278039. Consultar conclusiones de estos runs.
- **No** se comprobó todavía `downloads/qa-current.json.sourceSha` desde Pages; no afirmar que la demo pública ni enlace QA se actualizaron al nuevo corte. Instalable CI no equivale a despliegue físico ni a UAT institucional.
- Se registró seguimiento en Issue #37 y Línea Base Maestra de Drive.

## Siguiente avance — Issue #37 aún abierta

1. **Fase 2c — captura vinculada a versión:** conservar `formCode + organizationId + schemaVersion` en la entidad institucional correspondiente; validar valores mediante `FormSubmissionPolicy` contra esquema obtenido exclusivamente de la API/almacenamiento servidor, sin aceptar definición entregada por el cliente; límites de privacidad y auditoría.
2. **Fase 3 — editor/renderer responsive:** administración de campos no núcleo, condiciones, perfiles y etapas desde menú Sistema; integrar la Ficha del Insinuado real y actualizar demo sintética con pruebas móviles 360/768/1440 y no regresión. Coordinar interfaz con Claude conforme GOV-003.
3. **Fase 4 — informes/exportación y UAT:** respetar clasificación y versiones históricas, aceptación normativa institucional y Ley 21.719; `srv01` requiere autorización Sponsor y `main` no debe promoverse antes del piloto.
4. **Decisión pendiente de PO/Normativa:** política específica para administración de formularios a nivel de Taller (por ahora **denegada**); `CanConfigureSystem` se mantuvo sin ampliaciones.

**Arranque multi-IA:** leer HEAD actual `dev`, `START-HERE.md`, GOV-003 §16, Issues/PR vigentes, Línea Base Drive por diferencias, y reservar archivos de siguiente incremento antes de codificar. Este handoff es de continuidad, no reemplaza el estado vivo.
