# PMGM-ARCH-003 — Ciclo binario seguro y contrato de Object Storage

**Estado:** Aprobado para implementación  
**Versión objetivo:** v0.15.0  
**Dependencias:** PMGM-ADR-003, PMGM-REQ-026, PMGM-REQ-027, PMGM-ARCH-002  
**Dominios consumidores:** Gestión Documental Operativa, Biblioteca Virtual, Gran Archivo / Gran Archivero

## 1. Propósito
Definir el contrato técnico único para recibir, almacenar, verificar, analizar, autorizar, entregar y eventualmente eliminar archivos binarios del Proyecto Milenio.

El objetivo es impedir que cada módulo implemente su propio mecanismo de archivos y asegurar que Biblioteca Virtual y Gran Archivo puedan evolucionar sobre un núcleo seguro sin fusionarse funcionalmente.

## 2. Componentes

### DocumentManagement
Responsable de:
- reserva de versiones;
- metadata técnica y funcional;
- estado del procesamiento;
- hash de integridad;
- asociación entre documento y binario;
- autorización de operaciones;
- auditoría documental.

### IDocumentObjectStore
Contrato de infraestructura responsable de:
- almacenar un stream bajo una clave opaca;
- abrir un stream de lectura;
- verificar existencia;
- eliminar físicamente cuando exista autorización previa.

El contrato no decide permisos, retención, clasificación ni publicación.

### Proveedor S3-compatible
Implementación inicial recomendada para desarrollo/integración mediante AWS SDK for .NET y endpoint S3 configurable.

La infraestructura local podrá usar un emulador S3. La producción podrá usar AWS S3 u otro proveedor S3-compatible aprobado, sin cambiar el dominio.

### Malware Scanner
Contrato independiente del Object Store. Debe recibir el contenido o una referencia interna autorizada y devolver un resultado verificable.

Estados mínimos del análisis:
- `pending`
- `clean`
- `infected`
- `error`

No se considera aprobado un archivo cuando el scanner devuelve `error`.

## 3. Máquina de estados técnica

Flujo inicial:

`pending_upload → uploaded → scanning → available`

Rutas de rechazo:

`uploaded → rejected`

`scanning → rejected`

Estados terminales o de lifecycle posterior podrán añadirse sin alterar el principio de que sólo `available` puede alimentar una publicación o descarga normal.

### Reglas
1. `pending_upload` no posee un binario confirmado.
2. `uploaded` exige existencia física y SHA-256 válido.
3. `scanning` exige binario íntegro y privado.
4. `available` exige análisis de seguridad exitoso y evidencia asociada.
5. `rejected` nunca puede publicarse ni descargarse por usuarios finales.
6. Las transiciones se realizan exclusivamente por backend.
7. Toda transición registra actor/sistema, fecha, resultado y correlation ID.

## 4. Clave de objeto
Formato base:

`documents/{documentId:N}/{versionId:N}`

Reglas:
- no incluir nombre de persona, RUT, email, título, nombre del Taller ni clasificación;
- no usar nombre original del archivo;
- la clave no se devuelve en DTO públicos;
- una versión genera una clave nueva; no se sobrescribe la versión anterior;
- la clave debe ser estable durante la vida física del objeto salvo migración formal registrada.

## 5. Flujo de carga API

### Paso 1 — reservar versión
`POST /api/documentos/{documentId}/versiones`

Resultado:
- `versionId`
- número de versión
- metadata esperada
- estado `pending_upload`

No devuelve credenciales ni clave física.

### Paso 2 — cargar contenido
Contrato objetivo:

`PUT /api/documentos/versiones/{versionId}/contenido`

Características:
- autenticado;
- autorizado contra documento y organización;
- body binario directo;
- `Content-Type` obligatorio;
- `Content-Length` obligatorio en el primer corte;
- tamaño máximo configurable;
- debe coincidir con la metadata reservada o producir conflicto;
- nunca acepta path o nombre físico proporcionado por el cliente.

Proceso:
1. validar versión y permiso;
2. validar estado `pending_upload`;
3. validar tamaño y tipo declarados;
4. almacenar en Object Storage privado;
5. volver a abrir el objeto desde storage;
6. calcular SHA-256 desde el contenido persistido;
7. verificar tamaño real;
8. actualizar metadata;
9. pasar a `uploaded`;
10. auditar resultado.

Si falla el paso posterior al almacenamiento, el objeto queda marcado para reconciliación/limpieza y nunca como disponible.

## 6. Flujo de análisis antimalware

Contrato objetivo:

`POST /api/documentos/versiones/{versionId}/analizar`

En producción podrá ejecutarse mediante worker/cola; para el MVP puede invocarse de forma controlada siempre que el contrato permanezca desacoplado.

Proceso:
1. comprobar estado `uploaded`;
2. mover a `scanning`;
3. abrir stream privado;
4. ejecutar scanner;
5. registrar referencia/evidencia técnica sin incluir contenido;
6. si limpio → `available`;
7. si infectado/error no recuperable → `rejected`;
8. auditar.

