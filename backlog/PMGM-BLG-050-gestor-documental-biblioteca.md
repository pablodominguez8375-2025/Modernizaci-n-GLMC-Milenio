# PMGM-BLG-050 — Biblioteca Virtual

**Estado:** Planificado / parcialmente implementado  
**Prioridad:** P1  
**Fecha:** 2026-09-08  
**Requisito:** PMGM-REQ-031  
**Dependencias:** PMGM-REQ-027, PMGM-ADR-003

## Objetivo
Construir la **Biblioteca Virtual** como módulo independiente para publicación y consulta controlada de material autorizado, reutilizando el núcleo de almacenamiento documental seguro sin asumir funciones de custodia archivística.

## Alcance del incremento
- Colecciones y subcolecciones.
- Registro bibliográfico y metadata editorial.
- Taxonomías configurables.
- Autores, materias, palabras clave, idioma y tipo de recurso.
- Estados editoriales `draft`, `review`, `approved`, `published`, `withdrawn`, `rejected`.
- Política de acceso por perfil, membresía, grado, Taller o rol cuando aplique.
- Versiones publicadas trazables.
- Búsqueda y filtros de metadata.
- Proyección controlada de material proveniente de Docencia, Gestión Documental o Gran Archivo.
- Auditoría de altas, revisiones, publicación, retiro y descargas restringidas.

## Reglas funcionales
1. Biblioteca Virtual no es el Gran Archivo.
2. Biblioteca Virtual no es CENDOC y CENDOC no forma parte del Proyecto Milenio.
3. La existencia de un documento en otro módulo no lo publica automáticamente.
4. Sólo material aprobado puede quedar visible.
5. Toda restricción se aplica en backend.
6. El binario publicado se almacena fuera de PostgreSQL.
7. Un documento histórico publicado desde Gran Archivo utiliza un derivado autorizado y conserva referencia al registro de origen.
8. Retirar una publicación no elimina el original ni su registro histórico.
9. Las versiones anteriores se conservan cuando exista obligación de trazabilidad.
10. Toda publicación y retiro queda auditado.

## Entregables
- [ ] Entidades de Biblioteca y taxonomías.
- [ ] Migración PostgreSQL.
- [ ] API de colecciones y recursos.
- [ ] Workflow editorial.
- [ ] Política de acceso.
- [ ] Búsqueda y filtros.
- [ ] Integración con Object Storage.
- [ ] Integración con Gran Archivo mediante referencias/derivados.
- [ ] Integración con Docencia/Gestión Documental.
- [ ] UI Biblioteca Virtual.
- [ ] Auditoría.
- [ ] Pruebas unitarias, integración PostgreSQL y HTTP end-to-end.

## Fuera de alcance de este bloque
- Custodia archivística.
- Fondo/Sección/Serie/Expediente/Pieza.
- Cadena de custodia de originales físicos.
- Préstamos de archivo.
- CENDOC.
- OCR/indexación full-text avanzada en primera etapa.

## Criterio de aceptación
El bloque queda listo cuando un editor pueda registrar un recurso, enviarlo a revisión, aprobarlo, publicarlo para un conjunto autorizado de usuarios, retirarlo conservando trazabilidad y, cuando provenga del Gran Archivo, mantener la referencia de origen sin alterar ni duplicar el original de preservación.