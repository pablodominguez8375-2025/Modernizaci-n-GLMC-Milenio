# PMGM-BASE-001 — Estado Maestro del Proyecto Milenio

**Estado:** Activo  
**Rama de trabajo:** `dev`  
**Rama estable:** `main`  
**Corte funcional actual:** v0.14.x en `dev`  
**Siguiente incremento:** v0.15.0 — Object Storage y ciclo binario seguro

## Visión
Proyecto Milenio construye una plataforma institucional unificada para la Gran Logia Mixta de Chile, con acceso único, base maestra, trazabilidad histórica, seguridad por roles y módulos integrados.

## Núcleo técnico
- Backend ASP.NET Core / .NET 10.
- PostgreSQL + Entity Framework Core/Npgsql.
- Frontend React 19 + TypeScript 6 + Vite 8 (PMGM-ADR-002).
- Docker / Docker Compose.
- GitHub Actions CI.
- JWT/OIDC con proveedor definitivo pendiente.
- Cultura de presentación `es-CL`.
- Zona horaria institucional `America/Santiago`.
- Monolito modular con límites de dominio explícitos.
- Object Storage desacoplado para binarios documentales (PMGM-ADR-003).
- Ciclo binario seguro especificado para implementación en PMGM-ARCH-003.

## Principios funcionales consolidados
1. Una persona posee una identidad maestra única.
2. Un miembro mantiene su historia aun cuando cambia de Taller.
3. El Taller de origen conserva su actividad histórica y el Taller receptor crea una nueva pertenencia.
4. Régimen Interior dispone de visión transversal para control y toma de decisiones.
5. Gran Secretaría emite documentos oficiales y autorizaciones a partir de validaciones de las áreas responsables.
6. Gran Tesorería es fuente responsable de regularidad financiera.
7. Gran Hospitalaria es fuente responsable de regularidad de reposiciones hospitalarias.
8. Una ceremonia sólo se autoriza si las validaciones obligatorias aplicables están conformes o existe excepción formal auditada.
9. En iniciaciones, el insinuado debe publicarse previamente durante el plazo institucional configurado; valor inicial 20 días.
10. El insinuado no se convierte en miembro activo antes de la iniciación efectivamente registrada.
11. Las reglas críticas deben ser parametrizables y versionadas.
12. Los permisos dependen de rol, finalidad y contexto institucional.
13. No se utilizará RUT como clave primaria técnica.
14. Las reservas de templos y salas no pueden solaparse y deben mantener trazabilidad de actor y resultado.
15. La autorización formal de una ceremonia sólo puede ser emitida por Gran Secretaría una vez autorizado el flujo institucional previo.
16. Los binarios documentales no se almacenarán como contenido principal en PostgreSQL y no serán públicos por defecto.
17. Las notificaciones se desacoplarán de los módulos de negocio y deberán ser idempotentes y auditables.
18. El calendario institucional proyectará eventos desde sus fuentes de verdad sin duplicarlas.
19. Biblioteca Virtual y Gran Archivo son módulos distintos: Biblioteca publica y facilita consulta; Gran Archivo custodia y preserva el patrimonio documental histórico.
20. Gestión Documental Operativa, Biblioteca Virtual y Gran Archivo tienen responsabilidades separadas y no deben fusionarse en un único dominio funcional.
21. Los tres dominios documentales comparten un núcleo técnico binario seguro, sin escritura cruzada directa entre módulos.
22. CENDOC no forma parte del alcance del Proyecto Milenio.

## Requisitos funcionales formalizados
- PMGM-REQ-021 — Régimen Interior: reportes, control histórico y apoyo a decisiones.
- PMGM-REQ-022 — Gran Secretaría: decretos, comunicados, autorizaciones y gestión de espacios.
- PMGM-REQ-022-ADD-001 — Extensión Gran Secretaría: Hospitalaria y publicación previa.
- PMGM-REQ-023 — Transferencia entre Talleres.
- PMGM-REQ-024 — Localización Español (Chile).
- PMGM-REQ-025 — Elegibilidad de ceremonias y publicación de insinuados.
- PMGM-REQ-026 — Cumplimiento Ley 21.719.
- PMGM-REQ-027 — Almacenamiento seguro de documentos y versiones.
- PMGM-REQ-028 — Notificaciones institucionales multicanal.
- PMGM-REQ-029 — Calendario institucional unificado.
- PMGM-REQ-030 — Gran Archivero y Archivo Histórico Institucional.
- PMGM-REQ-031 — Biblioteca Virtual.

## Decisiones y arquitectura
- PMGM-ADR-001 — Arquitectura base del proyecto.
- PMGM-ADR-002 — React + TypeScript + Vite para frontend.
- PMGM-ADR-003 — Object Storage desacoplado para archivos binarios.
- PMGM-ARCH-002 — Límites entre Gestión Documental Operativa, Biblioteca Virtual y Gran Archivo.
- PMGM-ARCH-003 — Ciclo binario seguro y contrato de Object Storage.

## Estado de implementación en `dev`

### Núcleo institucional
- Personas, organizaciones y miembros.
- Pertenencias históricas.
- Eventos de estado y grado.
- Cargos y períodos.
- Transferencias entre Talleres conservando historia de origen y nueva pertenencia de destino.
- RBAC inicial por Orden/Taller.

