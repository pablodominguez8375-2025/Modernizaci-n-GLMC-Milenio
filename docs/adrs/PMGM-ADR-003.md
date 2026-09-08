# PMGM-ADR-003 — Almacenamiento binario desacoplado mediante Object Storage

## Estado
Aceptada.

## Contexto
Proyecto Milenio ya dispone de metadata, clasificación, versionado y publicación documental en PostgreSQL. El siguiente incremento requiere almacenar archivos reales sin convertir la base de datos relacional en repositorio de binarios ni acoplar el dominio a un proveedor cloud específico.

El almacenamiento debe además respetar mínimo privilegio, retención, legal hold, trazabilidad y las reglas de privacidad definidas en PMGM-REQ-026 y PMGM-REQ-027.

Biblioteca Virtual, Gestión Documental Operativa y Gran Archivo / Gran Archivero son dominios funcionales separados, pero pueden compartir un mismo núcleo técnico de almacenamiento binario seguro. CENDOC no forma parte del Proyecto Milenio.

## Decisión
Se adopta una arquitectura de almacenamiento binario desacoplado:

- PostgreSQL conservará únicamente metadata, relaciones, estados, hashes, políticas y auditoría.
- Los binarios se almacenarán en un servicio de Object Storage.
- El backend expondrá una interfaz propia de almacenamiento; el dominio no dependerá directamente del SDK de un proveedor.
- Para desarrollo local y entornos controlados se permitirá un servicio compatible con S3 o un emulador con semántica equivalente.
- La selección del proveedor productivo será configurable y requerirá revisión de seguridad, residencia/transferencia de datos, respaldo, cifrado y continuidad operacional.
- Los contenedores/buckets serán privados por defecto.
- Las descargas se autorizarán en el backend y se entregarán por streaming o URL firmada de corta duración.
- Las claves de objetos serán opacas, no semánticas y no sustituirán el identificador funcional del documento.
- Los módulos de Biblioteca Virtual y Gran Archivo no escribirán directamente en buckets; consumirán contratos del núcleo documental.

## Razones
1. Evita crecimiento innecesario y costoso de PostgreSQL por binarios.
2. Separa ciclo de vida documental de persistencia física.
3. Permite cambiar proveedor sin reescribir el dominio.
4. Facilita versionado, integridad por hash, retención y borrado físico controlado.
5. Reduce la posibilidad de exponer archivos mediante rutas públicas permanentes.
6. Permite ejecutar el MVP localmente con semántica S3 equivalente a la que puede utilizarse en producción.
7. Evita duplicación de archivos cuando un documento operativo pasa a custodia histórica o genera una copia autorizada para Biblioteca Virtual.

## Alternativas evaluadas

### Binarios dentro de PostgreSQL
Se descarta como estrategia principal porque mezcla dos responsabilidades, complica backup/restauración a gran escala y reduce flexibilidad operativa.

### Sistema de archivos local del servidor
Se descarta como persistencia productiva porque dificulta escalamiento, alta disponibilidad, replicación, trazabilidad y despliegues reemplazables por contenedor.

### Acoplamiento directo a un único proveedor cloud
Se descarta por ahora. La institución debe poder evaluar proveedor, ubicación de datos y condiciones contractuales sin convertir esa elección en una dependencia del dominio.

### Bucket público para Biblioteca Virtual
Se descarta. Incluso el material de consulta se entregará desde una política controlada. Una eventual publicación verdaderamente pública deberá ser una decisión explícita y separada, no una consecuencia de la configuración del almacenamiento.

## Consecuencias

### Positivas
- Mejor separación de responsabilidades.
- Portabilidad entre ambientes.
- Posibilidad de políticas de lifecycle y respaldo independientes.
- Descargas temporales y privadas.
- Evolución coherente hacia Biblioteca Virtual y Gran Archivo sin fusionar ambos dominios.
- Posibilidad de compartir un binario de origen manteniendo metadatos y permisos separados.

### Costos
- Se incorpora un servicio de infraestructura adicional.
- Será necesario administrar credenciales, backup y monitoreo del almacenamiento.
- El flujo de carga requiere estados intermedios y manejo de fallos.
- Se debe implementar reconciliación de objetos huérfanos y cargas incompletas.
- El análisis antimalware y la verificación de integridad agregan procesamiento asíncrono o etapas adicionales.

## Reglas de implementación
- Ningún secreto de storage podrá almacenarse en Git.
- El frontend no usará credenciales permanentes del storage.
- El backend verificará autorización antes de entregar contenido.
- La clave física del objeto no se expondrá en respuestas públicas.
- Toda versión publicable tendrá hash SHA-256 registrado.
- Los objetos rechazados por controles de seguridad no serán descargables por usuarios finales.
- La eliminación física deberá respetar retención y legal hold.
- Las pruebas de integración deberán utilizar un proveedor de storage real o emulado con semántica equivalente, no sólo mocks unitarios.
- La configuración de desarrollo no se considerará válida para producción sin revisión explícita de seguridad.
- La infraestructura productiva deberá bloquear acceso público al bucket/contenedor por defecto.

## Dependencias
- PMGM-REQ-026 — Cumplimiento Ley 21.719.
- PMGM-REQ-027 — Almacenamiento seguro de documentos y versiones.
- PMGM-ARCH-002 — Límites entre Gestión Documental, Biblioteca Virtual y Gran Archivo.
