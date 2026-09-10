# Proyecto Milenio — VM institucional definitiva y actualización continua

## 1. Objetivo

Montar Proyecto Milenio en una **VM institucional persistente** que sirva primero como entorno operacional de UAT para `v1.0.0-rc1` y, después de la aprobación del Sponsor/Product Owner, se promueva **en la misma VM** a `v1.0.0` estable y a versiones posteriores.

La VM no se recrea en cada versión. Se mantienen persistentes PostgreSQL, Keycloak, MinIO/documentos, certificados y respaldos. Las nuevas versiones se despliegan mediante un procedimiento controlado: backup → verificación → actualización → migraciones → smoke → aceptación/rollback.

## 2. Candidato inicial obligatorio

- Versión: `1.0.0-rc1`
- SHA de aplicación congelado: `739ba0b3a8d89087177b1edabd61c2981eed08a2`
- Rama de referencia: `uat/v1.0.0-rc1-739ba0b3`
- Perfil de despliegue inicial: `infrastructure/docker-compose.pilot.yml`
- `DemoData__Enabled=false`

La UAT debe ejecutarse sobre este SHA. La documentación posterior en `dev` no reemplaza el candidato evaluado.

## 3. Sistema operativo recomendado

### Recomendado
- Linux x86_64.
- Ubuntu Server 24.04 LTS o una distribución Linux empresarial equivalente soportada por Docker Engine.
- Instalación mínima, sin entorno gráfico.

### Motivo
El stack está compuesto por contenedores Linux: Caddy, frontend, API .NET, PostgreSQL, Keycloak, MinIO y ClamAV. Ejecutarlo sobre Linux reduce complejidad, consumo y dependencia del hipervisor.

## 4. Tamaño de VM

### Mínimo operacional aceptable para UAT
- 4 vCPU.
- 16 GB RAM.
- 150 GB SSD.
- 1 NIC.

### Recomendado para dejarla como VM institucional definitiva
- **8 vCPU.**
- **32 GB RAM.**
- **500 GB SSD/NVMe expandible.**
- 1 Gbps de interfaz virtual cuando la infraestructura lo permita.

### Distribución de almacenamiento recomendada
- Disco de sistema: 80–100 GB.
- Disco de datos Docker/aplicación: 400 GB o más, expandible.
- Repositorio de backup: **fuera de la VM**. No contar el disco local como única copia de respaldo.

El crecimiento estará dominado por los documentos de MinIO y los respaldos. Si el volumen documental aumenta, ampliar primero el disco de datos o migrar Object Storage a almacenamiento dedicado compatible S3.

## 5. Hipervisor

No existe dependencia de un fabricante. Puede ejecutarse sobre:
- VMware;
- Proxmox VE;
- Microsoft Hyper-V;
- Nutanix;
- nube privada/pública que entregue una VM Linux equivalente.

Recomendación: habilitar snapshots del hipervisor **como complemento**, no como sustituto del backup consistente de PostgreSQL + Keycloak + MinIO.

## 6. Red, DNS y firewall

Se requiere un FQDN institucional, por ejemplo:

`milenio.dominio-institucional.cl`

Requisitos:
- registro DNS A/AAAA apuntando al gateway público correspondiente;
- TCP 80 y 443 hacia Caddy;
- TCP 22/SSH sólo desde redes o IP administrativas autorizadas;
- salida HTTPS/443 para repositorios, imágenes Docker, certificados y actualizaciones;
- hora del servidor sincronizada mediante NTP.

**No publicar** hacia Internet:
- PostgreSQL 5432;
- MinIO 9000/9001;
- ClamAV 3310;
- Keycloak interno;
- API interna.

El Compose actual sólo publica Caddy y mantiene el resto en red Docker privada.

## 7. Software que debe instalarse en la VM

Obligatorio:
- Docker Engine;
- Docker Compose v2 plugin;
- Git;
- curl;
- OpenSSL;
- herramientas estándar `tar`, `gzip`, `sha256sum`.

Recomendado:
- `jq`;
- `unzip`;
- `rsync`;
- agente de monitoreo institucional si existe.

No es necesario instalar .NET, Node.js, PostgreSQL, Keycloak, MinIO ni ClamAV directamente en el host: se ejecutan como contenedores.

