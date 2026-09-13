# Proyecto Centenario — demo pública y Showcase de testing en GitHub Pages

## Objetivo

La demo pública permite revisar Proyecto Milenio / Proyecto Centenario desde un navegador sin instalar servidores y sin exponer la futura infraestructura institucional. Está orientada a demostraciones, revisión funcional, validación visual y conversación con autoridades o usuarios antes del UAT operacional.

## URL oficial de demostración

`https://pablodominguez8375-2025.github.io/Modernizaci-n-GLMC-Milenio/`

Ésta es la URL que debe utilizarse al mostrar el proyecto. Parámetros adicionales como `?utm_source=chatgpt.com` no forman parte de la aplicación y no son necesarios.

La URL de un Pull Request de GitHub no es una URL de demostración: sirve exclusivamente para revisión de código. Los cambios de un PR sólo aparecerán en la URL oficial de GitHub Pages después de ser integrados a `dev` y de que el despliegue de Pages termine correctamente.

## Naturaleza del entorno

- Entorno: demostración/testing público.
- Fuente estable: rama `dev`. Durante el incremento post-RC de Gestión Logial, la rama controlada `feature/treasury-payment-table-v1` puede publicar la demo sin modificar la RC1 congelada.
- Frontend: React + TypeScript + Vite.
- Datos: exclusivamente mocks/datos ficticios.
- Autenticación real: deshabilitada.
- Backend real: no utilizado.
- PostgreSQL: no utilizado.
- Keycloak/OIDC real: no utilizado.
- MinIO/S3 real: no utilizado.
- Datos personales reales: prohibidos.
- Finalidad: navegación, diseño, roles demostrativos y validación funcional visual.

La demo no es una copia de producción y nunca debe utilizarse para cargar información institucional real.

## Identificación de la versión visible

La cabecera de la aplicación muestra:

```text
Demo pública · datos ficticios · <SHA corto>
```

El SHA corto identifica el commit exacto del build publicado. El workflow inyecta `VITE_SHOWCASE_SHA=${{ github.sha }}` durante la compilación y comprueba que ese identificador haya quedado embebido en el resultado estático.

Esto permite responder con precisión preguntas como:

- ¿la demo pública ya contiene el último cambio integrado a `dev`?
- ¿la captura que estamos revisando corresponde al build actual?
- ¿un problema está en el código nuevo o la persona está viendo una publicación anterior?

## Perfiles de demostración

La demo permite cambiar entre perfiles ficticios para enseñar distintas vistas sin credenciales reales:

- Hermano.
- Autoridad de Taller.
- Autoridad de Gran Logia.

Los permisos del selector representan escenarios de UI para demostración; no sustituyen las políticas de autorización reales de Keycloak/API en el piloto institucional.

## Qué sí se debe probar en la demo

La demo es adecuada para revisar:

- navegación general;
- claridad de menús y nombres institucionales;
- diseño de Mi ficha;
- dashboards y reportería visual;
- formularios y flujos demostrativos;
- vistas de Régimen Interior;
- Tesorería/Hospitalaria a nivel de experiencia de usuario;
- Gran Secretaría;
- Gestión Logial;
- Secretaría, Tesorería y Hospitalaria propias de cada Taller;
- Docencia/Instrucción del Taller bajo responsabilidad de los Vigilantes y el Ex Venerable Maestro;
- Biblioteca Virtual y Gran Archivero;
- insinuados publicados;
- calendario y notificaciones;
- adaptación a computador, tablet y celular.

## Qué no prueba la demo

No valida:

- autenticación OIDC real;
- autorización backend;
- persistencia PostgreSQL;
- cifrado/gestión real de secretos;
- almacenamiento documental real;
- análisis antimalware real;
- migraciones de base de datos;
- backup/restauración;
- recuperación ante fallos;
- concurrencia;
- disponibilidad del servidor;
- UAT con integración completa.

Esos puntos corresponden al paquete instalable y al piloto en VM.

## Workflow

Archivo:

`.github/workflows/showcase-pages.yml`

Nombre en GitHub Actions:

`PMGM Showcase Demo`

El workflow se ejecuta:
- en pull requests hacia `dev` que afectan frontend/showcase, para validar build y evidencia visual sin publicar el PR;
- en push a `dev` o a la rama post-RC controlada, para construir, validar y publicar la demo;
- manualmente mediante `workflow_dispatch`.

## Secuencia de publicación

```text
cambio frontend
→ PR hacia dev
→ tests frontend
→ lint
→ build con mocks
→ validación de base path
→ validación de SHA visible
→ búsqueda preventiva de secretos
→ generación de evidencia responsive
→ merge a dev
→ build nuevamente
→ deploy GitHub Pages
→ URL oficial actualizada
```

Un cambio que existe sólo en una rama o PR todavía no aparece necesariamente en la URL pública.

