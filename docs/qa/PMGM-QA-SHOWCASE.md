# PMGM — QA mostrable

## Objetivo

Este corte permite dos modos complementarios:

1. **Showcase standalone**: demostración visual reproducible, sin OIDC, API, PostgreSQL, MinIO ni ClamAV. Usa exclusivamente datos ficticios embebidos en el frontend.
2. **QA integrado**: stack completo con autenticación OIDC, API .NET, PostgreSQL, MinIO y ClamAV para validar flujos reales.

Los datos del showcase son ficticios y no deben confundirse con datos institucionales reales.

---

## A. Showcase standalone — recomendado para presentaciones

### Requisito

- Docker Desktop / Docker Engine con Docker Compose v2.

### Levantar

Desde la raíz del repositorio:

```bash
docker compose -f infrastructure/docker-compose.showcase.yml up -d --build
```

Abrir:

```text
http://127.0.0.1:8082
```

### Verificar salud

```bash
curl http://127.0.0.1:8082/health/web
```

Debe responder:

```text
ok
```

### Detener

```bash
docker compose -f infrastructure/docker-compose.showcase.yml down
```

### Cambiar puerto

Linux/macOS:

```bash
PMGM_SHOWCASE_HTTP_PORT=8090 docker compose -f infrastructure/docker-compose.showcase.yml up -d --build
```

PowerShell:

```powershell
$env:PMGM_SHOWCASE_HTTP_PORT="8090"
docker compose -f infrastructure/docker-compose.showcase.yml up -d --build
```

---

## B. Recorrido recomendado de demostración

1. **Inicio**
   - Mostrar panel unificado.
   - Señalar `Modo demostración` en la cabecera.
   - Explicar que permisos y módulos se presentan como una sola plataforma.

2. **Insinuados**
   - Mostrar período de publicación y cumplimiento del plazo configurable.
   - Recalcar minimización de información publicada.

3. **Ceremonias**
   - Mostrar bandeja y requisitos institucionales.
   - Explicar validaciones de Régimen Interior, Gran Tesorería, Gran Hospitalaria y publicación previa cuando corresponde.

4. **Gran Secretaría**
   - Mostrar espacios, disponibilidad, reservas y autorización formal.
   - Explicar que una reserva no puede superponerse con otra confirmada.

5. **Gestión Logial**
   - Mostrar Tenidas, asistencia, docencia y actas versionadas.

6. **Calendario Institucional**
   - Abrir desde el menú `Calendario` o desde el acceso del inicio.
   - Vista `Agenda`: mostrar Tenida, docencia, ceremonia y reserva.
   - Mostrar el registro `Ocupado`: el sistema deja visible sólo la ocupación necesaria, sin revelar el detalle protegido.
   - Cambiar a vista `Mes`.
   - Filtrar por Taller, estado y tipo.
   - Como usuario administrador en demo, usar `Revisar conflictos` y `Reconciliar fuentes`.

7. **Biblioteca Virtual / Gestor Documental**
   - Explicar separación entre documento canónico y proyección publicada por permisos/finalidad.

---

## C. QA integrado

### Preparar variables

Copiar el ejemplo sin versionar secretos:

```bash
cp .env.qa.example .env.qa
```

Completar al menos:

- `POSTGRES_PASSWORD`
- `MINIO_ACCESS_KEY`
- `MINIO_SECRET_KEY`
- `OIDC_AUTHORITY`
- `OIDC_CLIENT_ID`
- `OIDC_API_AUDIENCE`

### Levantar

```bash
docker compose --env-file .env.qa -f infrastructure/docker-compose.qa.yml up -d --build
```

Por defecto la web queda disponible en:

```text
http://127.0.0.1:8081
```

### Verificaciones mínimas

```bash
curl http://127.0.0.1:8081/health/web
curl http://127.0.0.1:8081/health/live
curl http://127.0.0.1:8081/health/ready
```

### Detener sin borrar datos

```bash
docker compose --env-file .env.qa -f infrastructure/docker-compose.qa.yml down
```

### Reinicio completamente limpio

> Destructivo para los datos QA locales.

```bash
docker compose --env-file .env.qa -f infrastructure/docker-compose.qa.yml down -v
```

---

## D. Criterios de aceptación del QA mostrable

- [ ] El contenedor showcase construye ejecutando tests frontend.
- [ ] `/health/web` responde `200 ok`.
- [ ] El inicio carga sin autenticación en modo demostración.
- [ ] El menú muestra Calendario Institucional.
- [ ] Agenda y Mes funcionan en escritorio y móvil.
- [ ] El filtro por Taller no mezcla eventos de otro Taller.
- [ ] Un evento enmascarado se muestra como `Ocupado` y no presenta fuente, responsable ni detalle protegido.
- [ ] El calendario muestra Tenidas, docencia, ceremonias y reservas.
- [ ] El modo QA integrado requiere OIDC y usa bearer token; no depende de cookies de sesión para la API.
- [ ] CI valida Ley 21.719, clasificación de datos, seguridad de migraciones, backend, PostgreSQL, S3, ClamAV, frontend, contenedor normal y contenedor showcase.

---

## E. Regla de uso

El showcase es exclusivamente una superficie de demostración con datos ficticios. Las decisiones funcionales, pruebas de permisos, trazabilidad, persistencia y cumplimiento deben validarse en el QA integrado antes de promover a producción.
