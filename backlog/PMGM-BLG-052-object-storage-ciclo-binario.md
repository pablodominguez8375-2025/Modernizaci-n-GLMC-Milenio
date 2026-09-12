# PMGM-BLG-052 — Object Storage y ciclo binario documental seguro

**Estado:** Listo para desarrollo  
**Prioridad:** P0  
**Versión objetivo:** v0.15.0  
**Requisitos:** PMGM-REQ-027, PMGM-REQ-026  
**Arquitectura:** PMGM-ADR-003, PMGM-ARCH-003

## Objetivo
Implementar el almacenamiento binario real que soporte Gestión Documental Operativa, Biblioteca Virtual y Gran Archivo sin guardar archivos en PostgreSQL ni exponer buckets públicos.

## Alcance del incremento

### 1. Proveedor y abstracción
- Mantener `IDocumentObjectStore` como contrato del dominio.
- Implementar adaptador S3-compatible con endpoint configurable.
- Usar AWS SDK for .NET v4 o equivalente aprobado.
- Bucket privado `pmgm-documents` para desarrollo/integración.
- Configuración por `DocumentStorage` y variables de entorno.
- Sin secretos en Git.

### 2. Desarrollo local
- Incorporar S3 local/emulado a `docker-compose.dev.yml`.
- Inicializar bucket privado de desarrollo.
- Configurar API para usar el endpoint interno del contenedor.
- Añadir variables de ejemplo sólo para desarrollo.

### 3. Carga real
Agregar endpoint:

`PUT /api/documentos/versiones/{versionId}/contenido`

Reglas:
- autenticación obligatoria;
- autorización por documento/organización;
- sólo estado `pending_upload`;
- body binario directo;
- `Content-Type` y `Content-Length` obligatorios en primer corte;
- tamaño máximo configurable;
- tamaño y tipo deben corresponder con metadata reservada;
- almacenamiento bajo `ObjectKey` generado por backend;
- prohibida la sobreescritura de un objeto existente;
- cálculo de SHA-256 desde el objeto persistido;
- transición a `uploaded` sólo tras confirmar almacenamiento, tamaño e integridad.

### 4. Descarga autorizada
Agregar endpoint administrativo:

`GET /api/documentos/versiones/{versionId}/contenido`

Agregar endpoint de Biblioteca:

`GET /api/biblioteca/{documentId}/contenido`

Reglas:
- autorización backend previa;
- Biblioteca resuelve exclusivamente la versión publicada;
- nunca devolver `ObjectKey`;
- `Cache-Control: private, no-store` cuando corresponda;
- auditar descarga de contenido restringido/sensible.

### 5. Antimalware
- Definir `IDocumentMalwareScanner`.
- Implementar primer proveedor real o servicio integrable.
- Estados: `uploaded → scanning → available|rejected`.
- `available` requiere resultado limpio y evidencia técnica.
- `error` del scanner no equivale a limpio.
- ningún archivo sin análisis exitoso puede publicarse.

### 6. Reconciliación
Servicio o comando para detectar:
- cargas `pending_upload` vencidas;
- objetos sin metadata;
- metadata `uploaded` sin objeto;
- discrepancias de tamaño/hash;
- rechazados pendientes de eliminación.

No eliminar automáticamente objetos con retención, legal hold o custodia archivística.

### 7. Auditoría
Eventos mínimos:
- inicio/completitud/fallo de carga;
- verificación de integridad;
- inicio/resultado de escaneo;
- autorización/denegación de descarga;
- detección de reconciliación;
- eliminación física autorizada.

No registrar contenido, secretos ni `ObjectKey` en auditoría general.

## Cambios de código previstos
- `PMGM.Api.csproj`: paquete S3.
- `Program.cs`: registro DI/configuración.
- `Modules/DocumentManagement/Storage/*`.
- `DocumentManagementEndpoints.cs`: carga/descarga.
- `DocumentManagementCodes.cs`: contratos/estados si procede.
- configuración local y `.env.example`.
- pruebas unitarias e integración.

## Pruebas de aceptación
- [ ] carga contra S3/emulador real;
- [ ] objeto existe físicamente;
- [ ] SHA-256 calculado desde objeto almacenado;
- [ ] tamaño real validado;
- [ ] segunda carga sobre misma versión rechazada;
- [ ] usuario no autorizado no carga;
- [ ] usuario no autorizado no descarga;
- [ ] Biblioteca descarga sólo versión publicada y `available`;
- [ ] versión `rejected` no puede descargarse como contenido normal ni publicarse;
- [ ] `ObjectKey` no aparece en DTO de Biblioteca;
- [ ] archivo nuevo no sobrescribe versión anterior;
- [ ] integración PostgreSQL + S3 en CI o job dedicado;
- [ ] gates de privacidad y clasificación siguen verdes.

## Fuera de alcance de v0.15.0
- OCR y búsqueda full-text dentro del contenido.
- CDN público.
- publicación anónima en Internet.
- preservación archivística avanzada.
- migración de formato del Gran Archivo.
- proveedor productivo definitivo.

## Dependencias posteriores desbloqueadas
1. PMGM-BLG-050 — Biblioteca Virtual.
2. PMGM-BLG-051 — Gran Archivo / Gran Archivero.
3. Gestión de adjuntos oficiales de Gran Secretaría.
4. Actas y documentos versionados de Gestión Logial.

## Definición de terminado
Una versión documental puede reservarse, recibir binario real en almacenamiento privado, verificar integridad, pasar control antimalware, quedar disponible y ser descargada únicamente por un actor autorizado; el flujo cuenta con pruebas de integración y auditoría suficiente para continuar con Biblioteca Virtual y Gran Archivo.
