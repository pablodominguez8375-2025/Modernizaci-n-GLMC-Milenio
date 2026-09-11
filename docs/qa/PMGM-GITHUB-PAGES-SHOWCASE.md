# Proyecto Milenio — demo pública en GitHub Pages

## Objetivo

La demo pública permite revisar Proyecto Milenio desde un navegador sin instalar servidores y sin exponer la futura infraestructura institucional. Está orientada a demostraciones, revisión funcional, validación visual y conversación con autoridades o usuarios antes del UAT operacional.

## URL oficial de demostración

`https://pablodominguez8375-2025.github.io/Modernizaci-n-GLMC-Milenio/`

Ésta es la URL que debe utilizarse al mostrar el proyecto. Parámetros adicionales como `?utm_source=chatgpt.com` no forman parte de la aplicación y no son necesarios.

## Naturaleza del entorno

- Entorno: demostración/testing público.
- Fuente publicada: rama `dev` después de pasar el workflow de GitHub Pages.
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

El SHA corto identifica el commit exacto del build publicado. El workflow inyecta `VITE_SHOWCASE_SHA=${{ github.sha }}` durante la compilación y luego comprueba que ese identificador haya quedado embebido en el resultado estático.

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

## Flujo de publicación

Archivo del workflow:

`.github/workflows/showcase-pages.yml`

Nombre en GitHub Actions:

`PMGM Showcase Demo`

Secuencia:

```text
cambio frontend
→ PR hacia dev
→ tests frontend
→ lint
→ build con mocks
→ validación de base path
→ validación de SHA visible
→ búsqueda preventiva de secretos
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
9. creación del artifact de GitHub Pages;
10. despliegue al environment `github-pages` sólo desde eventos que no sean pull request.

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

La demo y el paquete pre-UAT son dos productos distintos del mismo código:

```text
GitHub Pages
= frontend estático + mocks + acceso público

Paquete pre-UAT
= frontend + backend + PostgreSQL + Keycloak + MinIO + infraestructura + scripts + documentación
```

La demo sirve para **mostrar**. El paquete sirve para **instalar y validar integralmente**.

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
- acción realizada;
- resultado observado;
- resultado esperado;
- captura de pantalla cuando ayude.

Así una observación visual puede asociarse con precisión al commit correspondiente.

## Criterio de actualización

Cada cambio de frontend integrado a `dev` vuelve a ejecutar el workflow. Si todos los gates pasan, GitHub Pages reemplaza la publicación anterior por el nuevo build.

El showcase no reemplaza los casos UAT institucionales. La aceptación final requiere el entorno operacional HTTPS con backend, identidad, persistencia, permisos, backup y recuperación reales.
