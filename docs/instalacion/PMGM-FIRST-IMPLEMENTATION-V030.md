# Proyecto Milenio — primera implementación integrada v0.30

## 1. Propósito

Este paquete permite levantar una primera versión operable de Proyecto Milenio para QA, evaluación institucional y piloto local. No utiliza mocks: frontend, API, PostgreSQL, Keycloak, MinIO y ClamAV funcionan como servicios reales dentro de Docker.

No es todavía una configuración productiva. Antes de producción deben cerrarse TLS, gestión externa de secretos, respaldos/restauración probados, observabilidad, alta disponibilidad cuando corresponda, hardening de Keycloak y estrategia formal de despliegue/migraciones.

## 2. Módulos cubiertos por la primera implementación

El dataset QA ficticio permite recorrer de forma integrada:

- Core institucional y organizaciones.
- Membresía y ficha de miembros.
- Historial de afiliación y traslado entre Talleres.
- Régimen Interior y reportería/control de miembros.
- Gran Tesorería: estados de regularidad.
- Gran Hospitalaria: estados de regularidad/reposiciones.
- Ceremonias: iniciación, aumento de salario y validaciones.
- Portal de insinuados con plazo configurable, actualmente 20 días en el dataset QA.
- Gran Secretaría: documentos, autorizaciones y reserva de espacios.
- Gestión Logial: Tenidas, asistencia, actas y docencia.
- Calendario institucional y notificaciones ya integrados en la plataforma.
- Gestor Documental.
- Biblioteca Virtual, incluyendo una plancha de trabajo ficticia autorizada.
- Gran Archivero como módulo separado de Biblioteca Virtual; las planchas quedan explícitamente fuera de Gran Archivero.
- Controles transversales de privacidad y Ley 21.719 ya incorporados en la arquitectura y gates de CI.

## 3. Arquitectura del paquete local

Servicios Docker:

| Servicio | Función | Exposición host |
| --- | --- | --- |
| `web` | React/Vite servido por Nginx y proxy de API | `127.0.0.1:8081` |
| `api` | .NET 10 / PMGM.Api | sólo red interna; accesible vía Nginx |
| `postgres` | persistencia PostgreSQL 17 | sólo red interna |
| `keycloak` | autenticación OIDC QA | `127.0.0.1:8180` |
| `minio` | almacenamiento documental privado | sólo red interna |
| `minio-init` | creación y bloqueo del bucket | sólo ejecución inicial |
| `clamav` | análisis antimalware documental | sólo red interna |

La exposición a `127.0.0.1` es intencional. Este stack no debe publicarse directamente a Internet ni a una LAN como solución productiva.

## 4. Requisitos del equipo

Requisitos funcionales:

- Windows 11 con Docker Desktop, o Linux/macOS con Docker Engine + Docker Compose v2.
- Docker Compose disponible como `docker compose`.
- Navegador moderno.
- Para el smoke test shell: `curl` y `python3`.

Recomendación de ingeniería para una evaluación fluida, no dimensionamiento productivo:

- 4 vCPU como base.
- 8 GB RAM disponibles para Docker como mínimo práctico; 12–16 GB recomendados para mayor holgura, especialmente por Keycloak y ClamAV.
- 20 GB libres como punto de partida para imágenes, volúmenes y documentos QA.

El dimensionamiento productivo se realizará posteriormente con población real estimada, concurrencia, volumen documental, respaldo, retención y crecimiento.

## 5. Inicio rápido en Windows

Desde la raíz del repositorio:

```powershell
.\scripts\start-first-implementation.ps1
```

El launcher:

1. verifica Docker;
2. genera secretos aleatorios locales si no existen;
3. guarda esos secretos en `infrastructure/.env.first.local`;
4. construye y levanta los contenedores;
5. espera Keycloak, API, PostgreSQL y frontend;
6. abre `http://127.0.0.1:8081` salvo que se use `-NoBrowser`.

Para levantar sin abrir navegador:

```powershell
.\scripts\start-first-implementation.ps1 -NoBrowser
```

Para eliminar volúmenes QA y reconstruir los datos ficticios desde cero:

```powershell
.\scripts\start-first-implementation.ps1 -Reset
```

## 6. Inicio rápido en Linux/macOS

```bash
sh scripts/start-first-implementation.sh
```

Reinicio desde cero:

```bash
sh scripts/start-first-implementation.sh --reset
```

## 7. Credenciales QA

Usuarios incluidos:

- `qa.admin`: ámbito Orden/Gran Logia y roles institucionales principales para evaluación integrada.
- `qa.taller23`: ámbito exclusivo del Taller QA Nº 23 con capacidades de Gestión Logial.

La contraseña de ambos usuarios se genera localmente y no está versionada. Se guarda en:

```text
infrastructure/.env.first.local
```

En Windows puede mostrarse explícitamente sólo cuando sea necesario:

```powershell
.\scripts\start-first-implementation.ps1 -NoBrowser -ShowCredentials
```

