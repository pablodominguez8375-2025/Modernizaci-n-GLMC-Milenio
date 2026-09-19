# Proyecto Centenario — instalación QA en srv01

Este procedimiento corresponde al entorno **QA operacional** del Proyecto Centenario. No es producción y utiliza identidades/datos de prueba.

## Baseline srv01

El servidor QA acordado para el proyecto es `srv01`. El perfil está diseñado para un host Linux con Docker Engine y Docker Compose v2, con un objetivo de 8 vCPU y aproximadamente 8 GB de RAM.

El stack QA expone únicamente:

- frontend/API vía el puerto configurable de aplicación, por defecto `8081`;
- Keycloak QA vía el puerto configurable OIDC, por defecto `8180`.

PostgreSQL, MinIO y ClamAV permanecen en la red Docker interna.

## Contenido del paquete

El workflow `Proyecto Centenario QA srv01 Installable` genera:

`Proyecto-Centenario-QA-srv01-<SHA>.zip`

El ZIP contiene el código exacto del SHA, infraestructura, migraciones, scripts, documentación, `BUILD-INFO.txt`, `MANIFEST.sha256`, un Git bundle y el launcher `INSTALAR.sh`.

## Instalación

1. Extraer el ZIP en una ruta de QA, por ejemplo `/opt/centenario/package`.
2. Entrar al directorio extraído.
3. Crear la configuración segura:

```bash
./INSTALAR.sh --prepare-env
```

4. Editar `/etc/pmgm/srv01.env` y reemplazar todos los valores `REEMPLAZAR_*`.
5. Usar como URL pública la IP/DNS que realmente abrirá el navegador QA. Ejemplo:

```text
PMGM_QA_WEB_PUBLIC_URL=http://10.0.0.25:8081
PMGM_QA_OIDC_PUBLIC_URL=http://10.0.0.25:8180
```

6. Ejecutar:

```bash
./INSTALAR.sh
```

El launcher valida integridad del paquete, preflight del host, configuración Compose, descarga imágenes, construye API/frontend, levanta el stack y ejecuta smoke autenticado.

## Datos y usuarios QA

`DemoData__Enabled=true` se mantiene únicamente para este entorno QA y carga datos ficticios/controlados.

El realm QA incluye identidades de prueba para los ocho cargos del Consejo de Administración del Taller 23, además de los perfiles técnicos ya existentes. Todas las credenciales se parametrizan mediante `PMGM_QA_USER_PASSWORD`; el repositorio nunca contiene la contraseña real.

## Seguridad

Este perfil permite HTTP para QA interno y no reemplaza el perfil piloto/producción con HTTPS. No debe exponerse a Internet ni utilizarse con datos personales reales.

Antes de producción se debe usar el perfil institucional con TLS, secretos definitivos, `DemoData__Enabled=false`, backup externo y los gates formales de promoción.

## Actualización

Cada nuevo artefacto de `dev` conserva volúmenes persistentes si se instala sobre el mismo proyecto Compose. Antes de actualizaciones con datos de prueba relevantes, conservar respaldo del entorno QA.

## Evidencia obligatoria

Registrar en cada instalación:

- SHA de `BUILD-INFO.txt`;
- hash/validación de `MANIFEST.sha256`;
- resultado del preflight;
- resultado del smoke;
- URL QA;
- fecha de instalación;
- incidencias QA/UAT.

Una demo de GitHub Pages y un instalable QA deben corresponder al mismo SHA del corte cuando se declare completa la triple salida.


## Regresión QA posterior a la instalación

Una instalación técnicamente correcta debe continuar con el kit de regresión ligado al SHA del paquete:

```bash
bash scripts/prepare-srv01-regression.sh
```

Luego registrar cada control con `scripts/record-srv01-regression-result.py` y validar la ejecución con:

```bash
python3 tests/qa_srv01_regression_gate.py evidence/PMGM-QA-srv01-<sha>.json --allow-pending
```

El cierre interno QA exige 25/25 controles `pass`, incluyendo `QA-021` para Consejo de Administración, `QA-022` para el flujo reglamentario integral de insinuaciones, `QA-023` para el cierre documental de Tenidas regulares y ceremoniales, `QA-024` para el Cuadro Mensual de Tesorería con segregación Taller/Gran Tesorería y `QA-025` para Hospitalaria integral con privacidad Taller/Gran Hospitalaria. Este cierre de regresión no sustituye la UAT institucional formal ni la aprobación del Sponsor/Product Owner. Consulte `docs/qa/PMGM-SRV01-REGRESSION-KIT.md`.


## Despliegue recomendado desde Pages

Para `srv01`, el método preferido deja de ser la copia manual del artifact. Usar:

```bash
cd /opt/centenario/app
git fetch origin dev
git checkout dev
git reset --hard origin/dev
bash scripts/deploy-srv01-from-pages.sh --sha "$(git rev-parse HEAD)"
```

El script descarga el instalable publicado en GitHub Pages, valida el SHA externo e interno, genera respaldo previo si corresponde, instala, ejecuta smoke y prepara la regresión QA.

Guía completa: `docs/installation/QA-SRV01-AUTODEPLOY.md`.
