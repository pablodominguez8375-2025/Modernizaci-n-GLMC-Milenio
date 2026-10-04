# Handoff — ChatGPT — identidad nueva de otra Obediencia

Issue #275 / PR draft #276. Reserva previa comentario 5971094465. Base propia `3647bb4db7395d6af02d5348de53b8d832aebbeb`; dev incorporado `14eceafa6b1e014147fd0046f8e5f47cd1a028dc` (Claude #295/UI v0.81), merge automático preservado; main `6dfb9546a4873baff15955cf86abfd7d47e3d111` congelado. Corte previo CI #1848/Showcase #1211/QA #849 SUCCESS sólo como antecedente.

Cambios propios: AdmissionExternalIntake.cs y map en AdmissionEndpoints; AdmissionExternalIntakeHttpTests; ExternalIncorporationDrawer y AdmissionsPage; externalIncorporation.ts/test, integración en pmgmApi y extensión de fixtures lookup. GOV-004: docs/modelo-datos/cambios/2026-10-03-issue-275-alta-externa.md. Sin migración, CSS/App/Authorization ni cambios manuales en reservas Claude.

Persona+expediente+auditoría en transacción compartida Serializable; identificación existente provoca 409 genérico, sin revelar tercero ni crear Member/CeremonyRequest. Grado marca evidencia a acreditar; Pacto null. UI tres pasos conserva borrador al cerrar. Demo en memoria, alcance local parcial.

Frontend 361/361 PASS, lint/build PASS. HTTP real incluye rollback y carrera concurrente, pendiente CI al escribir. No SDK .NET ni Chromium local: no afirmar DB/QA visual nueva hasta evidencia; el panel requiere revisión visual específica posterior. SHA/runs/ZIP SHA-256/MANIFEST/SOURCE_SHA/BUILD_RUN_ID y lecturas GitHub/Drive se completan en recibos del PR.

Limitación de identidad histórica: Person.Rut/identificación hasta 16, sin catálogo passport/país ni validación documental. No truncar extranjeros ni inventar IDs. Normalización no sanea todos los duplicados legados. Reintento 409 no devuelve recibo idempotente. Revisar además duplicación de expedientes en POST reutilización.

Siguiente: evidencia/fecha CRV revisada, modalidad posterior al alta, comisión normativa/autoridades, materialización/traslado idempotente y circuito completo. Después presentación Carga de insinuados/balotaje delegada por Claude, con fuentes leídas. #116 conserva NO FUSIONAR TODAVÍA, #276 draft sin cerrar #275. Sin squash/START-HERE hasta merge funcional; main/srv01/UAT fuera de alcance. Despliegue QA pendiente.
