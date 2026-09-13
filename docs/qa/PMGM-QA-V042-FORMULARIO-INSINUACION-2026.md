# PMGM QA v0.42 — Formulario de Insinuación 2026

## Objetivo

Validar que el flujo de iniciación mantenga un único expediente por solicitud y permita a Secretaría Logial completar los antecedentes definidos por el **Formulario de Insinuación 2026**, antes de la revisión y publicación controlada por Gran Secretaría.

## Alcance funcional incorporado

- Datos personales y de contacto del insinuado.
- Antecedentes laborales: actividad/profesión, empleador, dirección, cargo y teléfono laboral.
- Antecedentes logiales: Taller, Oriente, presentantes, fecha de ingreso, presentación en primer grado y secretario responsable.
- Fotografía privada JPEG/PNG proveniente del gestor documental, con análisis antivirus concluido y límite de 100 KB.
- Corrección mediante actualización de la ficha existente; no se crea una segunda ficha para la misma solicitud de iniciación.
- Cada corrección vuelve a dejar el expediente pendiente de Gran Secretaría y genera una nueva evidencia de validación y auditoría.
- La publicación para Hermanos sólo ocurre después de la aprobación explícita de Gran Secretaría.

## Trazabilidad del flujo

1. Existe una solicitud de ceremonia de tipo iniciación dentro del ámbito del Taller.
2. Secretaría Logial abre la solicitud y completa o corrige su ficha privada.
3. La API actualiza por `CeremonyRequestId`, clave única del expediente.
4. El sistema registra el cambio, conserva fechas y autor, y crea estado `pending_grand_secretariat`.
5. Gran Secretaría revisa, observa/rechaza o aprueba y publica.
6. Una observación permite corregir y reenviar el mismo expediente.

## Reglas verificadas

| Regla | Resultado |
|---|---|
| Sólo el Taller del ámbito puede crear o corregir | Implementada |
| Solicitud cerrada no admite cambios | Implementada |
| Uno a ocho presentantes | Implementada |
| Fecha de ingreso no futura | Implementada |
| Presentación en primer grado no futura ni anterior al ingreso | Implementada |
| Fotografía sólo JPEG/PNG, procesada y del mismo Taller | Implementada |
| Fotografía máximo 100 KB | Implementada |
| Fotografía mínimo 500 × 500 píxeles | Implementada en QA v0.43 |
| Corrección conserva la identidad del expediente | Implementada por restricción única y operación upsert |
| Guardar/corregir no publica automáticamente | Implementada |

## Evidencia automática

- Lint del frontend: aprobado.
- Pruebas frontend: 24 archivos, 103 pruebas aprobadas.
- Compilación de producción del frontend: aprobada.
- Compilación y pruebas del backend: ejecutadas por CI de la rama de avance.

## Datos de demostración

La demostración usa exclusivamente identidades, contactos, empleadores, Talleres y documentos ficticios. No contiene datos personales reales ni material aportado en los documentos de referencia.

## Criterio de salida

El incremento se considera apto para demostración cuando CI, despliegue de showcase y generación del instalable finalicen correctamente en `feature/treasury-payment-table-v1`. La validación de dimensiones fue cerrada en QA v0.43.
