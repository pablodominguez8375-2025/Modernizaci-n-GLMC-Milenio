# QA — Evidencia histórica de publicación (#237)

## Alcance aprobado

REQ-025 conserva Fotografía, Nombre completo y Taller en el portal autenticado transversal. La política `system.publication.candidate.visible_fields` permite registrar vigencias y fundamentos para esos tres campos; no permite agregar datos privados ni omitir un campo aprobado. No modifica roles, regularidades, derechos de ceremonia ni plazo de publicación.

## Persistencia y compatibilidad

La aprobación de Gran Secretaría guarda `publicationEvidence` (schema 1, PhotoVersionId y política con VersionId/campos/vigencia/fundamento) en su auditoría existente, en el mismo SaveChanges que la publicación. La foto pública utiliza la versión documental guardada allí. El expediente privado sigue utilizando su propia referencia mutable. La autorización copia la evidencia original y registra su estado. No se requiere migración; la referencia sigue siendo privada, y el portal no expone IDs documentales ni almacenamiento.

Una publicación histórica sin evidencia mantiene la lectura fotográfica anterior y su autorización queda `legacy_not_recorded`. Esto no certifica que la foto actual sea la foto histórica. Un registro de evidencia reconocido pero inválido bloquea la foto pública y no recurre al expediente mutable. No hay reconstrucción retrospectiva ni actualización de auditorías históricas.

La vigencia de la política se resuelve por intervalos en fecha `America/Santiago`, incluso cuando una versión anterior lleva estado `retired` al programar su sucesora. Solo se agregan fechas actuales/futuras y posteriores a todas las registradas. Sin versión persistida se usa explícitamente la decisión base REQ-025, con VersionId nulo.

## Verificación automatizada

- HTTP PostgreSQL: aprobación real con expediente completo; foto A publicada, expediente pasa a B y luego sin foto, portal mantiene A; repetición idempotente; políticas futuras no reemplazan la actual; rechazo sin balotaje y autorización con balotaje aprobado, pago y regularidades conserva foto/política iniciales; expiración revoca foto.
- HTTP PostgreSQL: configuración por miembro rechazada; PII, omisiones, retroactividad y vigencia intermedia rechazadas; legado conserva compatibilidad y estado sin evidencia; evidencia inválida devuelve 404 y PhotoUrl nulo.
- Unitarias: round-trip por sanitizador de auditoría, allowlist, metadatos ausentes e inválidos.
- Frontend: catálogo y adaptador QA preservan política vigente al programar sucesoras y rechazan PII/retroactividad. No simulan la persistencia real del backend; esta se verifica mediante HTTP PostgreSQL.

La prueba HTTP sustituye únicamente el transporte de notificaciones por un sumidero; no certifica SMTP ni entrega. Los servicios de acceso y las persistencias de publicación, auditoría, configuración, ficha y documentos son reales. Los fixtures son sintéticos. Las pruebas requieren `PMGM_TEST_POSTGRES`; CI debe acreditar ejecución con PostgreSQL y cero omisiones.

## Límites operacionales

La conservación depende de la auditoría y de las versiones documentales existentes: no eliminar sus objetos o registros. No activar srv01 ni declarar QA/UAT. El paquete y Pages se verifican tras la integración y se registran en el handoff de cierre.
