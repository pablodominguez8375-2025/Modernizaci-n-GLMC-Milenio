# Proyecto Milenio — Modernización Gran Logia Mixta de Chile

Repositorio central del proyecto de modernización digital de la Gran Logia Mixta de Chile.

## Objetivo

Construir una plataforma institucional unificada con acceso único, base maestra de datos, trazabilidad, seguridad, gestión logial, Régimen Interior, Gran Secretaría, Gran Tesorería, Gran Hospitalaria, Biblioteca Virtual, Gran Archivero y servicios digitales para los miembros.

## Demo pública y paquete instalable

**Demo oficial:**

`https://pablodominguez8375-2025.github.io/Modernizaci-n-GLMC-Milenio/`

La demo de GitHub Pages utiliza exclusivamente datos ficticios y frontend estático. No está conectada a la futura VM, PostgreSQL, Keycloak, MinIO ni datos institucionales reales. La cabecera de la demo identifica el entorno y el SHA corto del build publicado para facilitar soporte y trazabilidad.

**Paquete instalable pre-UAT:**

GitHub Actions genera `Proyecto-Centenario-preUAT-installable.zip` desde un SHA exacto. El ZIP incorpora código, infraestructura, documentación, manifest SHA-256, Git bundle y una entrada de instalación guiada `INSTALAR.sh` para preflight, arranque, smoke y respaldo inicial.

La demo sirve para **mostrar y revisar la experiencia**; el paquete pre-UAT sirve para **instalar y validar el sistema completo en una VM**. Ninguno de los dos autoriza por sí solo la carga de datos personales reales ni una promoción a producción estable.

Documentos principales:

- `docs/qa/PMGM-GITHUB-PAGES-SHOWCASE.md` — funcionamiento y límites de la demo pública.
- `docs/installation/PREUAT-INSTALL.md` — guía del paquete instalable actual.
- `docs/instalacion/PMGM-VM-DEFINITIVA-V100.md` — diseño y requisitos de la VM definitiva.
- `docs/instalacion/PMGM-PAQUETE-IMPLEMENTACION-V100.md` — paquete RC1 congelado y procedimiento de implementación.

## Estado actual

Proyecto Milenio se encuentra en preparación de **v1.0.0-rc1 — Piloto Operacional**. La rama `dev` concentra la integración estable y `release/v1.0-rc1` congela la Release Candidate para validar versión, seguridad, infraestructura, backup/restauración y UAT institucional.

La RC es una base técnica candidata; **no equivale por sí sola a una puesta en producción definitiva ni autoriza automáticamente la carga de datos personales reales**. La promoción a v1.0 estable exige los gates operacionales descritos en `docs/instalacion/PMGM-RELEASE-CANDIDATE-V100-RC1.md`.

Manifest de la RC: `release/PMGM-RELEASE-1.0.0-rc1.json`.

## Principios

- Identidad institucional única.
- Una sola ficha maestra por persona/miembro.
- Historial sin sobrescritura destructiva, sujeto a políticas de conservación y privacidad.
- Permisos por rol, finalidad y contexto de Taller.
- Separación entre roles de seguridad y cargos institucionales/ritualísticos.
- Auditoría transversal.
- Seguridad y privacidad desde el diseño y por defecto.
- Integración entre módulos sin duplicar fuentes maestras.
- Reglas institucionales parametrizables y versionadas.
- Cumplimiento técnico progresivo de la normativa chilena de protección de datos, con preparación explícita para Ley 21.719.

## Núcleo funcional integrado

- Personas, miembros, Talleres y pertenencias históricas.
- Grados, cargos y estados institucionales.
- Transferencias trazables entre Talleres, preservando historial del Taller de origen.
- Fichas de miembro y Taller.
- Régimen Interior, reportería ejecutiva, control de miembros y calidad/corroboración de datos.
- Gran Secretaría: documentos oficiales, autorizaciones y gestión de espacios.
- Elegibilidad de ceremonias con validaciones inter-área.
- Regularidad del Taller en Gran Tesorería y Gran Hospitalaria.
- Portal de insinuados para iniciaciones y plazo mínimo configurable, inicialmente 20 días.
- Gestión Logial y docencia.
- Gestor Documental con versionado, Object Storage privado, integridad y análisis antimalware.
- Biblioteca Virtual con publicación y acceso controlados por grado/permisos.
- Gran Archivero para custodia y preservación del patrimonio documental histórico.
- Notificaciones institucionales.
- Calendario institucional unificado como proyección de fuentes maestras, con masking `Ocupado` para información protegida.
- Bootstrap institucional controlado para Gran Logia/Talleres y catálogos base.
- Protección de datos: finalidad, minimización, conservación, derechos de titulares, incidentes, EIPD/DPIA y accountability.
- Backup, verificación, restauración y recovery drill para first implementation y piloto.

## Separación documental institucional

Proyecto Milenio establece tres responsabilidades distintas:

