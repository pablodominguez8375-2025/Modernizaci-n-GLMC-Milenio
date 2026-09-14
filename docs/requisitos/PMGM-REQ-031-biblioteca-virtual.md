# PMGM-REQ-031 — Biblioteca Virtual

**Estado:** Aprobado funcionalmente  
**Prioridad:** P1 institucional  
**Áreas:** Biblioteca Virtual / Docencia / Gran Archivo / Gestión Documental / Administración  
**Dependencias:** PMGM-REQ-026, PMGM-REQ-027, PMGM-REQ-030, PMGM-ADR-003

## 1. Objetivo
Incorporar al Proyecto Milenio una **Biblioteca Virtual institucional**, separada funcional y técnicamente del Gran Archivo, destinada a publicar, organizar, buscar y consultar material de lectura autorizado para miembros y otros perfiles definidos por política institucional.

El Proyecto Milenio **no incorpora CENDOC** como módulo, dependencia ni componente. El dominio documental se divide en servicios con responsabilidades distintas:
- **Biblioteca Virtual:** difusión y consulta de material autorizado.
- **Gran Archivo / Gran Archivero:** custodia y preservación del patrimonio documental histórico.
- **Gestión Documental Operativa:** documentos vigentes o de trabajo generados por los distintos módulos de negocio.

## 2. Alcance de la Biblioteca Virtual
La Biblioteca Virtual deberá permitir administrar y consultar, según permisos:
- libros y monografías;
- revistas y boletines;
- trabajos de docencia;
- planchas y material formativo autorizado;
- material histórico publicado desde Gran Archivo;
- reglamentos, manuales y documentos de consulta cuando corresponda;
- material audiovisual o multimedia autorizado;
- colecciones temáticas y bibliografías;
- documentos aportados por Talleres u órganos autorizados, previa revisión y publicación.

La existencia de un documento en Gran Archivo o Gestión Documental **no implica publicación automática** en Biblioteca Virtual.

## 3. Roles
Roles funcionales mínimos:
- `BIBLIOTECA_ADMIN`: administración global del módulo.
- `BIBLIOTECA_EDITOR`: alta y edición de metadata, preparación de publicaciones.
- `BIBLIOTECA_REVISOR`: aprobación o rechazo editorial.
- `BIBLIOTECA_LECTOR`: consulta según alcance autorizado.

Los roles podrán combinarse con permisos institucionales existentes, pero deberán mantener mínimo privilegio y separación de funciones cuando corresponda.

## 4. Colecciones y clasificación
La Biblioteca deberá soportar:
- colecciones;
- subcolecciones opcionales;
- materias/temas;
- autores;
- palabras clave;
- tipo de recurso;
- idioma;
- fecha de publicación;
- Taller u órgano relacionado;
- grado o nivel de acceso cuando aplique;
- estado editorial;
- disponibilidad.

Las taxonomías deberán ser configurables sin cambios de código.

## 5. Registro bibliográfico
Cada recurso podrá incluir:
- identificador interno;
- título;
- subtítulo;
- autor(es);
- editor o entidad responsable;
- fecha/año;
- edición;
- descripción/resumen;
- materias;
- palabras clave;
- idioma;
- tipo de contenido;
- colección;
- portada o miniatura autorizada;
- fuente/origen;
- documento o registro archivístico relacionado;
- clasificación de acceso;
- estado editorial;
- versión publicada;
- fecha de publicación y retiro;
- responsable de publicación.

## 6. Flujo editorial
Estados mínimos:
1. `draft` — borrador.
2. `review` — pendiente de revisión.
3. `approved` — aprobado para publicación.
4. `published` — visible para los usuarios autorizados.
5. `withdrawn` — retirado de consulta.
6. `rejected` — rechazado con observación.

La publicación deberá registrar actor, fecha, versión y política de acceso aplicada.