### Régimen Interior
- Reportes institucionales iniciales.
- Lectura transversal controlada por rol.
- Historial de membresía y estados institucionales.

### Tesorería y Hospitalaria
- Snapshots de regularidad institucional.
- Fuentes responsables diferenciadas.
- Integración con elegibilidad de ceremonias.

### Ceremonias
- Solicitudes de iniciación, aumento de salario y exaltación.
- Validación de Régimen Interior.
- Motor explicable de elegibilidad.
- Validaciones obligatorias de Tesorería y Hospitalaria.
- Publicación de insinuados con plazo configurable.
- Regla inicial de 20 días.
- Portal API y frontend de publicaciones vigentes.
- Autorización institucional y congelamiento de evidencias.
- Auditoría persistente de creación, validación, publicación y autorización/rechazo.

### Gran Secretaría
- Catálogo de templos y salas de Secretaría.
- Consulta de disponibilidad por rango horario.
- Reservas con prevención transaccional de solapamientos.
- Cancelación de reservas.
- Emisión de decretos y comunicados.
- Emisión formal de autorización de ceremonia sólo cuando el flujo institucional está previamente autorizado.
- Vinculación opcional de autorización formal con reserva de templo/sala.
- Auditoría de espacios, reservas, conflictos y documentos.

### Gestión Logial
- Tenidas.
- Registro de asistencia con enfoque append-only.
- Actas versionadas.
- Interfaz inicial conectada a la intranet.

### Gestión Documental Operativa
- Colecciones documentales.
- Metadata de documentos.
- Versiones, clasificación y políticas de acceso.
- Estados técnicos `pending_upload`, `uploaded`, `scanning`, `available`, `rejected`.
- `ObjectKey` opaco y contrato `IDocumentObjectStore` ya definidos en código.
- Endpoints de consulta y publicación sin exposición de secretos de almacenamiento.
- PMGM-ARCH-003 define carga binaria, descarga autorizada, integridad, scanner y reconciliación.
- Pendiente de v0.15.0: adaptador S3-compatible real, binarios, antimalware, descarga autorizada y pruebas de integración.

### Biblioteca Virtual
- Requisito funcional formalizado en PMGM-REQ-031.
- Backlog independiente PMGM-BLG-050.
- Separación explícita respecto de Gran Archivo.
- Flujo editorial, taxonomías, búsqueda, políticas de acceso y publicación controlada definidos.
- Integración prevista con Docencia, Gestión Documental y Gran Archivo mediante referencias/derivados autorizados.
- Implementación funcional completa pendiente sobre el núcleo documental seguro.

### Gran Archivo / Gran Archivero
- Requisito funcional formalizado en PMGM-REQ-030.
- Backlog independiente PMGM-BLG-051.
- Separación explícita respecto de Biblioteca Virtual.
- Custodia histórica, clasificación archivística, transferencias, digitalización, préstamos y cadena de custodia definidos.
- Integración prevista con Gran Secretaría, Talleres y Biblioteca Virtual.
- Implementación pendiente posterior al núcleo de Object Storage.

### Privacidad y Ley 21.719
- Registro de actividades de tratamiento.
- Retención y legal holds.
- Solicitudes de titulares.
- Encargados y transferencias internacionales.
- Evaluaciones de impacto.
- Gestión de incidentes y decisiones de notificación.
- Gates estructurales de privacidad y clasificación de datos en CI.

### Calidad e infraestructura
- PostgreSQL 17 real en CI.
- Migraciones completas desde base vacía.
- Migration Safety Gate.
- Privacy Gate — Ley 21.719.
- Data Classification Gate.
- Pruebas unitarias e integración PostgreSQL.
- Pruebas HTTP end-to-end en módulos críticos.
- Auditoría con actor y correlation ID.

## Backlog activo formalizado
1. PMGM-BLG-052 — Object Storage y ciclo binario documental seguro (`v0.15.0`, P0).
2. PMGM-BLG-028 — Notificaciones internas + correo institucional (`v0.16.0`).
3. PMGM-BLG-029 — Calendario institucional unificado (`v0.16.x`).
4. PMGM-BLG-050 — Biblioteca Virtual.
5. PMGM-BLG-051 — Gran Archivo / Gran Archivero.

## Orden de ejecución recomendado
1. PMGM-BLG-052 — Object Storage, carga, integridad, antimalware y descarga autorizada.
2. Biblioteca Virtual básica sobre el núcleo seguro.
3. Gran Archivo: estructura archivística, transferencias y custodia.
4. Digitalización, preservación y préstamos del Gran Archivo.
5. Notificaciones y calendario institucional.
6. Cliente OIDC/PKCE y proveedor definitivo de identidad.
7. OpenAPI automatizado para frontend.
8. Docencia y conexión con Biblioteca Virtual.
9. Integraciones externas y hardening de producción.

## Exclusiones explícitas
- CENDOC no se incluye como módulo, dependencia ni componente del Proyecto Milenio.

## Criterio de avance
Ningún incremento se considera estable si rompe compilación, migraciones, gates o pruebas del pipeline. Los cambios continúan en `dev` hasta revisión y aprobación para `main`.
