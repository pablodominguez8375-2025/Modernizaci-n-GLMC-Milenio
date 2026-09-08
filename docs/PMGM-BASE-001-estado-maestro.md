# PMGM-BASE-001 — Estado Maestro del Proyecto Milenio

**Estado:** Activo  
**Rama de trabajo:** `dev`  
**Rama estable:** `main`  
**Último incremento estable en CI:** v0.15.0 — núcleo documental seguro  
**Incremento en desarrollo:** v0.16.0 — Biblioteca Virtual con catálogo y seguridad por grado

## Visión
Proyecto Milenio construye una plataforma institucional unificada para la Gran Logia Mixta de Chile, con acceso único, base maestra, trazabilidad histórica, seguridad por roles/grado/contexto y módulos integrados.

## Núcleo técnico
- Backend ASP.NET Core / .NET 10.
- PostgreSQL + Entity Framework Core/Npgsql.
- Frontend React 19 + TypeScript 6 + Vite 8.
- Docker / Docker Compose.
- GitHub Actions CI.
- JWT/OIDC con proveedor definitivo pendiente.
- Cultura `es-CL` y zona horaria `America/Santiago`.
- Monolito modular con límites de dominio explícitos.
- Object Storage S3-compatible desacoplado para binarios.
- MinIO privado para desarrollo/CI/QA reproducible.
- ClamAV integrado al ciclo antimalware.
- SHA-256, validación MIME/firma y reconciliación segura.

## Principios funcionales consolidados
1. Una persona posee una identidad maestra única.
2. Un miembro mantiene su historia aun cuando cambia de Taller.
3. El Taller de origen conserva historia y el receptor crea una nueva pertenencia.
4. Régimen Interior dispone de visión transversal autorizada.
5. Gran Secretaría emite documentos oficiales y autorizaciones a partir de validaciones responsables.
6. Gran Tesorería es fuente de regularidad financiera.
7. Gran Hospitalaria es fuente de regularidad de reposiciones.
8. Las ceremonias sólo se autorizan con validaciones obligatorias conformes o excepción formal auditada.
9. En iniciaciones, el insinuado se publica durante el plazo configurable; valor inicial 20 días.
10. El insinuado no se convierte en miembro activo antes de iniciación registrada.
11. Reglas críticas son parametrizables y versionadas.
12. Permisos dependen de rol, grado, finalidad y contexto institucional.
13. El RUT no se utiliza como PK técnica.
14. Reservas de espacios no se solapan y son auditables.
15. Gran Secretaría sólo emite autorización formal de ceremonia tras el flujo institucional previo.
16. Binarios documentales permanecen fuera de PostgreSQL y privados por defecto.
17. Notificaciones se desacoplan de módulos de negocio.
18. Calendario proyecta eventos desde sus fuentes sin duplicarlas.
19. Biblioteca Virtual y Gran Archivo son módulos distintos.
20. Gestión Documental Operativa, Biblioteca Virtual y Gran Archivo mantienen responsabilidades separadas.
21. Los dominios documentales comparten núcleo binario seguro sin escritura cruzada directa.
22. CENDOC no forma parte del alcance del Proyecto Milenio.
23. En Biblioteca Virtual, el grado mínimo es acumulativo: mínimo 1 → 1/2/3+, mínimo 2 → 2/3+, mínimo 3 → 3+.
24. El grado efectivo se obtiene de la historia institucional de Membresía; nunca de un valor editable por el usuario.
25. En Gran Archivo, descubrir una ficha y acceder al contenido son permisos distintos.
26. El Gran Archivero puede administrar permisos por grado, hermano, Taller, cargo, órgano y vigencia dentro de sus competencias, sin acceso universal implícito.
27. Ventana Fraterna es comunidad interna y no un canal oficial de Gran Secretaría.
28. El Muro Institucional de Gran Secretaría es el canal oficial de publicaciones institucionales dentro de la intranet.
29. Todo ambiente QA debe poder levantarse desde documentación versionada y configuración reproducible.

