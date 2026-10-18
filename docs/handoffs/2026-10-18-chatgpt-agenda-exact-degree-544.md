# Handoff — Agenda Docencia por grado exacto — #544 / PR #549

## Corte y decisiones
- 18-10-2026: regla aprobada por Sponsor y registrada por Claude en Issue #544.
- Base dev: `29bf6688e53b9cd45bce3adb14ea620ac1937517`; main estable: `6dfb9546a4873baff15955cf86abfd7d47e3d111`.
- Rama: `feature/agenda-instrucciones-grado-exacto-544-chatgpt`; PR: https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/pull/549
- QA LAB2 ejecuta candidato firmado antiguo `4ef456fee3a578a9e6418f3f7072af5dd79eb045`. No se actualizó, no se hizo UAT.

## Implementación
- `CalendarGradeReader.CanSee` distingue `lodge_instruction_day`: para hermanos sin cargo vigente **igualdad exacta** entre grado del evento y grado efectivo; los demás eventos mantienen grado propio e inferiores.
- Se conserva validación de membresía activa del Taller antes del filtro por grado, excepción para cargo vigente de ESE Taller y administradores institucionales, evento general `all` sin nuevo filtro.
- Endpoint `GET /api/calendar` y exportación `GET /api/calendar/ics` utilizan `CalendarGradeReader` server-side.
- `frontend/src/DashboardPage.tsx` (Inicio) usa el mismo `calendarApi.getCalendar`: el resumen de próximas actividades recibe el resultado ya filtrado por la API real. No se modifica UI de Claude.
- Pruebas de unidad matriz 3x3, cargos vigentes/vencidos y evento general; prueba PostgreSQL de fecha de cargo añadida.
- GitHub Actions para HEAD inicial `401649af6b494718dd078ede6a12e83cbe53c3ab`: PMGM CI, Showcase y QA Installable **SUCCESS**. Nuevo commit documental requiere comprobar los gates exact-HEAD de nuevo.

## Dependencia importante (NO cerrada)
- La bandeja de Avisos/Inicio consume `notificationApi.getMine` y `/api/notifications/me`. La PR #512 DRAFT (Issue #510) propone guardia en encolado y reevaluación al leer, apoyándose en este `CalendarGradeReader` para negar mensajes de grado ajeno o revocados.
- #512 tiene CI / Showcase / Instalador SUCCESS para su HEAD `4c970ab6ed5e256b2ebe9eb53907065e5ca53049` pero está basada en un `dev` antiguo; no se integró. Es incorrecto afirmar que Avisos está protegido en `dev` o que #544 cerró integralmente.
- Antes de integración: reconciliar ambas PR con dev vivo, y el manifiesto `.github/backend-integration-shards.json` de #512 con #550; comprobar tests de CalendarNotificationRecipientGuard para instrucciones de grado exacto, bandeja y eventos con cargos; no tocar rama ajena sin coordinación.

## No hacer y siguientes pasos
- No desplegar QA ni modificar `main`, `srv01`, datos institucionales o perfiles.
- Integrar #512 con su puerta de seguridad y luego #549 solo tras rebase, CI exact-head, aprobación de revisiones pertinentes y verificación de avisos. Documentar por SHA en #97 y Drive; la UI queda a cargo de Claude.
- No clasificar G8 total ni G10/UAT por tests CI; QA físico permanece en candidato anterior.
