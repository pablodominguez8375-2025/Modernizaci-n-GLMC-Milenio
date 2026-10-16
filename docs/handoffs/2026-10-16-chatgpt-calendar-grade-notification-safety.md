# Issue #510 — avisos de Agenda por grado, revisión de seguridad

Fecha 16-10-2026; ChatGPT; trabajo `feature/calendar-grade-notification-safety-20261016-gpt`; base `dev@f791d180de007cf2c2ef2ef7e28a20f83098748a`.

Contrato: GET e ICS de Agenda ya filtran por grado y Taller (#500); los avisos son flujo independiente. Investigar productores reales antes de cambiar contrato. `NotificationService.QueueAsync` genera mensajes a `RecipientSubject`, mientras `/api/notifications/me` muestra el destinatario exacto. Endpoint de encolamiento está limitado a Gran Secretaría y Privacy Officer, pero no comprueba a quién se anuncia información clasificada de Agenda; no declarar filtración real sin evidencias de flujo de anuncio.

Alcance de PR: revisión concreta y control de seguridad para encolamiento de avisos vinculados a un evento de Agenda; pruebas negativas y positivas con datos sintéticos; evitar duplicar UI, no tocar QA ni main. El control de grado del servidor debe ser la autoridad antes de encolar, con ID de evento verificable y destinatario vinculado institucionalmente, no el grado declarado por cliente. No modificar workflows, frontend ni migraciones reservadas.

Registros y resultados se completarán en esta rama; actualización espejo en Drive adenda Agenda por grado. No afirmar despliegue, notificaciones enviadas ni QA realizada.

## Entrega técnica candidata — 16-10-2026 (seguimiento)

- **Dev al iniciar:** `f791d180de007cf2c2ef2ef7e28a20f83098748a`, postmerge PR #509. **Dev vivo reconciliado:** `3a25ab0bfbfe0bb5aa8009906505ccb86559d723`, luego de que Claude integrara la PR UI #511, sin solapamiento (7 archivos UI, 6 archivos backend/tests/handoff de esta PR). **Main:** `6dfb9546a4873baff15955cf86abfd7d47e3d111` sin tocar.
- **Issue #510 / PR #512 DRAFT**, rama `feature/calendar-grade-notification-safety-20261016-gpt`, sin merge ni cambios en glmdch. Se integra exclusivamente con `dev`; no reabrir Issue #499 backend cerrado.
- **Hallazgo:** `/api/notifications/queue` exige Gran Secretaría u Oficial Privacidad, pero admitía envío genérico con `RecipientSubject` declarado por emisor; `/api/notifications/me` usaba filtro subject sin reevaluar el permiso sobre Agenda. No se ha establecido que exista un productor automático de avisos Agenda, ni notificación indebida real; es un control preventivo.
- `backend/src/PMGM.Api/Modules/Notifications/CalendarNotificationRecipientGuard.cs` nuevo: para avisos etiquetados como Agenda/Docencia, exige `RelatedResourceType=calendar_event` con UUID existente, `iss` real del emisor firmado + `sub` destino asociado con EXACTAMENTE un vínculo identitario activo sin colisión de issuer, DTO de evento de Taller no-restricted, membresía y grado/cargo vigentes resueltos solo por `ICalendarGradeVisibilityService` a partir de BD. Rechaza canal email y `RecipientEmail` arbitrario, ids de fuente discordantes, acceso a otro Taller, cuentas inexistentes o revocadas; solo canal interno por ahora.
- `NotificationEndpoints.QueueAsync`: antes de encolar, verifica el guard; retorna `Forbid` si falta autorización, sin que se cree entrega por canal interno/email. `GetMineAsync`: reconsulta identidad única, y visibilidad actual de los avisos relacionados a Agenda; si hubo retiro, revocación, cambio de grado, otro Taller, evento borrado u ocultación por visibilidad, no devuelve esos mensajes. Mensajes no calendarizados no cambian.
- `backend/tests/PMGM.Api.Tests/Notifications/CalendarNotificationRecipientGuardTests.cs`: unitarias de matriz 1-3 grados, generales, membresías ajenas/inactivas, cargos, canales internos/email, fuentes restringidas y clasificación.
- `backend/tests/PMGM.Api.Tests/Integration/CalendarNotificationRecipientPostgreSqlTests.cs`: persistencia PostgreSQL sintética con identidad issuer/sub activa, evento de Taller, grado 1/3, destinatario no existente, event ID inexistente, vínculo revocado, email adicional, issuer ajeno y SourceEventId incongruente.
- `.github/backend-integration-shards.json`: nuevo test PostgreSQL asignado a shard b; no tocó workflows reservados ni migraciones.

## Límites / revisiones

- Los controles se aplican a **mensajes tipificados/enlazados con calendario** (type/template/resource/ruta conocida) y exigen metadatos canónicos; no prueban si un operador autorizado envía texto sin etiqueta como notificación genérica. La creación de un **emisor automático por Tenida/Docencia** NO es parte de esta PR.
- No se permite email calendario hasta contar con dirección vinculada a la ficha institucional actual, bloqueo de encolado/externalización y validación reintentos en worker. No declarar mensajería saliente completa.
- La comprobación antes de QueueAsync no reemplaza política de entrega diferida ni comprobación real de un worker para correo. La revalidación del inbox oculta el contenido, pero no borra datos históricos (retención Ley 21.719 pendiente).
- El `iss` y la identidad destino provienen de OIDC y tabla `member_identity_links`; si existen sujetos no únicos entre emisores, se deniega por seguridad. No se incluyen datos reales.
- Validar CI/Showcase/QA Installable **sobre el HEAD exacto final** y postmerge antes de integrar a `dev`. Resultados anteriores de SHAs intermedios no son certificación. Pages, QA física glmdch y UAT no verificadas.

## Continuidad

- Verificar SHA vivo `dev` (Claude puede actualizar UI) y compararlo con los archivos de la PR. Si cambia, rebase seguro con lease sin modificar ni restaurar archivos de Claude.
- Reconciliar estado Issue #508 UI y PR #511; no modificar UI como ChatGPT.
- Si todos los gates pasan, preparar PR Ready, squash solo `dev` según AGENTS/GOV-003 y registrar GitHub Issue #510 + Drive adenda Agenda; cerrar #510 sólo para el alcance preventivo probado, dejando los avisos automáticos/email como fases independientes documentadas.
