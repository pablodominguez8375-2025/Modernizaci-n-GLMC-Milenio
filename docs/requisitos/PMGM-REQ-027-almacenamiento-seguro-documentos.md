# PMGM-REQ-027 — Almacenamiento seguro de documentos y versiones

**Estado:** Aprobado funcionalmente  
**Prioridad:** P0  
**Áreas:** Gestión Documental Operativa, Biblioteca Virtual, Gran Archivo / Gran Archivero, Secretaría, Gran Secretaría  
**Dependencias:** PMGM-REQ-026, módulo DocumentManagement existente

## 1. Objetivo
Completar el núcleo documental del Proyecto Milenio incorporando almacenamiento binario seguro, versionado íntegro, publicación controlada, trazabilidad y políticas de conservación coherentes con la clasificación del documento y la Ley 21.719.

Este requisito es infraestructura transversal para tres dominios separados:
- Gestión Documental Operativa: documentos vigentes y expedientes de negocio.
- Biblioteca Virtual: publicación y consulta de material autorizado.
- Gran Archivo / Gran Archivero: custodia histórica y preservación documental.

CENDOC no forma parte del alcance del Proyecto Milenio.

## 2. Principio arquitectónico
- PostgreSQL almacenará metadatos, relaciones, permisos, hashes, estados y auditoría.
- Los archivos binarios se almacenarán fuera de la base de datos en un servicio de objetos.
- El backend utilizará una abstracción de almacenamiento para evitar dependencia rígida de un proveedor.
- Ningún bucket o contenedor será público por defecto.
- El frontend nunca recibirá credenciales permanentes del proveedor de almacenamiento.
- Los dominios consumidores referenciarán el binario mediante identificadores y contratos internos; no escribirán directamente en el almacenamiento físico.

## 3. Flujo de carga
Toda nueva versión deberá seguir, como mínimo, el flujo:
1. Solicitud de carga autorizada.
2. Reserva de versión en estado `pending_upload`.
3. Carga del binario.
4. Validación de tamaño y tipo permitido.
5. Cálculo de hash SHA-256.
6. Análisis antimalware o control equivalente antes de publicación.
7. Registro de evidencia técnica del análisis.
8. Cambio a estado `ready`/`available` si supera controles, según la nomenclatura técnica adoptada.
9. Publicación explícita por usuario autorizado cuando corresponda.

Un archivo que no complete el proceso no debe quedar accesible desde Biblioteca Virtual, Gran Archivo para consulta general ni vistas de publicación.

## 4. Estados mínimos de versión
Estados funcionales requeridos:
- `pending_upload`
- `uploaded`
- `scanning`
- `rejected`
- `ready` o `available`
- `published` cuando corresponda como proyección de publicación
- `superseded`
- `archived`
- `deleted`

La implementación podrá separar el estado técnico del binario del estado funcional/publicación del documento, siempre que no se pierda trazabilidad. Las transiciones deberán ser validadas por backend y auditadas.

## 5. Integridad
Cada versión deberá registrar:
- identificador de versión;
- documento al que pertenece;
- nombre original normalizado;
- tipo MIME declarado y tipo detectado cuando sea técnicamente posible;
- tamaño;
- SHA-256;
- clave técnica opaca del objeto;
- fecha de carga;
- actor que cargó;
- resultado del control de seguridad;
- fecha de publicación cuando corresponda;
- actor que publicó;
- versión anterior, cuando corresponda.

La clave técnica del objeto nunca deberá exponerse como dato público ni ser utilizada como nombre funcional del documento.

## 6. Acceso y descarga
La descarga deberá resolverse mediante autorización del backend.

Se permiten dos estrategias técnicas:
- streaming controlado por la API; o
- URL firmada de corta duración emitida después de verificar permisos.

En ambos casos:
- se verificará el rol, alcance institucional, finalidad y política de acceso;
- la autorización será temporal;
- no se expondrán secretos del almacenamiento;
- se registrará auditoría para documentos restringidos o sensibles;
- conocer un `documentId`, `versionId` u `objectKey` no será suficiente para descargar contenido.

