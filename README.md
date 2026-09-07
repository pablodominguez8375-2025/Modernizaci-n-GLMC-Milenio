# Proyecto Milenio — Modernización Gran Logia Mixta de Chile

Repositorio central del proyecto de modernización digital de la Gran Logia Mixta de Chile.

## Objetivo
Construir una plataforma institucional unificada con acceso único, base maestra de datos, trazabilidad, seguridad, gestión logial, Régimen Interior, Gran Secretaría, Gran Tesorería, Gran Hospitalaria, biblioteca/documentación y servicios digitales para los miembros.

## Principios
- Identidad institucional única.
- Una sola ficha maestra por persona/miembro.
- Historial sin sobrescritura destructiva.
- Permisos por rol y contexto de Taller.
- Auditoría transversal.
- Seguridad y privacidad desde el diseño.
- Integración entre módulos sin duplicar fuentes maestras.
- Reglas institucionales parametrizables y versionadas.

## Núcleo funcional en desarrollo
- Personas, miembros, Talleres y pertenencias históricas.
- Grados, cargos y estados institucionales.
- Transferencias trazables entre Talleres.
- Reportes operativos de Régimen Interior.
- Gran Secretaría: documentos oficiales, autorizaciones y gestión de espacios.
- Elegibilidad de ceremonias con validaciones inter-área.
- Control de regularidad del Taller en Gran Tesorería.
- Control de reposiciones del Taller en Gran Hospitalaria.
- Portal institucional de insinuados para iniciaciones.
- Plazo mínimo de publicación configurable, inicialmente 20 días.

## Regla de ceremonias
Una ceremonia sólo puede avanzar a autorización definitiva cuando las validaciones obligatorias aplicables se encuentren aprobadas o exista una excepción formal y auditada.

Como base:
1. Régimen Interior debe aprobar los antecedentes institucionales.
2. El Taller debe estar al día con Gran Tesorería.
3. El Taller debe estar al día con reposiciones de Gran Hospitalaria.
4. En iniciaciones, el insinuado debe cumplir el período mínimo de publicación definido por configuración institucional.
5. Gran Secretaría consume estas validaciones y genera el documento oficial cuando corresponda.

## Tecnología base
- Backend: ASP.NET Core / .NET 10 LTS.
- Base de datos: PostgreSQL.
- Persistencia: Entity Framework Core + Npgsql.
- Contenedores: Docker / Docker Compose.
- CI: GitHub Actions.
- Identidad: JWT/OIDC, proveedor definitivo pendiente de ADR.

## Flujo de trabajo
- `main`: rama estable.
- `dev`: integración y desarrollo.
- requisitos → diseño → aprobación → desarrollo → pruebas → documentación → publicación.

## Estado
El proyecto se encuentra en fase de fundación técnica y construcción del primer MVP ejecutable. La rama `dev` concentra la implementación activa y el PR #1 consolida los avances hacia `main`.

Consulta `/docs`, `/backlog`, `/backend` y `/infrastructure` para el detalle versionado.
