# Registro de cambio de datos — #531, aviso de decisión de espacios

## Identificación y alcance
- Fecha: 20-10-2026; agente ChatGPT, Sponsor revisor; Issue #531, PR #614.
- Base: `dev@591dfe19a4ad583d471f4920ba832565ec09b5ab`.
- Rama: `feature/space-request-atomic-recipient-notice-531-20261020-gpt`; **en revisión**, no integración/instalación demostrada.
- Fuente: #531, PMGM-GOV-004, `InstitutionalSpaceRequest`, `NotificationDbContext`, migración `20261020214500_AddSpaceRequestDecisionNotification`.
- Alcance: catálogo de plantillas de notificación y generación de una fila idempotente en mensajes/entregas cuando Gran Secretaría aprueba/rechaza; no altera estructura de tablas, ni su esquema, ni documentos/personas, ni los DTO de solicitudes, ni la UI.
- Registro anterior: definiciones de tablas y permisos en `backend/src/PMGM.Api/Data/NotificationDbContext.cs` + `backend/src/PMGM.Api/Modules/Notifications/Entities/NotificationEntities.cs` y esquema consolidado `docs/modelo-datos/PMGM-DB-004-diccionario-y-estructuras.md`.

## Diccionario afectado (corte implementado)

| Tabla/campo | Significado/tipo físico | Nulo/default y validación | Clasificación y acceso |
|---|---|---|---|
| `core.notification_templates.Id` | Identidad UUID PK, fila añadida `f0ab4672-d3e8-4b1e-8240-f184ec3a205a` | No nulo, fijo en migración | Interno; plantilla administrada |
| `notification_templates.Code` | varchar(160); `grand_secretariat.space_request.resolved` | No nulo; código+versión único | Interno; sin datos individuales |
| `notification_templates.Version` | integer `1` | No nulo | Interno |
| `notification_templates.Name` | varchar(240) | No nulo | Interno |
| `notification_templates.SubjectTemplate` | varchar(500); `Solicitud de espacio: {{status}}` | Nullable, variables restringidas | Interno |
| `notification_templates.BodyTemplate` | text; texto sin motivo, sala ni identidad | No nulo | Interno |
| `notification_templates.AllowedVariablesJson` | jsonb `["status"]` | No nulo; sólo `status` con `aprobada/rechazada` | Interno |
| `notification_templates.Sensitivity` | varchar(40), `internal` | No nulo | Interno |
| `notification_templates.Status` | varchar(40), `active` | No nulo | Interno |
| `notification_templates.EffectiveFromUtc/CreatedAtUtc` | timestamptz 2026-10-20 UTC | No nulo | Interno |
| `core.notification_messages.TypeCode` | varchar(160), `grand_secretariat.space_request.approved/rejected` | No nulo | Interno; ve sólo destinatario |
| `notification_messages.RecipientSubject` | varchar(320), `InstitutionalSpaceRequest.CreatedBySubject` | No nulo; sujeto autenticado persistido, nunca del cliente ni otro Taller | Identificador personal restringido |
| `notification_messages.RecipientEmail` | varchar(320) | NULL; no email ni consulta de correos | Interno |
| `notification_messages.IdempotencyKey` | varchar(500), `space-request:{id}:decision` | No nulo, unique | Interno |
| `notification_messages.SourceEventId / RelatedResourceId` | varchar(320)/varchar(120), UUID solicitud | Nullable en esquema; se puebla tras decidir | Interno |
| `notification_messages.ActionUrl` | varchar(1000), `/secretaria` | Nullable en esquema, ruta relativa permitida | Interno |
| `notification_deliveries.Channel/Status` | varchar(40), `internal/delivered` | No nulo, mensaje+canal único | Interno |
| `core.institutional_space_requests.Status` | varchar(20), `approved/rejected` | Estado existente, sin cambios de columna | Sólo Taller titular y Gran Secretaría |
| `institutional_space_requests.CreatedBySubject` | varchar(320) | Identidad OIDC ya persistida; no alterada | Restringido |

## Estructuras/relaciones
- Persistencia previa intacta: `notification_messages.NotificationTemplateId` N:1 `notification_templates.Id`; `notification_deliveries.NotificationMessageId` N:1 mensajes. Borrado de plantilla restringido, entregas cascade conforme configuración existente; índices únicos `(Code,Version)`, `IdempotencyKey`, `(MessageId,Channel)`.
- Asociación lógica **sin nueva FK**: `notification_messages.RelatedResourceType='space_request'` y `RelatedResourceId` == UUID de `core.institutional_space_requests.Id`. No diagrama ER nuevo porque no cambia cardinalidad ni FK física.
- Inserciones en tablas existentes en la misma transacción de solicitud/reserva/auditoría mediante conexión y `DbTransaction` PostgreSQL compartidos.
- No mensajes al crear, cancelar ni si hay error/conflicto. No se publica el motivo de rechazo, inventario, nombre del Taller o participantes.

## Antes / después y compatibilidad
- **Antes:** aprobaciones/rechazos tenían estado, reserva y auditoría pero **sin** aviso institucional garantizado al solicitante.
- **Después:** mismo contrato HTTP; notificación interna al solicitante de la decisión, atómica e idempotente, con plantilla versionada; sin contacto por correo.
- Migración nueva NotificationsDbContext: sólo `INSERT ... ON CONFLICT DO NOTHING`, con `Down` que borra exclusivamente el ID fijo de plantilla. No backfill histórico ni borrado de datos de negocio.
- Plantillas/cola de notificaciones deben migrarse antes de atender nuevas decisiones (dependencia de despliegue, no afirmar QA/producción).
- Decisiones históricas con `CreatedBySubject='unknown'` o vacío no generan aviso para evitar dirigirlo a un sujeto equivocado (se sigue conservando auditoría). Resolver identidad histórica formalmente, no inferir.
- Evento de **nueva solicitud a Gran Secretaría** fuera de esta fase hasta poder resolver de manera verificada sus destinatarios activos por perfil.
- Privacidad Ley 21.719: interno, sólo `sub` como destinatario; minimización de payload y cero email. Sin modificaciones a retención.
- Pruebas: HTTP PostgreSQL en `GrandSecretariatHttpWorkflowTests.Two_lodges_are_isolated...` verifica A/B, aviso en approved y rejected, no duplicación por 409 y ausencia al cancelar. Se espera CI exact-HEAD, Showcase e instalable.

## Recibo de salida
- SHA final, CI y merge: **pendientes**.
- No se ha promovido `main` ni desplegado QA físico glmdch.
