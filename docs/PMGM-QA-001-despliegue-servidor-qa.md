# PMGM-QA-001 — Despliegue de Proyecto Milenio en servidor QA

**Estado:** Activo  
**Objetivo:** permitir que un administrador técnico levante un ambiente de pruebas reproducible sin depender de conocimiento oral del proyecto.

## 1. Alcance
Este procedimiento instala en un único servidor QA:
- frontend React servido por Nginx;
- API ASP.NET Core / .NET 10;
- PostgreSQL 17;
- MinIO S3-compatible privado;
- ClamAV;
- volúmenes persistentes Docker.

El stack se define en `infrastructure/docker-compose.qa.yml`.

QA **no es producción**. En particular, antes de producción deberán definirse alta disponibilidad, backups externos, monitoreo, endurecimiento de host, proveedor OIDC definitivo, certificados, política de secretos y estrategia formal de migraciones.

## 2. Recomendación inicial de servidor QA
Como punto de partida para pruebas funcionales de equipo pequeño:
- Linux 64-bit actualizado;
- 4 vCPU;
- 8 GB RAM;
- 80 GB SSD libres como mínimo;
- Docker Engine y Docker Compose plugin;
- Git;
- DNS interno o nombre resoluble para el ambiente;
- salida a Internet para descargar imágenes/paquetes durante instalación.

El dimensionamiento deberá crecer según volumen documental, cantidad de usuarios concurrentes y retención de archivos.

## 3. Puertos
Exponer hacia usuarios únicamente:
- TCP 443 — HTTPS mediante reverse proxy;
- TCP 80 — opcional para redirección a HTTPS;
- TCP 22 — administración SSH restringida por firewall/VPN.

El compose QA **no publica PostgreSQL, MinIO, consola MinIO ni ClamAV** al host. El frontend escucha por defecto sólo en `127.0.0.1:8081`; un reverse proxy del servidor debe entregar HTTPS.

## 4. Preparar servidor
Ejemplo general en una distribución Linux con Docker ya instalado:

```bash
sudo mkdir -p /opt/pmgm
sudo chown "$USER":"$USER" /opt/pmgm
cd /opt/pmgm
```

Clonar repositorio:

```bash
git clone https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio.git app
cd app
git checkout dev
```

Para un QA controlado es preferible desplegar un **commit exacto** validado por CI en lugar de seguir automáticamente la punta móvil de `dev`:

```bash
git rev-parse HEAD
```

Guardar ese SHA en el registro de despliegue.

## 5. Configurar variables
Crear archivo QA desde el ejemplo:

```bash
cp .env.qa.example .env.qa
chmod 600 .env.qa
```

Editar `.env.qa` y reemplazar al menos:
- `POSTGRES_PASSWORD`;
- `MINIO_ACCESS_KEY`;
- `MINIO_SECRET_KEY`;
- valores OIDC cuando exista proveedor de identidad QA.

Generar secretos largos y aleatorios. No reutilizar claves de desarrollo ni producción.

**Nunca** hacer commit de `.env.qa`.

## 6. Validar configuración antes de iniciar

```bash
docker compose --env-file .env.qa \
  -f infrastructure/docker-compose.qa.yml config >/tmp/pmgm-qa-compose.rendered.yml
```

Si existe una variable obligatoria sin definir, Docker Compose debe detenerse antes de crear servicios.

Verificar que no aparezcan puertos públicos para PostgreSQL, MinIO o ClamAV.

## 7. Construir imágenes

```bash
docker compose --env-file .env.qa \
  -f infrastructure/docker-compose.qa.yml \
  build --pull
```

El frontend se construye con `VITE_USE_MOCKS=false`.

Si se cambian valores OIDC usados por Vite, reconstruir la imagen web, ya que son variables de build.

## 8. Levantar ambiente

```bash
docker compose --env-file .env.qa \
  -f infrastructure/docker-compose.qa.yml \
  up -d
```

Revisar estado:

```bash
docker compose --env-file .env.qa \
  -f infrastructure/docker-compose.qa.yml ps
```

Revisar logs si un servicio no inicia:

```bash
docker compose --env-file .env.qa \
  -f infrastructure/docker-compose.qa.yml logs --tail=200 api
```

## 9. Migraciones
En QA `APPLY_MIGRATIONS_ON_STARTUP=true` permite que la API ejecute migraciones durante el arranque conforme a la configuración actual del proyecto.

Antes de una actualización que incluya migraciones:
1. respaldar PostgreSQL;
2. registrar SHA anterior y nuevo;
3. revisar CI/Migration Safety Gate;
4. aplicar actualización;
5. ejecutar smoke tests.

Producción utilizará una estrategia de migraciones más controlada y no debe asumir automáticamente esta misma configuración.

## 10. Verificación básica
Desde el propio servidor:

```bash
curl -fsS http://127.0.0.1:8081/health/web
curl -fsS http://127.0.0.1:8081/health/live
curl -fsS http://127.0.0.1:8081/health/ready
curl -fsS http://127.0.0.1:8081/api/system/info
```