## 8. Cuenta de servicio y estructura del host

Crear un usuario operacional sin login root directo, por ejemplo `pmgm`.

Estructura sugerida:

```text
/opt/pmgm/app             código/version desplegada
/opt/pmgm/packages        paquetes recibidos
/opt/pmgm/backups-local   staging temporal de backups
/etc/pmgm                 configuración y secretos (root/pmgm, 750)
/var/log/pmgm             registros auxiliares operacionales
```

Los secretos no deben quedar en Git. El archivo operativo puede mantenerse como:

`/etc/pmgm/pmgm.env`

con permisos `600` y propietario restringido. Para utilizarlo con los scripts actuales:

```bash
export PMGM_PILOT_ENV_FILE=/etc/pmgm/pmgm.env
```

## 9. Secretos iniciales

Se requieren valores exclusivos y aleatorios para:
- contraseña PostgreSQL aplicación/Keycloak;
- usuario y contraseña MinIO;
- administrador técnico de Keycloak;
- primer administrador institucional temporal.

Cada contraseña debe cumplir el preflight (mínimo 20 caracteres y sin reutilización entre servicios).

No enviar secretos por issues de GitHub, correo abierto o documentos del repositorio.

## 10. FQDN y TLS

Configurar en el archivo de entorno:

```text
PMGM_PILOT_HOSTNAME=milenio.dominio-institucional.cl
PMGM_PILOT_PUBLIC_URL=https://milenio.dominio-institucional.cl
PMGM_PILOT_BIND_ADDRESS=0.0.0.0
PMGM_PILOT_HTTP_PORT=80
PMGM_PILOT_HTTPS_PORT=443
PMGM_PILOT_ALLOW_LOCALHOST=false
PMGM_PILOT_INSECURE_TLS=false
```

Caddy obtiene/renueva TLS cuando el FQDN y la conectividad pública lo permiten. Si la organización usa una PKI o proxy TLS institucional, adaptar Caddy manteniendo HTTPS extremo a extremo y validar nuevamente el smoke.

## 11. Primera instalación

### 11.1 Obtener el candidato congelado

```bash
sudo mkdir -p /opt/pmgm
sudo chown pmgm:pmgm /opt/pmgm
cd /opt/pmgm
git clone <URL-AUTORIZADA-DEL-REPOSITORIO> app
cd app
git fetch --all --prune
git checkout 739ba0b3a8d89087177b1edabd61c2981eed08a2
test "$(git rev-parse HEAD)" = "739ba0b3a8d89087177b1edabd61c2981eed08a2"
```

Si el repositorio se hace privado, utilizar una deploy key o credencial de sólo lectura administrada; nunca incrustarla en scripts.

### 11.2 Crear configuración

```bash
sudo install -d -m 750 -o root -g pmgm /etc/pmgm
sudo cp infrastructure/pilot.env.example /etc/pmgm/pmgm.env
sudo chown root:pmgm /etc/pmgm/pmgm.env
sudo chmod 600 /etc/pmgm/pmgm.env
sudoedit /etc/pmgm/pmgm.env
```

### 11.3 Preflight

```bash
cd /opt/pmgm/app
export PMGM_PILOT_ENV_FILE=/etc/pmgm/pmgm.env
bash scripts/preflight-pilot.sh
```

No continuar si el preflight falla.

### 11.4 Inicio

```bash
bash scripts/start-pilot.sh
```

El script construye el stack, lo levanta y ejecuta el smoke HTTPS/OIDC.

### 11.5 Validación

Comprobar:
- aplicación HTTPS disponible;
- `/api/system/info` devuelve `1.0.0-rc1`;
- OIDC/PKCE funciona;
- 401 sin autenticación donde corresponde;
- base de datos saludable;
- bucket privado disponible;
- ClamAV operativo;
- password grant rechazado.

## 12. Backup inicial obligatorio

Antes de comenzar UAT:

```bash
export PMGM_PILOT_ENV_FILE=/etc/pmgm/pmgm.env
bash scripts/backup-pilot.sh
```

Luego:

```bash
bash scripts/verify-pilot-backup.sh <RUTA_DEL_BACKUP>
```

