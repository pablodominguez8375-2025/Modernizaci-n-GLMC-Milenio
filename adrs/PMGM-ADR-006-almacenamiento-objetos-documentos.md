# PMGM-ADR-006 — Almacenamiento de objetos para Gestor Documental y Biblioteca

**Estado:** Aceptado  
**Fecha:** 2026-09-08  
**Ámbito:** Gestor Documental, Biblioteca Virtual y CENDOC

## Contexto
Proyecto Milenio debe almacenar documentos institucionales como PDF, Word, imágenes y otros archivos. PostgreSQL es la fuente de verdad para datos transaccionales, permisos, trazabilidad y metadata, pero no es el repositorio adecuado para conservar de forma masiva los binarios documentales.

Además, los documentos pueden contener información institucional sensible o restringida. El diseño debe impedir que el navegador reciba credenciales del sistema de almacenamiento y debe permitir versionado, integridad, conservación y auditoría.

## Decisión
1. Los binarios se conservarán en un **almacenamiento de objetos S3-compatible o equivalente**, desacoplado mediante una interfaz de aplicación.
2. PostgreSQL almacenará exclusivamente metadata y estado documental: documento, colección, versiones, clasificación, política de acceso, tamaño, tipo MIME, hash, estado de validación, objeto lógico y trazabilidad.
3. Una versión documental será **inmutable** una vez registrada. Una corrección crea una versión nueva; nunca sobrescribe el archivo histórico.
4. Cada objeto tendrá un `SHA-256` registrado para verificación de integridad.
5. Las claves de almacenamiento serán opacas y no incluirán nombres de personas, RUT, correos, títulos sensibles ni otros datos personales.
6. El navegador nunca recibirá `access key`, `secret key` ni credenciales permanentes del proveedor.
7. Toda carga o descarga se autorizará primero contra la API de Proyecto Milenio. Si el proveedor permite URL firmada temporal, ésta será de corta duración, alcance específico y emitida sólo después de la autorización.
8. Un archivo nuevo pasará por estados controlados antes de estar disponible: `pending_upload` → `uploaded` → `scanning` → `available` o `rejected`.
9. La publicación en Biblioteca sólo será posible para versiones `available`.
10. El contenido de archivos no se copiará a `AuditEvent.MetadataJson`. La auditoría conservará IDs técnicos, acción, resultado, versión y correlation ID.
11. Las políticas de retención y legal hold aplicarán a la metadata y al objeto físico coordinadamente. Un legal hold debe impedir la eliminación material del objeto.
12. El proveedor concreto de almacenamiento se configurará fuera del código de dominio y podrá cambiar sin modificar los contratos funcionales del Gestor Documental.

## Seguridad
- TLS obligatorio entre componentes fuera del entorno local de desarrollo.
- Cifrado en reposo provisto y verificado en el almacenamiento seleccionado.
- Buckets/contenedores privados por defecto.
- No se permitirá listado anónimo de objetos.
- Validación de extensión y MIME no sustituye el análisis del contenido.
- Se incorporará análisis antimalware antes de marcar una versión como `available`.
- Los límites de tamaño serán configurables por categoría documental.
- La API validará permisos nuevamente en cada descarga; conocer una clave de objeto no otorga acceso.

## Clasificación y acceso
El Gestor Documental soportará como mínimo:
- documentos de alcance Orden;
- documentos de alcance Taller;
- documentos restringidos por función/rol;
- Biblioteca Virtual como una proyección explícitamente publicada, no como exposición directa del repositorio interno.

Los permisos de Biblioteca no se derivarán de `CanReadOrganization` cuando éste conceda más acceso del necesario a órganos centrales; se implementará una política específica por finalidad documental.

## Consecuencias positivas
- PostgreSQL permanece optimizado para datos transaccionales.
- Se facilita escalar almacenamiento y copias de seguridad de archivos independientemente de la base.
- El versionado y hash permiten demostrar integridad documental.
- Un proveedor S3-compatible puede sustituirse sin reescribir el dominio.
- Se reduce el riesgo de exposición de credenciales en el frontend.

## Costos y riesgos
- Se agrega un componente de infraestructura adicional.
- Backup/restauración debe coordinar PostgreSQL y objetos.
- Deben definirse políticas de consistencia ante cargas incompletas.
- Es necesario incorporar análisis antimalware y pruebas de recuperación.

## Implementación incremental
1. Modelo PostgreSQL de documentos, colecciones y versiones.
2. Política de acceso documental y proyección de Biblioteca.
3. `IDocumentObjectStore` y contratos de carga/descarga.
4. Adaptador de almacenamiento seleccionado para staging/desarrollo.
5. Pipeline de hash + escaneo + disponibilidad.
6. UI de Gestor Documental y Biblioteca.
7. Pruebas de backup/restauración y legal hold sobre objetos.

## No decidido en este ADR
- Producto/proveedor concreto de almacenamiento.
- Producto antimalware concreto.
- Límites de tamaño definitivos.
- Taxonomía final de CENDOC.

Estas decisiones se tomarán por configuración o ADR específico después de validación técnica y operativa.
