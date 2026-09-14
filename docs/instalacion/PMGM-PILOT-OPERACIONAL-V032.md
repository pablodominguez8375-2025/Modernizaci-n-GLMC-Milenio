# Proyecto Milenio — Piloto operacional v0.32

## 1. Alcance
Este perfil permite instalar Proyecto Milenio como **piloto institucional controlado**. No es una declaración de preparación productiva.

Incluye Caddy HTTPS, frontend, API .NET 10, PostgreSQL 17, Keycloak 26.7.3 con PostgreSQL, MinIO y ClamAV. Sólo Caddy expone puertos al host.

## 2. Requisitos del servidor
- Docker Engine + Docker Compose v2.
- DNS del hostname piloto apuntando al servidor.
- Puertos TCP 80 y 443 disponibles hacia Caddy.
- Acceso administrativo al servidor y almacenamiento suficiente para BD, documentos y respaldos.
- Hora del sistema sincronizada.

## 3. Preparar configuración
Copiar:

```bash
cp infrastructure/pilot.env.example infrastructure/.env.pilot.local
```

Editar el archivo local y reemplazar todos los valores de ejemplo. No subirlo a Git.

Reglas principales:
- `PMGM_PILOT_PUBLIC_URL` debe ser HTTPS y no terminar en `/`.
- las cuatro contraseñas deben tener al menos 20 caracteres y ser diferentes;
- usar un correo operativo para el primer administrador;
- no reutilizar identidades `qa.*`;
- mantener `PMGM_PILOT_ALLOW_LOCALHOST=false` y `PMGM_PILOT_INSECURE_TLS=false` fuera de CI/local.

El primer usuario institucional recibe una contraseña temporal y Keycloak le exigirá cambiarla.

## 4. Preflight
Windows:

```powershell
.\scripts\preflight-pilot.ps1
```

Linux/macOS:

```bash
bash scripts/preflight-pilot.sh
```

El preflight rechaza HTTP público, hostname inconsistente, placeholders, secretos débiles/reutilizados, usuarios QA, password grant habilitado o Docker Compose inválido.

## 5. Arranque
Windows:

```powershell
.\scripts\start-pilot.ps1
```

Linux/macOS:

```bash
bash scripts/start-pilot.sh
```

El launcher ejecuta preflight, construye el stack y luego corre el smoke HTTPS/OIDC.

## 6. Rutas públicas
- Aplicación: `https://<hostname>/`
- API: `https://<hostname>/api/...`
- Salud: `https://<hostname>/health/...`
- Identidad: `https://<hostname>/identity/...`
- Callback SPA: `https://<hostname>/auth/callback`

`/identity` se mantiene separado de `/auth` para evitar colisión entre Keycloak y el callback del frontend.

## 7. Seguridad del perfil
- Sólo gateway/Caddy publica puertos.
- PostgreSQL, MinIO, ClamAV, API y Keycloak permanecen en red Docker interna.
- Keycloak usa `start`, no `start-dev`.
- Keycloak persiste en `pmgm_keycloak`; la aplicación usa `pmgm`.
- Authorization Code + PKCE S256.
- Direct Access Grants deshabilitado.
- `DemoData__Enabled=false`.
- secretos fuera del repositorio.
- HSTS y cabeceras de seguridad en Caddy para el perfil real.

## 8. Smoke
Windows:

```powershell
.\scripts\smoke-pilot.ps1
```

Linux/macOS:

```bash
bash scripts/smoke-pilot.sh
```

Valida HTTPS, discovery e issuer OIDC, salud del backend/PostgreSQL, frontend, separación `/auth`/`/identity`, API v0.32.0, 401 sin token y rechazo del password grant.

## 9. Respaldo
El respaldo del piloto contiene:
- `database/pmgm.dump`;
- `database/pmgm_keycloak.dump`;
- objetos del bucket `pmgm-documents`;
- `manifest.json` con SHA-256 y tamaños.

Windows:

```powershell
.\scripts\backup-pilot.ps1
```

Linux/macOS:

```bash
bash scripts/backup-pilot.sh
```

Durante la captura se detienen temporalmente gateway/web/API/Keycloak para evitar escrituras y luego se reanudan si estaban activos.

Verificar siempre antes de restaurar:

```powershell
.\scripts\verify-pilot-backup.ps1 -BackupPath <ruta>
```

```bash
bash scripts/verify-pilot-backup.sh <ruta>
```

## 10. Restauración
Es destructiva y requiere confirmación explícita.

Windows:

```powershell
.\scripts\restore-pilot.ps1 -BackupPath <ruta> -Yes
```

Linux/macOS:

```bash
bash scripts/restore-pilot.sh --backup <ruta> --yes
```

Si falla PostgreSQL o MinIO, los servicios públicos no se reanudan automáticamente. Después de una restauración exitosa ejecutar el smoke del piloto.

## 11. Ley 21.719 y respaldos
Un respaldo hereda la sensibilidad de los datos originales. Por lo tanto:
- `backups/` está excluido de Git;
- no compartir respaldos por canales no autorizados;
- aplicar cifrado en el medio o repositorio donde se almacenen;
- limitar acceso al personal autorizado;
- registrar creación, traslado y eliminación cuando el piloto use datos reales;
- definir formalmente retención/RPO/RTO antes de producción.

El repositorio no fija un plazo legal de retención inventado: debe aprobarse institucionalmente según finalidad y obligaciones aplicables.

## 12. Recovery drill
`scripts/recovery-drill-pilot.sh` es una prueba destructiva **exclusiva de localhost/CI**. Se niega a operar contra un hostname real. Crea sondas temporales en `pmgm`, `pmgm_keycloak` y MinIO, respalda, altera el estado, restaura, verifica las tres capas y ejecuta el smoke.

## 13. Antes de incorporar usuarios reales
- DNS y certificado HTTPS válidos.
- secretos exclusivos del piloto.
- respaldo inicial verificado y copia protegida fuera del host.
- responsables operativos designados.
- perfiles/roles aprobados.
- carga inicial de datos mediante proceso controlado; nunca activando `DemoData`.
- consentimiento/base jurídica, información a titulares y controles de Ley 21.719 según los tratamientos aplicables.

## 14. Pendiente para producción
Alta disponibilidad, KMS/HSM o secret manager, WAF/SIEM administrado, monitoreo y alertas, copia off-site/inmutable, RPO/RTO aprobados, respaldo del IdP productivo externo si se reemplaza Keycloak local, hardening de host y proceso formal de continuidad/DR.
