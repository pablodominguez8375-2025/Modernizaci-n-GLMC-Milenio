# Proyecto Milenio — Showcase de testing en GitHub Pages

## Objetivo

Publicar una demostración navegable de Proyecto Milenio para revisión funcional y visual sin depender del backend, la VM definitiva, Keycloak, PostgreSQL ni datos institucionales reales.

## Naturaleza del entorno

- Entorno: demostración/testing.
- Fuente: rama `dev`.
- Frontend: React + Vite.
- Datos: exclusivamente mocks/datos ficticios incluidos en el frontend.
- Autenticación real: deshabilitada.
- Backend real: no utilizado.
- Datos personales reales: prohibidos.
- Uso: revisión de interfaz, navegación, módulos y conversaciones funcionales con usuarios de prueba.

La aplicación muestra identificadores de QA/demostración y no debe confundirse con la instalación institucional definitiva.

## URL esperada

Con GitHub Pages habilitado para despliegue mediante GitHub Actions, la URL esperada del repositorio es:

`https://pablodominguez8375-2025.github.io/Modernizaci-n-GLMC-Milenio/`

La URL efectiva queda registrada por el job `Deploy testing showcase` dentro del environment `github-pages`.

## Workflow

Archivo:

`.github/workflows/showcase-pages.yml`

Nombre en Actions:

`PMGM Showcase Demo`

El workflow se ejecuta:
- en pull requests hacia `dev` que afectan frontend/showcase, para validar build;
- en push a `dev`, para construir y publicar la demo;
- manualmente mediante `workflow_dispatch`.

## Gates antes de publicar

1. `npm ci`.
2. tests de frontend.
3. lint.
4. build con `VITE_USE_MOCKS=true`.
5. base path `/Modernizaci-n-GLMC-Milenio/`.
6. validación de `index.html`.
7. búsqueda preventiva de patrones de secretos en el `dist` generado.
8. artifact de GitHub Pages.
9. deploy a environment `github-pages`.

## Seguridad

El showcase es público si el repositorio/GitHub Pages es público. Por ello:
- sólo usa datos ficticios;
- no utiliza secretos;
- no utiliza tokens OIDC;
- no apunta a la API institucional;
- no contiene credenciales;
- no debe conectarse a bases de datos ni Object Storage reales.

La eventual VM institucional y este showcase son entornos distintos.

## Actualización

Cada cambio de frontend integrado a `dev` vuelve a ejecutar el workflow y, si los gates pasan, reemplaza la demo publicada por la nueva versión de testing.

## Alcance

El showcase sirve para validar experiencia, diseño y flujo. No reemplaza UAT del backend ni los casos institucionales UAT-001..UAT-020, porque esas pruebas requieren el entorno operacional HTTPS con backend, identidad, persistencia, permisos, backup y recuperación reales.
