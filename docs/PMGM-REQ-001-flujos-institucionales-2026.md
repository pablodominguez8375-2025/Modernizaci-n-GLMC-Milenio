# PMGM-REQ-001 — Flujos institucionales derivados de documentación 2026

**Estado:** Base funcional para desarrollo  
**Fecha:** 2026-09-11  
**Rama:** `dev`

## 1. Objetivo
Traducir a requisitos funcionales del Proyecto Milenio los formularios, protocolos y modelos institucionales 2026 incorporados a la carpeta de referencia del Proyecto Centenario.

## 2. Principio de diseño
El sistema será la fuente operativa del dato. Los formularios, extractos, juramentos, planchas y nóminas deberán generarse desde información ya registrada y validada siempre que el proceso lo permita. Debe evitarse solicitar nuevamente información que ya exista en la ficha institucional.

## 3. Insinuaciones
### PMGM-REQ-001.01 — Apertura de insinuación
El flujo debe registrar como mínimo los datos del Formulario de Insinuación 2026: datos personales, fecha de nacimiento, RUT/DNI/pasaporte, domicilio, teléfono, correo, información laboral, Taller que presenta, fecha de ingreso, fecha de presentación en primer grado y secretario/a responsable.

### PMGM-REQ-001.02 — Patrocinio
La insinuación debe registrar un/a maestro/a patrocinante de la R∴L∴.

### PMGM-REQ-001.03 — Presentación en primer grado
Debe quedar registrada la presentación en tenida de primer grado mediante el Saco de Proposiciones y el estado posterior del trámite.

### PMGM-REQ-001.04 — Deliberación previa
El sistema debe poder controlar que hayan transcurrido al menos 7 días antes de la deliberación del Consejo de Administración o Cámara del Medio y registrar la votación abierta correspondiente.

### PMGM-REQ-001.05 — Aprobación inicial
La aprobación para continuar requiere unanimidad de los presentes, conforme al protocolo 2026.

### PMGM-REQ-001.06 — Publicación
Una insinuación aprobada debe poder publicarse en la intranet con los antecedentes requeridos y fotografía JPG/PNG de mínimo 500 × 500 px y máximo 100 kB.

### PMGM-REQ-001.07 — Fecha oficial de ingreso
Debe conservarse la fecha oficial de ingreso del trámite y su trazabilidad.

### PMGM-REQ-001.08 — Permanencia mínima
La insinuación debe permanecer publicada al menos 20 días corridos antes del balotaje de primer grado.

### PMGM-REQ-001.09 — Observaciones y corrección
Una solicitud observada debe volver al Taller para corrección conservando el mismo expediente y su historial; no debe generarse una solicitud nueva para corregir el trámite existente.

## 4. Entrevistas, antecedentes y balotaje
### PMGM-REQ-001.10 — Tres entrevistas
Una vez aprobada la publicación deben poder programarse y registrarse tres entrevistas con la persona insinuada.

### PMGM-REQ-001.11 — Antecedentes confidenciales
El expediente debe admitir el Cuestionario Confidencial y la Autobiografía, con permisos restringidos acordes a la sensibilidad del contenido.

### PMGM-REQ-001.12 — Evaluación en tercer grado
Debe registrarse la presentación de los tres informes, cuestionario y autobiografía en tenida de tercer grado, junto con la votación abierta correspondiente.

### PMGM-REQ-001.13 — Balotaje definitivo
Cumplidos al menos 20 días corridos desde la publicación, debe habilitarse el balotaje de primer grado y registrarse su resultado.

### PMGM-REQ-001.14 — Rechazo y nueva presentación
Cuando corresponda una nueva presentación después de rechazo, debe registrarse la fecha del rechazo anterior, controlarse el plazo mínimo de un año y dejar constancia de la subsanación de las causas que motivaron el rechazo.

## 5. Solicitudes de ceremonias
### PMGM-REQ-001.15 — Tipos
El sistema debe soportar al menos: Iniciación, Afiliación, Aumento de Salario, Exaltación, Incorporación y Otra.

