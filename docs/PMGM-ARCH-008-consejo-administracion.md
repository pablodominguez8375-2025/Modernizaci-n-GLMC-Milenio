# PMGM-ARCH-008 — Consejo de Administración del Taller

**Estado:** implementación inicial en `feature/consejo-administracion-v1`  
**Fecha:** 2026-09-17  
**Módulo:** Gestión Logial

## 1. Decisión

El Consejo de Administración se implementa como una entidad funcional propia dentro del módulo existente de **Gestión Logial**. No es una Tenida, no constituye un microservicio y no crea una aplicación paralela.

La implementación reutiliza:

- `LodgeManagementDbContext`;
- el control de acceso institucional por Taller;
- `CanParticipateInLodgeCouncil(...)`;
- la ficha histórica y `OfficeAssignment` de la base maestra;
- la bitácora auditable transversal;
- PostgreSQL/EF Core;
- el frontend React y su demo con datos ficticios.

## 2. Fuente normativa verificada

Constitución y Reglamento General GLMCh, Capítulo 10:

- **Art. 10.1:** integra al Venerable Maestro, Inmediato Ex-Venerable Maestro, Primer Vigilante, Segundo Vigilante, Orador, Secretario, Tesorero y Hospitalario; el Venerable preside; debe reunirse al menos una vez al mes con quórum calificado.
- **Art. 10.2:** entrega al Consejo funciones de recepción de bienes/documentación, control de cuentas de Tesorería y Hospitalaria, seguimiento del nivel masónico de las Columnas, alivio de cuotas cuando proceda y ayudas pecuniarias.
- El mismo Art. 10.2 distingue materias que el Consejo **propone a la Cámara del Medio**: presupuesto y modificaciones, programa anual de trabajos/instrucción y revisiones, cambio de oficiales electivos negligentes y Cartas de Retiro Forzoso por incumplimiento de cotización/asistencia.

También se consideran las referencias reglamentarias del Tesorero, Hospitalario y Venerable Maestro respecto del Consejo.

## 3. Regla de quórum

El texto vigente consultado exige **“quórum calificado”**, pero el Capítulo 10 no fija allí un número explícito. Por ello:

- no se codifica un umbral numérico inventado;
- la sesión registra una **confirmación institucional de quórum calificado**, con actor, fecha y cantidad observada de integrantes presentes con voto;
- el dato queda auditable;
- el umbral podrá parametrizarse posteriormente cuando exista una definición institucional expresa y aprobada.

## 4. Modelo de datos

### `LodgeCouncilSession`

Representa una sesión del Consejo por Taller y fecha. Registra estado, confirmación de quórum, actor de confirmación, creación y cierre.

### `LodgeCouncilAttendanceRecord`

Registra asistencia histórica de integrantes o invitados. Los ocho cargos reglamentarios pueden tener voto; un invitado puede tener voz, pero no voto.

Para impedir que un miembro activo cualquiera sea registrado como integrante con voto, el backend contrasta el cargo solicitado contra `OfficeAssignment` de la base maestra. La asignación debe pertenecer al mismo Taller y estar vigente en la **fecha de la sesión** (`StartDate <= SessionDate` y `EndDate` nula o posterior). Se aceptan códigos institucionales y variantes históricas normalizadas de los nombres de cargo para preservar compatibilidad de datos, sin ampliar el conjunto de los ocho cargos reglamentarios.

### `LodgeCouncilDecision`

Registra asunto, resolución, resultado, categoría normativa, eventual monto/documento y si la materia requiere revisión posterior de Cámara del Medio.

### `LodgeCouncilFinancialReview`

Registra controles de Tesorería, Hospitalaria o nivel/instrucción de Columnas, con período, conclusión, observaciones y eventual documento de respaldo.

## 5. Categorías de acuerdo

Competencias/actuaciones del Consejo:

- recepción/entrega de bienes y documentación;
- control financiero/administrativo;
- alivio de cuotas;
- ayudas de beneficencia;
- otras materias registradas dentro de su competencia.

Materias que **no quedan aprobadas definitivamente por el Consejo** y deben marcarse para remisión a Cámara del Medio:

- presupuesto y modificaciones;
- programa anual de trabajos/instrucción y revisiones;
- cambio de oficiales electivos negligentes;
- propuesta de Carta de Retiro Forzoso por incumplimiento de cotización/asistencia.

Para estas categorías el sistema acepta `approved_for_referral` o rechazo; no permite almacenarlas como aprobación final del Consejo.

## 6. Permisos

- Lectura/participación: los ocho cargos reglamentarios del Consejo, dentro del Taller de su `pmgm_org`.
- Registro administrativo de sesión, asistencia, quórum, acuerdos y revisiones: Venerable Maestro o Secretaría del Taller.
- La pertenencia activa al Taller no basta para obtener voto: el integrante registrado debe tener el cargo del Consejo vigente en la fecha de la sesión según `OfficeAssignment`.
- `lodge_admin` es un perfil técnico y no se considera por sí solo integrante masónico del Consejo.

## 7. Auditoría

Eventos iniciales:

- `lodge.council.session.created`;
- `lodge.council.attendance.recorded`;
- `lodge.council.quorum.confirmed`;
- `lodge.council.decision.recorded`;
- `lodge.council.review.recorded`;
- `lodge.council.session.closed`.

La confirmación de quórum deja expresamente `numericThresholdHardcoded = false` para impedir que un supuesto histórico pase inadvertidamente a la lógica del sistema.

## 8. No regresión

Este incremento no sustituye Tenidas, Cámara del Medio, Tesorería, Hospitalaria, Docencia ni Gestión Documental. Los vincula mediante referencias y trazabilidad, manteniendo sus competencias separadas.
