# PMGM #531 — aviso seguro de nueva solicitud de espacio: handoff multi-IA

**Fecha:** 22-10-2026. **Propietario backend:** ChatGPT, issue #531 etiqueta `agente:chatgpt`. **UI y UX:** Claude/Clau (después de contrato integrado).
**Base actual sincronizada:** `dev@fa9102935e2954e50a77209e91c52aea7c50af0c`. **main:** `b77de59e170ebfa2abec71b418f6d8b0407ba7ad`. Rama `feature/space-request-created-notice-531-20261021-gpt`.
Este primer commit es estrictamente **documental para abrir PR borrador** conforme PMGM-GOV-003. No se ha programado ni cambiado el servicio.

## Contrato observado
- `SpaceRequestEndpoints.CreateAsync` POST `/api/secretaria/solicitudes-espacio` valida Secretario y ámbito Taller, guarda `InstitutionalSpaceRequest` y auditoría pero **todavía no avisa a Gran Secretaría**.
- `SpaceRequestDecisionNotices.QueueInsideDecisionAsync` ya notifica al **creador** de la solicitud al aprobar/rechazar, usando transacción existente y `NotificationDbContext.UseTransactionAsync` sobre la misma conexión PostgreSQL; no copiar su destinatario para el aviso nuevo.
- `InstitutionalNotificationService.QueueAsync` exige `RecipientSubject` **sujeto OIDC individual** y plantilla activa; no admite como sustitutos nombre de perfil, correo arbitrario, `OrganizationId` ni `createdBySubject` del solicitante.
- `DynamicAccessEndpoints.CreateCatalog()` solo incluye perfiles base (Sistema, Taller), **no configura por defecto un sujeto o cargo Gran Secretaría**. Los `OfficeAssignments` y `member_identity_links` existen, pero falta definir y verificar el código institucional canónico de cargo Gran Secretaría y la regla temporal antes de enviar.

## Diseño backend acordable (fail-closed)
1. Resolver los destinatarios desde fuentes institucionales autoritativas: cargo Gran Secretaría del **ámbito Gran Logia** actualmente vigente, miembro activo en su ámbito, enlace identidad `Issuer+Subject` no revocado y **sin ambigüedad de vínculos**. Comprobar que el cargo canónico se corresponde con autorización de `IInstitutionalAccessService.CanManageGrandSecretariat`/perfil vigente; jamás confiar en campos del POST ni en un texto de correo. Si este vínculo no es demostrable, **no emitir mensajes ni fingir entrega**.
2. La creación debe mantener atomicidad: validaciones; inicio de transacción PostgreSQL; `SpaceRequest` + auditoría, mensajes internos privados (solo `recipientSubject` individuales) mediante `NotificationDbContext` en la misma conexión/transacción; `Commit`. Usar plantilla activa `grand_secretariat.space_request.created`, contenido mínimo `Nueva solicitud de espacio del Taller. Revise la bandeja de Gran Secretaría.`, sin notas, hermanos, ceremonia, nombre, RUT ni detalles sensibles. Acción interna `/gran-secretaria`.
3. Idempotencia **por solicitud y destinatario**: `space-request:<request-guid>:created:<subject-hash>` (o derivado estable sin almacenar el subject en clave visible); canal `internal`, relacionado con `space_request`, clasificación `internal`.
4. Destinatarios vacíos/revocados: decisión por defecto **no aceptar operación silenciosa**. Proponer `409 GRAND_SECRETARIAT_RECIPIENT_UNAVAILABLE` *antes del commit* y registrar falla técnica sin PII; validar este punto con Sponsor porque es decisión operativa. Las excepciones/no plantilla deben provocar rollback de solicitud, auditoría y avisos, nunca commits parciales.
5. Requerir `3/3 CI/Showcase/Instalable SUCCESS` exact HEAD, `behind=0`, merge solo `dev`; no tocar `main`, producción ni QA físico.

## Pruebas PostgreSQL obligatorias
- Secretaría del Taller A crea solicitud, no recibe el aviso para Gran Secretaría; solo sujetos autorizados vigentes a nivel Orden reciben `internal` (sin correo).
- Excluir Gran Secretaría con cargo vencido, membresía inactiva, vínculo de identidad revocado, sujetos duplicados/ambiguos y otro Taller.
- Cero destinatarios: conflicto controlado y ninguna solicitud/auditoría/mensaje persistidos, si Sponsor ratifica regla fail-closed.
- Repetición/idempotencia por destinatario y solicitud: sin avisos duplicados. Fallo al crear mensaje/plantilla: transacción se revierte y estado sigue limpio.
- No exponer nombre de hermanos, RUT, notas o detalles de ceremonia en mensajes, API pública ni logs.

## Trabajo reservado y dependencias
Archivos backend previstos: `SpaceRequestEndpoints.cs`; nuevo `SpaceRequestCreatedNotices.cs`; migración **nueva** en NotificationDbContext para plantilla (archivo caliente: verificar coordinación con migraciones de otros agentes); pruebas HTTP/PostgreSQL específicas; GOV004/handoff actualizados.

**Bloqueos reales:** la creación de PR borrador desde la rama sin diferencias fue rechazada por los controles de la herramienta durante el corte anterior. Este documento prepara una PR correcta con commit documental previo, sin escribir código antes del reclamo visible. Falta verificar autorización formal para mapear cargo Gran Secretaría ↔ sujeto OIDC real; no inventar el mapeo.

**Continuidad:** issue https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/issues/531 ; registro en la adenda Drive. No declarar implementación o despliegue hasta evidencia.

## Implementación añadida a la rama (22-10-2026)

La PR borrador #638 está abierta: https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/pull/638. Después de su creación se introdujeron los siguientes archivos y cambios:
- `SpaceRequestCreatedNotices.cs`: resuelve asignaciones vigentes a perfil explícito `grand-secretariat` (`order`, grant `secretariat/view`), exige enlace de identidad OIDC único y no revocado y membresía/estado activo. Envía únicamente avisos internos a subjects individuales. Igual transacción Npgsql para contexto de solicitud, institucional y notificaciones. Sin remitentes o destinatarios configurables desde el formulario.
- `SpaceRequestEndpoints.cs`: operación de alta serializable + auditoría + avisos; 409 `GRAND_SECRETARIAT_RECIPIENT_UNAVAILABLE` sin destinatarios y `SPACE_REQUEST_RETRY` en colisión transitoria, ambos sin commit parcial.
- `Migrations/20261022094500_AddSpaceRequestCreatedNotification.cs`: plantilla interna sin variables/PII, idempotente `ON CONFLICT`.
- `SpaceRequestCreatedNoticesPolicyTests`: pruebas unitarias de perfil/ámbito/grant/vigencia.
- `GrandSecretariatHttpWorkflowTests`: fixture sintético de asignación dinámica y vínculo de identidad, aviso inicial separado del aviso de resolución y validación de contenido mínimo.
- GOV004 `docs/modelo-datos/registros/2026-10-22-chatgpt-space-request-created-notice-531.md`.

**Punto operativo importante:** el perfil dedicado `grand-secretariat` no existe entre los perfiles predeterminados. El administrador debe crearlo y asignarlo válidamente en el catálogo de perfiles dinámicos antes de operar el envío; no se envía a un administrador genérico por defecto. Verificar la semántica con el Sponsor y QA/UAT antes de producción. Sin implementar asignaciones implícitas ni inventar enlaces de identidad.

**El código NO está integrado a dev** mientras la PR y los gates permanezcan pendientes. Clau no debe construir una UI que asuma que la notificación funciona hasta ver el SHA mergeado.
