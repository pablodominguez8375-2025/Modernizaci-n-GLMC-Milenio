# PMGM QA v0.52 — Bloqueo transversal por rechazo en Cámara del Medio

## Regla institucional

Un rechazo en la revisión de tercer grado (Cámara del Medio) deja un antecedente bloqueante asociado a la persona en toda la Orden. El historial del Taller que rechazó no se elimina ni se reemplaza.

## Alertas implementadas

- Secretaría de un Taller ve una alerta al abrir una ficha del candidato cuando existe un rechazo previo en otro Taller.
- La alerta indica Taller, fecha, motivo general y que Régimen Interior debe consultar el antecedente antes de una nueva presentación.
- Régimen Interior dispone de una consulta transversal restringida con candidato, Taller de origen, fecha, referencia de acta y observaciones.
- El endpoint responde con `Cache-Control: private, no-store` y requiere ámbito de Orden más rol de Régimen Interior o Gran Logia administradora.

## Contrato

`GET /api/insinuados/regimen-interior/alertas-rechazo`

La consulta devuelve sólo antecedentes de rechazos `candidate_third_degree_review` y no incluye el contenido privado de entrevistas, cuestionarios o autobiografías.

## Nueva presentación

El sistema puede mostrar la alerta y permitir que el Taller prepare una nueva ficha, pero la decisión queda bajo revisión de Régimen Interior. El expediente anterior permanece consultable y cualquier reapertura futura deberá respetar la regla institucional de plazo y subsanación.

## Verificación

- Frontend: 117 pruebas aprobadas.
- Lint y build TypeScript/Vite correctos.
- CI valida la consulta transversal y el filtro por rol.
- Versión visual: `UI QA v0.52`.
