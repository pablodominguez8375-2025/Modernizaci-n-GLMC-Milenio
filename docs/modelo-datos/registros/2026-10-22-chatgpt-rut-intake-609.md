# PMGM-GOV-004 — Registro de salida: validación RUT chileno al ingresar insinuado

Corte: 2026-10-22. Issue #609; rama `feature/609-explicit-chilean-rut-intake-20261022-gpt`; base `dev@2078769b21dd3aaaf1cea17742d7528d96cb5381`.

| Contrato/estructura | Antes | Después | Consideración |
|---|---|---|---|
| `CreateCandidateWorkshopRequest` / POST `/api/insinuados/taller/solicitudes` | `rutOrInstitutionalId` obligatorio, tipo ambiguo | Agrega `identityType?: 'chilean_rut'|'other'|null` (opcional, al final DTO) | No altera clientes anteriores |
| `Person.Rut` varchar(16) | Identificación ingresada en bruto | Solo si `identityType='chilean_rut'` se normaliza `body-DV`, sin puntos, DV K mayúscula | Sin migraciones; se mantienen históricos y países extranjeros |
| Validación de escritura | Longitud máxima 16 + identificador | RUT chileno declarado debe aprobar `ChileanRut.TryNormalize` (módulo 11); 400 `INVALID_CHILEAN_RUT` o 400 `CANDIDATE_IDENTITY_TYPE_INVALID` antes de crear persona | En servidores existentes se conserva semántica sin `identityType` |
| Duplicidad | Busca normalización alfanumérica del identificador | Reutiliza lógica existente sobre RUT normalizado | RUT/ID extranjero con igual clave alfanumérica exige estudio posterior |

**Controles**: la selección del tipo no confiere patrocinio ni rol; aplican los permisos previos de Secretaría por Taller y validación del origen/patrocinante. No se almacena nacionalidad ni se introduce columna de tipo de documento; esto requerirá decisión de modelo específica y migración posterior. No asignar tipo de documento histórico por inferencia, no transformar masivamente identificadores ajenos y no usar el resultado módulo 11 como prueba de titularidad.

**Pruebas**: cobertura HTTP/PostgreSQL `CandidateWorkshopSponsorHttpTests.Declared_Chilean_RUT_requires_modulo_11_but_foreign_and_legacy_ids_remain_accepted` con RUT sintético K aleatorio y documentos extranjeros sintéticos; ninguna identificación real en repositorio. El test ya pertenece al shard de integración existente, sin cambiar configuración de Actions.

**Coordinar con Clau**: en UI posterior, selector "RUT chileno" / "Otra identificación" y enviar `identityType`. Hasta que la UI implemente esto, el backend queda funcional como contrato pero la entrada desde la pantalla existente sigue en modo compatible sin verificación forzosa. No tocar una pantalla propiedad de Clau sin nueva coordinación. Aprobación del PO antes de exigir módulo 11 de forma global.

Estado de gate y SHA final se informan en la PR. No promueve main ni despliega QA físico.
