# PMGM-BLG-064 — Bootstrap institucional y carga inicial controlada

Estado objetivo: **Done v0.33** una vez que el mismo head supere CI completo.

## Entregables
- Superadmin separado de Administrador de Gran Logia.
- `BootstrapDbContext` y migración propia.
- `POST /api/platform/bootstrap/plan`.
- `POST /api/platform/bootstrap/apply`.
- `GET /api/platform/bootstrap/catalog`.
- SHA-256 e idempotencia por packageKey/version.
- Catálogo base de perfiles de seguridad.
- Catálogo base de cargos administrativos y ritualísticos.
- Auditoría de aplicación.
- UI Configuración inicial con dry-run y confirmación.
- Pruebas unitarias/frontera y PostgreSQL.
- Plantilla institucional v1 sin datos personales.

## Fuera de alcance
- Carga de padrón de personas reales.
- RUT, email, teléfono o domicilio.
- Creación automática de cuentas nominales de Taller.
- Perfilado granular de soporte en Keycloak; el catálogo deja preparada esa evolución para v1.x.
