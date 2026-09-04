# PMGM-ADR-001 — Stack tecnológico base

## Estado
Aceptada.

## Contexto
El Proyecto Milenio requiere una plataforma web institucional, segura, modular y preparada para crecer hacia servicios internos, integraciones y movilidad.

## Decisión

- Backend: **ASP.NET Core**.
- Base de datos: **PostgreSQL**.
- Contenedores: **Docker**.
- Proxy inverso: **Nginx**.
- Transporte seguro: **TLS**.
- Sistema operativo objetivo: **Ubuntu Server 24.04 LTS**.
- Autenticación federada: **OpenID Connect / SSO**.
- Autorización: **RBAC**.

La tecnología de frontend se mantiene pendiente hasta comparar React y Blazor contra velocidad de prototipado, mantenibilidad y experiencia del equipo.

## Consecuencias

### Positivas
- Stack moderno y ampliamente soportado.
- Buen soporte para APIs y arquitectura modular.
- PostgreSQL reduce dependencia de licencias propietarias.
- Docker facilita despliegues reproducibles.

### Pendientes
- Definir proveedor/servidor de identidad.
- Definir estrategia de secretos.
- Definir frontend.
- Definir CI/CD.
