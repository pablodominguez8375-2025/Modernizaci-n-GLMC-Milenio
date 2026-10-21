# #533 C19 — CSV HTTP preliminar de asambleístas

20-10-2026. Base dev@b966ac6249e0eea4d4fd7aa38f13d495b41c7a67. PR draft por agente:chatgpt antes de programar; no main ni QA físico.

Objetivo limitado: GET /api/regimen-interior/asambleas/{assemblyId}/asambleistas-preliminar.csv, **solo** RI o Gran Secretaría autenticada scope order y subject; únicamente asamblea `draft`. Genera listado de asambleístas desde `AssemblyMemberStatuses` persistidos y usa política `AssemblyRosterCsvPolicy.BuildPreview`, obligando para cada fila estado pendiente, `MayVote=false`, `MayAttend=false` porque las fuentes financieras/RI/Tribunal aún no están certificadas en conjunto. No declara habilitación de voto, nunca publica padrón oficial; solo un CSV de trabajo interno. Campos numeración, apellidos/nombres, RUT, Taller, firma vacía, marca Pendiente; UTF8 BOM, anti fórmula, orden es-CL; no expone motivos o identificadores de evaluaciones. Sin filas duplicadas/inválidas (409) y máximo 10000. Respuesta privada/no-store y Content-Disposition de vista preliminar. Auditoría sin RUT/nombres.

Prohibir endpoint para usuarios de Taller, evitar que navegador modifique personId o los estados; no exportar segundo CSV preliminar de sufragantes antes de resolver fuentes oficiales. Prueba HTTP/PostgreSQL para permisos, contenido, ausencia de derecho a voto y errores sin datos.

Sin migraciones ni modificación de tablas. GOV-004 documenta contrato CSV. Continuación #533: fuentes GT históricas firmadas, membresía/VM y Tribunal, cierre transaccional y padrón real PDF/CSV UAT.
