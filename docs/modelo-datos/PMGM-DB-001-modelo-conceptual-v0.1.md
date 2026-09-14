# PMGM-DB-001 — Modelo Conceptual de Datos v0.3

## Principio
El Proyecto Milenio utilizará una base de datos institucional única y normalizada. Las áreas no deben mantener planillas paralelas como fuente maestra.

El modelo debe permitir reconstruir la situación de un miembro, un trámite, un documento oficial y una reserva de espacio en una fecha determinada, sin depender únicamente de su estado actual.

## Entidad central: Miembro
Atributos conceptuales mínimos:
- Identificador interno UUID.
- Persona asociada.
- Estado institucional actual derivado de su historial.
- Taller principal vigente y relaciones históricas.
- Grado actual derivado del historial de grados.
- Condición Past / ex autoridad cuando corresponda a la nomenclatura institucional.
- Referencias a datos administrativos y documentales relacionados.

Los datos personales de nombre, contacto e identificación legal pertenecen a la entidad Persona y no deben duplicarse en Miembro.

## Entidades relacionadas

### Persona
- Identificador técnico UUID.
- Nombres y apellidos.
- Datos de contacto.
- Identificación legal cuando corresponda.
- Datos personales sujetos a políticas de privacidad.

### Taller / Organización
- Identificador UUID.
- Nombre y número.
- Oriente / ubicación.
- Tipo y jerarquía institucional.
- Estado.
- Autoridades por período.

### Pertenencia de miembro
Representa la relación histórica entre un miembro y un Taller.
- Miembro.
- Taller.
- Tipo de pertenencia.
- Fecha de inicio.
- Fecha de término.
- Estado.
- Motivo de término cuando corresponda.
- Evidencia/documento asociado.

No debe sobrescribirse una pertenencia anterior al cambiar de Taller.

### Hito de estado institucional
Registra cambios relevantes en la situación institucional del miembro.
- Miembro.
- Tipo de hito: alta, activo, inactivo, retiro voluntario, retiro forzoso, reintegro, defunción/Oriente Eterno u otro configurable.
- Fecha efectiva.
- Fecha de registro.
- Motivo.
- Taller o instancia relacionada cuando corresponda.
- Autoridad o usuario que registra.
- Evidencia/documento asociado.
- Observaciones.

El estado actual debe poder derivarse o validarse contra estos hitos.

### Grado e historial masónico
- Miembro.
- Grado.
- Fecha efectiva.
- Taller.
- Tipo de hito, incluyendo iniciación, aumento de salario y exaltación.
- Evidencia/documento asociado.
- Fecha de registro.
- Usuario que registra/corrige.

Una corrección no elimina el antecedente auditado del valor anterior.

### Cargo
- Miembro.
- Tipo de cargo.
- Taller o estructura institucional.
- Período.
- Fecha inicio y término.
- Condición vigente/histórica.
- Evidencia o acto de nombramiento cuando corresponda.

### Condición Past / ex autoridad
Cuando la institución utilice formalmente esta clasificación deberá modelarse como condición derivable del historial de cargos o como clasificación institucional parametrizable, evitando un campo manual aislado que pueda quedar inconsistente.

### Estado administrativo / financiero
Régimen Interior requiere consultar regularidad administrativa sin duplicar la contabilidad.
- Miembro.
- Taller o alcance institucional.
- Estado calculado: al día, moroso, pendiente, exento/no aplica u otro parametrizable.
- Fecha de corte.
- Fuente del cálculo: Tesorería.
- Referencia al cálculo o período.

Los saldos y movimientos permanecen en Tesorería; esta entidad representa una vista/resultado de regularidad para consumo autorizado.

### Movimiento de tesorería
- Tipo.
- Miembro/Taller relacionado.
- Concepto.
- Monto.
- Fecha.
- Comprobante.
- Período.

### Tenida
- Taller.
- Fecha.
- Grado.
- Tipo.
- Acta.
- Plancha.
- Estado.

### Asistencia
- Tenida.
- Miembro.
- Estado de asistencia.
- Excusa.
- Observaciones.

### Solicitud / Trámite
- Tipo.
- Solicitante.
- Miembro beneficiario o relacionado cuando corresponda.
- Taller.
- Responsable.
- Estado.
- Flujo de aprobación.
- Resolución.
- Historial.
- Fecha de creación.
- Fecha de cierre.

