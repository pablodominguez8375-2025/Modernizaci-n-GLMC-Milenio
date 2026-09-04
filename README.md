# Proyecto Milenio — Modernización Gran Logia Mixta de Chile

Repositorio oficial del proyecto **Modernización Gran Logia Mixta Milenio (PMGM)**.

## Propósito
Construir un ecosistema digital institucional único, seguro, trazable e integrado para la Gran Logia Mixta de Chile, reemplazando progresivamente sistemas aislados, procesos manuales y planillas duplicadas por una plataforma modular con identidad única, base de datos institucional única y servicios centralizados.

## Principios rectores
- Un solo acceso institucional (SSO) para cada usuario.
- Una base de datos maestra institucional.
- Seguridad y privacidad desde el diseño.
- Auditoría transversal y trazabilidad de cambios.
- Arquitectura modular e integrable.
- Evolución incremental mediante MVPs.
- GitHub como fuente única de verdad técnica.
- Separación entre continuidad de sistemas actuales y desarrollo del nuevo ecosistema.

## Gobierno
- **Sponsor / Product Owner:** Pablo Domínguez.
- **Arquitectura funcional y técnica:** ChatGPT / Chatito.
- **Desarrollo y automatización:** Codex.
- **Repositorio:** fuente única de verdad para código, documentación, backlog y decisiones.

## Flujo de trabajo
Requisito → Diseño → Aprobación de arquitectura → Desarrollo → Pruebas → Documentación → Publicación.

## Alcance funcional inicial
- Identidad, usuarios, roles y permisos.
- Dashboard.
- Gestión de miembros.
- Gestión de Talleres / Logias.
- Secretaría.
- Régimen Interior.
- Gestión Logial.
- Tesorería.
- Hospitalaria.
- Docencia.
- Biblioteca Virtual.
- CENDOC / gestor documental.
- Museo / archivo histórico digital.
- Calendario institucional.
- Gestión de Tenidas.
- Ceremonias y solicitudes.
- Comunicaciones internas y externas.
- Notificaciones.
- Auditoría y reportes.
- Administración general.
- Integraciones y experiencia móvil futura.

## Arquitectura base v0.1
- Backend: ASP.NET Core.
- Frontend: React o Blazor (decisión pendiente vía ADR).
- Base de datos: PostgreSQL.
- Identidad: OpenID Connect / SSO.
- Contenedores: Docker.
- Proxy / publicación: Nginx o equivalente.
- TLS obligatorio.
- Servidor de referencia: Ubuntu Server 24.04 LTS.
- CI/CD: GitHub Actions.

## MVP inicial
Login/SSO → Dashboard → Miembros → Talleres → Secretaría → Régimen Interior → Documentos/Biblioteca → Auditoría básica.

## Estructura del repositorio
- `docs/` documentación funcional y técnica.
- `backend/` servicios y API.
- `frontend/` interfaz de usuario.
- `database/` modelo, migraciones y scripts.
- `infrastructure/` despliegue, Docker, CI/CD e IaC.
- `tests/` pruebas automatizadas y de aceptación.
- `adrs/` Architecture Decision Records.
- `backlog/` backlog y planificación.
- `changelog/` historial de versiones y cambios.

## Repositorio relacionado
La migración de la plataforma Joomla actual se gestiona separadamente en `ehshackleton/glm-platform-migration`. Ese repositorio cubre continuidad operacional y migración de los sitios existentes; este repositorio contiene el nuevo ecosistema Milenio.

## Nomenclatura documental
- `PMGM-REQ-###` — requisitos.
- `PMGM-UC-###` — casos de uso.
- `PMGM-ARCH-###` — arquitectura.
- `PMGM-DB-###` — modelo de datos.
- `PMGM-SEC-###` — seguridad.
- `PMGM-ADR-###` — decisiones de arquitectura.
- `PMGM-BLG-###` — backlog.
- `PMGM-TEST-###` — pruebas.

## Ramas
- `main`: versión estable y aprobada.
- `dev`: integración de desarrollo.

## Estado
Versión base del proyecto: **v0.1**.
