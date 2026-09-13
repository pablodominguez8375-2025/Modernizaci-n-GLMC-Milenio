# PMGM QA v0.44 — Control de completitud de la insinuación

## Resultado

Gran Secretaría dispone de un control previo a la publicación que calcula la completitud del Formulario de Insinuación 2026, identifica los antecedentes faltantes y bloquea la aprobación hasta su corrección.

## Principio de expediente único

La observación no genera otra ficha. Secretaría Logial corrige mediante la misma solicitud de iniciación y el mismo `CeremonyRequestId`; el sistema conserva la identidad del expediente, actualiza su trazabilidad y lo devuelve a estado pendiente de revisión.

## Controles obligatorios

- Identidad: nombres, apellido paterno, RUT/identificación, nacimiento, nacionalidad y estado civil.
- Contacto: teléfono, correo, dirección y ciudad.
- Laborales: actividad/profesión, empleador, dirección, cargo y teléfono laboral.
- Logiales: Oriente, presentantes, presentación en primer grado y secretario responsable.
- Fotografía tipo pasaporte validada y vinculada.

La fecha de ingreso es obligatoria desde la creación de la ficha y, por su tipo no anulable, siempre forma parte del expediente.

## Defensa en profundidad

1. La pantalla muestra porcentaje y campos pendientes.
2. El botón **Aprobar y publicar** se deshabilita mientras falte información.
3. La API recalcula la completitud con datos persistidos y responde `409 Conflict` ante un intento directo incompleto.
4. Las publicaciones ya existentes conservan comportamiento idempotente y no se invalidan retroactivamente.

## Criterios QA

- Una ficha completa informa 100 % y habilita resolución.
- Una ficha incompleta enumera cada antecedente faltante.
- Una ficha incompleta no puede publicarse desde interfaz ni API.
- Observar exige fundamento.
- Corregir actualiza la ficha existente y genera una nueva evidencia auditada de revisión pendiente.
