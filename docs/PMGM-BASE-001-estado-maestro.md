# PMGM-BASE-001 — Estado Maestro del Proyecto Milenio

**Estado:** Activo  
**Rama de trabajo:** `dev`  
**Rama estable:** `main`

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

## Requisitos funcionales formalizados
- PMGM-REQ-021 — Régimen Interior: reportes, control histórico y apoyo a decisiones.
- PMGM-REQ-022 — Gran Secretaría: decretos, comunicados, autorizaciones y gestión de espacios.
- PMGM-REQ-023 — Transferencia entre Talleres.
- PMGM-REQ-024 — Localización Español (Chile).
- PMGM-REQ-025 — Elegibilidad de ceremonias y publicación de insinuados.
- PMGM-REQ-022-ADD-001 — Extensión Gran Secretaría: Hospitalaria y publicación previa.

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
- Portal API de publicaciones vigentes.
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
- Pruebas HTTP end-to-end de ceremonia y Gran Secretaría.
- Auditoría con actor y correlation ID.

## Próximos bloques
1. Frontend React/TypeScript del portal de insinuados.
2. Dashboard MVP institucional por rol.
3. Cliente OIDC/PKCE y selección del proveedor definitivo de identidad.
4. Generación/validación automatizada de contratos OpenAPI para frontend.
5. Gestión Logial: tenidas, asistencia, actas y Secretaría de Taller.
6. Biblioteca/documentos institucionales y almacenamiento de objetos.
7. Notificaciones y correo institucional.
8. Calendario integrado para templos, salas, ceremonias y reuniones.

## Criterio de avance
Ningún incremento se considera estable si rompe compilación, migraciones, gates o pruebas del pipeline. Los cambios continúan en `dev` hasta revisión y aprobación para `main`.
