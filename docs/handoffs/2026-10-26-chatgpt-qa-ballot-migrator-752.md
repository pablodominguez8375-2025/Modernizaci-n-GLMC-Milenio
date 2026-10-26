# #752 — Instalador BD2: migraciones de votaciones anónimas de Tenidas

Fecha: 2026-10-26. Base `dev@301a576c6d75187aeeb71901d9c276bf7b4e334c`, `main@6dfb9546a4873baff15955cf86abfd7d47e3d111` (intacta). PR draft #753, responsable ChatGPT. El Sponsor informó que Libertad Nº 23 es un Taller inicial cuya Ficha fue editada; preservarlo, sin crear duplicados ni cambiar identificadores. CSV Soporte QA aportó cuatro 500 en GET /api/gestion-logial/tenidas/{meetingId}/votaciones, sin 500 de triple GET Supervisión en ese extracto. CSV original e IDs de correlación no se publican.

## Cambios reservados y realizados
1. `backend/src/PMGM.Api/Program.cs`: migrador DDL BD2 ejecuta `LodgeManagementDbContext.MigrateAsync()` justo después de `PmgmDbContext.MigrateAsync()` y antes de Bootstrap, preservando los otros nueve contextos y la separación API DML/migrador. Estas migraciones (ya existentes) crean `core.lodge_anonymous_ballots` y añaden `ProcedureNumber`; no se crea migración adicional ni se altera dominio/roles.
2. `backend/tests/PMGM.Api.Tests/Integration/LodgeBallotMigrationHttpTests.cs`: regresión real PostgreSQL, reproduce la secuencia de migraciones, comprueba ambas y reejecución idempotente, GET autorizado sin filas 200 + no-store + items vacíos, inserta votación **sintética** y comprueba readback, perfil Tesorero 403. Se omite si no se dispone de PMGM_TEST_POSTGRES.

## Evidencia / límites
Fuente Issue #752, incidente #681, código y 61 eventos agregados CSV. **Hipótesis de causa física:** migraciones omitidas, falta tabla/columna o permiso SELECT; no afirmar confirmación hasta inspección real del QA runtime/DDL y logs privados. Especial cuidado: si tabla ya existe creada fuera del historial EF, el migrador puede fallar por colisión; comprobar estado físico previo al procedimiento de actualización y no aplicar DDL manual. Backup verificado, plan rollback y rol migrador; nunca API DML con permisos DDL.

## Verificaciones pendientes
- GitHub CI, Showcase y QA Instalador exact-head PR; check de regresión PostgreSQL no skip.
- Comparar `dev` vivo antes de squash; merge solo a dev después de gates requeridos.
- QA físico **NO desplegado**: comprobar tabla/columna/privilegios en read-only y historial EF; despliegue con autorización/protocolo y readback de GET; conservar Master/SMTP y `main`.
- Cierre #752 sólo tras QA físico y UAT; #681 sigue su diagnóstico aparte.

Enlaces: https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/issues/752 y https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/pull/753 .
