# Cierre de auditoría Issue #47 — 10-10-2026

PR de fortalecimiento técnico **pendiente de validación**. Base de rama `dev@c6583db98bb67715a6b77cad8d0adb7614c91e7f`.
Complementa PR #423 y #424; no modifica reglas, mínimos, roles, autorizaciones ni el servidor piloto.

## Aporte
El registro existente `ceremony.authorization.approved` conservaba la regla y las planchas aprobadas, pero no toda la evidencia de asistencia y configuración. La nueva traza registra en el evento de auditoría, al momento de autorización, ID de regla de mínimos y antigüedad, fecha de corte e inicio del grado, mínimos institucionales, cómputos reales y los IDs de Tenidas e instrucciones efectivamente consideradas, además de identificadores de planchas, para permitir recomputación posterior. La constancia `CeremonyValidation` persiste también el resumen `real/mínimo` de Tenidas, instrucciones y planchas.

Los identificadores de evidencia vinculados a hermanos se excluyen deliberadamente de la serialización de la respuesta de elegibilidad `GET /elegibilidad` mediante `JsonIgnore`, dejando visibles los mínimos y valores alcanzados necesarios para Gran Secretaría. La auditoría permanece privada para roles autorizados. Se añade prueba xUnit de trazabilidad y no filtración de identificadores.

## Control
- Mantener ISSUE #47 abierto hasta verificar **tres gates del HEAD exacto** y la revisión de los siete criterios.
- Antes de fusionar, comprobar HEAD de `dev` y no pisar cambios paralelos (GOV-003).
- Tras fusionar, comprobar CI, Showcase, QA/Pre-UAT del nuevo `dev`.
- No alterar `main`, `srv01`, ni el flujo normativo de dispensa Issue #48.
- Registrar resultados verificables en Issue #47 y Línea Base de Drive.
