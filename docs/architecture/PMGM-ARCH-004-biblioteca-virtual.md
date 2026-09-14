# PMGM-ARCH-004 — Biblioteca Virtual: proyección de publicación y consulta

**Estado:** Aprobado para implementación incremental  
**Incremento inicial:** v0.16.0  
**Requisitos:** PMGM-REQ-031, PMGM-REQ-027  
**Relacionado:** PMGM-ARCH-002, PMGM-ARCH-003, PMGM-ADR-003

## 1. Objetivo
Definir la Biblioteca Virtual como un dominio de publicación, descubrimiento y consulta institucional que reutiliza el núcleo documental seguro sin asumir custodia archivística ni duplicar los binarios de origen.

## 2. Límite de dominio
La Biblioteca Virtual:
- publica y descubre conocimiento institucional autorizado;
- consulta documentos cuya versión publicada está en estado técnico `available`;
- aplica autorización en backend antes de listar, leer o descargar;
- presenta metadata bibliográfica y de navegación minimizada;
- puede referenciar derivados autorizados provenientes de Gestión Documental o, posteriormente, del Gran Archivo.

La Biblioteca Virtual **no**:
- reemplaza Gestión Documental Operativa;
- reemplaza Gran Archivo / Gran Archivero;
- custodia por sí sola originales históricos;
- modifica directamente entidades propiedad de Gran Archivo;
- expone `ObjectKey`, SHA-256, referencias de escaneo, credenciales S3 ni nombres físicos innecesarios;
- forma parte de CENDOC, que está fuera del alcance del Proyecto Milenio.

## 3. Modelo de publicación
En v0.16.0 la publicación continúa referenciando:
- `InstitutionalDocument` como registro lógico publicado;
- `PublishedVersionId` como versión visible;
- `DocumentVersion` como metadata técnica de la versión;
- `IDocumentObjectStore` como origen binario privado.

No se crea una segunda copia del archivo para Biblioteca.

Una entrada es visible sólo cuando:
1. `InstitutionalDocument.Status == published`;
2. `PublishedVersionId` existe;
3. la versión referenciada pertenece al documento;
4. `DocumentVersion.ProcessingStatus == available`;
5. la política de acceso permite al usuario autenticado consultar ese documento.

## 4. Políticas de acceso
### `library_authenticated`
Visible para todo usuario institucional autenticado.

### `organization_authenticated`
Visible únicamente para:
- usuarios con claim de la organización correspondiente; o
- roles de Orden explícitamente habilitados para lectura documental transversal.

### `management_only`
No forma parte del catálogo general de Biblioteca.

La UI nunca se considera una frontera de seguridad. La autorización se aplica en la API antes de construir resultados o entregar contenido.

## 5. Catálogo v0.16.0
El primer corte incorpora búsqueda server-side sobre metadata publicada:
- texto libre por título, colección y tipo documental;
- colección;
- tipo documental;
- año inicial/final de publicación;
- paginación acotada;
- orden por fecha de publicación descendente;
- facetas mínimas de colecciones y tipos visibles.

La respuesta del catálogo sólo incluirá metadata necesaria para consulta:
- id del documento;
- título;
- tipo documental;
- colección;
- versión publicada;
- tipo de contenido;
- tamaño;
- fecha de publicación.

## 6. Búsqueda
### Fase 1 — v0.16.0
PostgreSQL sobre metadata estructurada y texto corto. Es suficiente para el volumen inicial y evita introducir un motor externo prematuramente.

### Fase futura
Cuando volumen y necesidades lo justifiquen:
- PostgreSQL Full Text Search;
- índices trigram para búsquedas tolerantes;
- extracción de texto de derivados autorizados;
- índice especializado si existen métricas que lo requieran.

La evolución de búsqueda no cambia la propiedad de los binarios.

## 7. Descarga y lectura
La descarga usa el endpoint autenticado del núcleo v0.15.0.

El backend verifica nuevamente:
- estado `published`;
- versión `available`;
- política de acceso;
- existencia física del objeto.

Se mantiene `Cache-Control: private, no-store` para contenido protegido.

## 8. Facetas
Las facetas se calculan únicamente sobre publicaciones visibles para el usuario actual. Nunca deben revelar la existencia de documentos que su política de acceso oculta.

Facetas iniciales:
- colecciones;
- tipos documentales.

## 9. Metadata bibliográfica futura
PMGM-REQ-031 contempla ampliar el modelo con metadata bibliográfica y taxonomías configurables. Se incorporarán de forma explícita en iteraciones posteriores, por ejemplo:
- autor/autor corporativo;
- fecha de edición/publicación;
- materia/tema;
- palabras clave controladas;
- idioma;
- descripción/resumen;
- relación con docencia;
- procedencia archivística cuando corresponda.

No se fuerza esa expansión dentro del primer corte de catálogo para evitar mezclar cambio de modelo con activación del consumo seguro.

## 10. Flujo editorial futuro
La Biblioteca tendrá estados editoriales propios (`draft`, `review`, `approved`, `published`, `withdrawn`, `rejected`) cuando se implemente su modelo editorial completo.

En v0.16.0 se reutiliza la publicación controlada ya existente en Gestión Documental como proyección mínima, manteniendo el límite de dominio preparado para extraer el flujo editorial posteriormente.

## 11. Auditoría y privacidad
- las descargas de clasificación sensible/restringida mantienen auditoría;
- el catálogo no expone metadata técnica privada;
- búsqueda y facetas deben cumplir minimización y finalidad;
- no se agregan datos personales a índices de búsqueda sin evaluación explícita de necesidad y base jurídica;
- cualquier ampliación hacia contenido personal se revisará contra PMGM-REQ-026 y Ley 21.719.

## 12. Criterios de aceptación v0.16.0
1. Búsqueda y filtros se ejecutan en backend.
2. Paginación tiene límites máximos definidos.
3. Sólo se devuelven publicaciones visibles y `available`.
4. Facetas respetan exactamente el mismo universo autorizado.
5. La respuesta no contiene claves S3, hashes, referencias ClamAV ni nombre físico de archivo.
6. La interfaz puede iniciar descarga autenticada sin revelar URL pública del objeto.
7. Las pruebas verifican autorización y minimización.
8. PostgreSQL, S3, ClamAV, frontend y gates existentes continúan verdes en CI.
