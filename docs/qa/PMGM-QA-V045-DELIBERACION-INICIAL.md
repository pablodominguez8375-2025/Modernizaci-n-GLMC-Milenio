# PMGM QA v0.45 — Deliberación inicial funcional

## Alcance

La etapa «Presentación y deliberación inicial» del expediente de Iniciación deja de ser un avance genérico. La demo y la instalación consumen el mismo contrato funcional para registrar fecha, asambleístas presentes, votos favorables y referencia del acta o extracto.

## Reglas verificadas

- Deben transcurrir al menos siete días completos desde la presentación.
- Debe existir al menos un asambleísta habilitado presente.
- Los votos favorables deben encontrarse entre cero y el total de presentes.
- La aprobación requiere unanimidad.
- La referencia del acta o extracto es obligatoria y admite hasta 240 caracteres.
- Una observación o rechazo no permite avanzar el expediente.
- La decisión, su motivo y el respaldo documental quedan incorporados a la trazabilidad.

## Escenarios de aceptación

| Escenario | Entrada | Resultado esperado |
|---|---|---|
| Plazo incompleto | Día 6, 12 de 12 votos | Observado por espera mínima |
| Sin unanimidad | Día 7, 11 de 12 votos | Rechazado |
| Conforme | Día 7, 12 de 12 votos, acta informada | Aprobado y habilita publicación |
| Sin respaldo | Referencia de acta vacía | Solicitud inválida |

## Prueba manual en la demo

1. Abrir «Circuito de Iniciación» y reiniciar el caso de prueba.
2. Registrar «Ingreso del insinuado» con perfil Taller.
3. En «Presentación y deliberación inicial», modificar fecha, presentes, votos y respaldo.
4. Comprobar que el día 6 queda observado y que 11 de 12 queda rechazado.
5. Reiniciar, repetir y registrar día 7 o posterior con unanimidad para avanzar.

Todos los nombres, identificadores y documentos usados en la demostración son ficticios.