## 7. Clasificación y permisos
La política de acceso deberá considerar al menos:
- `public`
- `authenticated`
- `internal`
- `restricted`
- `sensitive`

La clasificación del documento no podrá ser degradada automáticamente por el frontend.

La Biblioteca Virtual mostrará sólo documentos publicados y permitidos para el usuario actual.

El Gestor Documental administrativo podrá visualizar metadatos adicionales sin que ello implique autorización automática para descargar el contenido.

El Gran Archivero tendrá permisos propios de custodia, pero dichos permisos no implican acceso irrestricto a datos personales o sensibles fuera de la finalidad archivística autorizada.

## 8. Versionado
- Un documento puede poseer múltiples versiones.
- Sólo una versión podrá estar publicada como vigente por defecto cuando el dominio lo requiera.
- La publicación de una nueva versión no elimina la anterior.
- Las versiones reemplazadas se conservarán según política de retención y permisos.
- El historial de publicación deberá ser inmutable desde el punto de vista de auditoría.
- Los maestros de preservación del Gran Archivo no podrán sobrescribirse.

## 9. Eliminación y retención
La eliminación lógica deberá preceder a la eliminación física cuando existan requisitos de auditoría, retención o legal hold.

Antes de eliminar físicamente un objeto, el sistema deberá verificar:
- política de conservación;
- legal hold;
- dependencia con expediente o proceso vigente;
- dependencia archivística o histórica;
- clasificación y fundamento del tratamiento;
- autorización del actor.

Toda supresión física deberá dejar evidencia del objeto eliminado, hash, actor, fecha y fundamento, sin conservar innecesariamente el contenido eliminado.

## 10. Seguridad
Requisitos mínimos:
- TLS en tránsito;
- cifrado en reposo provisto por la plataforma o infraestructura;
- credenciales del storage fuera del repositorio;
- secretos suministrados por configuración segura;
- nombres de objeto no predecibles;
- límites configurables de tamaño;
- allow-list de extensiones/tipos cuando corresponda;
- validación del MIME real cuando sea posible;
- protección contra path traversal;
- protección contra sobreescritura accidental;
- rate limiting en cargas y descargas de alto volumen;
- backups y restauración verificable para colecciones críticas;
- buckets privados y bloqueo de acceso público por defecto;
- segregación de credenciales entre ambientes.

## 11. Auditoría
Se auditarán al menos:
- creación de documento;
- inicio/completitud/fallo de carga;
- análisis de seguridad;
- publicación y despublicación;
- cambio de clasificación;
- descarga de documentos restringidos o sensibles;
- cambio de retención;
- aplicación o levantamiento de legal hold;
- transferencia de custodia al Gran Archivo;
- eliminación lógica y física.

La auditoría no copiará el contenido binario, credenciales, claves físicas sensibles ni nombres innecesarios que aumenten exposición de datos.

## 12. Criterios de aceptación
1. Un archivo binario puede cargarse sin almacenarse en PostgreSQL.
2. La versión queda asociada a un documento y conserva SHA-256.
3. Un archivo no escaneado o rechazado no puede publicarse ni entregarse como contenido autorizado de Biblioteca.
4. Un usuario sin permiso no puede obtener el contenido aunque conozca el ID del documento, versión o clave técnica.
5. Biblioteca sólo muestra versiones publicadas autorizadas.
6. No existen buckets/contenedores públicos por defecto.
7. Las credenciales del proveedor no aparecen en frontend, logs ni repositorio.
8. El flujo soporta retención y legal hold antes de supresión física.
9. Existen pruebas de integración de autorización, carga, publicación y descarga.
10. El pipeline mantiene los gates de privacidad y clasificación de datos existentes.
11. El mismo núcleo binario puede ser consumido por Gestión Documental Operativa, Biblioteca Virtual y Gran Archivo sin duplicar innecesariamente archivos.
12. CENDOC no aparece como módulo, dependencia ni consumidor del almacenamiento documental.
