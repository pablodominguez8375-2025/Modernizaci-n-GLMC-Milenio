# PMGM-DB-004 — Diccionario y estructuras: índice y registro de salida

**Estado:** procedimiento de control solicitado por el Sponsor en #259; no certificación de diccionario físico exhaustivo.  
**Regla obligatoria:** [PMGM-GOV-004](../PMGM-GOV-004-registro-cambios-datos.md).

## Fuentes existentes y su alcance

| Fuente | Alcance y uso |
|---|---|
| [PMGM-DB-001](PMGM-DB-001-modelo-conceptual.md) | Modelo conceptual inicial, estado propuesta base; no confundir sus nombres/campos con el schema actual. Terminología/normativa posterior prevalece. |
| [PMGM-DB-002](PMGM-DB-002-transferencias-miembros.md) | Modelo funcional de transferencias aprobado; contrastar con entidades/configuración/migraciones vigentes. |
| [PMGM-DB-003](PMGM-DB-003-gran-archivo.md) | Propuesta conceptual Gran Archivo; distinguir lo implementado de lo sólo propuesto. |
| backend/src/PMGM.Api/Data/ y módulos/*DbContext.cs | Mapeos EF del código del corte: tablas, relaciones, restricciones e índices configurados. |
| backend/src/PMGM.Api/Modules/*/Entities/ | Propiedades del modelo; no asumir que cada propiedad es columna ni inferir tipos SQL desde C# solamente. |
| backend/src/PMGM.Api/Migrations/ | Historia de schema y SQL versionado; revisar Up/Down y transformaciones. No ejecutar contra srv01. |
| frontend/src/api/ y endpoints backend | Contratos, campos derivados/calculados y demo sintética, que también deben documentarse aunque no cambie schema. |
| docs/seguridad/PMGM-DATA-CLASSIFICATION-* | Clasificación y finalidad vigentes: reutilizar, no inventar categorías/reglas. |

## Cómo dejar la salida de cada modificación

1. Copiar [la plantilla](plantillas/registro-cambio-datos.md) a `docs/modelo-datos/cambios/AAAA-MM-DD-issue-N-tema.md`.
2. Completar datos reales del cambio; eliminar las instrucciones de plantilla. Actualizar el diccionario del módulo afectado en ese registro o enlazar el documento actualizado del mismo PR.
3. Incluir todas las propiedades/relaciones vigentes del alcance declarado, identificando las añadidas, alteradas o retiradas. Si la cobertura es parcial, declararla y dejar su pendiente; no titularla diccionario completo.
4. Vincular registro previo y conservarlo. “Desconocido/no verificado” nunca se reemplaza por una suposición; resolver los faltantes materiales antes de declarar el alcance terminado.
5. Revisar antes/después, migraciones, integridad, privacidad y compatibilidad con código/contratos/pruebas. Acompañar el diagrama con PK/FK/cardinalidades verificadas.
6. Registrar el SHA exacto en el recibo de cierre y en cualquier exportación. El documento versionado queda asociado al commit que lo contiene; no incrustar su propio hash antes de existir. Un archivo exportado debe identificar `SOURCE_SHA`, ruta/origen y checksum.
7. Enlazar salida y resultado en GitHub, Línea Base Maestra y handoff, con lectura de retorno. Todo artefacto incluido en QA debe pertenecer al mismo corte y su MANIFEST; no reutilizar evidencias de otro SHA.

## Inicio de este control

#259 / PR #260 establece regla, índice, plantilla y checklist. **Sin cambios de modelo, schema, contratos o datos operacionales**; no agrega una migración ni un dump. Este inicio no reconstruye retrospectivamente un diccionario completo del proyecto. Los próximos cambios de datos deben dejar su salida; una consolidación completa inicial requiere inspección específica de todos los dominios y cobertura declarada.

El uso de “dato” comprende campos persistidos, contratos y proyecciones/calculados. La modificación de un pago o una ficha individual sigue su auditoría funcional, sin producir una nueva versión del diccionario.

- 04-10, #190 / PR #318: [registro tarifario v2](cambios/2026-10-04-issue-190-tarifario.md), [DB-007](PMGM-DB-007-tarifario-decretos.md). Catálogo persistido y Ficha; implementación de rama, sin instalación.
