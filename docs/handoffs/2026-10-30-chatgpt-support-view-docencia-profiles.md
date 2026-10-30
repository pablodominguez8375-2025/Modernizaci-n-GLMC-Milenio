# #574 — «Ver cómo»: catálogo completo de Docencia y Gran Hospitalaria — 2026-10-30

## Incidencia comunicada por el Sponsor

Después de la actualización de QA glmdch a `dev@725bc31953629d87f01af77db1fddf5e05b1c26a`, los perfiles «Gran Primer Vigilante» y «Gran Segundo Vigilante» no aparecen en el selector «Ver cómo». Auditoría del mismo commit: la API `SupportViewProfiles.All` y el catálogo fallback `SUPPORT_VIEW_PROFILES` no incluyen seis cargos que sí existen en `demoProfiles.ts` y en los roles institucionales.

## Alcance de la corrección

- Issue #574, rama `fix/support-view-institutional-profiles-20261030-gpt` desde `dev@725bc31953629d87f01af77db1fddf5e05b1c26a`.
- Agregar únicamente los cargos omitidos al catálogo API y a la copia fallback en frontend, vinculando cada uno con su rol existente y la clave de demostración ya implementada:
  - Taller: `inmediato-ex-venerable` → `TallerInmediatoExVenerable` → `lodgePastMaster` (Maestros).
  - Gran Logia: `gran-segundo-vigilante` → `GranSegundoVigilante` → `grandSecondWarden` (Aprendices).
  - Gran Logia: `gran-primer-vigilante` → `GranPrimerVigilante` → `grandFirstWarden` (Compañeros).
  - Gran Logia: `inmediato-ex-gran-maestro` → `InmediatoExGranMaestro` → `immediatePastGrandMaster` (Maestros).
  - Gran Logia: `jefatura-docencia` → `JefaturaDepartamentoDocencia` → `instructionDepartmentHead` (tres grados).
  - Gran Logia: `gran-hospitalaria` → `GranHospitalaria` → `hospitalaria`.
- Añadir pruebas de igualdad de catálogo/API roles, ámbito de Taller 00/Orden, ausencia de roles técnicos privilegiados y denegación de funciones no propias; conservar sesión de 30 minutos, solo lectura, auditoría y bloqueo de descargas.
- Autoridad: `docs/PMGM-ARCH-007-perfiles-taller-responsabilidades-vistas.md`, `docs/PMGM-ARCH-018-docencia-de-la-orden-lectura.md`, `docs/qa/PMGM-QA-V072-DOCENCIA-ORDEN.md`. No crear roles nuevos, ampliar funciones ni cambiar reglas de grado.
- Archivos: `backend/src/PMGM.Api/Modules/Authorization/SupportViewProfiles.cs`, su test; `frontend/src/api/supportViewProfiles.ts`, su test, y este handoff.
- Sin migración, migrador, SQL, Keycloak, SMTP, cambios de esquema, despliegue, ni modificación de `main`.

## Riesgo y condición de cierre

La vista por perfil muestra únicamente identidad sintética, controlada por el servidor, y permisos de lectura propios del rol; no equivale a asignar un cargo real ni a autorizar una escritura. Antes de fusionar, exigir CI, Showcase y paquete QA exact-head, revisar `dev` vivo y coordinar la ventana de publicación con #525. El QA actual 725bc319 permanece intacto hasta decisión operativa posterior; no declarar UAT finalizada por el PR.

**Estado inicial:** rama creada, código y CI pendientes de la ejecución. Esta línea debe completarse con SHA/CI tras implementación.
