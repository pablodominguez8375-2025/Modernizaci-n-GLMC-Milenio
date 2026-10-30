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

**Implementación entregada en rama:** API y frontend incorporan seis perfiles, con las mismas claves, ámbito y role preexistente; sin nuevas entidades, permisos, migraciones o cambios de UI visual. Pruebas añadidas: teoría de grados 1/2/3 para Grandes Vigilantes y Ex Gran Maestro, Jefatura de Docencia tres grados, Gran Hospitalaria sin acceso docente, Inmediato Ex Venerable vinculado al Taller de soporte, y catálogo frontend 21 perfiles con demo válida/keys únicas.

**Evidencia GitHub:** PR [#808](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/pull/808) DRAFT; último commit de código/tests antes de este handoff `70f3c35ee40819d17470a7ad1e5a030485dd4b5c`, base `dev@725bc31953629d87f01af77db1fddf5e05b1c26a`, compare ahead=5/behind=0 y cinco archivos. Gates PMGM CI, Showcase y QA Installable iniciados; **no declarar PASS hasta resultado exact-head de PR actualizado**. Demo Pages pública y `glmdch` permanecen en corte anterior hasta publicación/handoff QA autorizado. No hay UAT físico de estos seis perfiles. Al integrar más adelante, comprobar URL /api/system/support/view-as/profiles, pantalla «Ver cómo» y que siga solo lectura, sin descargas ni permisos cruzados.


## Corrección CI exact-head — 30-10-2026

El primer HEAD `c45c26e8cfbb3b64165b1781349cdf527c08695e` tuvo **frontend lint/build FAILED** en PMGM CI #41119466271: `initial-load-budget.test.ts` midió 180039 bytes gzip frente al contrato estricto `<180000` (exceso de 39 bytes); 701 tests frontend aprobados, 2 omitidos y 1 falla, sin indicios de fallo de permisos o perfiles. Se leyó el log del job `Frontend lint and build` y se corrigió **sin ampliar el presupuesto de carga inicial** ni tocar el test.

`frontend/src/api/supportViewProfiles.ts` conserva exactamente las 21 claves/etiquetas/ámbitos y correspondencias de demo, pero ahora define un único `PROFILE_ROWS` tipado; `SUPPORT_VIEW_PROFILES` y `DEMO_PROFILE_FOR` derivan de él. Esta normalización elimina la duplicación estática de las claves en el bundle inicial, con funcionalidad estable. Commit de la optimización: `7c61d79d5f68a455a8e5173f5dadc31099882987`. **Resultado de CI después de la corrección: verificar sobre el HEAD final; no anticipar éxito.**
