# #533 C23 — persistencia de candidatos acreditados (fase interna)
Fecha 23-10-2026. Sponsor: continuar desarrollo. dev de origen 204c6c028f53cc3b9bff4138ec93053d69c0494a.

## Reclamo para coordinación multi-IA
ChatGPT construye una unidad de persistencia PostgreSQL transaccional de candidatos *ya preparados* por C22, sin endpoints públicos ni cambios a archivos de Claude. Incluye guardas del corte/estado de Asamblea, identidad de rol Régimen Interior, duplicados, idempotencia por negativa a reimportación, y filas iniciales de habilitación `pendiente`, `MayVote=false`, `MayAttend=false`. Auditoría insertada junto a las filas en la misma transacción. Pruebas PostgreSQL con identidades ficticias. Ninguna modificación a migraciones.

Archivos planificados:
- `backend/src/PMGM.Api/Modules/RegimenInterior/AssemblyAccreditedRosterPersistenceService.cs` (nuevo)
- `backend/tests/PMGM.Api.Tests/Integration/AssemblyAccreditedRosterPersistencePostgreSqlTests.cs` (nuevo)
- este handoff (nuevo)
- `docs/modelo-datos/registros/2026-10-23-chatgpt-assembly-accredited-roster-persistence-c23.md` (nuevo)

## Límites y dependencias
NO se crea conector de constancias, no se reconoce como verificada una solicitud HTTP, no hay evaluación de voto ni padrón oficial. El servicio no está publicado como endpoint ni registrado para consumo externo: sólo futura orquestación **servidor confiable** con fuentes Gran Secretaría/VM, membresía, GT, RI y Tribunal podrá llamarlo. No autorizar cierre ni exportación. C22 de #664 integrada a dev; #665 de permisos pasó 3/3 pero el conector bloqueó merge, registrado en #523, no tocar su rama. PR nuevo a dev + 3/3 exact head; main y QA físico intactos.

## Implementación entregada
Nueva unidad interna PostgreSQL con comprobación de rol RI, Asamblea borrador/corte, idempotencia por rechazo a reimportación, inserción transaccional de candidatos con un historial inicial de evaluación pendiente, auditoría transaccional minimizada y pruebas PostgreSQL. No hay ruta HTTP pública ni cambios de fuente institucional. PR #666 requiere 3/3 CI exact-head antes de fusionar.