## Gates antes de publicar

1. instalación reproducible de dependencias con `npm ci`;
2. tests de frontend;
3. lint;
4. build con `VITE_USE_MOCKS=true`;
5. base path `/Modernizaci-n-GLMC-Milenio/`;
6. SHA del commit embebido en el build;
7. validación de `index.html` y assets;
8. búsqueda preventiva de patrones de secretos;
9. generación de evidencia visual responsive;
10. validación de existencia de al menos 21 capturas PNG;
11. carga del artefacto `pmgm-responsive-visual-evidence`;
12. creación del artifact de GitHub Pages cuando corresponda;
13. despliegue al environment `github-pages` sólo fuera de Pull Requests.

## Evidencia visual responsive

El workflow mantiene dos niveles de evidencia:

- **Mi ficha en los siete tamaños de aceptación:** 360 × 800, 390 × 844, 768 × 1024, 1024 × 768, 1366 × 768, 1440 × 900 y 1920 × 1080.
- **Matriz representativa móvil/escritorio:** 390 × 844 y 1440 × 900 para Inicio, Biblioteca Virtual, Gestión Logial, Gran Tesorería, Gran Hospitalaria, Gran Secretaría y Gran Archivero.

La automatización multipantalla se implementa en `.github/scripts/capture-showcase-views.mjs`. Controla Chrome mediante DevTools, cambia el perfil QA usando el selector real y accede a los módulos mediante los botones reales de navegación. No se agregan deep links, credenciales, rutas especiales ni comportamientos exclusivos de captura al runtime de producción.

Las capturas se almacenan temporalmente en el artefacto `pmgm-responsive-visual-evidence` para revisión técnica y UAT visual.

## Seguridad

Al ser un entorno público:

- no contiene secretos;
- no contiene tokens OIDC;
- no apunta a la API institucional;
- no contiene credenciales;
- no debe incorporar RUT/RUN, correos, teléfonos, domicilios ni archivos reales;
- no se conecta a bases de datos, MinIO ni servicios institucionales.

El workflow busca patrones sensibles básicos dentro del `dist` antes de permitir la publicación.

## Relación con el paquete instalable

La demo y el paquete instalable son **la misma aplicación funcional construida desde el mismo código y SHA**. No constituyen productos funcionalmente separados. La demo reemplaza exclusivamente las integraciones de infraestructura por adaptadores de datos ficticios que respetan los mismos contratos de operación:

```text
GitHub Pages
= misma interfaz y mismos flujos + adaptadores ficticios + acceso público

Paquete instalable
= misma interfaz y mismos flujos + API + PostgreSQL + Keycloak + MinIO + infraestructura
```

La demo sirve para revisar, probar y recepcionar funcionalidad y apariencia con datos ficticios. Una función sólo se presenta como operativa en la demo cuando existe en el instalable o utiliza un adaptador ficticio con el mismo contrato que el backend desarrollado. La conformidad de la demo no reemplaza las pruebas de infraestructura de la VM.

Para Tenidas y Docencia, tanto demo como instalable deben respetar el mismo ciclo: **programar → proyectar al calendario → marcar realizada/cerrada → registrar o corregir asistencia**. La misma fuente alimenta el calendario personal, el calendario del Taller y la vista consolidada de la Orden, filtrada por permisos.

## Procedimiento para una demostración institucional

Antes de una presentación:

1. abrir la URL oficial;
2. comprobar que la cabecera indique `Demo pública · datos ficticios`;
3. anotar el SHA corto visible;
4. probar los perfiles Hermano, Autoridad de Taller y Autoridad de Gran Logia;
5. revisar las pantallas que se presentarán;
6. evitar describir como “operativo en producción” aquello que todavía se encuentre sólo en demostración.

## Reporte de observaciones

Toda observación sobre la demo debería incluir:

- SHA corto visible;
- perfil de demostración seleccionado;
- pantalla/módulo;
- resolución aproximada;
- acción realizada;
- resultado observado;
- resultado esperado;
- captura de pantalla cuando ayude.

Así una observación visual puede asociarse con precisión al commit correspondiente.

## Actualización y alcance

Cada cambio de frontend integrado a `dev` vuelve a ejecutar el workflow. Si todos los gates pasan, GitHub Pages reemplaza la publicación anterior por el nuevo build.

Un PR puede tener todos sus controles verdes y, aun así, no modificar la URL pública hasta que el cambio se integre a `dev`. Esta separación evita que una rama de trabajo reemplace accidentalmente la demostración institucional estable.

El Showcase sirve para validar experiencia, diseño, navegación y flujos demostrativos. La evidencia PNG permite revisar además composición y responsividad de forma repetible.

No reemplaza UAT del backend ni los casos institucionales funcionales, porque esas pruebas requieren el entorno operacional HTTPS con backend, identidad, persistencia, permisos, backup y recuperación reales.
