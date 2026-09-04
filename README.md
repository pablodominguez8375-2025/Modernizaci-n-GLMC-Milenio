# Proyecto Milenio

**Nombre formal:** Modernización Gran Logia Mixta Milenio  
**Sigla:** PMGM

Repositorio central para el diseño, desarrollo, pruebas y documentación del proyecto de modernización digital de la Gran Logia Mixta de Chile.

## Gobierno del proyecto

- **Sponsor / Product Owner:** Pablo Domínguez
- **Arquitectura funcional y técnica:** ChatGPT
- **Desarrollo / automatización:** Codex
- **Fuente única de verdad:** GitHub

## Flujo de trabajo

1. Requisito
2. Diseño
3. Aprobación de arquitectura
4. Desarrollo
5. Pruebas
6. Documentación
7. Publicación

## Arquitectura base acordada

- Backend: ASP.NET Core
- Base de datos: PostgreSQL
- Identidad: OpenID Connect / SSO
- Infraestructura: Docker + Nginx + TLS
- Sistema operativo objetivo: Ubuntu Server 24.04 LTS
- Seguridad: RBAC, MFA, auditoría, logging y respaldos
- Frontend: decisión pendiente entre React y Blazor

## Principios funcionales

- Identidad digital única
- Intranet única
- Base de datos institucional centralizada
- Expediente masónico digital
- Acceso por roles y permisos
- Trazabilidad completa
- Principio de un solo dato institucional

## Etapas iniciales

### Etapa 1 — Cimientos
Base de datos, identidad, roles, Hermanos y Logias.

### Etapa 2 — Administración
Gestión Logial, Secretaría, Régimen Interior, Tesorería y Auditoría.

### Etapa 3 — Servicios
Intranet, calendario, biblioteca, docencia y comunicaciones.

### Etapa 4 — Integraciones
Conectores y servicios externos.

### Etapa 5 — Movilidad
Experiencia móvil y servicios asociados.

## Convenciones documentales

- ADR: `PMGM-ADR-###`
- Backlog: `PMGM-BLG-###`
- Arquitectura: `PMGM-ARCH-###`
- Base de datos: `PMGM-DB-###`

## Ramas

- `main`: versión estable y aprobada
- `dev`: integración de desarrollo
