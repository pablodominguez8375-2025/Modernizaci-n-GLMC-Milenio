# Proyecto Centenario — despliegue automatizado QA en srv01

## Objetivo

Cerrar el hito operacional de QA usando exactamente el mismo SHA publicado en GitHub Pages y empaquetado como instalable.

La demo pública publica además:

- `/downloads/qa-current.json`;
- `/downloads/Proyecto-Centenario-QA-srv01-<SHA12>.zip`.

El manifiesto público contiene sólo SHA, nombre de archivo y SHA-256. No contiene secretos.

## Requisitos de srv01

- Ubuntu/Linux con Docker Engine y Docker Compose v2;
- usuario con acceso al daemon Docker;
- escritura en `/opt/centenario`;
- salida HTTPS a GitHub Pages;
- repositorio clonado en `/opt/centenario/app`;
- archivo secreto QA en `/etc/pmgm/srv01.env`, fuera de Git.

## Primera preparación

Desde `/opt/centenario/app`:

```bash
git fetch origin dev
git checkout dev
git reset --hard origin/dev
bash scripts/deploy-srv01-from-pages.sh --sha "$(git rev-parse HEAD)" --prepare-env
```

Editar luego:

```text
/etc/pmgm/srv01.env
```

y reemplazar todos los valores `REEMPLAZAR_*`.

## Despliegue

```bash
cd /opt/centenario/app
git fetch origin dev
git checkout dev
git reset --hard origin/dev
bash scripts/deploy-srv01-from-pages.sh --sha "$(git rev-parse HEAD)"
```

El script:

1. obtiene `qa-current.json` desde Pages;
2. exige que el SHA publicado coincida con el SHA solicitado;
3. descarga el ZIP;
4. verifica SHA-256 externo;
5. verifica `BUILD-INFO.txt`;
6. verifica `MANIFEST.sha256`;
7. conserva releases por SHA bajo `/opt/centenario/releases`;
8. si ya existe una instalación operativa, genera respaldo previo;
9. instala el nuevo corte;
10. ejecuta smoke autenticado;
11. actualiza `/opt/centenario/current`;
12. prepara la evidencia de regresión de 21 controles.

## Persistencia QA

El proyecto Compose es fijo: `pmgm-srv01`.

Esto evita crear volúmenes distintos al cambiar de directorio/release.

PostgreSQL contiene:

- `pmgm`: datos de aplicación;
- `pmgm_keycloak`: identidad QA de Keycloak.

MinIO mantiene el bucket privado `pmgm-documents`.

## Backup

Manual:

```bash
PMGM_QA_ENV_FILE=/etc/pmgm/srv01.env \
  bash /opt/centenario/current/scripts/backup-srv01-qa.sh
```

El respaldo incluye:

- PostgreSQL `pmgm`;
- PostgreSQL `pmgm_keycloak`;
- objetos de MinIO;
- manifest y hashes SHA-256.

## Restore

```bash
PMGM_QA_ENV_FILE=/etc/pmgm/srv01.env \
  bash /opt/centenario/current/scripts/restore-srv01-qa.sh \
  --backup /opt/centenario/backups/<RESPALDO> \
  --yes
```

## Rollback de release

Si un despliegue nuevo falla después de haber generado un respaldo:

```bash
bash /opt/centenario/app/scripts/rollback-srv01-qa.sh \
  --release-sha <SHA_ANTERIOR> \
  --backup /opt/centenario/backups/<RESPALDO> \
  --yes
```

El rollback reinstala el código anterior, restaura PostgreSQL/MinIO y vuelve a ejecutar smoke.

## Regresión

Después de un deploy exitoso queda preparada:

```text
/opt/centenario/evidence/PMGM-QA-srv01-<SHA12>.json
```

Registrar QA-001..QA-021 y cerrar con:

```bash
python3 /opt/centenario/current/tests/qa_srv01_regression_gate.py \
  /opt/centenario/evidence/PMGM-QA-srv01-<SHA12>.json
```

Se exige 21/21 `pass`.

## Seguridad

- no hay secretos en Pages;
- `srv01.env` nunca entra al repositorio;
- el ZIP público contiene sólo código ya público, configuración de ejemplo y datos ficticios;
- no usar datos personales reales;
- QA por HTTP se limita al entorno interno controlado;
- producción seguirá usando el perfil institucional con TLS.

## Estado de cierre

Un deploy sólo puede registrarse como operacional cuando:

- SHA Pages = SHA instalado;
- ZIP SHA-256 válido;
- manifest interno válido;
- smoke autenticado = OK;
- Consejo de Administración accesible con perfil autorizado;
- regresión QA = 21/21;
- evidencia registrada.
