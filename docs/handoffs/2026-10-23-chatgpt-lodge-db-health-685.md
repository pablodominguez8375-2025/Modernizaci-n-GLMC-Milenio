# Handoff 2026-10-23 — Soporte / diagnóstico read-only del 500 (issue #685)

## Situación y continuidad
Sponsor informa HTTP 500 al SELECCIONAR Taller Libertad 23, antes de cualquier POST. Gestión Logial consulta GET de tenidas, miembros e instrucciones. PR #682 (tests triple GET) 3/3 SUCCESS pero su integración fue bloqueada por controles de herramientas, no se intentó evadir. PR #684 de consola HTTP Soporte integrada en `dev@ffe5d66ba57ca1000501c7d73fc46e3268407456`, aún sin despliegue físico a QA según #525. QA físico histórico API+WEB `33c805ec260e7cb80413647cf094067438711a7d`. `main@6dfb9546a4873baff15955cf86abfd7d47e3d111` protegido.
Este PR agrega **diagnóstico estructural y de permisos en solo lectura**: no repara sin evidencia ni sustituye logs privados, no cambia migraciones, esquema, datos, Silo ni secretos.

## Alcance exclusivo
- `backend/src/PMGM.Api/Modules/Audit/SupportLodgeDataHealthEndpoints.cs`: GET `/api/system/support/lodge-data-health`, solo `CanConfigureSystem`, metadatos PostgreSQL sin filas, UUID, IDs de personas, nombres, correo, contraseñas ni consultas arbitrarias.
- `backend/src/PMGM.Api/Program.cs`: registrar ruta, sin cambiar middleware.
- `backend/tests/PMGM.Api.Tests/Integration/SupportLodgeDataHealthHttpTests.cs`: 403/200 con roles de prueba y PostgreSQL, sin datos reales.
- `.github/backend-integration-shards.json`: registra la clase HTTP de integración en el shard A; la CI rechaza pruebas sin asignar.
- Este handoff nuevo.

## Revisión inicial del código
`LodgeManagementEndpoints.GetMeetingsAsync` usa `LodgeManagementDbContext.LodgeMeetings`; `LodgeInstructionEndpoints` usa `LodgeManagementDbContext.LodgeInstructionSessions`; `GetMemberOptionsAsync` usa `PmgmDbContext.Memberships` y relaciones con `Members/People`. Migraciones `20260908101500_AddLodgeManagement` y `20260909011000_AddLodgeInstruction` pertenecen a `PmgmDbContext`; su ejecución física y privilegios no están verificados. No confundir migraciones adicionales `LodgeManagementDbContext` inactivas con causa demostrada del GET.

## Verificación y pendientes
1. Esperar gates 3/3 SUCCESS exact SHA antes de squash únicamente dev, y comprobar dev vivo behind=0.
2. Desplegar de modo autorizado QA glmdch (no desde esta PR) y probar endpoint con cuenta de administrador, verificando 403 a Taller.
3. Comparar `present / schemaUsage / canSelect / requiredColumnsPresent` de cada una de las 5 tablas. Diagnosticar solo con flags; si falla conexión, endpoint devuelve 503, nunca falso éxito.
4. Consultar logs reales *en canal privado* con correlationId de consola Soporte para distinguir GET que falla; no publicar stacktrace ni PII. Después preparar corrección real (DDL/grant bajo backup o bug backend) y UAT.
5. GitHub issue #685 / #681, Drive Estado Ejecutivo; no promoción de main, no utilizar PC Sponsor.

No se ha accedido a glmdch ni instalado o alterado registros por este PR.