1. **Gestión Documental Operativa:** documentos vigentes o de trabajo de los módulos de negocio.
2. **Biblioteca Virtual:** publicación, organización, búsqueda y consulta de material autorizado, incluidas planchas cuando corresponda por grado y permiso.
3. **Gran Archivo / Gran Archivero:** custodia, clasificación, preservación, digitalización y acceso controlado al patrimonio histórico institucional. Las planchas de trabajo no pertenecen al Gran Archivero.

**CENDOC no forma parte del alcance del Proyecto Milenio.**

## Ley 21.719

Proyecto Milenio incorpora como requisito P0 transversal la preparación para la Ley N° 21.719.

Documentos principales:

- `PMGM-REQ-026` — Cumplimiento Ley 21.719.
- `PMGM-SEC-003` — Matriz técnica de cumplimiento.
- `PMGM-BLG-048` — Backlog de implementación.
- catálogos `PMGM-DATA-CLASSIFICATION-*` — clasificación y guardrails de datos.

La plataforma considera especialmente sensible la información que pueda revelar pertenencia, trayectoria o convicciones filosóficas/ideológicas. La conservación histórica se diseña por finalidad y base jurídica, con capacidades de minimización, anonimización/supresión cuando corresponda y auditoría de acciones críticas.

Los controles técnicos son una línea base de ingeniería y no sustituyen la revisión jurídica/organizacional previa al tratamiento de datos personales reales.

## Regla de ceremonias

Una ceremonia sólo puede avanzar a autorización definitiva cuando las validaciones obligatorias aplicables estén aprobadas o exista una excepción formal y auditada.

Como base:

1. Régimen Interior debe aprobar los antecedentes institucionales.
2. El Taller debe estar al día con Gran Tesorería.
3. El Taller debe estar al día con reposiciones de Gran Hospitalaria.
4. En iniciaciones, el insinuado debe cumplir el período mínimo de publicación definido por configuración institucional.
5. Gran Secretaría consume estas validaciones y genera el documento oficial cuando corresponde.

## Bootstrap institucional

v0.33 incorporó una configuración inicial transaccional e idempotente con dry-run previo. El paquete inicial versionado crea/verifica exclusivamente estructura institucional:

- Gran Logia Mixta de Chile.
- Respetable Logia Libertad Nº 23.
- catálogos de perfiles de seguridad y cargos institucionales/ritualísticos.

No se versionan personas, RUT/RUN, correos, teléfonos ni credenciales. El Superadmin de plataforma (`platform_superadmin`) está separado del Administrador de Gran Logia y requiere alcance institucional `order` para la administración transversal.

## Tecnología base

- Backend: ASP.NET Core / .NET 10.
- Base de datos: PostgreSQL.
- Persistencia: Entity Framework Core + Npgsql.
- Frontend: React + TypeScript + Vite.
- Contenedores: Docker / Docker Compose.
- CI: GitHub Actions.
- Identidad: Keycloak / OIDC Authorization Code Flow + PKCE S256.
- Archivos binarios: MinIO/S3 privado.
- Análisis antimalware: ClamAV.
- Gateway piloto: HTTPS mediante Caddy.

## Calidad y release

PMGM CI mantiene cinco jobs críticos: backend, frontend, infraestructura, first-implementation smoke/recovery y piloto HTTPS/recovery. También ejecuta gates de privacidad Ley 21.719, clasificación de datos y seguridad de migraciones.

La RC agrega `tests/release_gate.py`, que comprueba coherencia entre el manifest, la versión del assembly, la infraestructura piloto, OIDC, bootstrap y los jobs requeridos. `/api/system/info` obtiene su versión desde el assembly para evitar discrepancias entre binario y release validada.

El workflow `Proyecto Centenario Pre-UAT Installable` construye además un ZIP reproducible del SHA actual y verifica que `INSTALAR.sh`, `LEAME-INSTALACION.md`, `BUILD-INFO.txt`, `ESTADO-PAQUETE.txt` y `MANIFEST.sha256` estén presentes y sean íntegros antes de publicar el artifact.

## Flujo de trabajo

- `main`: rama estable.
- `dev`: integración.
- ramas `feature/*` / `feat/*`: cambios funcionales.
- ramas `release/*`: congelamiento y preparación de candidatos.
- requisito → diseño → desarrollo → pruebas → documentación → PR → CI exact-head → merge.

No se fusiona un PR con CI fallando, incompleto o correspondiente a un SHA anterior.

## Documentación de referencia

- `docs/qa/PMGM-GITHUB-PAGES-SHOWCASE.md` — demo pública de GitHub Pages.
- `docs/installation/PREUAT-INSTALL.md` — paquete instalable pre-UAT.
- `docs/instalacion/PMGM-PILOT-OPERACIONAL-V032.md` — despliegue del piloto.
- `docs/instalacion/PMGM-PILOT-BOOTSTRAP-V033.json` — paquete de bootstrap inicial.
- `docs/instalacion/PMGM-RELEASE-CANDIDATE-V100-RC1.md` — criterios de RC, UAT, promoción y rollback.
- `changelog/v1.0.0-rc1.md` — resumen funcional de la Release Candidate.
- `/docs`, `/backlog`, `/backend`, `/frontend` e `/infrastructure` — fuente versionada del detalle técnico y funcional.
