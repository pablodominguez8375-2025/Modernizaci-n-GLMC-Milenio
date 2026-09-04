# PMGM-DB-001 — Modelo Conceptual de Datos v0.1

## Principio
El Proyecto Milenio utilizará una base de datos institucional única y normalizada. Las áreas no deben mantener planillas paralelas como fuente maestra.

## Entidad central: Miembro
Atributos conceptuales mínimos:
- Identificador interno.
- Nombres y apellidos.
- Datos de contacto.
- Identificación legal cuando corresponda.
- Estado institucional.
- Taller principal y relaciones históricas.
- Grado actual.
- Fechas masónicas relevantes.
- Fecha y tipo de retiro.
- Fecha de Oriente Eterno.

## Entidades relacionadas
### Taller
- Identificador.
- Nombre y número.
- Oriente / ubicación.
- Estado.
- Autoridades por período.

### Grado e historial masónico
- Grado.
- Fecha de obtención.
- Taller.
- Evidencia / documento asociado.

### Cargo
- Tipo de cargo.
- Taller o estructura institucional.
- Período.
- Fecha inicio y término.

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
- Estado.
- Flujo de aprobación.
- Resolución.
- Historial.

### Documento
- Tipo.
- Propietario lógico.
- Clasificación.
- Versiones.
- Permisos.
- Metadatos.

### Movimiento de tesorería
- Tipo.
- Miembro/Taller relacionado.
- Concepto.
- Monto.
- Fecha.
- Comprobante.

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

## Reglas
- No usar datos personales como clave técnica primaria.
- Mantener historial en lugar de sobrescribir información relevante.
- Separar datos maestros, datos transaccionales y auditoría.
- Aplicar restricciones de integridad desde base de datos y aplicación.
- Diseñar migraciones versionadas.