Resultados esperados:
- web responde `ok`;
- API live responde estado `ok`;
- ready confirma conectividad PostgreSQL;
- system/info informa Proyecto Milenio y versión del API.

El hecho de que estos checks estén verdes no sustituye las pruebas funcionales.

## 11. Reverse proxy y HTTPS
El compose enlaza el frontend a `127.0.0.1` para evitar exposición directa.

Configurar Nginx, Caddy, Traefik u otro reverse proxy del host con:
- certificado TLS válido;
- redirección HTTP → HTTPS;
- proxy hacia `http://127.0.0.1:8081`;
- preservación de `Host`, `X-Forwarded-For` y `X-Forwarded-Proto`;
- límites de tamaño compatibles con `DOCUMENT_MAX_UPLOAD_BYTES`;
- timeouts adecuados para carga documental;
- logs sin tokens ni parámetros de autenticación sensibles.

## 12. Identidad OIDC en QA
Los flujos autenticados requieren un proveedor OIDC de pruebas.

Configurar:
- `OIDC_AUTHORITY`;
- `OIDC_API_AUDIENCE`;
- `OIDC_CLIENT_ID`;
- `OIDC_SCOPE`.

El tenant/realm de QA debe usar cuentas de prueba y no credenciales reales de hermanos cuando todavía se esté desarrollando el modelo de identidad.

Hasta definir el proveedor definitivo, el ambiente puede validar infraestructura, salud y endpoints anónimos, pero no debe considerarse representativo de producción para autenticación.

## 13. Datos de QA
Reglas obligatorias:
- no importar padrón real completo para pruebas ordinarias;
- preferir datos sintéticos;
- si excepcionalmente se autorizan datos reales, aplicar minimización, autorización, finalidad, acceso y plazo de eliminación conforme a PMGM-REQ-026;
- separar totalmente credenciales y bases QA/producción.

Preparar perfiles sintéticos que cubran al menos:
- hermano grado 1;
- hermano grado 2;
- hermano grado 3;
- autoridad de Taller;
- Gran Secretaría;
- Régimen Interior;
- Gran Archivero;
- Biblioteca editor/revisor;
- casos inactivos, retiros y transferencias históricas.

## 14. Backups mínimos de QA
Aunque QA no es producción, antes de cambios de esquema conviene respaldar.

PostgreSQL:

```bash
mkdir -p backups

docker compose --env-file .env.qa -f infrastructure/docker-compose.qa.yml \
  exec -T postgres pg_dump -U "${POSTGRES_USER:-pmgm_app}" -d "${POSTGRES_DB:-pmgm}" \
  > "backups/pmgm-$(date +%Y%m%d-%H%M%S).sql"
```

Para Object Storage, utilizar snapshot/backup del volumen o una tarea `mc mirror` hacia un destino de respaldo autorizado. No copiar documentos sensibles a ubicaciones personales o no cifradas.

## 15. Actualizar QA

```bash
cd /opt/pmgm/app
git fetch origin
git checkout dev
git pull --ff-only origin dev
NEW_SHA=$(git rev-parse HEAD)
echo "$NEW_SHA"

docker compose --env-file .env.qa -f infrastructure/docker-compose.qa.yml build --pull
docker compose --env-file .env.qa -f infrastructure/docker-compose.qa.yml up -d
```

Registrar:
- fecha/hora;
- SHA desplegado;
- responsable;
- resultado de CI del SHA;
- backup previo si aplica;
- resultado de pruebas QA.

## 16. Rollback
El rollback de código puede realizarse volviendo al SHA anterior y reconstruyendo imágenes.

```bash
git checkout <SHA_ANTERIOR>
docker compose --env-file .env.qa -f infrastructure/docker-compose.qa.yml build
docker compose --env-file .env.qa -f infrastructure/docker-compose.qa.yml up -d
```

**Importante:** si el release cambió el esquema de base de datos, volver sólo el código puede no ser seguro. En ese caso se debe seguir el plan de migración/rollback definido para ese release o restaurar backup de QA.

## 17. Detener QA
Sin borrar datos:

```bash
docker compose --env-file .env.qa -f infrastructure/docker-compose.qa.yml down
```

Borrar volúmenes sólo cuando se quiera destruir expresamente el ambiente QA:

```bash
docker compose --env-file .env.qa -f infrastructure/docker-compose.qa.yml down -v
```

La opción `-v` destruye PostgreSQL, archivos de MinIO y base local de ClamAV del ambiente.

## 18. Evidencia de despliegue
Por cada despliegue mantener:
- SHA Git;
- versión funcional;
- captura/salida de `docker compose ps`;
- resultados health;
- resultado de pruebas QA;
- incidencias encontradas;
- decisión aprobado/rechazado;
- persona responsable.

## 19. Paso a producción
QA sólo puede promoverse cuando:
- CI del SHA está verde;
- checklist PMGM-QA-002 está aprobado;
- no existen defectos críticos abiertos;
- migraciones y rollback fueron revisados;
- identidad, TLS, backups, monitoreo y secretos están configurados;
- Product Owner autoriza la promoción correspondiente.
