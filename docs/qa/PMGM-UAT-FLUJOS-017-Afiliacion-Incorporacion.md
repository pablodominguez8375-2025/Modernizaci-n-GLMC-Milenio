# PMGM-UAT-FLUJOS-017 — Afiliación e Incorporación 2026

Issue #275 / PR #276. Matriz de aceptación para recuperar el residual de PR #116 sobre el `dev` actual. Todos los datos deben ser sintéticos; no acredita despliegue en `srv01` ni sustituye la aprobación institucional.

| ID | Escenario | Resultado esperado | Evidencia |
|---|---|---|---|
| UAT-017-01 | Crear insinuado como borrador | Persona, solicitud y ficha privada se crean sin fecha ceremonial obligatoria | captura/HTTP/auditoría |
| UAT-017-02 | Completar y enviar expediente | El mismo expediente cambia de estado; no se duplica | ID de expediente y evento |
| UAT-017-03 | Afiliación simple/activación | Exige Carta de Retiro Voluntario y verificación de firma manuscrita cuando aplica | requisitos y auditoría |
| UAT-017-04 | Afiliación estándar | Presentación, comisión, decisión y balotaje respetan cronología | estados y fechas |
| UAT-017-05 | Reintegro | Conserva historia y genera continuidad válida desde el nuevo evento | pertenencias/hitos |
| UAT-017-06 | Traslado | Cierra origen, crea destino con el mismo `MemberId` y conserva historial | origen/destino |
| UAT-017-07 | Incorporación desde otra Obediencia | Valida antecedentes legalizados, Pacto de Paz/Amistad y aprobación especial de Gran Maestría cuando corresponda | requisitos |
| UAT-017-08 | Fecha ceremonial | Sólo aparece al solicitar plancha; cierre de Secretaría exige misma fecha, tipo y Taller | solicitud/plancha/acta |
| UAT-017-09 | Rechazo y reintento | Reintentar materializa el mismo expediente y conserva observaciones | historial |
| UAT-017-10 | Privacidad | Borrador privado no es visible para Gran Secretaría antes del envío | prueba de acceso |
| UAT-017-11 | Permisos | Secretaría, Régimen Interior y Venerable sólo ejecutan acciones autorizadas | matriz de claims |
| UAT-017-12 | Voto secreto | Sólo se conservan totales agregados, nunca votos individuales | payload/auditoría |

## Criterio de aceptación

Todos los casos deben ser PASS, o quedar OBSERVADOS con fundamento y decisión del Sponsor. La ejecución debe identificar SHA exacto, usuario sintético, fecha/hora, endpoint o pantalla, resultado y evidencia. Un check CI verde no equivale a UAT institucional ni a instalación en `srv01`.
