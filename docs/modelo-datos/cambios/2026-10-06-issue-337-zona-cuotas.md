# Registro GOV004 — Zona de cuotas exclusivamente desde la Ficha

Issue #337 · PR #338 · rama feature/treasury-zone-from-lodge-20261006-gpt · base dev a0df0378fe6af4d2bc32401f01372d6303598e31. Fuente: instrucciones PO 06-10 (Doc 1UMBWl9tgX1NZknVMeFQR3E1bDMPVkq5vi7r6F7wChNQ), reglas PO 04-10 v2. Corte anterior: [DB-007](../PMGM-DB-007-tarifario-decretos.md); este registro actualiza únicamente zona/Ficha y consulta del decreto.

## Diccionario del alcance

| Campo/contrato | Tipo físico/lógico, nulabilidad y tamaño | Regla y cambio |
| --- | --- | --- |
| core.organizations.Id | uuid, PK, no nulo | Identificador del Taller; sin cambio |
| OrienteCode | varchar(40), nullable, sin default | Catálogo santiago / other_chile / peru; elegido en Ficha. Sin código legacy se deriva de ubicación; código explícito contradictorio produce zona null |
| City / Country | varchar(120), nullable, sin default | Ciudad obligatoria para zona; trim y comparación sin distinguir mayúsculas; Chile: Santiago o otra ciudad; Perú/peru: Perú; otros países no tarifados |
| TreasuryTerritory | varchar(40), nullable, sin default | Cache materializado derivado: santiago / other_oriente / peru. Guardar Ficha actualiza con auditoría existente. Consultas y cálculos usan Ficha, aunque cache esté desactualizado |
| GET profile.organization.treasuryTerritory | string nullable | Antes exponía cache; ahora deriva con la misma regla de consultas/cálculos de Tesorería |
| GET profile.quotaDecreeNumber | string nullable, máximo 80 caracteres del catálogo | Campo aditivo de solo lectura: número del decreto publicado vigente hoy en Chile. Null si no hay vigente. No expone importes ni permisos de administración |
| POST tesoreria/talleres/{id}/oriente + SetTreasuryTerritoryRequest | Contrato retirado | Antes respondía 409 sin escribir; ahora ruta GET solamente, POST retorna 405. Método cliente/demo retirado |
| GET talleres/orientes y GET taller/{id}/oriente | Contratos de lectura existentes | Conservan nombres/campos y autorización; Ficha incoherente devuelve zona null; UI dirige a Ficha con permisos existentes |

Clasificación: metadatos institucionales de Ficha y tarifario, no datos personales de miembros. Sólo ejemplos sintéticos. Sin nuevas tablas, columnas, PK/FK, cardinalidades, índices, restricciones o cascadas; no corresponde un ER nuevo. Relaciones con planes/cargos y versiones tarifarias preservadas según DB-007. Pagos, monedas CLP/USD, sobrepago local, cargos y snapshots históricos no se reescriben. No cambia retención ni permisos; enlace no concede edición al Gran Tesorero.

## Consistencia y recuperación

**Sin migración nueva:** no cambia schema; la migración 20261004205000 ya normaliza ubicación/Oriente y deriva el cache. Se entrega [verificador SQL de solo lectura](../sql/2026-10-06-verificar-zonas-cuotas.sql) para todos los Talleres, con ID, zona almacenada, zona recalculada y corrección sugerida. Identifica tanto diferencias como Fichas incompletas/incoherentes. No modifica datos: guardar Ficha es la única corrección operacional, auditada por organization.workshop_profile.metadata_updated (anterior/nuevo).

No se consultó la base institucional (srv01 pausado); no hay listado acreditado de Talleres reales discrepantes ni afirmación de schema instalado. Caso sintético probado: Ficha Santiago con cache Perú; consulta/Ficha devuelven Santiago, POST no altera cache y la edición auditada de Ficha lo reconcilia. Inconsistencias code Perú + ubicación Santiago y other_chile + Santiago quedan sin zona. Reversión técnica: revertir PR, sin cambios de datos que restaurar; el contrato retirado sólo rechazaba escrituras.

## Validación y corte

Prueba HTTP PostgreSQL: POST retirado para las tres zonas, cache desactualizado ignorado, misma fuente en Ficha/consulta, cambios Santiago/Regiones/Perú auditados. Pruebas de derivación incluyen incompletos/incoherentes/legacy y países no tarifados. Demo prueba paridad Ficha/consulta/tarifario y CLP/USD. Build, lint, suite frontend y gates estructurales; CI backend, Showcase y QA Installable por SHA exacto se acreditan en recibo persistente de PR #338 y handoff. Este registro no acredita QA física/UAT ni despliegue institucional.
