# PMGM QA v0.48 — Revisión de tercer grado

## Objetivo

Hacer operativa la etapa posterior a entrevistas del circuito de Iniciación. La implementación toma como referencia visual y documental el archivo institucional `EXTRACTO-DE-ACTA-2026.docx` y usa únicamente datos ficticios en la demostración pública.

## Regla funcional

- La revisión sólo puede registrarse si ya existe una validación aprobada del paquete de entrevistas y antecedentes.
- La votación es abierta y el sistema conserva únicamente su cómputo agregado: presentes, favorables, desfavorables y abstenciones.
- La suma de las tres categorías debe coincidir con la asistencia registrada.
- El acuerdo consignado puede ser favorable o desfavorable. No se presume ni implementa un umbral reglamentario que no esté definido en la fuente.
- Es obligatoria una referencia documental de hasta 240 caracteres al extracto de acta.
- No se almacena la relación entre una persona y su voto, ni se vuelve a copiar el contenido de entrevistas, cuestionario o autobiografía.

## Correspondencia con el extracto 2026

La referencia de demostración incluye Logia, número, tenida, grado y número de extracto. La fecha y la asistencia se capturan como datos estructurados. El documento institucional también contempla Oriente, cuadro de asistencia, balotajes y firmas; esos campos permanecen en el extracto oficial y no se reproducen innecesariamente en esta etapa.

## Contrato técnico

`POST /api/insinuados/solicitudes/{requestId}/revision-tercer-grado`

Campos: `reviewDate`, `presentVoters`, `votesInFavor`, `votesAgainst`, `abstentions`, `openVoteApproved` y `sourceReference`.

El backend verifica autorización sobre el Taller, estado de la solicitud, fecha no futura, publicación previa, paquete de antecedentes aprobado, referencia obligatoria y consistencia aritmética. La auditoría registra sólo cantidades agregadas y el resultado.

## Prueba manual de la demo

1. Reiniciar el caso de prueba y completar ingreso, deliberación inicial y publicación.
2. Cargar Word o PDF en al menos tres entrevistas y validar sus antecedentes.
3. Abrir “Revisión en 3.er grado”.
4. Registrar fecha, asistencia, cómputo, acuerdo y referencia del extracto.
5. Confirmar que un cómputo inconsistente impide avanzar.
6. Confirmar que un acuerdo favorable cierra la etapa y habilita el balotaje; uno desfavorable detiene el expediente.

## Verificación automatizada

- Frontend: 24 archivos y 112 pruebas.
- `oxlint`: sin errores.
- Compilación TypeScript/Vite: correcta.
- Backend: se añadieron pruebas unitarias de cómputo consistente y resultado desfavorable; su ejecución queda cubierta por CI con .NET.

Versión visual: `UI QA v0.48`.
