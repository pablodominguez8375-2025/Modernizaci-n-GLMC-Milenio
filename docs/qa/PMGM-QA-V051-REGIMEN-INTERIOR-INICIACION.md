# PMGM QA v0.51 — Aprobación de Régimen Interior para Iniciación

## Resultado

La etapa de Régimen Interior quedó incorporada al circuito de Iniciación. Permite revisar y aprobar el procedimiento, documentos, tenidas, asistencia y balotaje antes de continuar con Gran Tesorería.

## Dependencias y seguridad

- Sólo puede resolverse después del balotaje definitivo aprobado.
- También exige que la solicitud formal de Iniciación haya sido enviada en el mismo expediente.
- Requiere permisos de Régimen Interior o Gran Logia administradora.
- Exige referencia documental; las observaciones se guardan en la validación institucional.
- La operación queda auditada y no duplica fichas ni solicitudes.

## Contrato utilizado

`POST /api/ceremonias/solicitudes/{requestId}/validaciones/regimen-interior`

La pantalla del circuito usa el contrato institucional existente con decisión aprobada, referencia del acuerdo y observaciones internas. El `requestId` sigue siendo el del expediente original.

## Prueba manual

1. Completar balotaje definitivo y solicitud formal.
2. Cambiar al perfil QA de Régimen Interior.
3. Abrir la etapa “Aprobación de Régimen Interior”.
4. Registrar la referencia `ACTA-RI-DEMO-2026-023` y las observaciones.
5. Aprobar y comprobar el avance a Gran Tesorería.
6. Reiniciar y comprobar que el endpoint bloquea la resolución si falta el balotaje o la solicitud formal.

## Verificación

- Frontend: 116 pruebas aprobadas.
- Lint y build TypeScript/Vite correctos.
- Backend: guardas de dependencia y referencia documental incorporadas; ejecución final cubierta por CI .NET.
- Versión visual: `UI QA v0.51`.
