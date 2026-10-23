# GOV-004 — diccionario de salida: nueva solicitud de espacio #531

Fecha 22-10-2026. Issue #531, PR #638, backend ChatGPT y UI futura Clau/Claude. Base dev `fa9102935e2954e50a77209e91c52aea7c50af0c`.

| Contrato / estructura | Situación anterior | Cambio propuesto | Política de acceso |
|---|---|---|---|
| `POST /api/secretaria/solicitudes-espacio` | Solicitud/auditoría sin aviso inicial | Un único commit de solicitud + auditoría + aviso(s) internos; respuesta 409 `GRAND_SECRETARIAT_RECIPIENT_UNAVAILABLE` si no hay destinatario verificable | Secretaría solo su Taller; no aceptar destinatario ni Taller en payload |
| `core.dynamic_access_snapshots` | Matriz dinámica vigente | **No se altera el esquema**. Se espera perfil explícito código `grand-secretariat`, ámbito `order`, menú `order`, grant `secretariat/view` y asignación efectiva `OrganizationId=NULL` a sujetos individuales | Solo administrador de acceso define/activa perfil/asignación |
| `core.member_identity_links` | Vínculos OIDC | Solo lectura dentro de la transacción, se exige un único vínculo no revocado por `Subject` (sin deducir destinatario desde correo/rol) | Validación servidor, no se serializa a cliente |
| `core.memberships` / `institutional_status_events` | Membresía y estado | Solo lectura de membresía vigente y último estado efectivo; excluye inactivos/retiros/defunción | Validación servidor, ámbito institucional |
| `core.notification_templates` | Plantilla de resolución de solicitud | Nueva plantilla activa `grand_secretariat.space_request.created` v1, sensibilidad `internal`, **sin variables** | Mensaje genérico sin nombres, notas ni datos sensibles |
| `core.notification_messages` y `notification_deliveries` | Aviso al creador al resolver | Aviso por creación a `RecipientSubject` individual verificado, `internal` solamente; idempotencia por solicitud/destinatario (huella SHA-256 del subject en clave) | Sin correos, números de identidad, datos de ceremonia ni PII |
| `core.audit_events` | Evento creación existente | Se mantiene y se guarda junto al aviso o se revierte todo | Sin nuevos identificadores personales |

## Reglas que continúan
- Las aprobaciones/rechazos de Gran Secretaría siguen notificando al **solicitante**, mediante `SpaceRequestDecisionNotices`, con la misma transacción del cambio. No confundir ambos tipos de destinatario.
- No se incorporan permisos para un Taller sobre bandeja ajena ni para un hermano sin cargo.
- No hay columnas nuevas ni migración destructiva; únicamente inserción versionada de plantilla en `NotificationDbContext`.
- Perfil `grand-secretariat` es **un contrato administrativo nuevo**: su configuración institucional/QA debe estar hecha para permitir solicitudes. Sin ese perfil o sin identidad verificada se rechaza la creación en lugar de fingir entrega.
- La comprobación de perfil/identidad y la escritura ocurren dentro de la misma transacción PostgreSQL. La pertenencia a perfil es otorgada por administradores de sistema (circuito de perfiles dinámicos), no por el usuario que crea la solicitud.

## Pruebas y cautelas
- Prueba HTTP PostgreSQL ampliada para que el aviso inicial llegue al sujeto verificado, excluya datos sensibles y sea separado de las notificaciones de resolución. Prueba unitaria de selección de perfil/ámbito/grant vigentes.
- Pendientes antes del cierre del Issue: prueba directa del caso sin destinatario + rollback, auditoría UAT, habilitación del perfil por administrador autorizado, CI exact-head 3/3 SUCCESS, revisión de privacidad y aceptación del Sponsor.
- No publicitar como desplegado en Pages/QA; no modificar `main`, servidor glmdch ni producción.