El archivo `.env.first.local` está excluido de Git y debe permanecer local.

## 8. Validación automática

### Windows

Con el stack ya levantado:

```powershell
.\scripts\smoke-first-implementation.ps1
```

### Linux/macOS

```bash
bash scripts/smoke-first-implementation.sh
```

El smoke test valida, como mínimo:

- discovery OIDC de Keycloak;
- API live y ready;
- frontend Nginx;
- autenticación real de `qa.admin` y `qa.taller23`;
- `/api/session/me`;
- consulta real de Membresía del Taller QA Nº 23;
- Portal de Insinuados;
- Biblioteca Virtual;
- Gran Archivero;
- denegación `403` de Gran Archivero para el perfil Taller;
- scope `organization` y capacidad de Gestión Logial para `qa.taller23`.

Resultado esperado:

```text
SMOKE FIRST IMPLEMENTATION OK
```

## 9. Recorrido funcional recomendado para la evaluación

1. Ingresar como `qa.admin`.
2. Revisar Inicio / Centro de mando.
3. Abrir Fichas de miembros y el Taller QA Nº 23.
4. Revisar historial de traslado del miembro ficticio entre Taller Nº 1 y Nº 23.
5. Consultar Régimen Interior y reportería institucional.
6. Revisar regularidad de Gran Tesorería y Gran Hospitalaria.
7. Abrir Insinuados y verificar el período de publicación configurado.
8. Revisar Ceremonias y la solicitud ficticia de aumento de salario/iniciación.
9. Abrir Gran Secretaría y la reserva del Templo Principal QA.
10. Revisar Calendario Institucional.
11. Revisar Gestión Logial: Tenida, asistencia, acta y docencia.
12. Abrir Biblioteca Virtual y comprobar la plancha ficticia autorizada.
13. Abrir Gran Archivero y comprobar que sólo contiene patrimonio documental institucional elegible.
14. Cerrar sesión e ingresar como `qa.taller23` para verificar la segregación por ámbito y roles.

## 10. Operación Docker

Estado de servicios:

```bash
docker compose --env-file infrastructure/.env.first.local -f infrastructure/docker-compose.first.yml ps
```

Logs generales:

```bash
docker compose --env-file infrastructure/.env.first.local -f infrastructure/docker-compose.first.yml logs --tail=200
```

Logs de la API:

```bash
docker compose --env-file infrastructure/.env.first.local -f infrastructure/docker-compose.first.yml logs api --tail=200
```

Detener conservando datos:

```bash
docker compose --env-file infrastructure/.env.first.local -f infrastructure/docker-compose.first.yml down
```

Eliminar el entorno QA y todos sus volúmenes:

```bash
docker compose --env-file infrastructure/.env.first.local -f infrastructure/docker-compose.first.yml down -v --remove-orphans
```

La última operación destruye la base QA, documentos QA, datos de Keycloak y firmas de ClamAV almacenadas en volúmenes del paquete local.

## 11. Seguridad y protección de datos

- Los datos precargados son ficticios y usan dominios `.invalid`.
- `DemoData:Enabled` provoca una excepción de arranque si se intenta usar con `ASPNETCORE_ENVIRONMENT=Production`.
- Las credenciales QA no se almacenan en el repositorio: Keycloak resuelve la contraseña desde una variable de entorno durante el import del realm.
- MinIO no expone el bucket de documentos públicamente.
- Los documentos se sirven mediante endpoints autenticados; no se generan enlaces permanentes públicos.
- La excepción HTTP del OIDC corresponde sólo al stack loopback QA. El frontend mantiene HTTPS obligatorio fuera de esa configuración local explícita.
- Los gates de privacidad, clasificación de datos y seguridad de migraciones continúan siendo obligatorios en CI.

## 12. Criterio de cierre de v0.30

La versión sólo se considera cerrada cuando el head exacto de PR cumple simultáneamente:

- backend build/test verde;
- PostgreSQL, S3/MinIO y ClamAV integration tests verdes;
- Privacy Gate Ley 21.719 verde;
- Data Classification Gate verde;
- Migration Safety Gate verde;
- frontend lint/build/tests verdes;
- configuración OIDC local validada;
- Docker Compose de primera implementación válido;
- realm Keycloak válido y sin contraseñas fijas;
- smoke test del stack integrado verde;
- PR listo para revisión y squash merge a `dev`.

## 13. Pendientes posteriores a esta primera implementación

Para una versión preproductiva/productiva deberán abordarse en sprints posteriores:

- TLS y hostname institucional definitivo;
- Keycloak en modo productivo y estrategia de identidad institucional;
- rotación/gestión centralizada de secretos;
- backup y restore probado de PostgreSQL, MinIO y configuración de identidad;
- observabilidad, métricas y alertas;
- política de actualización de imágenes/contenedores;
- dimensionamiento y pruebas de carga;
- migración de datos reales con validación de calidad;
- perfiles/roles definitivos aprobados por la institución;
- UAT institucional y plan de puesta en marcha.
