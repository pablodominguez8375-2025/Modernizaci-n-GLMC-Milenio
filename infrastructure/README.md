# Infrastructure

Infraestructura reproducible del Proyecto Milenio.

## Base objetivo
- Ubuntu Server 24.04 LTS.
- Docker / Docker Compose.
- Nginx como servidor del frontend y proxy de mismo origen hacia la API.
- TLS en el punto de publicación productivo.
- PostgreSQL 17, alineado con el entorno de CI.

## Ambiente de desarrollo integrado
Desde la raíz del repositorio:

```bash
docker compose -f infrastructure/docker-compose.dev.yml up --build
```

Servicios expuestos:
- Web Proyecto Milenio: `http://localhost:8081`
- API directa: `http://localhost:8080`
- PostgreSQL: `localhost:5432`

La API de desarrollo aplica las migraciones versionadas al iniciar mediante `Database__ApplyMigrationsOnStartup=true`. Esta opción es explícita y no está activada por defecto en otros ambientes.

El contenedor web usa Nginx y redirige `/api/*` y `/health/*` al servicio `api`, evitando CORS en el despliegue integrado.

## Modo demostración
Mientras se implementa el SSO/OIDC definitivo, el frontend puede construirse con datos demostrativos:

```bash
VITE_USE_MOCKS=true docker compose -f infrastructure/docker-compose.dev.yml up --build
```

Para conectar la UI a la API real:

```bash
VITE_USE_MOCKS=false docker compose -f infrastructure/docker-compose.dev.yml up --build
```

La API real seguirá exigiendo autenticación para endpoints protegidos; desactivar mocks no reemplaza el SSO.

## Seguridad
- `POSTGRES_PASSWORD` debe reemplazarse fuera de desarrollo.
- No guardar secretos en variables `VITE_*`: quedan incluidas en el bundle del navegador.
- TLS y secretos productivos se configurarán por ambiente, no dentro del repositorio.
