# PMGM-QA-003 — UAT de flujos institucionales 2026

**Estado:** borrador ejecutable  
**Fecha:** 2026-09-11  
**Base:** PMGM-REQ-001 y PMGM-GAP-001  
**Ambiente:** QA

## Casos críticos

### UAT-001 — Presentación de insinuación
Registrar formulario completo, fotografía válida, patrocinante y presentación en tenida de 1.er grado. Debe crearse un expediente auditable sin permitir saltos de etapa.

### UAT-002 — Espera mínima de 7 días
Intentar deliberar antes de cumplirse 7 días desde la presentación. El backend debe bloquear la operación y explicar el motivo.

### UAT-003 — Unanimidad
Registrar una votación abierta no unánime. No debe habilitarse la publicación.

### UAT-004 — Publicación y plazo de 20 días
Publicar una insinuación aprobada y luego intentar balotaje antes de 20 días corridos. Debe bloquearse indicando días cumplidos y mínimo requerido.

### UAT-005 — Antecedentes previos al 3.er grado
Intentar avanzar con menos de tres entrevistas completas o sin los antecedentes requeridos. Debe quedar bloqueado por expediente incompleto; una cuarta entrevista debe admitirse si la solicita el Venerable Maestro.

### UAT-006 — Reingreso posterior a rechazo
Intentar nueva presentación antes de un año: debe bloquearse. Después de un año, y registradas las causas como subsanadas, debe permitirse una nueva presentación vinculada al historial anterior.

### UAT-007 — Matriz de vistos buenos
Con Régimen Interior aprobado, Gran Tesorería pendiente, Gran Hospitalaria aprobada y Gran Maestría pendiente, la solicitud debe mostrar que no es autorizable y no debe emitir plancha.

### UAT-008 — Autorización completa
Con todos los vistos buenos requeridos conformes, Gran Secretaría debe poder registrar la descripción, adjuntar la Plancha en PDF firmado físicamente y confirmar expresamente sus firmas. Sin cualquiera de esos elementos, la carga debe permanecer bloqueada. El sistema no genera el documento.

### UAT-009 — Afiliación
Una afiliación sin Carta de Retiro Voluntario verificada debe quedar observada o bloqueada según el flujo definido.

### UAT-010 — Incorporación desde otra Obediencia
Si no existe Pacto de Paz y Amistad, debe exigirse aprobación específica de Gran Maestría. Si faltan antecedentes legalizados, el expediente no debe ser autorizable.

### UAT-011 — Aumento de salario / exaltación
Cuando no se cumplen los requisitos ordinarios, el trámite debe quedar bloqueado salvo que exista una dispensa válida registrada por la instancia institucional competente.

### UAT-012 — Reserva del Gran Templo
Solicitudes con menos de 7 días hábiles de anticipación deben rechazarse. Dos solicitudes para el mismo espacio y horario no pueden quedar confirmadas simultáneamente.

### UAT-013 — Cierre de ceremonia
Al registrar una ceremonia realizada, debe actualizarse el historial institucional y generarse la trazabilidad posterior para Gran Secretaría y Régimen Interior.

### UAT-014 — CRV / CRF
CRV y CRF deben registrarse como eventos históricos. Un cambio CRF→CRV agrega un nuevo evento y no elimina el anterior.

### UAT-015 — Gran Tesorería
El cálculo mensual debe admitir cuota normal, excepciones con referencia de plancha y conciliación contra transferencia/depósito. Diferencia distinta de cero debe generar alerta.

### UAT-016 — Fondo de Defunción
Cuando el trámite requiere incorporación o reposiciones, la falta del antecedente correspondiente debe dejar la validación no conforme.

### UAT-FLUJOS-017 — Afiliación, reintegro, traslado e incorporación

**Referencia histórica/corta:** `UAT-017`. El identificador canónico de este catálogo es
`UAT-FLUJOS-017` para evitar colisión con `UAT-RC1-017` (Biblioteca) del catálogo
base de aceptación de la release v1.0.0-rc1.

El flujo debe distinguir modalidad de afiliación `simple/con activación` de la clasificación procedimental `estándar/reintegro/traslado`. Reintegro e incorporación requieren comisión de tres Maestros; un traslado sólo puede obviarla mediante dispensa expresa de Cámara del Medio. La decisión de 3.er grado no debe admitir una conclusión de comisión antigua o posterior. La materialización sólo puede ocurrir cuando la Tenida ceremonial correspondiente esté cerrada con Plancha de Gran Secretaría y Extracto de Acta adjuntos, y un reintento no debe crear una segunda pertenencia ni alterar el actor real auditado.

## Evidencia por ejecución
Cada UAT debe guardar identificador, commit desplegado, actor/rol, datos sintéticos, resultado esperado, resultado obtenido, evidencia y eventual incidencia asociada.

## Gate de promoción
No se promueve a `main` mientras exista un UAT crítico fallido en insinuaciones, autorización de ceremonias, carga y vinculación de Plancha firmada, permisos o trazabilidad histórica.
