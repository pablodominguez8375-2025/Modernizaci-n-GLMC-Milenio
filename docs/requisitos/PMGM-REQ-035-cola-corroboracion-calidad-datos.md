# PMGM-REQ-035 — Cola de corroboración de calidad de datos

**Versión objetivo:** v0.28  
**Módulo:** Régimen Interior  
**Estado:** Implementado en rama / pendiente de CI y merge

## Objetivo

Permitir que Régimen Interior convierta un hallazgo vigente de calidad de datos en un caso formal de corroboración, asignable y trazable, sin que la detección ni la resolución modifiquen automáticamente los datos históricos que originaron la observación.

## Alcance funcional

La solución debe permitir:

1. abrir un caso exclusivamente desde un hallazgo que siga vigente al momento de la solicitud;
2. evitar más de un caso activo para la misma regla, miembro y ámbito organizacional;
3. listar casos por estado, regla, Taller y asignación al usuario actual;
4. tomar un caso abierto para revisión;
5. impedir que dos revisores tomen simultáneamente el mismo caso;
6. resolver un caso en revisión como `resolved_confirmed` o `dismissed`;
7. exigir una descripción breve de la resolución y permitir una referencia de respaldo;
8. conservar un historial de transiciones del caso;
9. permitir que un mismo hallazgo vuelva a originar un caso futuro sólo después de haber cerrado el caso activo anterior;
10. mantener separados el workflow de corroboración y las fuentes maestras de Membresía/Régimen Interior.

## Estados

- `open`: caso abierto, aún sin responsable.
- `under_review`: caso tomado por un revisor.
- `resolved_confirmed`: hallazgo corroborado y cerrado.
- `dismissed`: hallazgo descartado y cerrado.

Transiciones admitidas:

`open → under_review → resolved_confirmed`

`open → under_review → dismissed`

No existe una transición que corrija automáticamente el dato fuente.

## Reglas de seguridad y concurrencia

- Sólo usuarios con `CanRunRegimenInteriorReports` pueden acceder a la cola.
- La toma del caso debe realizarse con actualización condicional atómica para impedir doble asignación concurrente.
- Una resolución normal sólo puede ser registrada por el revisor asignado.
- `grand_lodge_admin` puede resolver con override administrativo, manteniendo trazabilidad del actor.
- Las respuestas usan `Cache-Control: private, no-store`.
- El frontend usa Bearer token, `credentials: omit`, `redirect: error` y same-origin institucional.

## Minimización y Ley 21.719

La finalidad queda comprendida en `PMGM-DPA-002 — Régimen Interior`: reportes, consistencia histórica y apoyo a decisiones autorizadas.

El caso persiste únicamente los datos necesarios para el workflow:

- huella técnica opaca del hallazgo;
- código de regla y severidad;
- referencia al miembro y, cuando corresponda, a la organización;
- fecha de corte y fechas mínimas relacionadas con la inconsistencia;
- estado y responsables técnicos del workflow;
- resumen de resolución;
- referencia al respaldo, no una copia del documento;
- historial de transiciones.

La proyección API no expone los identificadores técnicos `sub` de los actores. Sólo entrega nombres visibles autorizados y un booleano `assignedToCurrentUser` calculado por el servidor.

No se persiste una copia del nombre, correo, teléfono, dirección, notas privadas, razones de retiro, contenido de actas/decretos ni datos de Hospitalaria individual dentro del caso.

Catálogo de clasificación: `PMGM-DATA-CLASSIFICATION-DATA-QUALITY-CASES.json`.

## Criterios de aceptación

1. Un hallazgo inexistente o ya corregido no puede abrir un caso.
2. Dos aperturas equivalentes devuelven el mismo caso activo.
3. Dos revisores no pueden adjudicarse simultáneamente el mismo caso.
4. Un caso cerrado no puede volver a tomarse ni resolverse.
5. Un revisor no puede resolver el caso de otro revisor, salvo override de Gran Logia Admin.
6. Cada transición genera un evento de historial.
7. Resolver no altera tablas de afiliación, grados, estados, cargos ni traslados.
8. La referencia de respaldo tiene máximo 500 caracteres y la resolución entre 10 y 2000 caracteres.
9. Los identificadores técnicos de actores no aparecen en la proyección JSON al frontend.
10. El workflow funciona en PostgreSQL y en el showcase QA con datos ficticios.
11. Los gates de Ley 21.719, clasificación y migración deben quedar verdes antes del merge.
12. Backend, frontend, PostgreSQL, S3, ClamAV e infraestructura deben quedar verdes sobre el mismo head del PR.
