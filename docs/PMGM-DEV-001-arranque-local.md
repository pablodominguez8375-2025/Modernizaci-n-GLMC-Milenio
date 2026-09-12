# PMGM-DEV-001 — Arranque local del Proyecto Milenio

## Objetivo
Levantar el backend y PostgreSQL de desarrollo de manera reproducible usando Docker.

## Requisitos
- Git.
- Docker con soporte para Docker Compose.

Para desarrollo directo sin contenedores también puede utilizarse .NET SDK 10 y PostgreSQL 18.

## 1. Clonar y cambiar a `dev`
```bash
git clone https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio.git
cd Modernizaci-n-GLMC-Milenio
git checkout dev
```

## 2. Variables locales
Copiar el archivo de ejemplo:
```bash
cp .env.example .env
```

El valor incluido es exclusivamente de desarrollo. Debe reemplazarse en cualquier ambiente compartido o productivo.

## 3. Levantar PostgreSQL y API
```bash
docker compose --env-file .env -f infrastructure/docker-compose.dev.yml up --build -d
```

## 4. Verificar salud
Proceso API:
```text
http://localhost:8080/health/live
```

Conectividad a PostgreSQL:
```text
http://localhost:8080/health/ready
```

Información del servicio:
```text
http://localhost:8080/api/system/info
```

## 5. Aplicar migraciones
Mientras no se automatice la migración durante el arranque, ejecutar desde un equipo con .NET SDK 10:

```bash
dotnet tool install --global dotnet-ef --version 10.0.11

dotnet ef database update \
  --project backend/src/PMGM.Api/PMGM.Api.csproj \
  --startup-project backend/src/PMGM.Api/PMGM.Api.csproj
```

Si se ejecuta fuera de Docker, configurar la cadena `ConnectionStrings__MainDatabase` apuntando al PostgreSQL local.

## 6. Apagar ambiente
```bash
docker compose --env-file .env -f infrastructure/docker-compose.dev.yml down
```

Para eliminar además el volumen de datos de desarrollo:
```bash
docker compose --env-file .env -f infrastructure/docker-compose.dev.yml down -v
```

## Reglas
- No subir `.env` al repositorio.
- No reutilizar credenciales de desarrollo en producción.
- No usar datos reales de miembros para desarrollo.
- Toda modificación de esquema debe quedar en una migración versionada.
- Todo cambio debe entrar por `dev` y promoverse a `main` mediante revisión.
