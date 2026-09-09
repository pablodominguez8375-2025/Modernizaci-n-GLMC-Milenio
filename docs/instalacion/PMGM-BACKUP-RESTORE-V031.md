# PMGM v0.31 — Respaldo y recuperación del piloto

## Objetivo

Este runbook agrega una primera disciplina operativa de respaldo y recuperación para la instalación QA/piloto local de Proyecto Milenio. El objetivo es que PostgreSQL y los documentos almacenados en MinIO puedan recuperarse de forma verificable, sin depender de copias manuales de volúmenes Docker.

> Alcance: QA/piloto local. No reemplaza una estrategia productiva de continuidad, alta disponibilidad, cifrado gestionado, almacenamiento off-site ni pruebas formales de desastre.

## Qué se respalda

1. **PostgreSQL `pmgm`** mediante `pg_dump` en formato custom, sin propietarios ni privilegios locales.
2. **Bucket MinIO `pmgm-documents`** mediante la API S3 usando `mc mirror`.
3. **`manifest.json`** con:
   - versión de formato;
   - fecha UTC;
   - commit Git fuente cuando está disponible;
   - componentes respaldados;
   - listado exacto de archivos;
   - tamaño por archivo;
   - SHA-256 por archivo;
   - cantidad y tamaño total.

El backup se realiza en una ventana quiescida: los servicios `api` y `web` que estén activos se detienen durante la captura y se reanudan al terminar. PostgreSQL y MinIO permanecen disponibles para generar el respaldo.

## Qué no se respalda en v0.31

- El volumen interno de Keycloak. En esta primera implementación QA, realm, roles y usuarios ficticios se recrean desde `infrastructure/keycloak/pmgm-realm.json` y las contraseñas locales provienen de variables de entorno.
- La base de firmas de ClamAV, porque puede descargarse nuevamente.
- Imágenes Docker, código fuente o secretos locales.
- Configuración productiva de TLS, HSM/KMS, alta disponibilidad o proveedor de identidad externo.

Si el piloto comienza a administrar identidades reales o cambios persistentes directamente en Keycloak, su base de datos deberá incorporarse al alcance antes de considerar la solución apta para producción.

## Protección de datos — Ley 21.719

Un respaldo puede contener datos personales, documentos institucionales y antecedentes sujetos a control de acceso. Por ello:

- `backups/` está excluido de Git mediante `.gitignore`;
- no se deben adjuntar respaldos a issues, PR, correo ni servicios personales de transferencia;
- el respaldo debe almacenarse en un medio cifrado y con acceso limitado a personal autorizado;
- las copias deben tener una política institucional de retención y eliminación segura;
- el `manifest.json` no contiene contraseñas ni secretos;
- la restauración debe quedar incorporada al registro operativo/auditoría del ambiente que corresponda;
- para producción, el respaldo debe tener cifrado gestionado, copia externa/off-site y controles formales de acceso, retención y destrucción.

v0.31 **no fija por código una cantidad de días de retención**: esa decisión debe responder a la política institucional y a la finalidad de tratamiento.

## Requisitos

- Docker Desktop / Docker Engine con `docker compose`.
- Primera implementación v0.30 o superior inicializada.
- Archivo local `infrastructure/.env.first.local` generado por el launcher, o las variables `PMGM_FIRST_*` y `PMGM_QA_USER_PASSWORD` definidas en el proceso.
- PowerShell 7 recomendado en Windows.
- Python 3 para la versión Bash del verificador/manifiesto.

## Crear respaldo — Windows

```powershell
.\scripts\backup-first-implementation.ps1
```

Ruta específica:

```powershell
.\scripts\backup-first-implementation.ps1 -OutputPath "D:\PMGM-Backups\pmgm-2026-09-09"
```

## Crear respaldo — Linux/macOS

```bash
bash scripts/backup-first-implementation.sh
```

Ruta específica:

```bash
bash scripts/backup-first-implementation.sh --output /srv/pmgm-backups/pmgm-2026-09-09
```

## Verificar integridad antes de mover o restaurar

Windows:

```powershell
.\scripts\verify-first-backup.ps1 -BackupPath "D:\PMGM-Backups\pmgm-2026-09-09"
```

Linux/macOS:

```bash
bash scripts/verify-first-backup.sh /srv/pmgm-backups/pmgm-2026-09-09
```

La verificación falla ante:

- archivo faltante;
- archivo adicional no contemplado en el manifiesto;
- SHA-256 distinto;
- tamaño distinto;
- ruta insegura o duplicada;
- versión de formato no soportada;
- falta del dump PostgreSQL.

## Restaurar — operación destructiva

La restauración reemplaza los datos actuales de PostgreSQL y sincroniza exactamente `pmgm-documents` con el respaldo. Requiere confirmación explícita.

Windows:

```powershell
.\scripts\restore-first-implementation.ps1 `
  -BackupPath "D:\PMGM-Backups\pmgm-2026-09-09" `
  -Yes
```

Linux/macOS:

```bash
bash scripts/restore-first-implementation.sh \
  --backup /srv/pmgm-backups/pmgm-2026-09-09 \
  --yes
```

Secuencia de seguridad:

1. verifica íntegramente el respaldo;
2. detecta si `api` y `web` estaban activos;
3. detiene el acceso a la aplicación;
4. asegura disponibilidad de PostgreSQL y MinIO;
5. ejecuta `pg_restore --clean --if-exists --exit-on-error`;
6. restaura MinIO con `mc mirror --overwrite --remove`;
7. compara el número de objetos restaurados con el respaldo;
8. sólo si todo fue correcto reanuda `api` y `web` que estaban activos;
9. si ocurre una falla, `api/web` permanecen detenidos para no exponer un estado parcial.

## Validación funcional posterior

Siempre ejecutar el smoke autenticado después de restaurar.

Windows:

```powershell
.\scripts\smoke-first-implementation.ps1
```

Linux/macOS:

```bash
bash scripts/smoke-first-implementation.sh
```

## Recovery drill

El recovery drill está pensado sólo para QA/CI. Crea un respaldo, altera deliberadamente datos **ficticios**, vacía el bucket de documentos, restaura y vuelve a ejecutar todo el smoke autenticado.

```bash
bash scripts/recovery-drill-first-implementation.sh /tmp/pmgm-recovery-drill
```

Nunca ejecutar este drill sobre producción ni sobre un entorno con información institucional real.

## Criterio de aceptación v0.31

El bloque se considera recuperable cuando CI demuestra, sobre el mismo head:

1. creación de respaldo;
2. verificación SHA-256;
3. corrupción controlada del miembro ficticio `GLM-QA-0230`;
4. eliminación controlada de objetos MinIO ficticios;
5. restauración completa;
6. smoke autenticado nuevamente verde;
7. invariancia de permisos, traslado y módulos ya probados en v0.30.

## Pendientes para producción

Antes de producción se requiere, como mínimo:

- RPO/RTO institucionalmente aprobados;
- cifrado de backups con claves gestionadas;
- almacenamiento externo/off-site e idealmente inmutable;
- rotación y retención formal;
- monitoreo de ejecución y alertas;
- respaldo del proveedor de identidad productivo;
- ejercicios periódicos de restauración;
- plan documentado de continuidad y desastre;
- segregación de funciones entre operador, custodio y auditor.
