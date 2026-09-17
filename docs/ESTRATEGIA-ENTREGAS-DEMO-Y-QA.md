# Estrategia permanente de entregas — Demo GitHub Pages + QA instalable

Estado: **vigente y obligatoria para el Proyecto Centenario**  
Fecha: 2026-09-17

## Regla principal

El Proyecto Centenario mantiene una sola línea continua de programación y, para cada incremento funcional relevante, debe producir dos salidas coordinadas del mismo desarrollo:

1. **Demo navegable en GitHub Pages**, destinada a revisión visual, funcional y de flujo por Sponsor/UAT.
2. **Versión instalable y operacional para QA en `srv01`**, destinada a validar backend, base de datos, autenticación, persistencia, permisos, auditoría, archivos, integraciones y comportamiento real del sistema.

Estas dos salidas no son proyectos separados. Ambas derivan del mismo código, requisitos, modelos, perfiles, flujos y línea base del Proyecto Centenario.

## Demo GitHub Pages

La demo debe:

- mantenerse alineada con los módulos y vistas ya aprobados;
- reflejar los perfiles, permisos y flujos definidos para la versión vigente;
- utilizar datos ficticios/controlados para demostración;
- permitir revisar navegación, formularios, paneles, reportes y estados del flujo cuando técnicamente sea posible en una publicación estática;
- identificar claramente cualquier función simulada que todavía no dependa de servicios reales;
- evitar inventar funciones que no existan en la línea base o en el código vigente.

GitHub Pages es una superficie de demostración. No sustituye las pruebas operacionales de servidor cuando una función requiere API, PostgreSQL, autenticación, archivos, correo, auditoría u otra capacidad de backend.

## QA operacional en srv01

La versión QA debe:

- ser instalable/reproducible desde el repositorio oficial;
- ejecutarse sobre la infraestructura acordada de `srv01`;
- utilizar contenedores y configuración versionada cuando corresponda;
- incluir backend, frontend, PostgreSQL, migraciones y servicios necesarios según el avance de la versión;
- validar permisos y segregación de datos en backend, no solo ocultamiento visual;
- mantener trazabilidad y auditoría conforme a la línea base;
- permitir carga y operación real de los artefactos habilitados para UAT;
- ser la referencia para confirmar que una funcionalidad es realmente operacional.

## Regla de paridad

Cada incremento debe buscar paridad entre:

`Requisito aprobado → Código en main → Demo GitHub Pages → QA instalable`

Una función puede aparecer primero como simulación visual en la demo si el backend aún no está terminado, pero debe quedar identificada como simulada y no considerarse operacional hasta estar integrada y probada en QA.

No se debe mantener una demo que evolucione por una línea distinta al sistema instalable. La demo debe actualizarse desde la misma definición funcional y, cuando sea técnicamente razonable, reutilizar componentes, modelos y contratos de la aplicación real.

## Criterio de avance

Para cada funcionalidad relevante, el estado debe poder distinguirse al menos como:

- Definida/documentada.
- Implementada en código.
- Visible en demo GitHub Pages.
- Integrada en QA.
- Probada en QA/UAT.

No se debe declarar una función como "operacional" únicamente porque esté visible en GitHub Pages.

## Continuidad

Antes de crear o modificar una demo o instalación QA se debe revisar el último `main`, la Línea Base Maestra, Control de Cambios, ADR, migraciones y código existente.

Queda prohibido reconstruir la demo o QA desde versiones antiguas, conversaciones históricas o prototipos que hayan sido superados por una versión posterior consolidada.
