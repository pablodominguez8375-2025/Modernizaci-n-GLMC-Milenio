# PMGM-DB-001 — Modelo conceptual de datos institucional

**Estado:** Propuesta base  
**Versión:** 0.1

## 1. Objetivo
Definir el modelo conceptual inicial de la base maestra del Proyecto Milenio, evitando duplicación de datos y permitiendo que los distintos módulos reutilicen una única fuente institucional para personas, miembros, Talleres, cargos, periodos, tenidas, documentos y auditoría.

## 2. Principios de datos
- Una persona debe tener un único registro maestro.
- La condición de miembro es una relación institucional, no una copia de la persona.
- La pertenencia a uno o más Talleres se modela históricamente.
- Los cargos se asocian a periodos y organizaciones concretas.
- El estado actual debe poder derivarse sin perder historia.
- Los documentos deben conservar metadatos, versión, clasificación y permisos.
- Las bajas, retiros, fallecimientos u otros cambios no deben eliminar historia.
- Los identificadores técnicos internos serán UUID u otro identificador no significativo; datos nacionales como RUT no deben ser la clave primaria técnica.
- Se deberá aplicar minimización de datos y segregación de información sensible.

## 3. Entidades maestras

### PERSONA
Representa a la persona natural.
Campos conceptuales:
- `persona_id`.
- nombres.
- apellidos.
- nombre preferido/institucional.
- fecha de nacimiento, cuando sea necesaria.
- identificadores legales, protegidos y opcionales según finalidad.
- correo principal.
- teléfonos.
- datos de contacto.
- estado de registro.
- timestamps de creación y modificación.

### MIEMBRO
Representa la relación de una persona con la institución.
- `miembro_id`.
- `persona_id`.
- número institucional interno, si corresponde.
- fecha de incorporación.
- estado institucional.
- fecha y motivo de término cuando proceda.
- observaciones gobernadas.

### ORGANIZACION
Permite representar Gran Logia, Talleres/Logias y futuras estructuras.
- `organizacion_id`.
- tipo de organización.
- nombre.
- número.
- jurisdicción.
- estado.
- organización superior opcional.

### MEMBRESIA_ORGANIZACION
Historial de pertenencia de un miembro a una organización.
- `membresia_id`.
- `miembro_id`.
- `organizacion_id`.
- fecha inicio.
- fecha término.
- tipo/condición.
- estado.

### GRADO
Catálogo institucional de grados.
- `grado_id`.
- código.
- nombre.
- orden/secuencia.
- estado.

### HISTORIAL_GRADO
Registra hitos de progresión sin sobrescribir historia.
- `historial_grado_id`.
- `miembro_id`.
- `grado_id`.
- fecha de adquisición.
- organización asociada.
- referencia documental opcional.

### CARGO
Catálogo de cargos institucionales y logiales.
- `cargo_id`.
- nombre.
- ámbito.
- nivel.
- estado.

### PERIODO
Periodo administrativo o masónico.
- `periodo_id`.
- nombre.
- fecha inicio.
- fecha término.
- estado.

### DESIGNACION_CARGO
Asigna un miembro a un cargo durante un periodo y organización.
- `designacion_id`.
- `miembro_id`.
- `cargo_id`.
- `organizacion_id`.
- `periodo_id`.
- fecha inicio/fin efectiva.
- tipo de designación.

## 4. Gestión Logial y Secretaría

### TENIDA
- `tenida_id`.
- `organizacion_id`.
- fecha/hora.
- tipo de tenida.
- grado.
- estado.
- convocatoria.
- acta/documento asociado.

### ASISTENCIA
- `asistencia_id`.
- `tenida_id`.
- `miembro_id`.
- condición: presente, ausente, excusado, visita u otra.
- fecha de registro.
- observación controlada.

### ACTA
Puede implementarse como documento especializado con metadatos de tenida y control de versión.

### SOLICITUD
Entidad genérica para flujos internos tales como ceremonias, certificados, autorizaciones u otros servicios.
- `solicitud_id`.
- solicitante.
- tipo.
- fecha.
- estado.
- responsable actual.
- organización.
- datos específicos estructurados.

## 5. Documentación, biblioteca y CENDOC

### DOCUMENTO
- `documento_id`.
- título.
- tipo documental.
- clasificación.
- propietario institucional.
- organización.
- fecha documental.
- estado.
- nivel de acceso.
- hash de integridad.

### DOCUMENTO_VERSION
- `version_id`.
- `documento_id`.
- número de versión.
- ubicación física/objeto.
- hash.
- tamaño.
- tipo MIME.
- autor/cargador.
- fecha.

### CATEGORIA_DOCUMENTAL
Taxonomía reutilizable por biblioteca, CENDOC, archivo histórico y documentos administrativos.

## 6. Identidad y autorización

### USUARIO
Debe relacionarse con PERSONA cuando corresponda, pero la identidad técnica y la ficha institucional no serán la misma entidad.

### ROL
Ejemplos conceptuales:
- Administrador general.
- Autoridad de Gran Logia.
- Régimen Interior.
- Secretaría de Gran Logia.
- Venerable Maestro.
- Secretario de Taller.
- Tesorero.
- Docencia.
- Biblioteca/CENDOC.
- Miembro.

### PERMISO
Acción específica sobre recurso o función.

### USUARIO_ROL
Asignación de rol con contexto opcional de organización y vigencia.

## 7. Auditoría

### AUDIT_EVENT
- `audit_event_id`.
- actor.
- fecha/hora.
- acción.
- entidad.
- identificador afectado.
- resultado.
- correlation ID.
- origen técnico.
- metadatos permitidos.

La auditoría no debe almacenar indiscriminadamente datos personales completos en texto plano.

## 8. Relaciones principales
- PERSONA 1—0..1 MIEMBRO.
- MIEMBRO 1—N MEMBRESIA_ORGANIZACION.
- ORGANIZACION 1—N MEMBRESIA_ORGANIZACION.
- MIEMBRO 1—N HISTORIAL_GRADO.
- MIEMBRO 1—N DESIGNACION_CARGO.
- ORGANIZACION 1—N TENIDA.
- TENIDA 1—N ASISTENCIA.
- MIEMBRO 1—N ASISTENCIA.
- DOCUMENTO 1—N DOCUMENTO_VERSION.
- USUARIO N—N ROL mediante USUARIO_ROL.

## 9. Reglas de integridad iniciales
1. No puede existir una membresía activa duplicada para el mismo miembro y organización con la misma condición.
2. Los periodos de cargos no deben quedar abiertos de forma inconsistente.
3. Una asistencia debe referenciar una tenida existente.
4. La eliminación física de personas o miembros estará restringida y sujeta a política de retención.
5. Los cambios de grado se registran como historia; no se sobrescribe el hito anterior.
6. Toda designación de cargo relevante debe ser auditable.
7. Los datos visibles para otros miembros deben diferenciarse de los datos administrativos internos.

## 10. Separación de datos por sensibilidad
Nivel sugerido:
- Público.
- Institucional general.
- Institucional restringido.
- Confidencial administrativo.
- Datos personales sensibles/especialmente protegidos, si existieran.

Las políticas de acceso se aplicarán por finalidad, rol y contexto.

## 11. Próximo paso técnico
Transformar este modelo conceptual en un modelo lógico PostgreSQL con:
- esquemas por dominio o convención modular;
- claves UUID;
- índices;
- constraints;
- soft delete solo donde sea apropiado;
- columnas de auditoría;
- migraciones EF Core versionadas;
- dataset sintético de prueba sin datos reales.