## 7. Acceso y segmentación
La política de acceso podrá considerar:
- público externo, si una política institucional lo autoriza;
- miembro autenticado;
- miembro activo;
- grado masónico;
- Taller específico;
- cargo o función;
- Grandes Oficiales;
- acceso especial mediante autorización.

Toda restricción deberá ser aplicada en backend. La interfaz no será el único control de acceso.

## 8. Búsqueda y navegación
La Biblioteca deberá ofrecer:
- búsqueda por texto de metadata;
- filtros por autor, año, tema, colección, tipo, idioma y origen;
- navegación por colecciones;
- destacados o novedades;
- favoritos o lista personal, si se habilita;
- historial de recursos consultados de manera opcional y sujeto a privacidad;
- enlaces entre recursos relacionados.

La indexación full-text del contenido podrá incorporarse por etapas, respetando clasificación y permisos.

## 9. Integración con Gran Archivo
Gran Archivo podrá generar una **representación publicable** de un documento histórico.

La publicación en Biblioteca deberá:
- conservar referencia al registro archivístico de origen;
- utilizar una copia/derivado autorizado;
- no alterar el original ni el archivo maestro de preservación;
- respetar restricciones, fechas de apertura y privacidad;
- permitir retirar la publicación sin borrar el registro archivístico.

## 10. Integración con Gestión Documental y Docencia
Los módulos de Gestión Documental y Docencia podrán proponer material para publicación. La Biblioteca recibirá una referencia y una versión aprobada, evitando duplicaciones innecesarias.

La Biblioteca no reemplaza el expediente operativo, el acta, la plancha original ni el documento maestro de sus módulos de origen.

## 11. Versiones
Una nueva edición o versión publicada no sobrescribirá destructivamente la anterior cuando exista obligación de trazabilidad.

El sistema deberá permitir:
- versión vigente;
- versiones anteriores;
- fecha de vigencia;
- motivo de reemplazo;
- retiro de una versión;
- auditoría de cambios editoriales.

## 12. Seguridad documental
- Los binarios permanecerán en Object Storage privado.
- Las descargas deberán pasar por autorización del backend o URL temporal firmada.
- No se expondrán claves físicas de almacenamiento.
- Se validarán tipo MIME, extensión, tamaño e integridad.
- Se realizará análisis antimalware antes de disponibilidad.
- Los archivos restringidos no se indexarán en servicios públicos.

## 13. Privacidad y Ley 21.719
La Biblioteca deberá aplicar minimización de datos personales y no publicar información sensible por defecto.

Para publicaciones que incluyan información personal o que puedan revelar antecedentes especialmente protegidos, se deberá verificar la finalidad, base de licitud, nivel de acceso, conservación y demás controles definidos en PMGM-REQ-026.

## 14. Métricas y reportes
Reportes mínimos:
- recursos publicados por colección;
- publicaciones nuevas por período;
- recursos retirados;
- recursos pendientes de revisión;
- recursos por tipo/autor/tema;
- errores de procesamiento;
- material sin metadata obligatoria.

Las métricas de uso individual deberán configurarse de acuerdo con privacidad y finalidad institucional.

## 15. Auditoría
Auditar como mínimo:
- alta y modificación de recursos;
- envío a revisión;
- aprobación/rechazo;
- publicación y retiro;
- cambios de política de acceso;
- descargas de material restringido;
- vinculación o desvinculación con registros de Gran Archivo.

## 16. Criterios de aceptación
1. Biblioteca Virtual existe como módulo independiente de Gran Archivo.
2. CENDOC no aparece como módulo del Proyecto Milenio.
3. Un recurso puede pasar por flujo borrador → revisión → aprobación → publicación.
4. La publicación aplica permisos en backend.
5. Los recursos pueden organizarse en colecciones y taxonomías configurables.
6. Se puede publicar un derivado autorizado desde Gran Archivo sin alterar el original.
7. Las versiones quedan trazables.
8. Las operaciones sensibles quedan auditadas.
9. Los binarios se almacenan fuera de PostgreSQL.
10. La Biblioteca puede evolucionar sin asumir funciones de custodia archivística.