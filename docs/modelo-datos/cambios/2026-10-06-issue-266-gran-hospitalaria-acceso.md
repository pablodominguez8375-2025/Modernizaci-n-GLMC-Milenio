# Registro de cambio — #266 / #348 — Gran Hospitalaria

Fuente: alcance PO #266 y continuación 06-10. Base dev `8c092e2e37aaa5b8532b3f127c0e26a25ba2fa70`; rama `feature/grand-hospitalaria-access-20261006-gpt`. Complementa [DB-009](../PMGM-DB-009-acceso-efectivo-hospitalaria.md) y DB-005; no altera cifras, tarifa, cálculo de reposiciones, cargos ni competencias. Estado en rama, no instalado; SHA de entrega en recibo PR.

## Diccionario del contrato derivado

| Contrato/campo | Tipo JSON / nulabilidad | Regla y finalidad | Clasificación |
|---|---|---|---|
| GET /api/hospitalaria/acceso: version | entero no nulo | Versión del snapshot del catálogo existente, default 0 | interno, acceso propio privado |
| managed | boolean no nulo | Existe asignación histórica al sujeto exacto con OrganizationId nulo, incluso revocada/no vigente | interno, no incluye identidad ni asignaciones |
| actions | array de cadenas no nulo, vacío permitido | view/create/write; intersección técnica, endpoint sólo autorizado a gestores institucionales | interno, private/no-store |
| Assignments.OrganizationId (existente) | UUID nullable | null = Orden; UUID = Taller. No se mezclan ámbitos | restringido; permanece en snapshot JSONB versionado |
| Assignments.Subject/IsActive/EffectiveFrom/EffectiveTo (existentes) | cadena/bool/fecha/fecha nullable | sujeto exacto, activo, vigencia civil Chile inclusiva | restringido; no se devuelve en proyección propia |

## Estructuras y relaciones

Sin migración: reutiliza `core.dynamic_access_snapshots`, sus PK/índice único Version, JSONB y auditoría de administración de DB-005. No cambia FK, cardinalidades, unicidad, bajas lógicas, retención ni tablas Hospitalaria. `GrandHospitalariaAccessDto` es sólo proyección no persistida. Asignación vincula lógicamente sujeto/perfil/ámbito como antes; no hay relación nueva ni backfill.

## Antes y después

| Operación | Antes | Después |
|---|---|---|
| Tarifa/casos/rendiciones | autoridad institucional | autoridad + view hospitalaria Orden para sujeto administrado |
| Crear tarifa/sincronizar defunciones | autoridad institucional | autoridad + create; sincronización sigue explícita confirmada |
| Revisar transferencia/rendición; regularidad manual | autoridad institucional | autoridad + write |
| GET regularidad por Taller | detalle si gestor, mínima a demás lectores institucionales | detalle gestor restringido por view Orden; lectores mínimos conservados |
| Revocación/expiración | no restringía este módulo | sujeto administrado sigue managed, cero acciones, sin retorno legacy |
| Pantalla | controles por cargo; respuestas sin aislamiento Gran Hospitalaria | controles por capacidades; resultados ocultos por sujeto/Taller/período/versión, generación y cliente |

Compatibilidad: sujetos sin historia de asignación Orden conservan autoridad previa; el grant nunca concede cargo. Perfil sólo Taller no autoriza operaciones de Orden. Impresión/exportación y navegación global permanecen fuera de este corte. UI regularidad editable montada sólo con write y remonta al cambiar contexto. API real y demo aplican mismo contrato/grants; demo comprueba autoridad sintética para operaciones de Orden.

Auditoría operacional anterior preservada; denegación antes de consultas/mutaciones, sin nuevos datos ni pérdidas. Consulta no ejecuta sincronización. HTTP PostgreSQL comprueba consultas sin efectos, bloqueos, revisión válida/auditoría y revocación; unitarias cubren vigencia/perfil/ámbito. Frontend prueba API directa y controles. SDK .NET no disponible localmente: backend depende de CI exact-head. QA/UAT pausados #97, despliegue QA pendiente; no certificar schema físico ni aceptación.
