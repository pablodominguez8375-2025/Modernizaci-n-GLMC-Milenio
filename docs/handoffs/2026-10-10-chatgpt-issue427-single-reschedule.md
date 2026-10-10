# Handoff — #427 Reprogramación individual de Maestro entrevistador (10-10-2026)

Sponsor aprobó el flujo de 3 Maestros, programación, cambios auditados y avisos privados. Este subincremento crea una edición **de fecha de una sola entrevista** sin volver a designar el equipo ni borrar entregas. Parte desde dev@4706ede7d537ad40fee8996d0e545ce1c4d58de0.

## Reserva
Solo ChatGPT; no modificar archivos de PR #434, especialmente tests Integration y `.github/backend-integration-shards.json`. Archivo backend `CandidateInterviewAssignmentEndpoints.cs`, plantilla/códigos de notificación y migración aditiva, UI `CandidateInterviewAssignmentsPage.tsx`, API `candidateIntakeApi.ts`, nuevo test unitario.

## Criterios de cierre
- Sólo Venerable activo del mismo Taller, expediente publicado, asignación no sustituida ni informe entregado
- Fecha válida posterior al acuerdo y actual/futura; motivo obligatorio de 10–1000 caracteres; auditoría de antigua y nueva fecha, rol, actor
- Notificación `restricted` al Maestro afectado, sin nombre del postulante ni detalles sensibles
- No cambiar otras posiciones, IDs, aceptación personal, o historial
- CI, Showcase, QA SUCCESS exact HEAD, merge sincronizado a dev con GOV-003; main y srv01 intactos
- Documentar seguimiento en Issue #427 y Línea Base Maestra. START-HERE se indexa sólo postmerge.

## Continuación 23:19 UTC
- Base sincronizada a `dev@5da7a3d3ac1e0f82f812a76e208911368b33a411` tras PR #434 y #436. `main@6dfb9546a4873baff15955cf86abfd7d47e3d111` intacta.
- Reserva ampliada a la prueba HTTP positiva ya integrada por #434 y `candidateIntakeApi.test.ts`; no hay otra PR abierta sobre estos tests.
- Reprogramación exige publicación vigente y conserva aceptación. Demo valida fecha real, zona America/Santiago, límite de dos años, fecha del acuerdo y motivo 10–1000 caracteres como API.
- Prueba HTTP comprueba rechazo de Maestro sin cargo, cambio individual, aviso privado al destinatario correcto, reintento sin duplicar aviso/auditoría y aceptación de los tres intacta. Prueba demo cubre cambio de día UTC/Chile, fechas inválidas, aislamiento y bloqueo tras entrega.
- Validación local frontend: 11/11 pruebas focalizadas aprobadas; tsc/lint se registran en PR. .NET no está instalado localmente: backend e integración PostgreSQL deben pasar CI exact-head antes de merge.
- Pendientes de #427: entrega E2E de tres PDF/DOCX y paquete, revisión visual móvil institucional; srv01/QA física/UAT pausadas #97. No cerrar issue por esta PR.
- Aviso idempotente por ID del cambio auditado: regresar a una fecha anterior produce un nuevo aviso; repetir el mismo PATCH no lo duplica. Prueba HTTP A → B → A con reintentos comprueba tres cambios y tres avisos privados. No se agregan columnas ni migraciones de datos por esta corrección.
- Verificación local visual real con Chromium y datos sintéticos a 360, 768 y 1440 px: designar tres Maestros → reprogramar primero → mensaje de éxito, sin desbordamiento horizontal en los tres tamaños. Se corrigió min-width de grids/controles con CSS exclusivo de la página; no cambia tema global ni identidad. Capturas de sesión inspeccionadas, no equivalen a aceptación visual institucional.
- CI del SHA intermedio 10f3a08 falló por analizador xUnit2031 en la prueba añadida; corregido con `Assert.Single(rows, predicate)`. Exigir nuevos gates del SHA final, no reutilizar resultados previos.
