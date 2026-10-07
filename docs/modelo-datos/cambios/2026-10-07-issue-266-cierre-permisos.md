# GOV-004 — #266 / #360 — cierre de aplicación de permisos

Base dev c554a6e8d08d46cfe1f3f70f0552bc495b56ea3c.

Antes: contrato persistente de perfiles/grants/asignaciones e intersección financiera; otros endpoints sólo consultaban el cargo institucional. Después: filtro común y clientes compatibles aplican view/create/write/edit/delete; print tiene solicitud auditada independiente. Conservadas autorizaciones institucionales, snapshots JSONB y revocación histórica.

Datos antes/después: sin modificaciones a tablas/columnas/migraciones ni conversión de datos. Reutiliza dynamic_access_snapshots y audit_events, acción system.access.view_print_requested, entidad DynamicView, código de vista y OrganizationId opcional. No incluye contenido ni datos personales en la auditoría de impresión.

Pruebas: unitarias de política, HTTP PostgreSQL (módulos, acciones, origen de registros, denegación, administrador crea/edita, auditoría/revocación), frontend y gates CI/Showcase/QA. SHA final y recibo de publicación se registran en PR/issue/Drive; no declarar UAT operacional.

Rollback: revert del commit funcional; sin Down ni pérdida de snapshots/historia. Despliegue QA pendiente por #97.