## Requisitos funcionales formalizados
- PMGM-REQ-021 — Régimen Interior: reportes, control histórico y apoyo a decisiones.
- PMGM-REQ-022 — Gran Secretaría: decretos, comunicados, autorizaciones y espacios.
- PMGM-REQ-022-ADD-001 — Extensión Gran Secretaría: Hospitalaria y publicación previa.
- PMGM-REQ-023 — Transferencia entre Talleres.
- PMGM-REQ-024 — Localización Español (Chile).
- PMGM-REQ-025 — Elegibilidad de ceremonias y publicación de insinuados.
- PMGM-REQ-026 — Cumplimiento Ley 21.719.
- PMGM-REQ-027 — Almacenamiento seguro de documentos y versiones.
- PMGM-REQ-028 — Notificaciones institucionales multicanal.
- PMGM-REQ-029 — Calendario institucional unificado.
- PMGM-REQ-030 — Gran Archivero y Archivo Histórico Institucional.
- PMGM-REQ-030-ADD-001 — Catálogo y permisos granulares del Gran Archivo.
- PMGM-REQ-031 — Biblioteca Virtual.
- PMGM-REQ-031-ADD-001 — Acceso acumulativo de Biblioteca por grado.
- PMGM-REQ-032 — Ventana Fraterna.
- PMGM-REQ-033 — Muro Institucional de Gran Secretaría.

## Decisiones y arquitectura
- PMGM-ADR-001 — Arquitectura base.
- PMGM-ADR-002 — React + TypeScript + Vite.
- PMGM-ADR-003 — Object Storage desacoplado.
- PMGM-ARCH-002 — Límites documentales.
- PMGM-ARCH-003 — Ciclo binario seguro y Object Storage.
- PMGM-ARCH-004 — Catálogo de Biblioteca Virtual.

## Estado de implementación

### Núcleo institucional
- Personas, organizaciones y miembros.
- Pertenencias históricas.
- Eventos de estado y grado.
- Cargos y períodos.
- Transferencias entre Talleres con historia preservada.
- RBAC inicial por Orden/Taller.

### Régimen Interior
- Reportes institucionales iniciales.
- Lectura transversal por rol.
- Historial de membresía y estados.

### Tesorería y Hospitalaria
- Snapshots de regularidad diferenciados.
- Integración con elegibilidad de ceremonias.

### Ceremonias
- Iniciación, aumento de salario y exaltación.
- Validaciones de Régimen Interior, Tesorería y Hospitalaria.
- Publicación de insinuados con plazo configurable.
- Portal/API de publicaciones.
- Autorización y congelamiento de evidencias.
- Auditoría persistente.

### Gran Secretaría
- Templos y salas de Secretaría.
- Disponibilidad y reservas sin solapamiento.
- Decretos y comunicados.
- Autorización formal de ceremonias.
- Auditoría de espacios, reservas y documentos.
- PMGM-REQ-033 formaliza el futuro Muro Institucional oficial con segmentación por grado/Taller/cargo/hermano y confirmación de lectura opcional.

### Gestión Logial
- Tenidas.
- Asistencia append-only.
- Actas versionadas.
- Interfaz inicial de intranet.

### Gestión Documental Operativa — v0.15.0 estable
- Colecciones y metadata.
- Versiones y políticas de acceso.
- Estados `pending_upload`, `uploaded`, `scanning`, `available`, `rejected`.
- ObjectKey opaco.
- S3-compatible real con MinIO.
- Bucket privado.
- Carga autorizada sin sobrescritura.
- SHA-256 y tamaño verificados.
- Extensión/MIME/firma real validados.
- ClamAV real y detección EICAR en CI.
- Descarga autorizada.
- Reconciliación de cargas incompletas.
- Auditoría sin secretos físicos innecesarios.
- Pruebas end-to-end del ciclo documental.

