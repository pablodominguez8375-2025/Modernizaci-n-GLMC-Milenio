# PMGM QA v0.50 — Solicitud formal de Iniciación

## Resultado

La etapa “Solicitud de Iniciación” ahora envía formalmente el expediente existente después del balotaje favorable. No crea una segunda ficha, persona ni solicitud paralela.

## Datos que se registran

- fecha de solicitud;
- fecha propuesta de ceremonia;
- confirmación del Venerable Maestro;
- Secretaría responsable;
- referencia documental.

La identidad, Taller, publicación, entrevistas, antecedentes y votaciones se reutilizan mediante el mismo `CeremonyRequestId`.

## Controles

El backend exige balotaje definitivo aprobado, fecha válida, confirmación del Venerable, Secretaría y referencia documental. El envío es idempotente: un segundo intento devuelve la validación existente y no duplica el expediente. La operación deja auditoría append-only.

Endpoint: `POST /api/insinuados/solicitudes/{requestId}/solicitud-iniciacion`.

## Verificación

- Frontend: 116 pruebas, incluyendo contrato mock y HTTP.
- Lint y build TypeScript/Vite correctos.
- Pruebas backend de reglas de fecha, roles y solicitud completa.
- Versión visual: `UI QA v0.50`.