## 7. Flujo de descarga

### Gestión documental
Contrato objetivo:

`GET /api/documentos/versiones/{versionId}/contenido`

Sólo si:
- el actor está autenticado;
- posee permiso sobre el documento;
- la finalidad lo permite;
- el estado técnico permite entrega;
- no existe restricción adicional de privacidad, retención o custodia.

### Biblioteca Virtual
La Biblioteca no descarga desde el bucket. Solicita al backend el contenido de la versión publicada autorizada.

Contrato objetivo:

`GET /api/biblioteca/{documentId}/contenido`

El backend resuelve internamente `PublishedVersionId` y vuelve a validar política de acceso.

### Gran Archivo
La descarga/consulta archivística utilizará un endpoint propio o servicio interno, pero deberá resolver el contenido a través del mismo núcleo seguro.

## 8. Estrategia de entrega
Primer corte recomendado: **streaming desde API**.

Razones:
- mantiene la autorización en un punto único;
- simplifica el MVP;
- evita problemas de URL firmada con proveedores S3-compatible distintos;
- permite auditar descargas sensibles antes de entregar bytes.

URL firmada puede incorporarse posteriormente para archivos grandes, con expiración corta y autorización previa.

## 9. Configuración
Sección propuesta:

`DocumentStorage`

Campos:
- `Provider`
- `Bucket`
- `Region`
- `ServiceUrl` opcional
- `AccessKey` sólo en desarrollo/secret store
- `SecretKey` sólo en desarrollo/secret store
- `ForcePathStyle`
- `MaxUploadBytes`

Producción no deberá depender de secretos escritos en archivos versionados.

## 10. Desarrollo local
Se adopta para integración un endpoint S3 local/emulado, separado de PostgreSQL.

La configuración local debe:
- crear bucket privado `pmgm-documents`;
- no exponerlo como público;
- usar credenciales exclusivas de desarrollo;
- permitir pruebas de carga, lectura, existencia y borrado;
- no ser considerada configuración productiva.

## 11. Reconciliación
Debe existir un proceso capaz de detectar:
- versión `pending_upload` vencida sin objeto;
- objeto existente sin metadata correspondiente;
- metadata `uploaded` cuyo objeto falta;
- objeto rechazado pendiente de eliminación según política;
- discrepancia de hash o tamaño.

La reconciliación nunca elimina automáticamente un objeto sujeto a legal hold, retención activa o custodia archivística.

## 12. Integridad periódica
Para Gran Archivo se debe permitir posteriormente una tarea programada que:
- reabra el objeto;
- recalcule SHA-256;
- compare con hash registrado;
- deje evidencia de fecha y resultado;
- genere incidente/alerta ante corrupción o ausencia.

## 13. Límites de seguridad
- bucket privado por defecto;
- acceso del storage sólo desde backend/worker;
- no permitir ACL públicas desde la aplicación;
- no registrar credenciales ni URL firmadas completas en logs;
- no registrar contenido;
- validar `Content-Type` y tamaño;
- limitar frecuencia y concurrencia de cargas;
- proteger contra sobreescritura de claves;
- rechazar cargas a versiones fuera de estado;
- autorización backend en todas las lecturas;
- respuestas `Cache-Control: private, no-store` para contenido restringido/sensible.

## 14. Auditoría mínima
Eventos sugeridos:
- `documents.content.upload_started`
- `documents.content.upload_completed`
- `documents.content.upload_failed`
- `documents.content.integrity_verified`
- `documents.content.scan_started`
- `documents.content.scan_completed`
- `documents.content.scan_rejected`
- `documents.content.download_authorized`
- `documents.content.download_denied`
- `documents.content.deleted`
- `documents.content.reconciliation_detected`

No incluir `ObjectKey`, secretos ni contenido en metadata de auditoría general.

## 15. Pruebas obligatorias
1. carga real contra S3/emulador;
2. objeto físicamente existente después de carga válida;
3. hash SHA-256 calculado desde el objeto persistido;
4. tamaño real coincide con metadata;
5. intento de segunda carga sobre la misma versión es rechazado;
6. usuario sin permiso no puede cargar;
7. usuario sin permiso no puede descargar;
8. `pending_upload`, `uploaded`, `scanning` y `rejected` no se publican;
9. sólo `available` puede alimentar Biblioteca;
10. objeto inexistente produce error controlado y auditoría;
11. claves físicas no aparecen en DTO de Biblioteca;
12. una versión nueva no sobrescribe el binario anterior;
13. CENDOC no aparece como dependencia ni consumidor.

## 16. Criterio de terminado v0.15.0
El bloque se considera terminado cuando una versión documental puede reservarse, recibir un archivo real en almacenamiento S3-compatible privado, verificar tamaño/hash, superar el control de seguridad, quedar `available` y ser descargada por un usuario autorizado, manteniendo auditoría y pruebas de integración reproducibles.