### PMGM-REQ-001.16 — Expediente único
La solicitud de ceremonia debe reutilizar la identidad y antecedentes existentes de la persona, agregando únicamente datos propios del trámite y documentación requerida.

### PMGM-REQ-001.17 — Validaciones institucionales
Antes de emitir autorización deben existir validaciones de las instancias que correspondan, incluyendo Régimen Interior, Gran Tesorería, Gran Hospitalidad y Gran Maestría cuando proceda.

### PMGM-REQ-001.18 — Antecedentes financieros/documentales
El expediente debe admitir, según corresponda: Cuadro del Taller asociado al último pago, comprobante de cuota mensual, derecho de ceremonia, incorporación al Fondo de Defunción, reposiciones del Fondo de Defunción, cuota de Hospitalidad y antecedentes de identidad exigidos.

### PMGM-REQ-001.19 — Estado de autorización
La autorización no puede emitirse mientras exista una validación institucional requerida pendiente o rechazada.

### PMGM-REQ-001.20 — Plancha de autorización
Aprobado el expediente y con visto bueno de Gran Maestría, Gran Secretaría podrá generar la plancha de autorización numerada a partir de una plantilla institucional.

### PMGM-REQ-001.21 — Confirmación posterior
La realización de la ceremonia debe registrarse posteriormente para validación, actualización del historial masónico y posterior publicación institucional cuando corresponda.

## 6. Afiliación e incorporación
### PMGM-REQ-001.22 — Carta de Retiro Voluntario
Las solicitudes de afiliación deben contemplar como antecedente obligatorio la Carta de Retiro Voluntario cuando corresponda y registrar su verificación.

### PMGM-REQ-001.23 — Incorporación desde otra Obediencia
El expediente debe registrar Obediencia de origen, relación de Pacto de Paz y Amistad, logia de origen, grado, fechas de iniciación/aumento/exaltación, retiro y documentos legalizados de procedencia.

### PMGM-REQ-001.24 — Autorización de Gran Maestría
Cuando la Obediencia de origen no mantenga Pacto de Paz y Amistad con la GLMCh, debe existir aprobación explícita de Gran Maestría antes de continuar.

## 7. Aumento de Salario y Exaltación
### PMGM-REQ-001.25 — Requisitos históricos
El sistema debe calcular y mostrar antecedentes de antigüedad, planchas, tenidas e instrucciones/cámaras de instrucción a partir de la historia institucional disponible.

### PMGM-REQ-001.26 — Dispensas
Debe poder registrarse una dispensa otorgada por Cámara del Medio, indicando requisito afectado y reducción autorizada. La existencia de una dispensa nunca debe sobrescribir los datos históricos originales.

## 8. Retiros y membresía
### PMGM-REQ-001.27 — CRV
Debe existir flujo para Carta de Retiro Voluntario con Taller, hermano, RUT, grado, acta, fecha, fundamentos y firmas institucionales requeridas.

### PMGM-REQ-001.28 — CRF
Debe existir flujo para Carta de Retiro Forzoso con los mismos elementos de trazabilidad y fundamentos correspondientes.

### PMGM-REQ-001.29 — Cambio CRF a CRV
Debe poder registrarse el cambio de una CRF anterior a CRV cuando la persona regularice su situación, conservando ambos eventos y el acta que fundamenta el cambio.

## 9. Gran Tesorería
### PMGM-REQ-001.30 — Nómina mensual
La rendición mensual a Gran Tesorería debe poder generarse desde el Cuadro del Taller vigente, separando Maestros, Compañeros y Aprendices cuando corresponda.

### PMGM-REQ-001.31 — Datos por hermano
La nómina debe contemplar RUT, nombre, cargo cuando corresponda, valor de cuota y observaciones autorizadas.

### PMGM-REQ-001.32 — Excepciones de cuota
Deben soportarse valores diferenciados con fundamento/documento, por ejemplo tercera edad, cónyuge, estudiante o Past Activo, sin codificar montos fijos en la aplicación.

### PMGM-REQ-001.33 — Formas de pago y conciliación
La rendición debe registrar transferencias y depósitos, fecha, responsable, monto y diferencia entre total esperado y total pagado.

