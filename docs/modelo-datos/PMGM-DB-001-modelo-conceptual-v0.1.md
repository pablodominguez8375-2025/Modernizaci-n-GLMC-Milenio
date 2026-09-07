# PMGM-DB-001 — Modelo Conceptual de Datos v0.2

## Principio
El Proyecto Milenio utilizará una base de datos institucional única y normalizada. Las áreas no deben mantener planillas paralelas como fuente maestra.

El modelo debe permitir reconstruir la situación de un miembro en una fecha determinada, sin depender únicamente de su estado actual.

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

### Documento
- Tipo.
- Propietario lógico.
- Clasificación.
- Versiones.
- Permisos.
- Metadatos.
- Hash de integridad.

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
- solicitudes y elegibilidad para ceremonias/reconocimientos.

## Reglas de integridad
- No usar datos personales como clave técnica primaria.
- Mantener historial en lugar de sobrescribir información relevante.
- Las fechas de iniciación, aumento de salario, exaltación, retiro, reintegro y defunción deben ser trazables.
- Los estados actuales deben ser coherentes con su historial.
- El dato financiero maestro pertenece a Tesorería; Régimen Interior consume regularidad calculada.
- Una evaluación automática nunca debe sustituir la resolución de la autoridad competente.
- Separar datos maestros, datos transaccionales, resultados derivados y auditoría.
- Aplicar restricciones de integridad desde base de datos y aplicación.
- Diseñar migraciones versionadas.
