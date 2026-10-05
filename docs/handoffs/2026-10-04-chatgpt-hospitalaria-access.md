# Permisos efectivos — Hospitalaria del Taller

Issue #266. Base dev a07aab8e37a8998812850de61e0b80a5503f69cd; main 6dfb9546a4873baff15955cf86abfd7d47e3d111. Revisión viva de GitHub/Drive antes de programar: sólo la Línea Base modificada por el cierre anterior #320/#321; sin nueva regla institucional. Lectura nativa actual confirmada, historial preservado.

Alcance: intersección de autoridad institucional y hospitalaria/view,create,write por sujeto/Taller para movimientos, autorizaciones, rendiciones, reposiciones/pagos/transferencias locales. Proyección propia mínima no-store, controles de pantalla y adaptador demo coherentes. Sin migración, tarifa ni nueva atribución. Gran Hospitalaria y regularidad institucional fuera de alcance; navegación global pendiente por reserva caliente App.tsx del PR histórico #116, que permanece NO FUSIONAR TODAVÍA.

Reserva: backend/src/PMGM.Api/Modules/Authorization/DynamicHospitalariaAccess.cs; Modules/Treasury/LodgeHospitalariaEndpoints.cs; Modules/Hospitalaria/HospitalariaEndpoints.cs; tests Authorization/Integration nuevos; frontend/src/HospitalariaPage.tsx y test; useHospitalariaAccess.ts; api/dynamicAccess.ts, pmgmApi.ts y nuevo test; docs/modelo-datos/DB-009 y registro nuevo; este handoff. Cruces no calientes con #116 se informan en PR. Ninguna rama ajena ni archivo caliente se modifica.

Estado: reclamo previo a implementación. Estimación: un corte técnico, gates CI/Showcase/QA exact-head, squash autorizado a dev y PR documental posterior. Evidencia final se completa en recibos GitHub/Drive, sin certificar un gate todavía no ejecutado. Despliegue QA pendiente; main/srv01/UAT pausados. #266 sigue abierta para módulos restantes; #66 requiere UAT institucional y permanece abierta.
