# Proyecto Centenario — instalación del paquete pre-UAT

Este documento acompaña el artefacto `Proyecto-Centenario-preUAT-installable.zip`. El paquete se construye automáticamente desde un SHA exacto, incluye controles de integridad y no contiene secretos ni datos institucionales reales.

> **Importante:** instalar el paquete no equivale a autorizar producción ni carga de datos personales reales. La promoción estable requiere UAT y aprobación institucional.

## 1. Qué es cada entorno

| Entorno | Finalidad | Backend real | Datos reales | URL/forma de acceso |
|---|---|---:|---:|---|
| Demo pública GitHub Pages | Revisión visual y funcional de interfaz | No | Prohibidos | `https://pablodominguez8375-2025.github.io/Modernizaci-n-GLMC-Milenio/` |
| Paquete pre-UAT | Instalar un SHA exacto para pruebas integradas | Sí | No por defecto | VM propia |
| Piloto institucional | HTTPS + OIDC + persistencia + backup | Sí | Sólo tras gates aplicables | FQDN institucional |
| Producción estable | Operación oficial | Sí | Sí, bajo gobierno aprobado | FQDN institucional definitivo |

La demo pública y la VM son entornos completamente independientes.

## 2. Servidor recomendado

### VM institucional recomendada

- Ubuntu Server 24.04 LTS x86_64.
- 8 vCPU.
- 32 GB RAM.
- 500 GB SSD/NVMe expandible.
- Docker Engine y Docker Compose v2.
- DNS institucional definido.
- TCP 80/443 disponibles.
- SSH administrativo restringido.
- Salida HTTPS para descarga inicial de imágenes y actualizaciones aprobadas.
- Destino de backup fuera de la propia VM.

### Mínimo para laboratorio/UAT

- 4 vCPU.
- 16 GB RAM.
- 150 GB de almacenamiento.

El preflight del paquete advierte cuando la VM sólo cumple nivel UAT y falla cuando está por debajo del mínimo técnico.

## 3. Qué contiene el ZIP

En la raíz encontrarás:

```text
Proyecto-Centenario-preUAT-installable/
├── INSTALAR.sh
├── LEAME-INSTALACION.md
├── ESTADO-PAQUETE.txt
├── BUILD-INFO.txt
├── MANIFEST.sha256
├── pmgm-repository.bundle
├── backend/
├── frontend/
├── infrastructure/
├── scripts/
├── docs/
└── ... resto del código exacto del SHA construido
```

`BUILD-INFO.txt` identifica SHA, rama/ref, número de ejecución y fecha UTC de construcción. `MANIFEST.sha256` cubre todos los archivos entregados salvo el propio manifest. `pmgm-repository.bundle` conserva historia Git para trazabilidad y recuperación sin depender inmediatamente de GitHub.

## 4. Preparación de la VM

Descomprime el ZIP en una carpeta exclusiva, por ejemplo:

```bash
sudo mkdir -p /opt/proyecto-centenario
sudo chown "$USER":"$USER" /opt/proyecto-centenario
cd /opt/proyecto-centenario
unzip /ruta/Proyecto-Centenario-preUAT-installable.zip
cd Proyecto-Centenario-preUAT-installable
```

Valida primero el ZIP antes de copiarlo o, una vez extraído, valida el contenido:

```bash
sha256sum -c MANIFEST.sha256
```

La salida debe finalizar sin archivos fallidos.

## 5. Archivo de entorno institucional

El instalador usa por defecto:

```text
/etc/pmgm/pmgm.env
```

Puedes crear una plantilla protegida con:

```bash
sudo mkdir -p /etc/pmgm
sudo chown "$USER":"$USER" /etc/pmgm
./INSTALAR.sh --prepare-env
```

Luego edita `/etc/pmgm/pmgm.env` y reemplaza todos los placeholders. Debes definir, como mínimo, FQDN/URL HTTPS, contraseñas distintas para PostgreSQL, MinIO y Keycloak, usuario bootstrap institucional y correo administrativo válido.

No reutilices secretos de QA. No guardes el archivo real dentro del repositorio ni lo subas a GitHub.

Si deseas usar otra ruta:

```bash
./INSTALAR.sh --env /ruta/segura/pmgm.env --prepare-env
```

## 6. Instalación guiada

Una vez completado el archivo de entorno:

```bash
./INSTALAR.sh
```

El instalador ejecuta en orden:

```text
integridad del paquete
→ preflight de VM
→ validación de configuración del piloto
→ arranque Docker Compose
→ HTTPS/OIDC
→ smoke autenticado
→ respaldo inicial
→ verificación del respaldo
```

Si cualquiera de los controles críticos falla, el proceso termina con error y no declara la instalación completada.

Para laboratorio controlado se puede omitir explícitamente el backup inicial:

```bash
./INSTALAR.sh --skip-initial-backup
```

No se recomienda utilizar esa opción en la VM que se pretende conservar como instalación institucional.

## 7. Servicios esperados

La instalación piloto integra:

- gateway HTTPS Caddy;
- frontend React/Vite servido en contenedor;
- API ASP.NET Core/.NET;
- PostgreSQL;
- Keycloak/OIDC;
- MinIO/S3 privado para documentos;
- ClamAV para análisis antimalware cuando corresponde al flujo documental.

PostgreSQL, MinIO, ClamAV y servicios internos no deben exponerse directamente a Internet. El acceso público debe concentrarse en HTTPS.

## 8. Verificación posterior

Después de instalar, confirma:

```bash
docker compose --env-file /etc/pmgm/pmgm.env -f infrastructure/docker-compose.pilot.yml ps
```

Y vuelve a ejecutar el smoke cuando necesites una comprobación operacional:

```bash
PMGM_PILOT_ENV_FILE=/etc/pmgm/pmgm.env bash scripts/smoke-pilot.sh
```

Los health checks y el smoke deben aprobar antes de iniciar UAT.

## 9. Respaldo inicial

El instalador crea un respaldo verificado bajo `backups/` salvo que se haya indicado otra ruta con `--backup-root`.

Ejemplo:

```bash
./INSTALAR.sh --backup-root /srv/pmgm-backups
```

Una vez creado, copia una copia validada **fuera de la VM**: almacenamiento de respaldo, NAS, repositorio cifrado u otro destino institucional definido. Un backup almacenado sólo en la misma VM no protege contra pérdida total del host.

## 10. UAT y paso a estable

La secuencia institucional acordada es:

```text
instalación técnica
→ smoke
→ backup/restauración
→ UAT funcional
→ cierre de observaciones
→ aprobación Sponsor/Product Owner
→ versión/tag aprobado
→ promoción estable
```

No se debe convertir `dev` en producción ni configurar la VM para seguir automáticamente esa rama.

## 11. Actualizaciones futuras de la misma VM

La VM institucional está concebida como infraestructura persistente. Cada nueva versión aprobada se aplicará sobre ella de forma controlada.

Antes de actualizar:

1. backup verificado y copia externa;
2. revisión de migraciones;
3. identificación exacta de tag/SHA aprobado;
4. ventana de mantenimiento;
5. actualización;
6. health checks y smoke autenticado;
7. validación funcional mínima posterior.

Las actualizaciones estables utilizarán el procedimiento y script de actualización definitiva versionado; nunca un `git pull` automático desde `dev`.

## 12. Seguridad y privacidad

- `DemoData:Enabled` debe permanecer deshabilitado en Production.
- MinIO/S3 debe ser privado; los documentos se entregan mediante endpoints autenticados y autorizados.
- RUT/RUN, domicilio, correo, teléfono, entrevistas, documentos y observaciones internas de insinuados no deben aparecer en vistas transversales públicas.
- Los archivos de secretos deben mantenerse fuera de Git y con permisos restrictivos.
- El paquete no autoriza por sí solo tratamiento de datos personales reales.
- Antes de producción deben completarse los controles técnicos, jurídicos y organizacionales aplicables a la Ley 21.719 y demás normativa institucional.

## 13. Diagnóstico rápido

Si el instalador falla, ejecuta primero:

```bash
PMGM_PILOT_ENV_FILE=/etc/pmgm/pmgm.env bash scripts/preflight-definitive-host.sh
PMGM_PILOT_ENV_FILE=/etc/pmgm/pmgm.env bash scripts/preflight-pilot.sh
```

Para revisar servicios:

```bash
docker compose --env-file /etc/pmgm/pmgm.env -f infrastructure/docker-compose.pilot.yml ps
docker compose --env-file /etc/pmgm/pmgm.env -f infrastructure/docker-compose.pilot.yml logs --tail=300
```

Para una instalación real, registra el SHA de `BUILD-INFO.txt`, resultado de preflight, resultado de smoke, ruta/hash del backup inicial y resultado de UAT. Esa evidencia será parte de la trazabilidad de implementación institucional.