El backup cubre:
- PostgreSQL `pmgm`;
- PostgreSQL `pmgm_keycloak`;
- objetos `pmgm-documents`;
- manifest con tamaños y SHA-256.

Copiar el respaldo verificado a almacenamiento fuera de la VM y protegido.

## 13. UAT en la VM definitiva

La misma VM se utiliza para los 20 casos del protocolo UAT. No se carga información personal real salvo autorización expresa y cierre del gate legal/privacidad.

Resultado requerido antes de promover estable:
- 20/20 PASS;
- evidencia por caso;
- backup/restore probado;
- aprobación Sponsor/Product Owner.

## 14. Promoción en la misma VM a v1.0.0 estable

No reinstalar la VM. Después de la aprobación:
1. generar backup completo y verificarlo;
2. conservar copia fuera de host;
3. recibir el paquete `v1.0.0` aprobado;
4. validar manifest y checksum;
5. actualizar código a la referencia aprobada;
6. ejecutar preflight;
7. reconstruir/actualizar contenedores;
8. aplicar migraciones autorizadas;
9. ejecutar smoke;
10. dejar evidencia del cambio.

Los volúmenes Docker persistentes conservan la información. No ejecutar `docker compose down -v` en operación institucional.

## 15. Política de actualizaciones futuras

Cada versión debe seguir:

```text
Desarrollo → CI → RC → QA/UAT proporcional → backup verificado → despliegue → smoke → cierre
```

Para una actualización normal:
- identificar versión/SHA exactos;
- revisar changelog y migraciones;
- backup previo obligatorio;
- desplegar sólo una versión aprobada;
- `docker compose ... up -d --build`;
- smoke posterior;
- monitorear logs;
- registrar versión instalada.

Una actualización de base de datos puede hacer inseguro volver sólo al código anterior. Si el despliegue modifica esquema y falla, el rollback correcto puede requerir **restaurar el backup pre-update**, no sólo cambiar el commit.

## 16. Estrategia de ramas y entorno definitivo

- `main`: sólo versiones institucionalmente aprobadas/estables.
- `dev`: desarrollo integrado.
- `uat/...`: candidato congelado para una aceptación determinada.
- ramas feature/fix: cambios en curso.

La VM definitiva **no debe seguir automáticamente `dev`**. Sólo recibe versiones o SHA expresamente promovidos.

## 17. Seguridad mínima antes de datos reales

Obligatorio:
- repositorio privado o confirmación institucional de la política de código;
- no almacenar secretos en Git;
- SSH restringido;
- firewall activo;
- actualizaciones de seguridad del SO;
- MFA para administradores donde aplique;
- contraseñas temporales rotadas;
- backup off-host protegido;
- TLS válido;
- roles institucionales revisados;
- revisión Ley 21.719 previa a datos reales;
- registro de responsables operacionales.

## 18. Continuidad y operación

Antes de declarar producción formal deben definirse institucionalmente:
- RPO;
- RTO;
- periodicidad de backups;
- retención de backups;
- responsable de restauraciones;
- monitoreo/alertamiento;
- parcheo del host;
- ventana de mantenimiento;
- procedimiento de incidentes.

El proyecto no inventa plazos de retención: deben ser aprobados conforme a finalidad, obligaciones y política institucional.

## 19. Checklist de go-live técnico

- [ ] VM 8 vCPU / 32 GB / 500 GB o capacidad aprobada.
- [ ] Linux actualizado.
- [ ] Docker Engine + Compose v2.
- [ ] FQDN resuelve correctamente.
- [ ] 80/443 disponibles.
- [ ] SSH restringido.
- [ ] secretos exclusivos en `/etc/pmgm/pmgm.env`.
- [ ] candidato SHA verificado.
- [ ] preflight PASS.
- [ ] arranque PASS.
- [ ] HTTPS/OIDC smoke PASS.
- [ ] backup inicial PASS.
- [ ] copia de backup fuera de VM.
- [ ] UAT 20/20 PASS antes de estable.
- [ ] aprobación Sponsor/Product Owner.
- [ ] privacidad/legal cerrado antes de datos personales reales.

## 20. Regla de oro

**La VM es definitiva; la versión no.**

Se preservan datos y servicios en la VM, mientras cada nueva versión de Proyecto Milenio se incorpora de manera controlada, trazable y reversible mediante backup.