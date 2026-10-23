# #609 — RUT chileno explícito al ingresar insinuados: contrato backend

Fecha 2026-10-22. Repo dev base `2078769b21dd3aaaf1cea17742d7528d96cb5381`. Responsable ChatGPT (`agente:chatgpt`). Protocolo `AGENTS.md`, `PMGM-GOV-003`; no se cambian pantallas/ramas de Clau.

## Decisión de contrato
El endpoint `POST /api/insinuados/taller/solicitudes` ya recibe `rutOrInstitutionalId` y acepta identificaciones institucionales **no chilenas**. Nunca imponer módulo 11 indiscriminadamente: se agrega `identityType` **opcional** con dos valores aceptados `chilean_rut` y `other`.

* Si `identityType=chilean_rut`, se invoca el validador servidor `ChileanRut.TryNormalize` ya fusionado en #634; si falla, responder `400 INVALID_CHILEAN_RUT` antes de crear `Person` o solicitud. Si pasa, normalizar `body-DV` sin puntos y DV K mayúscula para persistencia.
* Si `identityType=other` o se omite por compatibilidad (clientes y datos históricos), preservar los controles actuales sin intentar validar un identificador extranjero como RUT.
* Si `identityType` es desconocido, responder `400 CANDIDATE_IDENTITY_TYPE_INVALID` sin persistir datos.
* Los expedientes anteriores no se reescriben ni presuponen un tipo. La validez de módulo 11 no acredita identidad real.
* Mantener búsqueda anti-duplicidad por identidad normalizada y el gate de origen/recomendante y Maestro patrocinante de Taller receptor sin cambios.

## QA y coordinación
Pruebas HTTP/PostgreSQL: rechazo de DV inválido, aceptación y persistencia canónica del RUT válido, no bloquear ID extranjero explícito/legacy, no grabar datos en rechazos. Ejecutar unit y 3 Actions exact-head, 0 behind antes de fusionar **solo dev**.

**Clau / otras IA:** una vez fusionado contrato, actualizar selector visual de Secretaría para escoger "RUT chileno" o "Otra identificación" y enviar `identityType`; no añadir autoselección por país sin aprobación. La UI tiene dueño distinto, no escribir en `CandidateWorkshopIntakePage.tsx` ni archivos calientes sin coordinación. Nunca incluir RUT reales en demos o capturas. Registro GOV-004 en archivo nuevo de esta rama. No `main`, servidor QA físico ni producción.

Estado: contrato documentado; confirmación de código, pruebas y gate se añade al PR/Issue cuando efectivamente ocurra.

## Seguimiento 23-10-2026 — corrección de prueba e integración base dev
La ejecución PMGM CI 39868276839 falló únicamente en la aserción PostgreSQL línea 104: identificador extranjero `EXT-<hex>` aleatorio con minúsculas fue guardado en mayúsculas conforme al comportamiento heredado `ToUpperInvariant`; la prueba comparaba en forma sensible a mayúsculas. Se ajustaron las aserciones de extranjero y legado al valor canónico esperado (mayúsculas), sin flexibilizar la validación módulo 11 para `chilean_rut` ni cambiar control de origen/patrocinio.
Se reconcilió preservando completamente árbol vigente `dev` y aplicando solo los cambios originales de esta PR con merge de dos padres (dev HEAD vivo + rama PR). PR #631 permanece independiente: sus cambios de directorio privado no se fusionan por esta PR; evitar sobrescribirlo en integración posterior. No se altera main ni servidor QA físico. Revalidar CI, Showcase e Instalable del HEAD exacto resultante antes de fusionar a dev.
