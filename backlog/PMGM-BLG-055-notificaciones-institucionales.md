# PMGM-BLG-055 — Notificaciones institucionales

**Prioridad:** P1  
**Estado:** En implementación / vertical slice backend  
**Requisito origen:** `docs/requisitos/PMGM-REQ-028-notificaciones-institucionales.md`

## Objetivo
Implementar un servicio transversal de notificaciones institucionales que permita a los módulos del Proyecto Milenio generar avisos trazables sin acoplarse al mecanismo de entrega.

## Alcance de este corte v0.18

- plantillas versionadas y vigentes;
- variables permitidas por plantilla;
- clasificación de sensibilidad;
- mensaje lógico persistente;
- canales iniciales `internal` y `email`;
- idempotencia mediante clave única;
- entrega interna inmediata y bandeja personal;
- cola persistente para email;
- historial de intentos, errores y `dead_letter`;
- correlación con evento origen;
- panel operativo para Gran Secretaría;
- marcado de lectura por el destinatario;
- auditoría de altas, cola, lectura y resultados de entrega;
- migración PostgreSQL independiente mediante `NotificationDbContext`;
- pruebas de catálogo, migración e idempotencia.

## Seguridad y privacidad

- La bandeja `/me` sólo resuelve el `sub` autenticado.
- El panel operativo queda restringido a Gran Secretaría / administración institucional.
- El rol de privacidad puede encolar avisos institucionales de su dominio, pero no obtiene lectura masiva de la bandeja de terceros.
- La metadata de auditoría no debe contener cuerpo, email ni datos personales del destinatario.
- El correo permanece en cola hasta configurar un proveedor autorizado; no se incorporan credenciales al repositorio.

## Criterios de aceptación del corte

1. Dos solicitudes con la misma `IdempotencyKey` producen un solo mensaje lógico.
2. Una notificación interna queda disponible en la bandeja del `sub` destinatario.
3. Una notificación por email sin dirección de correo es rechazada.
4. Una variable no autorizada por la plantilla es rechazada.
5. Los intentos de entrega conservan número, estado, proveedor y código de error sin registrar contenido sensible.
6. Las migraciones pueden aplicarse sobre PostgreSQL QA.
7. El API reporta versión `0.18.0`.

## Pendiente para cierre total de PMGM-REQ-028

- adaptador real de correo institucional (SMTP/API) con secretos externos al repositorio;
- worker de reintentos con backoff y límite configurable;
- preferencias de usuario para avisos recomendados/opcionales;
- integración automática desde los eventos de Ceremonias, Gran Secretaría, Biblioteca, Talleres y Privacidad;
- interfaz frontend de bandeja y panel operativo;
- métricas de entrega y alertas por `dead_letter`.
