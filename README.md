# Proyecto Centenario — Modernización Gran Logia Mixta Milenio

Repositorio oficial del **Proyecto Centenario / Modernización Gran Logia Mixta Milenio**.

## Línea base vigente

La referencia funcional, técnica y documental vigente es **LB-PC-2026-09-17**.

Documentos maestros:
- `docs/LINEA-BASE-PROYECTO-CENTENARIO-2026-09-17.md`
- `docs/PERFILES-TALLER-Y-FIRMAS.md`
- `docs/CONTROL-DE-CAMBIOS.md`
- `docs/REGLAS-CONTINUIDAD-Y-NO-REGRESION.md`
- `docs/README-INDEX.md`

## Protocolo permanente para retomar el proyecto

Antes de continuar desarrollo, análisis, correcciones o despliegues, incluso desde un chat nuevo:

1. revisar el último estado de GitHub `main`;
2. revisar la Línea Base Maestra vigente y Control de Cambios;
3. revisar los documentos oficiales de Drive que afecten la tarea;
4. comprobar el código, ADR, migraciones, pruebas y configuraciones ya desarrollados;
5. usar chats o hilos anteriores solamente como contexto histórico.

**Nunca se debe reconstruir el proyecto desde una conversación antigua si existe una versión posterior consolidada.**

**Regla de no regresión:** no eliminar, degradar ni reemplazar código, funcionalidades o acuerdos aprobados/versionados sin una decisión explícita registrada, con motivo, impacto y versión de reemplazo.

Regla de trabajo: los cambios aprobados deben quedar consolidados tanto en GitHub como en la documentación maestra de Google Drive. Una conversación por sí sola no constituye una modificación vigente del proyecto.

## Propósito

Construir una plataforma institucional única, segura, trazable, modular y multi-Taller para la Gran Logia Mixta de Chile, reemplazando progresivamente procesos manuales, sistemas aislados y planillas duplicadas.

## Principios rectores

- Base de datos institucional única.
- Seguridad y privacidad desde el diseño.
- Auditoría transversal y trazabilidad.
- Segregación de información por Taller y acceso transversal controlado.
- Arquitectura modular e integrable.
- Parametrización de perfiles, vistas, flujos y reglas cuando sea técnicamente razonable.
- Evolución incremental mediante versiones demostrables e instalables.
- GitHub como fuente única de verdad técnica.
- Google Drive como repositorio de documentos oficiales y línea base funcional/documental.

## Gobierno

- **Sponsor / Product Owner:** Pablo Domínguez.
- **Arquitectura funcional y técnica:** ChatGPT / Chatito.
- **Desarrollo y automatización:** Codex.

Flujo: Requisito → Diseño → Aprobación → Desarrollo → QA → Documentación → Despliegue.

## Alcance funcional vigente

- Identidad, usuarios, perfiles, vistas y permisos.
- Dashboard.
- Miembros y estados históricos.
- Talleres / Logias.
- Secretaría y Gestión Logial.
- Tenidas y actas.
- Insinuaciones, entrevistas y candidatos.
- Ceremonias y aprobaciones.
- Tesorería.
- Hospitalaria.
- Docencia.
- Régimen Interior.
- Gran Secretaría.
- Gran Tesorería.
- Gran Hospitalaria.
- Biblioteca Virtual.
- Gran Archivero.
- Gestión Documental.
- Agenda de templos y salas.
- Notificaciones.
- Reportes.
- Auditoría.
- Administración del Sistema.

**CENDOC como módulo único no forma parte del alcance vigente.** Biblioteca Virtual y Gran Archivero se gestionan como componentes separados.

## Perfiles de Taller base

- Venerable Maestro.
- Inmediato Ex-Venerable Maestro.
- Primer Vigilante.
- Segundo Vigilante.
- Orador/a.
- Secretario/a.
- Tesorero/a.
- Hospitalario/a.

Orden de subrogación: **Venerable Maestro → Inmediato Ex-Venerable Maestro → Primer Vigilante → Segundo Vigilante**.

Docencia: **Segundo Vigilante → Aprendices; Primer Vigilante → Compañeros; Inmediato Ex-Venerable Maestro → Maestros**.

Las CRV y CRF contemplan como firmantes funcionales a **Venerable Maestro, Tesorero/a, Orador/a y Secretario/a**.

## Arquitectura base

- Backend: ASP.NET Core.
- Frontend: React o Blazor, sujeto a ADR definitivo.
- Base de datos: PostgreSQL.
- Identidad: OpenID Connect / SSO.
- Contenedores: Docker.
- Proxy/publicación: Nginx o equivalente.
- TLS obligatorio.
- CI/CD: GitHub Actions.

## Infraestructura QA confirmada

Servidor `srv01`:
- Ubuntu 26.04.1 LTS.
- 8 vCPU.
- Aproximadamente 8 GB RAM.
- Docker 29.1.3.
- Docker Compose 2.40.3.
- Repositorio clonado en `/opt/centenario/app`.
- Deploy Key GitHub operativa.

## Estructura objetivo

- `docs/` documentación funcional y técnica.
- `backend/` servicios y API.
- `frontend/` interfaz de usuario.
- `database/` modelo, migraciones y scripts.
- `infrastructure/` Docker, despliegue, CI/CD e IaC.
- `tests/` pruebas automatizadas y de aceptación.
- `adrs/` Architecture Decision Records.
- `backlog/` backlog y planificación.
- `changelog/` historial de versiones y cambios.

## Estado

La documentación de línea base y las reglas permanentes de continuidad ya están versionadas en `main`.

**Siguiente hito técnico:** consolidar y subir el código funcional de backend, frontend, base de datos, infraestructura y pruebas, y desplegarlo de forma reproducible en `srv01`.