### Validación de trámite
Representa una aprobación u observación emitida por un área institucional.
- Solicitud/trámite.
- Área validadora: Régimen Interior, Gran Tesorería, Gran Secretaría u otra configurable.
- Tipo de validación.
- Resultado: aprobado, observado, rechazado, exento/no aplica u otro configurable.
- Fecha/hora.
- Responsable/autoridad.
- Fundamento u observaciones.
- Fecha de corte cuando dependa de información financiera o temporal.
- Evidencia/documento asociado.
- Versión de reglas o criterios aplicados cuando corresponda.

La validación debe quedar congelada como evidencia de la decisión utilizada por etapas posteriores del flujo.

### Ceremonia
- Solicitud asociada.
- Tipo de ceremonia.
- Miembro.
- Taller.
- Grado relacionado.
- Fecha solicitada/prevista.
- Estado.
- Autorización.
- Fecha de realización cuando corresponda.
- Espacio reservado cuando corresponda.
- Documentos asociados.

### Reconocimiento / Distinción
- Miembro.
- Tipo.
- Fecha propuesta.
- Fecha otorgada.
- Estado.
- Autoridad otorgante.
- Fundamento.
- Evidencias y documentos.

### Regla de elegibilidad
Define condiciones institucionales parametrizables para decisiones asistidas.
- Tipo de trámite/ceremonia/reconocimiento.
- Regla.
- Vigencia desde/hasta.
- Severidad: requisito, observación o informativa.
- Parámetros.
- Versión.

### Evaluación de elegibilidad
Conserva qué se evaluó al momento de una decisión.
- Solicitud o trámite.
- Miembro.
- Fecha/hora de evaluación.
- Versión de reglas aplicada.
- Resultado: Cumple, Observado, No cumple.
- Detalle de reglas evaluadas.
- Antecedentes utilizados.
- Observaciones.

La evaluación no equivale a autorización.

### Decisión / Resolución
- Solicitud o trámite.
- Autoridad que decide.
- Fecha/hora.
- Resolución: autorizado, rechazado, excepción u otros configurables.
- Fundamento.
- Evaluación de elegibilidad considerada.
- Validaciones consideradas.
- Documentos asociados.

### Alerta de consistencia
Representa una discrepancia detectada sin modificar automáticamente los datos.
- Miembro o entidad afectada.
- Tipo de inconsistencia.
- Severidad.
- Fecha de detección.
- Detalle.
- Estado: pendiente, en revisión, corregida, justificada.
- Responsable de revisión.
- Resolución.

Ejemplos: exaltación anterior a aumento de salario, reintegro sin retiro previo, miembro activo con defunción registrada o pertenencias incompatibles.

## Gran Secretaría y documentos oficiales

### Documento oficial
Representa un acto/documento emitido formalmente por la institución.
- UUID.
- Tipo: decreto, comunicado, circular, autorización de ceremonia, autorización de espacio u otro configurable.
- Serie documental.
- Número/correlativo oficial.
- Año/período.
- Fecha de emisión.
- Asunto/título.
- Contenido o referencia al contenido versionado.
- Autoridad emisora.
- Responsable de redacción.
- Estado: borrador, revisión, aprobado, emitido, anulado, reemplazado u otro configurable.
- Clasificación/confidencialidad.
- Solicitud/trámite relacionado cuando corresponda.
- Miembro(s) y Taller(es) relacionados cuando corresponda.
- Plantilla y versión de plantilla utilizadas.
- Documento reemplazado o reemplazante cuando aplique.
- Motivo de anulación/reemplazo.
- Fecha/hora de emisión.
- Firma/validación institucional cuando corresponda.
- Hash de integridad.

Un documento emitido no debe modificarse destructivamente; una corrección genera una nueva versión o documento de reemplazo según política.

### Serie / correlativo documental
Controla la numeración oficial.
- Tipo/serie.
- Prefijo/sufijo.
- Período de vigencia.
- Último correlativo asignado.
- Regla de incremento.
- Estado.
- Política de reserva/asignación de número.

Un correlativo asignado a un documento emitido o anulado no puede reutilizarse.

### Plantilla institucional
- UUID.
- Tipo de documento.
- Nombre.
- Versión.
- Vigencia desde/hasta.
- Contenido/estructura.
- Campos automáticos disponibles.
- Clasificación.
- Estado.
- Responsable de aprobación.

La emisión conserva referencia exacta a la versión de plantilla utilizada.

### Distribución de documento
- Documento oficial.
- Destinatario individual, Taller, cargo, grupo o lista institucional.
- Canal de entrega.
- Fecha/hora de envío.
- Estado de entrega.
- Fecha/hora de lectura/acuse cuando aplique.
- Error de entrega cuando exista.

