# Proyecto Centenario — Issue #65: plancha numerada con evidencia verificable

**Corte:** 11-10-2026, America/Santiago. **PR:** [#450](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/pull/450) desde `dev@d0cfc902cd49d92723d681d3812c86042a8a21e2`. **Ramas protegidas:** `main@6dfb9546a4873baff15955cf86abfd7d47e3d111` sin cambios, `srv01` físico pausado (#97).

## Fuentes normativas y flujo ya existente

- `docs/PMGM-REQ-001-flujos-institucionales-2026.md` 001.17–001.21: aprobaciones institucionales previas, plancha numerada, acto posterior.
- `docs/requisitos/PMGM-REQ-022-gran-secretaria-documentos-autorizaciones-espacios.md` §4.3: correlativo, datos de Taller y ceremonia, referencias a vistos buenos, trazabilidad.
- `docs/PMGM-ADR-002-expediente-y-snapshot-autorizacion.md`: una plancha remite a la decisión histórica congelada, no recalcula regularidad actual.
- `docs/PMGM-GOV-003-coordinacion-multi-ia.md`: issue #65 etiqueta agente:chatgpt, PR draft previo a código. No PR ajeno funcional abierto al reservar.

La ruta `POST /api/gran-secretaria/ceremonias/{requestId}/autorizacion` ya existía y exigía `CeremonyRequest.Status == authorized`; también revisaba plancha vigente, pero esta comprobación era vulnerable a dos solicitudes concurrentes. El `DocumentCode` anterior terminaba en 8 caracteres aleatorios, no era numeración correlativa. El contenido no guardaba referencia directa ni hash del acto que autorizó.

## Entrega técnica del PR #450

1. `backend/src/PMGM.Api/Modules/GrandSecretariat/CeremonyAuthorizationPlanchaPolicy.cs`: numeración `PLA-AUT-CER-AAAA-NNNNNN` por año **local de Chile**, empezando en 000001. Revisa sólo correlativos numéricos de 6 dígitos, preserva sin alterar códigos aleatorios históricos y detecta agotamiento del rango. Genera texto institucional con número, expediente, Taller, ceremonia, fecha/lugar y referencia de auditoría de autorización junto con hash SHA-256 de su metadata.
2. `GrandSecretariatEndpoints.cs`: exige rol Gran Secretaría o admin con ámbito Orden y estado `authorized`. La emisión ahora **exige evento de auditoría** `ceremony.authorization.approved` con identificación de expediente y Taller y metadatos de autorización. Si falta evidencia, responde 409 sin documento ni correlativo consumido.
3. Una transacción de `GrandSecretariatDbContext` toma **un único `pg_advisory_xact_lock` global para todas las emisiones** (incluidas las realizadas a ambos lados de la medianoche de Año Nuevo), **antes** de consultar la existencia de una plancha vigente y el último correlativo anual. Dos llamadas concurrentes —para el mismo expediente o para otros— se serializan; el índice único en `DocumentCode` sigue activo. Escritura documental y evento `grand_secretariat.ceremony_authorization.issued` se confirman en la misma transacción. A la auditoría de emisión se agregan id de autorización original y SHA-256 de snapshot. Reserva vinculada al expediente sigue siendo opcional, pero se valida si se indica.
4. `backend/src/PMGM.Api/Modules/Ceremonies/CeremonyEndpoints.cs`: el evento existente `ceremony.authorization.approved` incorpora IDs y estados de Régimen Interior, Gran Maestría, Gran Tesorería y Gran Hospitalaria, con saldo/valor de derecho de ceremonia; mantiene historial de publicación/ascenso ya registrado. La plancha toma esta evidencia **histórica**, sin asumir que el estado financiero actual represente la decisión del pasado.
5. Pruebas: `backend/tests/PMGM.Api.Tests/GrandSecretariat/CeremonyAuthorizationPlanchaPolicyTests.cs` cubre correlativo, reinicio anual, compatibilidad con GUID histórico, agotamiento, huella y representación. Se amplía `backend/tests/PMGM.Api.Tests/Integration/GrandSecretariatHttpWorkflowTests.cs` con estado autorizado + audit snapshot, emisión de plancha, bloqueo de duplicado, falta de evidencia y dos peticiones concurrentes con exactamente un éxito; plancha de segundo expediente recibe número consecutivo, registros y auditoría preservados. La clase HTTP ya estaba en un shard de integración: no necesita nueva entrada en `.github/backend-integration-shards.json`.

## Restricciones y deuda controlada

- **No se automatiza firma ni sello**; la plancha textual no equivale a original firmado. Para emitir el documento oficial listo para impresión y firma institucional será necesaria la plantilla personalizada/vigente de la GLMCh, su aprobación formal y el proceso de archivo PDF/Word en Gestor Documental. No afirmar que este PR implementa esos pasos.
- La nomenclatura `PLA-AUT-CER-AAAA-NNNNNN` es convención técnica inicial; REQ-022 contempla nomenclatura parametrizable, pendiente de definición institucional. Los códigos aleatorios antiguos permanecen como legado y no se renumeran.
- Expedientes legados con estado `authorized` pero sin evento de auditoría `ceremony.authorization.approved` no son emitibles sin regularización documental **por autoridad competente**; no se genera evidencia retroactiva falsa.
- No se admite una plancha desde `POST /api/gran-secretaria/documentos` general. Gran Secretaría emite desde el expediente, no permite eludir las compuertas de autorización.
- El estado técnico de PR/Gates debe actualizarse con SHA exacto de CI, Showcase, instalable QA y commit fusionado **si procede**, luego registrar issue #65 y Drive. No prometer QA física, UAT operativa ni Pages publicadas sólo por éxito de Actions. **#65 permanece abierto** hasta matriz E2E y aceptación institucional.

## Reanudación

Revisar SHA vivo dev, PR #450 y sus gates, handoff en este archivo y estado ejecutivo Drive `1rqG3YAi_csweWyxT4362Wzf3guJRdzy3foqn-Pvizf0`. Al integrar, crear PR documental de índice en START-HERE conforme PMGM-GOV-003 (sin tocarlo en este PR); revisión de plantilla/firma pendiente.
