# 2026-09-17 — RBAC Taller v0.1

## Incorporado

- Proyecto `Centenario.Authorization` para permisos de cargos de Taller.
- Catálogo inicial de vistas/acciones con fundamento normativo, institucional u operativo.
- Separación expresa Tesorería/Hospitalaria.
- Control de autorización de ayudas hospitalarias por Consejo o Venerable.
- Validación de cuatro firmantes para CRV/CRF.
- Permisos temporales para subrogaciones, sin herencia automática.
- Smoke tests sin dependencias externas de testing.
- Esquema PostgreSQL inicial en `database/001_rbac_taller.sql` para permisos, concesiones temporales, decisiones de Consejo, autorización hospitalaria y firmas de retiro.
- ADR-001 y matriz funcional-normativa v1.0.

## No incluido todavía

- Integración con proveedor OIDC/SSO.
- Sincronización automática del catálogo C# hacia las tablas PostgreSQL.
- API HTTP.
- UI React/Blazor.
- Auditoría persistente.
- Despliegue en srv01.

Estos elementos se mantienen como siguientes pasos, evitando afirmar que ya están implementados.
