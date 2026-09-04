# PMGM-ARCH-001 — Arquitectura base

## Estado
Aprobada como línea base inicial.

## Objetivo
Definir una arquitectura modular, segura, trazable y mantenible para la modernización digital de la Gran Logia Mixta de Chile.

## Componentes principales

- **Frontend:** aplicación web responsive. Decisión final React vs Blazor pendiente.
- **Backend:** ASP.NET Core Web API.
- **Base de datos:** PostgreSQL.
- **Autenticación:** OpenID Connect / SSO.
- **Autorización:** RBAC con permisos granulares.
- **Infraestructura:** Docker, Nginx, TLS.
- **Sistema operativo objetivo:** Ubuntu Server 24.04 LTS.
- **Auditoría:** registro de cambios y eventos críticos.
- **Backups:** política de respaldos automatizados y recuperación.

## Principios

1. Identidad única por persona.
2. Dato institucional único y centralizado.
3. Separación clara entre dominio, infraestructura y presentación.
4. Menor privilegio.
5. Auditoría de acciones sensibles.
6. API-first para facilitar integraciones futuras.
7. Diseño multi-taller desde el modelo de dominio.

## Módulos iniciales

- Identidad y acceso
- Hermanos / miembros
- Logias / talleres
- Gestión Logial
- Secretaría
- Régimen Interior
- Tesorería
- Hospitalaria
- Tenidas y actas
- Biblioteca y docencia
- Comunicaciones
- Auditoría

## Próxima decisión

Definir frontend definitivo y estrategia de identidad para el primer prototipo funcional.
