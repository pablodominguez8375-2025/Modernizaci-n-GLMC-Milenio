# GOV-004 — #533 C19: contrato CSV de asambleístas preliminares

- Autor ChatGPT, fecha 20-10-2026. PR #615; base dev@b966ac6249e0eea4d4fd7aa38f13d495b41c7a67. Estado rama hasta readback de CI y merge. Referencia previa `docs/handoffs/2026-10-20-chatgpt-assembly-roster-safe-csv-c18.md`.
- Entidades usadas: `core.assemblies`, `core.assembly_member_statuses` del contexto `RegimenInteriorDbContext` (nombres lógicos; validar tabla física en migración `20261019180000_AddAssemblyRosterFoundations`). No se alteran campos, claves, índices, FK ni restricciones existentes; sin migración.
- Nuevo contrato HTTP: GET `/api/regimen-interior/asambleas/{assemblyId}/asambleistas-preliminar.csv`, authentication RI/Gran Secretaría, scope order, subject no vacío. Origen: `AssemblyRecord` en `draft` sin evidencia de cierre + `AssemblyMemberStatusRecord` persistidos y verificados. No contiene solicitudes ni selectores de participantes en body o query. Sin lista de sufragantes, sin certificación, sin autorización de papeletas ni cierre.
- Salida `text/csv; charset=utf-8`, UTF-8 BOM, orden es-CL y mitigación de fórmulas. Nombre `asambleistas-preliminar-{assemblyId}.csv`; `Cache-Control: private,no-store` y `X-PMGM-Official-Roster:false`, `X-PMGM-May-Authorize-Suffrage:false`.
- Diccionario de columnas de archivo:

| Columna CSV | Fuente persistida/tipo lógico | Obligatoria y regla | Privacidad |
|---|---|---|---|
| N° | calculado integer 1..N | orden global es-CL (tildes ignoradas, Ñ preservada), desempate RUT | interno |
| Apellidos y nombres | `LastNamesAtCutoff` + `FirstNamesAtCutoff`; string | no blancos, anti fórmula, saltos de línea neutralizados | datos personales |
| RUT | `RutAtCutoff`; string | no blanco; CSV encapsulado seguro; no publicar fuera rol autorizado | identificador personal |
| Taller | `WorkshopAtCutoff`; string | no blanco; sin datos de miembros externos al listado | interno |
| Firma | literal vacío; string | nunca reproduce firmas almacenadas | interno |
| Estado de sufragio | literal `Pendiente`; string | todos pendientes, no depende de `MayVote` histórico/preliminar ni afirma sufragio | interno |

- Validación/errores: 403 sin autoridad; 404 Asamblea desconocida; 409 si no draft/metadata cerrada, vacío, más de 10.000 filas, source office no acreditada, datos incompletos o duplicados. `BuildPreview` rechaza RUT/nombre/taller vacíos, persona duplicada y contradicciones. No se exponen motivos de inhabilidad, saldos, fuentes financieras reservadas, ni voto.
- Antes: política C18 exportaba bytearrays sintéticos exclusivamente desde función interna sin endpoint real. Después: solamente vista CSV **preliminar** de asambleístas en API, autenticada, fuente PostgreSQL, con auditoría sin RUT/nombres en `PmgmDbContext`.
- Relaciones físicas: unchanged `AssemblyRecord.Id` 1:N `AssemblyMemberStatusRecord.AssemblyId`; usar referencia exacta de migraciones. Sin cambios de FK y cardinalidad; no diagrama ER nuevo.
- Clasificación: RUT/nombres = datos personales sensibles por contexto institucional; minimizar y no almacenar CSV; tránsito privado `no-store`. Auditar sólo assemblyId, estado y conteo. Retención: políticas existentes, sin cambios.
- Pruebas PostgreSQL: autor RI, contenido BOM/firma vacía/marca pendiente, 403 Taller, 409 no draft. CI/Showcase/Installable SHA **pendiente**.
- Instalación QA físico, UAT y certificación oficial Gran Tesorería/Tribunal/RI: **no verificados**. `main` no se modifica.
