# PMGM-REQ-027 — Almacenamiento seguro de documentos y versiones

**Estado:** Aprobado funcionalmente  
**Prioridad:** P0  
**Áreas:** Gestor Documental, Biblioteca Virtual, Secretaría, Gran Secretaría, CENDOC  
**Dependencias:** PMGM-REQ-026, módulo DocumentManagement existente

## 1. Objetivo
Completar el Gestor Documental y la Biblioteca Virtual incorporando almacenamiento binario seguro, versionado íntegro, publicación controlada, trazabilidad y políticas de conservación coherentes con la clasificación del documento y la Ley 21.719.

## 2. Principio arquitectónico
- PostgreSQL almacenará metadatos, relaciones, permisos, hashes, estados y auditoría.
- Los archivos binarios se almacenarán fuera de la base de datos en un servicio de objetos.
- El backend utilizará una abstracción de almacenamiento para evitar dependencia rígida de un proveedor.
- Ningún bucket o contenedor será público por defecto.
- El frontend nunca recibirá credenciales permanentes del proveedor de almacenamiento.

## 3. Flujo de carga
Toda nueva versión deberá seguir, como mínimo, el flujo:
1. Solicitud de carga autorizada.
2. Reserva de versión en estado `pending_upload`.
3. Carga del binario.
4. Validación de tamaño y tipo permitido.
5. Cálculo de hash SHA-256.
6. Análisis antimalware o control equivalente antes de publicación.
7. Registro de evidencia técnica del análisis.
8. Cambio a estado `ready` si supera controles.
9. Publicación explícita por usuario autorizado cuando corresponda.

Un archivo que no complete el proceso no debe quedar accesible desde Biblioteca ni desde vistas de publicación.

## 4. Estados mínimos de versión
- `pending_upload`
- `uploaded`
- `scanning`
- `rejected`
- `ready`
- `published`
- `superseded`
- `archived`
- `deleted`

Las transiciones deberán ser validadas por el backend y auditadas.

## 5. Integridad
Cada versión deberá registrar:
- identificador de versión;
- documento al que pertenece;
- nombre original normalizado;
- tipo MIME detectado;
- tamaño;
- SHA-256;
- clave técnica del objeto;
- fecha de carga;
- actor que cargó;
- resultado del control de seguridad;
- fecha de publicación;
- actor que publicó;
- versión anterior, cuando corresponda.

La clave técnica del objeto nunca deberá exponerse como dato público ni ser utilizada como nombre funcional del documento.

## 6. Acceso y descarga
La descarga deberá resolverse mediante autorización del backend.

Se permiten dos estrategias técnicas:
- streaming controlado por la API; o
- URL firmada de corta duración emitida después de verificar permisos.

En ambos casos:
- se verificará el rol, alcance institucional y política de acceso;
- la autorización será temporal;
- no se expondrán secretos del almacenamiento;
- se registrará auditoría para documentos restringidos o sensibles.

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

## 8. Versionado
- Un documento puede poseer múltiples versiones.
- Sólo una versión podrá estar publicada como vigente por defecto.
- La publicación de una nueva versión no elimina la anterior.
- Las versiones reemplazadas se conservarán según política de retención y permisos.
- El historial de publicación deberá ser inmutable desde el punto de vista de auditoría.

## 9. Eliminación y retención
La eliminación lógica deberá preceder a la eliminación física cuando existan requisitos de auditoría, retención o legal hold.

Antes de eliminar físicamente un objeto, el sistema deberá verificar:
- política de conservación;
- legal hold;
- dependencia con expediente o proceso vigente;
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
- validación del MIME real;
- protección contra path traversal;
- protección contra sobreescritura accidental;
- rate limiting en cargas y descargas de alto volumen;
- backups y restauración verificable para colecciones críticas.

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
- eliminación lógica y física.

## 12. Criterios de aceptación
1. Un archivo binario puede cargarse sin almacenarse en PostgreSQL.
2. La versión queda asociada a un documento y conserva SHA-256.
3. Un archivo no escaneado o rechazado no puede publicarse.
4. Un usuario sin permiso no puede obtener el contenido aunque conozca el ID del documento o versión.
5. Biblioteca sólo muestra versiones publicadas autorizadas.
6. No existen buckets/contenedores públicos por defecto.
7. Las credenciales del proveedor no aparecen en frontend, logs ni repositorio.
8. El flujo soporta retención y legal hold antes de supresión física.
9. Existen pruebas de integración de autorización, carga, publicación y descarga.
10. El pipeline mantiene los gates de privacidad y clasificación de datos existentes.
