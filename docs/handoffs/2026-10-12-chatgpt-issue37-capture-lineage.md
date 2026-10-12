# Handoff — Issue #37 — capturas complementarias versionadas de Insinuado (fase 2c)

**Proyecto Centenario / 12-10-2026.** Trabajo de ChatGPT en [PR #458](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/pull/458), rama `feature/issue37-candidate-versioned-captures-20261012-gpt`, base original `dev@15ec08613b9e0f1fada341ebe701fff37304d555`. **Confirmar siempre HEAD vivo, estado de PR y CI antes de dar por integrado.** `main@6dfb9546a4873baff15955cf86abfd7d47e3d111` estable; UAT física `srv01` suspendida por #97.

## Principio fundamental

La Ficha de Insinuado actual almacena nombres, apellidos, RUT/ID, contacto, datos privados y estado reglamentario en entidades canónicas. La nueva captura versionada **no modifica, ni duplica, ni reemplaza** esa ficha: almacena exclusivamente atributos adicionales administrados por esquema, vinculados al ID de solicitud de iniciación, al Taller y al `FormSchemaVersion.Id` inmutable. El endpoint clásico `PUT .../ficha` sigue funcionando sin exigir un esquema nuevo. **No se migran ni reescriben expedientes antiguos.**

## Entrega propuesta en PR #458

1. Entidad `CandidateFormCapture` y tabla `core.candidate_form_captures` con FK a `ceremony_requests` y `form_schema_versions`, índice `(CeremonyRequestId,CaptureRevision)` único, checksum SHA-256 sobre bytes exactos de `ValuesJson` (almacenado como `text`) y trigger PostgreSQL que bloquea UPDATE/DELETE. Capturas históricas conservan su versión/código de esquema de origen, sin reinterpretación silenciosa.
2. API autenticada bajo `/api/insinuados/solicitudes/{requestId}/formularios/capturas`:
   - `GET /schema`: entrega **solo** campos configurables visibles del esquema vigente (lodge en primer lugar, fallback order), versión, ámbito y última revisión para un futuro renderer autorizado.
   - `POST /`: recibe `{ schemaVersion, expectedCaptureRevision, values }` como **captura completa de campos adicionales**, no patch ni definición de esquema enviada por cliente. Valida contra `FormSubmissionPolicy` y núcleo institucional desde servidor, firma de versión histórica y vigencia, estado activo del expediente y permisos existentes. Sujeta escritura a advisory locks (versiones publicadas y solicitud) y UNIQUE, con 409 ante replay/stale. Guarda nueva fila y evento `AuditEvent` en una transacción.
   - `GET /`: lista únicamente metadatos de revisiones, **sin valores privados**.
   - `GET /{captureRevision}`: verifica checksum y versión exacta, audita lectura e incluye valores solo al rol ya autorizado; `Cache-Control: private, no-store`.
3. Se valida identidad del expediente contra solicitud real, pertenencia del Taller y permisos `CanManageOrganization` para escribir; Gran Secretaría solo puede consultar según `CanManageGrandSecretariat`. No se amplían atribuciones. Se bloquean escrituras si el expediente está autorizado, rechazado o publicado. El cliente no decide `scopeKey`, claves núcleo o catálogo.
4. Limita contenido a 32 KiB, prohíbe envío de campos núcleo, tipo/valor incorrecto, campos no publicados o de dependencias inválidas y referencias documentales/fotográficas no autorizadas (estas esperan flujo de vinculación S3/antivirus existente).
5. Pruebas unitarias e integración PostgreSQL/HTTP con datos sintéticos: núcleo inmutable, privacidad, anonimato y Taller externo, lectura exacta, copia de seguridad histórica, versión vencida, concurrencia, auditoría sin PII, migración y trigger contra borrado. La clase de integración está registrada en shard A.

## Archivos concretos

- `backend/src/PMGM.Api/Modules/FormConfiguration/CandidateFormCapture.cs`
- `backend/src/PMGM.Api/Modules/FormConfiguration/CandidateFormCapturePolicy.cs`
- `backend/src/PMGM.Api/Modules/FormConfiguration/CandidateFormCaptureEndpoints.cs`
- `backend/src/PMGM.Api/Data/PmgmDbContext.cs`
- `backend/src/PMGM.Api/Migrations/20261012200000_AddCandidateFormCaptures.cs`
- `backend/src/PMGM.Api/Program.cs` (registro de endpoints)
- `backend/tests/PMGM.Api.Tests/Unit/CandidateFormCapturePolicyTests.cs`
- `backend/tests/PMGM.Api.Tests/Integration/CandidateFormCaptureHttpTests.cs`
- `.github/backend-integration-shards.json` (shard A)

## Evidencia y cautelas al retomar

- Primera CI de PR #458 detectó requisito del gate de migraciones: claves foráneas con parámetros `name:,column:,principalSchema:,principalTable:,principalColumn:`; corregido.
- Siguiente CI descubrió error de compilación C# CS1763 por valor opcional `object` en prueba de integración; corregido usando `string`. **Consultar conclusión de los gates del HEAD final**; no dar por integrado hasta CI, Showcase y QA Installable SUCCESS y merge verificado.
- La implementación no incorpora editor visual, rutas públicas, escritura automática de datos núcleo, transferencias entre Talleres, importación de datos antiguos ni UAT. El runtime ya puede almacenar campos adicionales solo cuando hay esquema configurado/vigente; no hay datos de candidatos sintéticos en Pages por este incremento.
- **Privacidad/retención:** el historial inmutable contiene valores potencialmente sensibles y requiere reglas de retención/rectificación/supresión con revisión normativa Ley 21.719 antes de producción. Una política de purga legal auditada deberá conciliarse con el trigger de inmutabilidad. No tratar el trigger como sustituto del cumplimiento legal.
- **Definition of Done institucional:** paridad demo funcional con datos 100% ficticios, QA instalable SHA exacto, UAT presencial suspendida hasta Sponsor; `main` protegida. Issue #37 continúa abierta.

**Siguiente tarea sugerida:** enlazar UI administrativa responsiva y Ficha real a `GET /schema` y `POST /capturas` para los campos configurables; implementar validación e historial visibles solo a roles habilitados, pruebas 360/768/1440 y demo ficticia, en coordinación con Claude. Mantener normativa/catálogo núcleo desde servidor; no editar datos de Ficha canónica mediante JSON configurable.

**Continuidad:** leer `START-HERE.md`, GOV-003 §16, comentario Issue #37, PR #458, este handoff y diferencias de la Línea Base Maestra Drive. Tras CI/merge, registrar SHA final exacto y estado Pages/QA en GitHub y Drive.
