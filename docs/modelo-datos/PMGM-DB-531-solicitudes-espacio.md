# PMGM-DB-531 — Solicitud de templos y salas (registro GOV-004)

**Issue:** #531. **Estado:** fase de implementación, NO desplegado. **Fuente:** PO Opción B 16-10-2026; `PMGM-REQ-022`; formulario Drive `FORMULARIO-USO-GRAN-TEMPLO-2026.docx`. Rama inicial desde `dev@346912923c43c6b6bb9537d5e3fadc9429396356`.

## Antes / después
**Antes:** `core.institutional_spaces` y `core.institutional_space_reservations` pertenecen a Gran Secretaría; no existe solicitud editable desde el Taller. **Después propuesto:** tabla aditiva `core.institutional_space_requests`, sin tocar las reservas históricas; transiciones `pending → approved/rejected/cancelled`; aprobar referencia una reserva existente nueva `SpaceReservationId` confirmada por Gran Secretaría. Sin cambio del mecanismo de titularidad ni voto, ni migración destructiva.

## Diccionario y clasificación
| Campo | Tipo | Regla | Privacidad |
|---|---|---|---|
| Id | uuid PK | UUID irrepetible | Interno |
| OrganizationId | uuid | Taller derivado exclusivamente de `pmgm_org`; índice por Taller; no tomar del body | Interno |
| PreferredSpaceType | varchar(80) | `temple` o `secretariat_room` | Interno |
| PreferredSpaceId | uuid? FK a `institutional_spaces` | Preferencia, no reserva | Interno |
| StartsAtUtc / EndsAtUtc | timestamptz | Fin posterior al inicio; período futuro | Interno |
| Purpose | varchar(80) | `meeting`, `instruction`, `ceremony`, `other` | Interno |
| CeremonyRequestId | uuid? | Solo ceremonia del mismo Taller; autorización para **confirmar reserva** según circuito vigente | Restringido |
| Notes | varchar(500)? | texto operativo minimizado, sin datos del candidato | Interno |
| Status | varchar(20) | pending, approved, rejected, cancelled | Interno |
| ResponseReason | varchar(500)? | rechazo obligatorio | Interno |
| SpaceReservationId | uuid? FK restrictiva | reservado al aprobar | Interno |
| CreatedBySubject / DecidedBySubject | varchar(320) | actor autenticado; no exponer a otros Talleres | Restringido |
| CreatedAtUtc / DecidedAtUtc | timestamptz | historial cronológico | Interno |
| ExpectedAttendees | int? | >0 si informado; aforo se verifica cuando conocido | Interno |
| EquipmentRequest | varchar(500)? | piano/amplificación/proyector/aire acondicionado, requisitos especiales | Interno |
| EquipmentResponsible | varchar(240)? | mínimo necesario, cuando aplique Gran Templo | Personal |
| VmApprovalReference | varchar(240)? | referencia verificable de firma V:.M:. sin suplantar firma física | Restringido |

## Integridad y relaciones
`institutional_space_requests` N:1 Taller (OrganizationId), N:1 preferencia espacio, N:1 ceremonia, 0..1 reserva aprobada. FK de espacio/reserva restringe borrados, índices `(OrganizationId,CreatedAtUtc)`, `(Status,StartsAtUtc)`, único opcional SpaceReservationId. La aprobación y reserva comparten transacción serializable/guardas de concurrencia; una solicitud sólo se aprueba una vez.

## Seguridad y reglas externas
- El Taller no puede elegir/alterar OrganizationId ni ver solicitudes ajenas. Gran Secretaría puede revisar globalmente por rol y alcance Orden.
- Calendario de disponibilidad del Taller expone libre/ocupado y el mínimo dato de espacio, nunca el nombre/actividad del Taller ocupante.
- Gran Templo: el formulario oficial exige 7 días **hábiles** de anticipación, referencia a firma del V:.M:. y responsable de equipamiento autorizado. No aplicar el plazo a salas de otro tipo sin norma. Feriados oficiales y firma física requieren confirmación del mecanismo de validación antes de aceptar la reserva.
- Ceremonias: una solicitud `pending` nunca garantiza fecha; no confirmar reserva ni programar sin las autorizaciones y la Plancha según línea base vigente.
- Notificaciones: destinatarios solo desde vínculos efectivos de perfil/Taller; sin resolución segura de destinatarios no enviar avisos genéricos ni exponer datos. Registro de emisión pendiente en esta fase.
- Registro de actor, fecha, acción y motivo en auditoría. No almacenar secretos ni datos privados del candidato.

## Migraciones, compatibilidad y QA
Migración solo aditiva y reversible en rama aún no desplegada; `DbContext` y tabla nuevos, ningún dato previo afectado. API de reservas existente sin cambios destructivos; consumidores siguen funcionando.
Pruebas de contrato: Taller A/B (403/no filtración), no-secretario 403, estado pending y cancelación, rechazo con motivo, choque 409 concurrente, ceremonia no autorizada, plazo Gran Templo y flujo de autorización, auditoría, no doble reserva. Runner CI/Compose sintético no equivale a host QA: `glmdch` pendiente G1-G10 y supervisión de Sponsor.

**Corte final, hashes y evidencias:** completar en PR #531 antes de merge; una PR y CI aprobado no equivalen a despliegue real.
