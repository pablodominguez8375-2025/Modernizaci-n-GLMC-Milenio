# PMGM-BLG-051 — Gran Archivero y Archivo Histórico

**Estado:** Planificado  
**Prioridad:** P1 institucional  
**Fecha:** 2026-09-08  
**Requisito:** PMGM-REQ-030  
**Dependencias:** PMGM-REQ-026, PMGM-REQ-027, PMGM-ADR-003

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

## Entregables
- [ ] Entidades archivísticas y catálogos.
- [ ] Migración PostgreSQL.
- [ ] Rol y permisos del Gran Archivero.
- [ ] API de fondos, series, expedientes y piezas.
- [ ] Flujo de transferencia documental.
- [ ] Registro de ubicación física.
- [ ] Digitalización y derivados.
- [ ] Verificación de integridad.
- [ ] Préstamos y devoluciones.
- [ ] Solicitudes de acceso restringido.
- [ ] Reportes del Gran Archivero.
- [ ] Integración con Gran Secretaría.
- [ ] Integración con Talleres.
- [ ] Integración con Biblioteca Virtual.
- [ ] UI Gran Archivo.
- [ ] Auditoría y pruebas automatizadas.

## Fases sugeridas
### Fase A — Estructura archivística
Fondos, series, expedientes, piezas, procedencia, restricciones y metadata.

### Fase B — Transferencias y custodia
Actas de transferencia, aceptación/rechazo, custodia lógica, ubicación física y trazabilidad.

### Fase C — Digitalización y preservación
Maestros, derivados, integridad, verificaciones periódicas y acciones de preservación.

### Fase D — Consulta y préstamos
Solicitudes, autorizaciones, préstamos físicos, devoluciones e incidentes.

### Fase E — Publicación histórica
Derivados autorizados hacia Biblioteca Virtual conservando referencia al registro archivístico.

## Criterio de aceptación del primer corte
El primer corte queda listo cuando el Gran Archivero pueda recibir una transferencia formal de un expediente cerrado, clasificarlo dentro de la jerarquía archivística, registrar restricciones y ubicación, conservar procedencia y cadena de custodia, y consultar el inventario con permisos aplicados en backend.