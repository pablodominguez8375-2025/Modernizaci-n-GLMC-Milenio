# Issue #510 — avisos de Agenda por grado, revisión de seguridad

Fecha 16-10-2026; ChatGPT; trabajo `feature/calendar-grade-notification-safety-20261016-gpt`; base `dev@f791d180de007cf2c2ef2ef7e28a20f83098748a`.

Contrato: GET e ICS de Agenda ya filtran por grado y Taller (#500); los avisos son flujo independiente. Investigar productores reales antes de cambiar contrato. `NotificationService.QueueAsync` genera mensajes a `RecipientSubject`, mientras `/api/notifications/me` muestra el destinatario exacto. Endpoint de encolamiento está limitado a Gran Secretaría y Privacy Officer, pero no comprueba a quién se anuncia información clasificada de Agenda; no declarar filtración real sin evidencias de flujo de anuncio.

Alcance de PR: revisión concreta y control de seguridad para encolamiento de avisos vinculados a un evento de Agenda; pruebas negativas y positivas con datos sintéticos; evitar duplicar UI, no tocar QA ni main. El control de grado del servidor debe ser la autoridad antes de encolar, con ID de evento verificable y destinatario vinculado institucionalmente, no el grado declarado por cliente. No modificar workflows, frontend ni migraciones reservadas.

Registros y resultados se completarán en esta rama; actualización espejo en Drive adenda Agenda por grado. No afirmar despliegue, notificaciones enviadas ni QA realizada.