### Autorización formal de ceremonia
Puede representarse como Documento Oficial vinculado a la Ceremonia y Solicitud.
Debe conservar:
- validación aprobada de Régimen Interior;
- validación aprobada de Gran Tesorería cuando corresponda;
- otras validaciones obligatorias;
- excepción formal si alguna regla permite continuar;
- fecha y lugar autorizados;
- miembro(s) involucrado(s);
- Taller;
- grado/ceremonia;
- autoridad emisora.

La emisión debe congelar las referencias exactas de las validaciones utilizadas.

## Gestión de espacios

### Espacio institucional
- UUID.
- Nombre.
- Tipo: templo, sala de Secretaría, sala de reuniones u otro configurable.
- Ubicación.
- Capacidad.
- Condiciones/restricciones de uso.
- Equipamiento relevante.
- Horarios habilitados.
- Estado: disponible, mantenimiento, bloqueado, fuera de servicio u otro configurable.
- Responsable/administrador.

### Bloqueo de espacio
- Espacio.
- Fecha/hora de inicio y término.
- Tipo: mantenimiento, indisponibilidad administrativa, evento institucional u otro.
- Motivo.
- Responsable.
- Estado.

### Reserva de espacio
- UUID.
- Espacio.
- Taller/unidad solicitante.
- Responsable solicitante.
- Solicitud/trámite relacionado cuando corresponda.
- Ceremonia/reunión relacionada cuando corresponda.
- Tipo de actividad.
- Fecha/hora de inicio y término solicitadas.
- Fecha/hora autorizadas.
- Asistentes estimados.
- Requerimientos especiales.
- Estado: solicitada, observada, aprobada, rechazada, cancelada, reprogramada u otro configurable.
- Autoridad/responsable que resuelve.
- Fecha/hora de resolución.
- Motivo/observaciones.

Debe existir una restricción de negocio que impida dos reservas confirmadas del mismo espacio con intervalos temporales solapados.

### Historial de reserva
- Reserva.
- Estado anterior/nuevo.
- Fecha/hora.
- Actor.
- Fecha/hora anterior/nueva cuando exista reprogramación.
- Motivo.

## Gestor documental general

### Documento
- Tipo.
- Propietario lógico.
- Clasificación.
- Versiones.
- Permisos.
- Metadatos.
- Hash de integridad.

Documento Oficial reutiliza el núcleo documental, pero agrega reglas institucionales de emisión, correlativo, autoridad y no alteración destructiva.

### Rol y permiso
- Usuario.
- Rol.
- Alcance.
- Permisos efectivos.

### Auditoría
- Usuario.
- Fecha/hora.
- Entidad.
- Operación.
- Datos anteriores.
- Datos nuevos.
- Motivo.
- Contexto técnico.
- Correlation ID.

## Vistas/reportes derivados relevantes
El modelo debe soportar consultas derivadas sin crear tablas maestras duplicadas para:
- total de miembros;
- activos/inactivos;
- retiros voluntarios/forzosos;
- reintegros;
- defunciones;
- miembros por Taller y período;
- distribución por grado;
- Hermanos Past activos;
- morosidad total y por Taller;
- hitos de iniciación, aumento de salario y exaltación;
- inconsistencias pendientes;
- solicitudes y elegibilidad para ceremonias/reconocimientos;
- validaciones pendientes por área;
- documentos oficiales por tipo, período y estado;
- ceremonias pendientes/autorizadas/rechazadas;
- documentos pendientes de firma/emisión;
- ocupación y disponibilidad de templos/salas;
- reservas, cancelaciones y reprogramaciones;
- tiempos de tramitación.

## Reglas de integridad
- No usar datos personales como clave técnica primaria.
- Mantener historial en lugar de sobrescribir información relevante.
- Las fechas de iniciación, aumento de salario, exaltación, retiro, reintegro y defunción deben ser trazables.
- Los estados actuales deben ser coherentes con su historial.
- El dato financiero maestro pertenece a Tesorería; Régimen Interior y Gran Secretaría consumen resultados de regularidad autorizados.
- Gran Secretaría no recalcula elegibilidad masónica ni saldos financieros.
- Una autorización de ceremonia debe referenciar las validaciones exactas utilizadas para emitirla.
- Un documento oficial emitido no se modifica destructivamente.
- Los correlativos oficiales emitidos/anulados no se reutilizan.
- No se permiten reservas confirmadas solapadas para un mismo espacio.
- Una evaluación automática nunca debe sustituir la resolución de la autoridad competente.
- Separar datos maestros, datos transaccionales, resultados derivados y auditoría.
- Aplicar restricciones de integridad desde base de datos y aplicación.
- Diseñar migraciones versionadas.
