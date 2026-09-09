# PMGM-BLG-050 — Biblioteca Virtual

**Estado:** Núcleo v0.16.0 estable en CI; mejora editorial pendiente  
**Prioridad:** P1 institucional / P0 seguridad de acceso  
**Fecha:** 2026-09-08  
**Requisitos:** PMGM-REQ-031, PMGM-REQ-031-ADD-001  
**Dependencias:** PMGM-REQ-027, PMGM-ADR-003, PMGM-ARCH-003

## Objetivo
Construir la **Biblioteca Virtual** como módulo independiente para publicación y consulta controlada de material autorizado, reutilizando el núcleo de almacenamiento documental seguro sin asumir funciones de custodia archivística.

## Alcance del incremento
- Colecciones y subcolecciones.
- Registro bibliográfico y metadata editorial.
- Taxonomías configurables.
- Autores, materias, palabras clave, idioma y tipo de recurso.
- Estados editoriales `draft`, `review`, `approved`, `published`, `withdrawn`, `rejected`.
- Política de acceso por perfil, membresía, grado, Taller o rol cuando aplique.
- **Grado mínimo requerido acumulativo:** 1 → grados 1/2/3+, 2 → 2/3+, 3 → 3+.
- Versiones publicadas trazables.
- Catálogo paginado server-side.
- Búsqueda y filtros de metadata.
- Facetas calculadas únicamente sobre recursos que el usuario puede descubrir.
- Descarga autenticada/autorizada sin URL pública de Object Storage.
- Proyección controlada de material proveniente de Docencia, Gestión Documental o Gran Archivo.
- Auditoría de altas, revisiones, publicación, cambios de audiencia, retiro y descargas restringidas.

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
11. El grado efectivo del hermano se obtiene de la historia institucional de Membresía, nunca de un valor editable por el propio usuario.
12. Un usuario de grado insuficiente no recibe por defecto el recurso en resultados, conteos ni facetas.
13. Conocer el ID de un recurso no permite eludir la política de grado (protección IDOR/BOLA).
14. Las demás restricciones declaradas se combinan por defecto de forma acumulativa (`AND`).
15. La identidad autenticada se vincula al miembro mediante `issuer/sub`, no mediante RUT ni grado suministrado por frontend.
16. Las planchas de trabajo publicadas en Biblioteca mantienen referencia a Gestión Logial y no ingresan por defecto al Gran Archivo.

## Estado v0.16
### Implementado y verde
- [x] Catálogo server-side de documentos publicados con versión `available`.
- [x] Búsqueda por metadata.
- [x] Filtro por tipo documental y colección.
- [x] Paginación.
- [x] Facetas de colección/tipo.
- [x] DTO minimizado sin `ObjectKey`, SHA-256, nombre físico ni referencia ClamAV.
- [x] Descarga autenticada mediante API.
- [x] UI React conectada a búsqueda/facetas/descarga.
- [x] Pruebas frontend del catálogo.
- [x] Prueba HTTP backend del catálogo estabilizada.
- [x] `MinimumDegreeRequired` agregado al modelo y migración PostgreSQL.
- [x] Vínculo institucional `issuer/sub → MemberId` para autorización.
- [x] Resolución del grado efectivo desde `DegreeEvent` vigente.
- [x] Filtro por grado en catálogo y facetas.
- [x] Protección por grado de detalle y descarga directa por ID.
- [x] Endpoint auditable para configurar/cambiar grado mínimo.
- [x] Prueba integral acumulativa 1.º → 2.º → 3.º sobre búsqueda, facetas, detalle y descarga.
- [x] Protección contra bypass por ID directo.
- [x] CI #519 completamente verde: backend, PostgreSQL, S3/MinIO, ClamAV, frontend e infraestructura.

### Mejora editorial pendiente, no bloqueante del núcleo seguro
- [ ] UI editorial para visualizar/configurar `MinimumDegreeRequired` sin usar llamada técnica manual.
- [ ] Estimación/visualización de audiencia efectiva antes de publicar.
- [ ] Advertencia editorial cuando se cambie el grado mínimo de una publicación vigente.

## Entregables posteriores
- [ ] Entidades bibliográficas/taxonomías enriquecidas.
- [ ] Workflow editorial completo.
- [ ] Autores, materias y palabras clave.
- [ ] Integración con Gran Archivo mediante referencias/derivados.
- [ ] Integración con Docencia/Gestión Logial, incluyendo Planchas de Trabajo.
- [ ] Auditoría editorial ampliada.
- [ ] OCR/indexación full-text en fase posterior, siempre respetando ACL.

## Fuera de alcance de este bloque
- Custodia archivística.
- Fondo/Sección/Serie/Expediente/Pieza.
- Cadena de custodia de originales físicos.
- Préstamos de archivo.
- CENDOC.
- OCR/indexación full-text avanzada en primera etapa.

## Criterio de aceptación del núcleo seguro
Cumplido en CI #519: catálogo, filtros, facetas, detalle y descarga están verdes y la autorización acumulativa por grado se aplica en backend usando identidad y grado institucional vigente, sin filtración de metadata ni bypass por ID directo.