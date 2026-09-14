# PMGM-ARCH-001 — Arquitectura v0.1

## Objetivo
Definir la arquitectura base del nuevo ecosistema Milenio con foco en modularidad, seguridad, trazabilidad e integración progresiva.

## Capas principales
1. Experiencia de usuario: portal público, intranet y futura experiencia móvil.
2. Identidad: SSO mediante OpenID Connect, MFA cuando corresponda, RBAC.
3. Aplicación: backend ASP.NET Core organizado por dominios/módulos.
4. Datos: PostgreSQL como base institucional principal.
5. Integraciones: APIs para sistemas legados, correo, documentos y servicios externos.
6. Infraestructura: Docker, proxy reverso, TLS, observabilidad, respaldos y CI/CD.

## Módulos de dominio iniciales
- Identidad y acceso.
- Miembros.
- Talleres.
- Secretaría.
- Régimen Interior.
- Gestión Logial.
- Documentos y Biblioteca.
- Auditoría.

## Evolución posterior
- Tesorería.
- Hospitalaria.
- Docencia.
- CENDOC.
- Calendario y ceremonias.
- Comunicaciones.
- Reportes.
- Aplicación móvil e integraciones adicionales.

## Stack base
- Backend: ASP.NET Core.
- Frontend: React o Blazor, pendiente de ADR específico.
- Base de datos: PostgreSQL.
- Contenedores: Docker.
- Publicación: Nginx o equivalente.
- Sistema operativo objetivo: Ubuntu Server 24.04 LTS.
- Automatización: GitHub Actions.

## Requisitos no funcionales
- Seguridad por defecto.
- Auditoría transversal.
- Alta trazabilidad.
- Backups y recuperación.
- Separación de ambientes.
- Configuración externa a código.
- Secretos fuera del repositorio.
- APIs versionadas.
- Observabilidad y logging estructurado.

## Regla de integración con legado
Ningún sistema existente se elimina sin inventario, evaluación, estrategia de migración y rollback. Cada componente legado debe clasificarse como Mantener, Integrar, Migrar, Reemplazar o Retirar.