### Biblioteca Virtual — v0.16.0 en desarrollo
- PMGM-REQ-031 + ADD-001.
- PMGM-BLG-050.
- Catálogo server-side de documentos publicados y versiones disponibles.
- Búsqueda, filtros, paginación y facetas implementados.
- DTO minimizado sin ObjectKey/SHA/nombre físico/scan reference.
- Descarga autenticada desde UI.
- Frontend React conectado y pruebas frontend verdes.
- Pendiente cierre de una prueba HTTP backend del catálogo antes de declarar estable el corte.
- Siguiente bloque obligatorio dentro de v0.16: `minimum_degree_required`, resolución del grado institucional vigente y pruebas 1/2/3 + acceso directo por ID.
- Los conteos/facetas deberán excluir por defecto recursos que el hermano no pueda descubrir.

### Gran Archivo / Gran Archivero
- PMGM-REQ-030 + ADD-001.
- PMGM-BLG-051.
- Custodia histórica de planchas, decretos, cartas, investigaciones, amonestaciones, disciplinarios y otros antecedentes de valor histórico.
- Catálogo y contenido con permisos separados.
- Grants por grado, hermano, Taller, cargo/rol, comisión, vigencia y autorización especial.
- Expedientes sensibles con política restrictiva por defecto.
- Implementación prevista después de estabilizar Biblioteca.

### Ventana Fraterna
- PMGM-REQ-032 / PMGM-BLG-053 formalizados.
- Comunidad interna para trabajo, ayuda, servicios, compraventa, libros y avisos fraternales.
- Moderación, auditoría y adjuntos seguros definidos.
- Sin pagos ni garantía institucional de transacciones en primera etapa.

### Muro Institucional Gran Secretaría
- PMGM-REQ-033 / PMGM-BLG-054 formalizados.
- Decretos, planchas oficiales, comunicados, circulares y avisos.
- Segmentación por grado/Taller/cargo/órgano/hermano.
- Versionado oficial y confirmación de lectura opcional.
- Integración futura con notificaciones y Gran Archivo.

### Privacidad Ley 21.719
- Registro de actividades de tratamiento.
- Retención/legal holds.
- Derechos de titulares.
- Encargados/transferencias internacionales.
- EIPD, incidentes y decisiones de notificación.
- Gates de privacidad y clasificación en CI.

### QA, instalación y operación
- `infrastructure/docker-compose.qa.yml` creado con PostgreSQL, MinIO privado, ClamAV, API y web.
- `.env.qa.example` creado con secretos obligatorios y OIDC de QA.
- PMGM-QA-001 documenta instalación, migraciones, TLS/reverse proxy, backups, actualización y rollback.
- PMGM-QA-002 entrega checklist funcional/técnico de aceptación.
- PostgreSQL, MinIO y ClamAV no se publican al host en QA.
- El web escucha por defecto en `127.0.0.1:8081` para quedar detrás de HTTPS.
- CI valida `docker-compose.dev.yml` y `docker-compose.qa.yml`.
- CI publica resultados backend TRX para diagnóstico y evidencia QA.

## Backlog activo
1. PMGM-BLG-050 — Biblioteca Virtual v0.16.0.
2. PMGM-BLG-051 — Gran Archivo / Gran Archivero.
3. PMGM-BLG-054 — Muro Institucional de Gran Secretaría.
4. PMGM-BLG-053 — Ventana Fraterna.
5. PMGM-BLG-028 — Notificaciones.
6. PMGM-BLG-029 — Calendario institucional.

## Orden de ejecución actual
1. Cerrar CI de catálogo Biblioteca v0.16.
2. Implementar seguridad acumulativa por grado en Biblioteca.
3. Completar workflow editorial/taxonomías de Biblioteca.
4. Gran Archivo: estructura, catálogo seguro, permisos y transferencias.
5. Gran Archivo: digitalización, preservación, consultas y préstamos.
6. Muro Institucional de Gran Secretaría.
7. Ventana Fraterna.
8. Notificaciones y calendario.
9. Cliente OIDC/PKCE y proveedor definitivo.
10. OpenAPI automatizado, Docencia e integraciones externas.
11. Hardening y operación de producción.

## Exclusiones explícitas
- CENDOC no se incluye como módulo, dependencia ni componente del Proyecto Milenio.

## Criterio de avance
Ningún incremento se considera estable si rompe compilación, migraciones, seguridad, gates o pruebas del pipeline. Los cambios continúan en `dev` hasta revisión y aprobación expresa para `main`.