### PMGM-REQ-001.34 — Bloqueo de inconsistencia
El sistema debe advertir diferencias de conciliación y mantenerlas visibles hasta su regularización.

## 10. Gran Hospitalaria / Fondo de Defunción
### PMGM-REQ-001.35 — Validación de obligaciones
La autorización de ceremonias debe considerar el estado de las obligaciones de Gran Hospitalaria que resulten aplicables al Taller y al trámite.

### PMGM-REQ-001.36 — Voluntad testamentaria
La ficha institucional debe permitir custodiar la Voluntad Testamentaria del Fondo de Defunción con beneficiarios de primera prioridad y beneficiarios subsidiarios, con acceso restringido, historial y auditoría.

## 11. Tenidas y Extracto de Acta
### PMGM-REQ-001.37 — Generación del extracto
El Extracto de Acta debe generarse desde la Tenida y sus registros: asistentes, cuadro por grado, apertura, acta anterior, excusas, correspondencia, decretos, Saco de Proposiciones, balotajes, trabajo presentado, aportes, Bien General, Tronco de Beneficencia, clausura, cierre de cadena y firmas.

### PMGM-REQ-001.38 — Evitar doble digitación
Asistencia, cargos, miembros, resultados de balotaje y demás información ya registrada en el sistema deben incorporarse automáticamente al extracto.

## 12. Uso del Gran Templo
### PMGM-REQ-001.39 — Solicitud de espacio
La solicitud debe registrar Taller, fecha, actividad/ceremonia, horario, requerimientos técnicos, responsable autorizado y requerimientos especiales.

### PMGM-REQ-001.40 — Anticipación
Debe validarse el requisito de solicitud con al menos siete días hábiles de anticipación, salvo excepción institucional autorizada.

### PMGM-REQ-001.41 — Disponibilidad
No debe aprobarse una reserva si existe conflicto de disponibilidad del espacio en el mismo intervalo.

## 13. Juramentos y documentos generados
### PMGM-REQ-001.42 — Juramento de afiliación
Debe poder generarse desde el expediente aprobado utilizando nombre, Taller, número, Oriente y fecha.

### PMGM-REQ-001.43 — Juramentos de oficiales y vigilantes
Debe poder generarse documentación para oficiales y vigilantes vinculada al período de oficialidad correspondiente.

### PMGM-REQ-001.44 — Modelos de plancha
Gran Secretaría debe disponer de plantillas parametrizables para Iniciación, Iniciación de Triángulo patrocinado, Afiliación, Aumento de Salario y Exaltación.

## 14. Seguridad, privacidad y auditoría
### PMGM-REQ-001.45 — Minimización
Los módulos sólo expondrán a cada rol los datos necesarios para la función que desempeña.

### PMGM-REQ-001.46 — Datos sensibles/confidenciales
Autobiografía, cuestionarios, antecedentes personales, documentos de identidad, voluntad testamentaria y antecedentes equivalentes deben tener controles de acceso reforzados, trazabilidad de lectura/descarga cuando corresponda y políticas de retención definidas.

### PMGM-REQ-001.47 — Historial inmutable
Aprobaciones, rechazos, votos registrados cuando corresponda, estados, cambios de membresía, autorizaciones, documentos emitidos y correcciones deben conservar historial auditable; una corrección no debe eliminar el hecho anterior.

## 15. Flujo integrado objetivo
Insinuación → presentación en primer grado → espera reglamentaria → deliberación/aprobación → publicación → entrevistas y antecedentes → evaluación en tercer grado → permanencia mínima publicada → balotaje → solicitud de ceremonia → validaciones institucionales → autorización de Gran Secretaría/Gran Maestría → reserva de espacio cuando corresponda → ceremonia → validación posterior → actualización del historial institucional → documento/boletín/archivo según corresponda.

## 16. Pendientes de definición
- Matriz exacta de estados y transiciones por tipo de trámite.
- Catálogo de montos y vigencias de derechos/cuotas.
- Política detallada de firma electrónica/digital versus firma manuscrita para cada documento.
- Política de retención y eliminación por categoría documental conforme a normativa y gobierno institucional.
- Reglas de notificación, plazos de recordatorio y escalamiento.
