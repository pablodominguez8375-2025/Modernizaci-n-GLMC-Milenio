# Proyecto Milenio — Showcase de testing en GitHub Pages

## Objetivo

Publicar una demostración navegable de Proyecto Milenio para revisión funcional y visual sin depender del backend, la VM definitiva, Keycloak, PostgreSQL ni datos institucionales reales.

## Naturaleza del entorno

- Entorno: demostración/testing.
- Fuente publicada: rama `dev`.
- Frontend: React + Vite.
- Datos: exclusivamente mocks/datos ficticios incluidos en el frontend.
- Autenticación real: deshabilitada.
- Backend real: no utilizado.
- Datos personales reales: prohibidos.
- Uso: revisión de interfaz, navegación, módulos y conversaciones funcionales con usuarios de prueba.

La aplicación muestra identificadores de QA/demostración y no debe confundirse con la instalación institucional definitiva.

## URL oficial de testing

La URL oficial y establecida para el Showcase de Proyecto Milenio es:

`https://pablodominguez8375-2025.github.io/Modernizaci-n-GLMC-Milenio/`

No se requiere ningún parámetro `utm_source` para acceder al sitio.

La URL de un Pull Request de GitHub no es una URL de demostración: sirve exclusivamente para revisión de código. Los cambios de un PR sólo aparecerán en la URL oficial de GitHub Pages después de ser integrados a `dev` y de que el despliegue de Pages termine correctamente.

La URL efectiva de cada despliegue queda además registrada por el job `Deploy testing showcase` dentro del environment `github-pages`.

## Workflow

Archivo:

`.github/workflows/showcase-pages.yml`

Nombre en Actions:

`PMGM Showcase Demo`

El workflow se ejecuta:
- en pull requests hacia `dev` que afectan frontend/showcase, para validar build y evidencia visual sin publicar el PR;
- en push a `dev`, para construir, validar y publicar la demo;
- manualmente mediante `workflow_dispatch`.

## Gates antes de publicar

1. `npm ci`.
2. Tests de frontend.
3. Lint.
4. Build con `VITE_USE_MOCKS=true`.
5. Base path `/Modernizaci-n-GLMC-Milenio/`.
6. Validación de `index.html`.
7. Búsqueda preventiva de patrones de secretos en el `dist` generado.
8. Generación de evidencia visual responsive.
9. Validación de existencia de al menos 21 capturas PNG de evidencia.
10. Carga del artefacto `pmgm-responsive-visual-evidence`.
11. Artifact de GitHub Pages cuando el evento corresponde a publicación.
12. Deploy al environment `github-pages` sólo fuera de Pull Requests.

## Evidencia visual responsive

El workflow mantiene dos niveles de evidencia:

- **Mi ficha en los siete tamaños de aceptación:** 360 × 800, 390 × 844, 768 × 1024, 1024 × 768, 1366 × 768, 1440 × 900 y 1920 × 1080.
- **Matriz representativa móvil/escritorio:** 390 × 844 y 1440 × 900 para Inicio, Biblioteca Virtual, Gestión Logial, Gran Tesorería, Gran Hospitalaria, Gran Secretaría y Gran Archivero.

La automatización multipantalla se implementa en `.github/scripts/capture-showcase-views.mjs`. Controla Chrome mediante DevTools, cambia el perfil QA usando el selector real y accede a los módulos mediante los botones reales de navegación. No se agregan deep links, credenciales, rutas especiales ni comportamientos exclusivos de captura al runtime de producción.

Las capturas se almacenan temporalmente en el artefacto `pmgm-responsive-visual-evidence` para revisión técnica y UAT visual.

## Seguridad

El showcase es público cuando GitHub Pages está publicado desde este repositorio. Por ello:
- sólo usa datos ficticios;
- no utiliza secretos;
- no utiliza tokens OIDC;
- no apunta a la API institucional;
- no contiene credenciales;
- no debe conectarse a bases de datos ni Object Storage reales.

La eventual VM institucional y este Showcase son entornos distintos. El Showcase no debe utilizarse para ingresar información real de miembros, insinuados, Talleres o autoridades.

## Actualización

Cada cambio de frontend integrado a `dev` vuelve a ejecutar el workflow y, si todos los gates pasan, reemplaza la demo publicada por la nueva versión de testing.

Un PR puede tener todos sus controles verdes y, aun así, no modificar la URL pública hasta que el cambio se integre a `dev`. Esta separación evita que una rama de trabajo reemplace accidentalmente la demostración institucional estable.

## Alcance

El Showcase sirve para validar experiencia, diseño, navegación y flujos demostrativos. La evidencia PNG permite revisar además composición y responsividad de forma repetible.

No reemplaza UAT del backend ni los casos institucionales UAT-001..UAT-020, porque esas pruebas requieren el entorno operacional HTTPS con backend, identidad, persistencia, permisos, backup y recuperación reales.
