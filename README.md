# Proyecto Centenario — Modernización Gran Logia Mixta Milenio

Repositorio oficial del **Proyecto Centenario / Modernización Gran Logia Mixta Milenio**.

## Línea base vigente

La referencia funcional, técnica y documental vigente es **LB-PC-2026-09-17**.

## Inicio obligatorio para cualquier IA o desarrollador

Antes de continuar desde un chat nuevo, otra sesión o una IA distinta, leer primero:

1. `AGENTS.md`
2. `docs/INSTRUCCIONES-CONTINUIDAD-IA.md`
3. `docs/REGLAS-CONTINUIDAD-Y-NO-REGRESION.md`
4. `docs/CONTROL-DE-CAMBIOS.md`
5. `docs/LINEA-BASE-PROYECTO-CENTENARIO-2026-09-17.md`
6. los ADR, migraciones, pruebas y configuraciones relacionados con la tarea;
7. la Línea Base Maestra y documentación oficial vigente en Google Drive.

**Nunca reconstruir el proyecto desde chats antiguos, resúmenes o memoria de una IA si existe una versión posterior en GitHub o Drive.**

## Regla de continuidad única

**Existe un solo Proyecto Centenario y un solo desarrollo continuo.**

Todo documento, componente, carpeta, servicio, matriz, prueba, migración, módulo o código que se incorpore a este repositorio forma parte de la misma aplicación del Proyecto Centenario, salvo decisión expresa del Sponsor/Product Owner registrada en el Control de Cambios.

Nombres técnicos como `Centenario.Authorization`, `RBAC Taller`, `Matriz Funcional Normativa` o futuros componentes describen partes internas de la solución; **no constituyen proyectos, productos ni desarrollos paralelos**.

Cada nueva implementación debe:

1. continuar desde el último código vigente de `main`;
2. reutilizar y extender la arquitectura y modelos ya definidos;
3. preservar funcionalidades ya implementadas que no hayan sido expresamente sustituidas;
4. integrarse con los módulos existentes del Proyecto Centenario;
5. evitar crear soluciones paralelas, repositorios alternativos o estructuras incompatibles;
6. registrar cualquier cambio de arquitectura que realmente sea necesario mediante ADR y Control de Cambios.

## Regla permanente de entrega: Demo + QA

**Todo incremento funcional relevante del Proyecto Centenario debe mantener dos salidas coordinadas del mismo desarrollo:**

1. **Demo navegable en GitHub Pages**, para revisión visual, funcional y de flujos con datos ficticios/controlados.
2. **Versión instalable y operacional para QA en `srv01`**, para validar API, PostgreSQL, autenticación, permisos, persistencia, auditoría, archivos, integraciones y comportamiento real.

La demo GitHub Pages y la versión QA **no son desarrollos separados**. Deben evolucionar desde la misma línea base, el mismo código y los mismos requisitos. Una función puede aparecer primero simulada visualmente en GitHub Pages, pero no se considera operacional hasta estar integrada y probada en QA.

Regla de paridad incremental:

`Requisito aprobado → Código en main → Demo GitHub Pages → QA instalable → Prueba QA/UAT`

Documento rector: `docs/ESTRATEGIA-ENTREGAS-DEMO-Y-QA.md`.

Documentos maestros:
- `docs/LINEA-BASE-PROYECTO-CENTENARIO-2026-09-17.md`
- `docs/PERFILES-TALLER-Y-FIRMAS.md`
- `docs/MATRIZ-FUNCIONAL-NORMATIVA-TALLER-v1.0.md`
- `docs/ESTRATEGIA-ENTREGAS-DEMO-Y-QA.md`
- `docs/INSTRUCCIONES-CONTINUIDAD-IA.md`
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

## Código desarrollado

### Autorización de cargos de Taller

Como parte del **mismo desarrollo del Proyecto Centenario**, se incorpora el componente interno de autorización:

- `backend/Centenario.Authorization/`
- `backend/Centenario.Authorization.SmokeTests/`
- `adrs/ADR-001-RBAC-NORMATIVO-TALLER.md`
- `database/001_rbac_taller.sql`

Incluye:

- perfiles de Taller;
- vistas;
- acciones;
- fundamento de cada permiso;
- separación normativa/protocolo/control operativo;
- subrogaciones mediante permisos temporales;
- validación de autorización de ayudas hospitalarias;
- validación de firmantes CRV/CRF.

`Centenario.Authorization` es una biblioteca/componente interno de la solución; no constituye una aplicación independiente. La autorización está desacoplada del frontend para no forzar aún la decisión React vs. Blazor y deberá integrarse con la API, identidad, base de datos y vistas de la misma aplicación Centenario.

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

La línea base, reglas permanentes, protocolo de continuidad para IA y el primer componente funcional de autorización están versionados en `main` como partes del único desarrollo del Proyecto Centenario.

**Siguiente hito técnico:** integrar el catálogo RBAC con PostgreSQL, autenticación OIDC/SSO y API autorizada dentro de la aplicación Centenario; actualizar en paralelo la demo GitHub Pages y preparar la versión instalable/operacional para QA en `srv01`.
