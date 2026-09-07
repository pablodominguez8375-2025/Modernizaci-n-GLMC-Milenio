# PMGM-BASE-001 — Estado Maestro del Proyecto Milenio

**Estado:** Activo  
**Rama de trabajo:** `dev`  
**Rama estable:** `main`

## Visión
Proyecto Milenio construye una plataforma institucional unificada para la Gran Logia Mixta de Chile, con acceso único, base maestra, trazabilidad histórica, seguridad por roles y módulos integrados.

## Núcleo técnico
- ASP.NET Core / .NET 10 LTS.
- PostgreSQL + Entity Framework Core/Npgsql.
- Docker / Docker Compose.
- GitHub Actions CI.
- JWT/OIDC con proveedor definitivo pendiente.
- Cultura de presentación `es-CL`.
- Zona horaria institucional `America/Santiago`.

## Principios funcionales consolidados
1. Una persona posee una identidad maestra única.
2. Un miembro mantiene su historia aun cuando cambia de Taller.
3. El Taller de origen conserva su actividad histórica y el Taller receptor crea una nueva pertenencia.
4. Régimen Interior dispone de visión transversal para control y toma de decisiones.
5. Gran Secretaría emite documentos oficiales y autorizaciones a partir de validaciones de las áreas responsables.
6. Gran Tesorería es fuente responsable de regularidad financiera.
7. Gran Hospitalaria es fuente responsable de regularidad de reposiciones hospitalarias.
8. Una ceremonia sólo se autoriza si las validaciones obligatorias aplicables están conformes o existe excepción formal auditada.
9. En iniciaciones, el insinuado debe publicarse previamente durante el plazo institucional configurado; valor inicial 20 días.
10. El insinuado no se convierte en miembro activo antes de la iniciación efectivamente registrada.
11. Las reglas críticas deben ser parametrizables y versionadas.
12. Los permisos dependen de rol, finalidad y contexto institucional.
13. No se utilizará RUT como clave primaria técnica.

## Requisitos funcionales formalizados
- PMGM-REQ-021 — Régimen Interior: reportes, control histórico y apoyo a decisiones.
- PMGM-REQ-022 — Gran Secretaría: decretos, comunicados, autorizaciones y gestión de espacios.
- PMGM-REQ-023 — Transferencia entre Talleres.
- PMGM-REQ-024 — Localización Español (Chile).
- PMGM-REQ-025 — Elegibilidad de ceremonias y publicación de insinuados.
- PMGM-REQ-022-ADD-001 — Extensión Gran Secretaría: Hospitalaria y publicación previa.

## Estado de implementación
Implementado o iniciado en `dev`:
- Personas, organizaciones y miembros.
- Pertenencias históricas.
- Eventos de estado y grado.
- Cargos y períodos.
- Transferencias entre Talleres.
- RBAC inicial por Orden/Taller.
- reportes iniciales de Régimen Interior.
- pruebas automatizadas del RBAC.
- modelos de solicitud y validación de ceremonias.
- motor explicable de elegibilidad.
- control de Tesorería/Hospitalaria como validaciones obligatorias.
- publicación de insinuados con plazo configurable.
- API autenticada para publicaciones vigentes.
- migración PostgreSQL de ceremonias/publicaciones/reglas.

## Próximos bloques
1. Auditoría transversal persistente.
2. Endpoints administrativos para solicitudes de ceremonia y validaciones.
3. Configuración administrativa del plazo de publicación.
4. Integración ejecutable con fuentes de Gran Tesorería y Gran Hospitalaria.
5. Frontend del portal de insinuados.
6. Gran Secretaría: emisión efectiva de autorizaciones y documentos.
7. Gestión de templos/salas y calendario.
8. Dashboard del MVP.

## Criterio de avance
Ningún incremento se considera estable si rompe compilación o pruebas del pipeline. Los cambios continúan en `dev` hasta revisión y aprobación para `main`.
