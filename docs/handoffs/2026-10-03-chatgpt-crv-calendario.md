# Handoff — CRV, meses calendario — ChatGPT

Issue #275 / PR draft #276; #116 conserva NO FUSIONAR TODAVÍA. Se continúa la rama propia sin copiar ramas históricas.

Inicio: rama `450171df4a4abde7a23a83039a08373072aa7aab`; dev `68bef4f4559fdfa0ea6ea617718a65e3573bd3a0`, luego avanzó a `b070dbc71d25f811f0b70999dab8f378a5343cc8`; main `6dfb9546a4873baff15955cf86abfd7d47e3d111`. No merge en esta intervención.

Corrección: cálculo frontend CRV alineado con meses calendario de DateOnly.AddMonths(3), ajuste al último día del mes, fecha civil America/Santiago, límite inclusive y rechazo de fechas futuras/inválidas. 13 pruebas de comportamiento y 2 contractuales iniciales PASS; lint/build y suite completa se registran con resultado exacto en recibo del PR. Archivos: admissionDates.ts, admissionDates.test.ts, AdmissionsPage.tsx y dos documentos nuevos. No App, CSS ni migraciones nuevas; reservas Claude preservadas.

Salida de datos: [registro CRV](../modelo-datos/cambios/2026-10-03-issue-275-fecha-carta-retiro.md), cobertura parcial declarada. Los campos añadidos previamente quedan documentados sin rellenar antecedentes legacy.

Drive revisado: Línea Base y listado de novedades desde su último corte; adenda Claude #287 confirma patrón vista operativa aprobado y lema sin cambio. Novedades listadas de logo/responsive/densidad/avisos son UI, no autorización para cambiar normativa; no se implementan propuestas desde títulos. PR #289 presenta siguiente entrega UI; dev cambió durante revisión. Recibo de esta intervención y lectura de retorno Drive deben verificarse antes de declararla entregada.

Pendientes principales: actualizar base viva y gates propios; backend pruebas CRV; demo equivalente; búsqueda pertinente para afiliación/incorporación; concordancia fecha/evidencia; autorización normativa de comisión; materialización/traslado e idempotencia y evidencia automatizada del circuito. No resolver silenciosamente cuándo evaluar modalidad después del alta. Lista detallada en registro de datos.

CI #1838 y QA #836 SUCCESS pertenecen al SHA anterior 450171d; Showcase #1198 estaba en curso al iniciar. No certifican este nuevo commit. No paquete descargado/verificado en esta intervención; SHA-256/MANIFEST/SOURCE_SHA/BUILD_RUN_ID pendientes del corte final. No afirmar Pages actualizada con esta rama.

Main sin cambios; srv01 pausado; despliegue QA pendiente, sin QA física/UAT. PR documental START-HERE (+1 línea) únicamente después del merge. SHA final y gates vivos se dejan en recibo GitHub/Drive, no se incrusta hash autorreferencial.
