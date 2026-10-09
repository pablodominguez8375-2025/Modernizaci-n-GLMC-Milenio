# Issue #47 — Corrección de duplicados de Extracto de Tenida

- Estado: implementación realizada en rama; integración condicionada a gates exact-head, rama `fix/advancement-workpaper-extract-dedupe-20261009-gpt`, base `dev@62049891eb9bdedc1c0adeab8d2ed9747969b471`.
- Alcance: el enlace de versión documental con una Tenida celebrada puede aparecer varias veces por registros administrativos. Actualmente `AttachMeetingLinks` agrupa por `MeetingId` y usa el primer registro: el indicador de Extracto remitido depende del orden de la base.
- Corrección propuesta: consolidar por Tenida sólo los vínculos válidos a la misma versión y conservar estado `ExtractSubmitted` cuando **alguno** de esos vínculos esté remitido/recibido. Mantener IDs únicos y ordenados.
- Verificación: tests unitarios con órdenes opuestos y registros inválidos; CI, Showcase y QA exact-head antes de integración.
- Restricción de seguridad: **no** atribuir presentación ni autorizar ceremonia; `ConfirmedPresented=0`, `PresentationEvidenceAvailable=false` y `authorizesCeremony=false` permanecen invariantes. Issue #47 sigue abierto hasta confirmación normativa.
- `main`, `srv01` y UAT permanecen intactos.

## Cambio implementado

- `AdvancementWorkPaperReviewPolicy.AttachMeetingLinks` agrupa los vínculos válidos por ID de Tenida y realiza `Any(link => link.ExtractSubmitted)` dentro de cada grupo, evitando que un registro pendiente o la falta de orden de una consulta oculte uno remitido.
- `AdvancementWorkPaperReviewPolicyTests.DuplicateMeetingLinks_RetainSubmittedExtractRegardlessOfOrdering` prueba las dos permutaciones, ignora versión distinta y comprueba que la presentación permanece no acreditada.
- El mensaje de revisión distingue mejor evidencia administrativa y constancia formal de presentación.
- CI/Showcase/QA: verificar para HEAD final del PR; no presumirlos aprobados. La demo no cambia porque es una ruta privada de diagnóstico de backend.
- Pendientes principales (#47/#48): norma vigente de antigüedad, mecanismo institucional de acreditación de planchas y dispensa verificable/versionada.
