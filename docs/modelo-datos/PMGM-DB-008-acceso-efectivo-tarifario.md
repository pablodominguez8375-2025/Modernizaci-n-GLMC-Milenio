# PMGM-DB-008 — Acceso efectivo al tarifario por decreto

Issue #266 / PR #320; base `be69bc341f99daa9e9542b776c073adab31ae651`.
Continuidad: [DB-005](PMGM-DB-005-control-acceso-dinamico.md), [DB-006](PMGM-DB-006-acceso-efectivo-tesoreria.md), [DB-007](PMGM-DB-007-tarifario-decretos.md).

## Diccionario del contrato derivado

`GET /api/tesoreria/tarifarios/decretos/acceso`, sesión autenticada con autoridad vigente `CanManageTreasuryRegularity`. Respuesta `private, no-store`, sin sujeto, asignaciones, terceros, cargos o catálogo completo.

| Campo | Tipo lógico / transporte | Nulable / default | Regla / privacidad |
|---|---|---|---|
| version | entero / JSON number | no / versión actual | Derivado del snapshot de acceso vigente; dato técnico privado |
| managed | booleano / JSON boolean | no / false | Existe cualquier asignación de ese sujeto en Orden (OrganizationId null), incluso revocada, futura o vencida; privado |
| actions | lista / JSON string[] | no / [] | Subconjunto de view/create soportadas por este corte. Intersección con autoridad institucional ya validada; privado |

La versión no es el número de versión tarifaria. `expectedVersion` del POST de decretos conserva su contrato y concurrencia anteriores.

## Estructuras y relaciones existentes

Sin migración ni transformación de filas: `core.dynamic_access_snapshots` (PK Id, versión única, payload JSONB inmutable) y su catálogo lógico de perfiles/grants/asignaciones se mantienen. Asignación → perfil por ProfileCode dentro del snapshot; sujeto exacto y alcance Orden (`OrganizationId = null`). La proyección no persiste una segunda tabla ni replica roles. `core.grand_treasury_tariff_versions` conserva columnas, PK, versión única e índice EffectiveFrom/Version, payload y auditoría según DB-007. No cambian FK, cardinalidad, índices o borrado: decretos inmutables, revocación/baja lógica conserva historia.

## Evaluación

1. El autorizador institucional vigente exige alcance Orden y Gran Tesorería o Gran Logia Admin. Un grant técnico jamás crea esos cargos.
2. Para sujetos sin ninguna asignación en Orden se conserva la autoridad existente. Una asignación de Taller no sustituye una de Orden.
3. Para sujetos administrados en Orden, exigir sujeto/ámbito exactos, asignación activa y vigente inclusivamente en fecha civil Chile, perfil custom/menú/vista activos y grant `treasury/view` más la acción requerida.
4. GET catálogo de decretos requiere view; POST registrar requiere create (y, por validación del grant, view). La proyección propia permanece consultable con acciones vacías para informar la revocación.
5. Revocar, vencer o desactivar no retorna a la autoridad legacy. API real comprueba cada petición; demo reproduce la política. Pantalla retira datos/formulario ante pérdida de acceso y refresca por cambios de catálogo, foco y cada 30 s. El servidor gobierna solicitudes entre refrescos.

## Límites de cobertura

Sólo catálogo, versiones y registro de decretos; no certificar todas las operaciones de Gran Tesorería, navegación global, regularidad, cuadros institucionales, consulta del tarifario vigente usada por otros dominios, Hospitalaria ni demás módulos. Continúan en #266. Sin impresión/exportación nueva, nueva atribución, tarifa o cambio histórico. Despliegue QA/schema instalado sin verificar; srv01/UAT pausados.
