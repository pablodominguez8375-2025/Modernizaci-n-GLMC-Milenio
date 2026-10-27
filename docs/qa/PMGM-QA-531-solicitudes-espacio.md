# PMGM-QA-531 — Validación funcional de solicitudes de templos y salas

**Estado:** contrato en PR #539, QA sintético mediante CI; despliegue físico `glmdch` **NO validado**. Baseline `dev@346912923c43c6b6bb9537d5e3fadc9429396356`. Fuente del requisito: Issue #531, `PMGM-REQ-022`, formulario Gran Templo 2026 de Drive.

**Decisión del PO (27-10-2026):** solo la Secretaría del Taller pide templo o sala a la Gran Secretaría, que aprueba o rechaza. No se exige firma del Venerable Maestro: el templo se aprueba igual que una sala (disponibilidad, capacidad, Plancha si es ceremonia). Sustituye la guardia anterior que devolvía 409 para templos.

## Matriz de aceptación (entorno sintético)

| Código | Actor / operación | Resultado esperado |
|---|---|---|
| QA-531-01 | Taller A / POST solicitud futura | 201, estado pending, Taller de claim |
| QA-531-02 | Tesorero u otro cargo / POST | 403 |
| QA-531-03 | Taller B / GET solicitudes creadas por A | A no figura en respuesta |
| QA-531-04 | Taller B / cancelar ID de A | 404 sin filtración |
| QA-531-05 | Taller A / disponibilidad | libre/ocupado sin nombre, actividad o notas de ocupante |
| QA-531-06 | Gran Secretaría / aprobar pendiente | 200, reserva vinculada, cambio audited |
| QA-531-07 | Repetir aprobación | 409, sin segunda reserva |
| QA-531-08 | Taller B / solicitud solapada | 201 pending, aprobar da 409, permanece pending |
| QA-531-09 | Gran Secretaría / rechazo motivado | 200 rejected, auditable |
| QA-531-10 | Taller A / cancelar pendiente propia | 200 cancelled, auditable |
| QA-531-11 | Ceremonia con autorización pero sin Plancha emitida | aprobar da 409, sin reserva |
| QA-531-12 | Data model | migración aditiva, FK a Taller/espacio/reserva/ceremonia, índices, no PII del candidato |
| QA-531-13 | Gran Templo | firma V:.M:. **ya no se exige** (PO 27-10-2026); el templo se aprueba como una sala. Plazo de 7 días hábiles y requisitos de equipos: no implementados, sin decisión del PO |
| QA-531-14 | Avisos por rol/Taller al crear/resolver | **pendiente** de integrar destinatarios seguros, sin avisos a destinatarios genéricos |
| QA-531-15 | UI Taller + Gran Secretaría | **pendiente** de contrato backend completo y trabajo Claude |

### Cómo reproducir sin servidor institucional

En GitHub Actions de la PR #539, verificar en SHA exacto los jobs `Backend unit and policy tests` y `Backend integration shard a/b`, más `Infrastructure configuration`, `PMGM Showcase Demo` y `QA Installable`. Las pruebas `SpaceRequestPolicyTests` no requieren PostgreSQL; `SpaceRequestHttpWorkflowTests` usan `PMGM_TEST_POSTGRES` y auth sintética definida **solo en assembly de tests**, y no pasan tokens reales ni crean secretos institucionales.

**No trasladar** los headers `X-Synthetic-Role`, `X-Synthetic-Lodge` a ninguna configuración de autenticación real. Los headers solo se aceptan por un handler compilado en el proyecto de tests.

### Puertas para pasar de CI a QA funcional en glmdch

1. Aprobar identidad del host, huella verificada fuera de banda, conexión de inspección G7 y Environment protegido. No aceptar material SSH en issue, Drive o chat.
2. Validar G1–G10 según Issue #97: imágenes/licencia/procedencia Silo sintético, HTTPS/OIDC real del dominio, BD2 con roles separados, respaldo cifrado externo, restore y plan de recuperación.
3. Comparar commit exacto, SHA y digest del paquete candidato firmado con `downloads/qa-current.json` y la evidencia de CI; el ZIP Actions por sí solo no demuestra un paquete seguro/instalable.
4. Requerir primer despliegue sintético supervisado y evidencia explícita del host antes de habilitar cualquier servicio o temporizador.
5. Ejecutar QA-531-01..15 contra usuarios ficticios de Taller A/B y rol Gran Secretaría de QA, documentar estado por prueba y conservar solo resultados minimizados. No usar RUT real, datos de hermanos o secretos.
6. QA físicamente aprobada exige también smoke/regresión general QA-001..026 y visto bueno UAT según gates del proyecto.

## Registro de resultado

- **CI:** a completar solo tras conclusión exact-HEAD; ninguna prueba pendiente se marca PASS.
- **Host glmdch:** NO EVALUADO; no hubo ejecución SSH ni instalación real desde esta PR.
- **Main/srv01:** sin cambios, Silo únicamente sintético.

**DoD del Issue #531:** no cerrar ni delegar UI hasta completar notificaciones por destinatario, pruebas finales; actualizar contrato y handoff con SHA después de CI.
