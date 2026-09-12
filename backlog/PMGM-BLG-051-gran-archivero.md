# PMGM-BLG-051 — Gran Archivero y Archivo Histórico

**Estado:** Planificado  
**Prioridad:** P1 institucional / P0 seguridad documental  
**Fecha:** 2026-09-08  
**Requisitos:** PMGM-REQ-030, PMGM-REQ-030-ADD-001  
**Dependencias:** PMGM-REQ-026, PMGM-REQ-027, PMGM-ADR-003, PMGM-ARCH-003

## Objetivo
Implementar el módulo **Gran Archivo / Gran Archivero** para custodia, clasificación, preservación, digitalización, consulta controlada y cadena de custodia del patrimonio documental histórico de la Gran Logia Mixta de Chile.

## Alcance del incremento
- Rol `GRAN_ARCHIVERO`.
- Cuadro de clasificación configurable.
- Jerarquía Fondo → Sección → Serie → Subserie → Expediente → Pieza.
- Registro de procedencia y productor.
- Transferencias documentales desde Gran Secretaría, Talleres y otras áreas autorizadas.
- Cambio formal de custodia operativa a histórica.
- Registro de ubicación física.
- Restricciones y fechas de apertura.
- Digitalización con archivo maestro y derivados.
- Integridad mediante SHA-256.
- Acciones de preservación digital.
- Solicitudes de acceso a material restringido.
- Préstamos físicos y cadena de custodia.
- Integración con Biblioteca Virtual mediante derivados autorizados.
- Auditoría integral.
- Custodia expresa de **planchas de Secretaría y otros documentos oficiales**, decretos, cartas/correspondencia, investigaciones, amonestaciones, expedientes disciplinarios, informes reservados y otros antecedentes de valor histórico formalmente transferidos.
- Las **planchas de trabajo de los hermanos no ingresan al Gran Archivo por defecto**; permanecen en Gestión Logial y sólo se publican en Biblioteca cuando sean autorizadas. Un eventual ingreso archivístico requeriría una decisión institucional específica y transferencia formal independiente.
- Separación entre **descubrimiento en catálogo** y **acceso al contenido**.
- Visibilidad `hidden`, `catalog_only`, `catalog_extended` o `content_allowed`.
- Permisos por grado, hermano específico, Taller, cargo/rol, comisión/órgano, finalidad, fecha de apertura o autorización especial.
- Concesiones individuales con alcance, vigencia y revocación.

## Reglas funcionales
1. Gran Archivo es independiente de Biblioteca Virtual.
2. CENDOC no forma parte del Proyecto Milenio.
3. La transferencia documental no debe borrar ni perder el vínculo con el productor original.
4. Una reclasificación no elimina la clasificación anterior.
5. El archivo maestro de preservación no se sobrescribe.
6. Las restricciones de privacidad no pueden relajarse automáticamente por tratarse de material histórico.
7. La ubicación física interna no se expone a perfiles no autorizados.
8. Los préstamos físicos mantienen cadena de custodia completa.
9. Biblioteca Virtual recibe sólo copias o derivados autorizados.
10. Toda operación sensible queda auditada.
11. Poder descubrir una ficha archivística no concede derecho a abrir o descargar su contenido.
12. El Gran Archivero puede administrar permisos dentro de sus competencias, pero su rol no implica acceso universal irrestricto.
13. Un expediente puede exigir simultáneamente grado mínimo y autorización individual.
14. Investigaciones, amonestaciones y expedientes disciplinarios parten con política restrictiva.
15. Toda concesión/revocación de acceso por hermano registra otorgante, motivo, alcance, vigencia y evidencia cuando corresponda.
16. Conocer un identificador interno no permite eludir autorización.
17. “Plancha de trabajo” y “plancha de Secretaría” son categorías diferentes y nunca deben mezclarse en el modelo documental.

## Entregables
- [ ] Entidades archivísticas y catálogos.
- [ ] Migración PostgreSQL.
- [ ] Rol y permisos del Gran Archivero.
- [ ] API de fondos, series, expedientes y piezas.
- [ ] Política independiente de visibilidad de catálogo y contenido.
- [ ] Grants por hermano, grado, rol/Taller y combinaciones.
- [ ] Flujo de solicitud/aprobación/revocación de acceso.
- [ ] Flujo de transferencia documental.
- [ ] Registro de ubicación física.
- [ ] Digitalización y derivados.
- [ ] Verificación de integridad.
- [ ] Préstamos y devoluciones.
- [ ] Reportes del Gran Archivero.
- [ ] Integración con Gran Secretaría.
- [ ] Integración con Talleres.
- [ ] Integración con Biblioteca Virtual.
- [ ] UI Gran Archivo.
- [ ] Auditoría reforzada y pruebas automatizadas.

## Fases sugeridas
### Fase A — Estructura archivística y seguridad
Fondos, series, expedientes, piezas, procedencia, restricciones, metadata, catálogo visible y política de acceso.

### Fase B — Transferencias y custodia
Actas de transferencia, aceptación/rechazo, custodia lógica, ubicación física y trazabilidad.

### Fase C — Digitalización y preservación
Maestros, derivados, integridad, verificaciones periódicas y acciones de preservación.

### Fase D — Consulta y préstamos
Solicitudes, autorizaciones por hermano/grado, préstamos físicos, devoluciones e incidentes.

### Fase E — Publicación histórica
Derivados autorizados hacia Biblioteca Virtual conservando referencia al registro archivístico.

## Criterio de aceptación del primer corte
El primer corte queda listo cuando el Gran Archivero pueda recibir una transferencia formal de un expediente cerrado, clasificarlo, configurar visibilidad de catálogo y contenido por políticas granulares, otorgar acceso temporal a un hermano cuando corresponda, registrar ubicación y conservar procedencia/cadena de custodia con autorización aplicada en backend y auditoría completa.