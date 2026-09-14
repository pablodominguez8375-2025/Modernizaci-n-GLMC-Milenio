# PMGM-ADR-001 — Estrategia de repositorios y ramas

## Estado
Aceptado — v0.1.

## Contexto
El Proyecto Milenio debe desarrollar una plataforma nueva sin mezclar ese trabajo con la migración operativa de los sitios Joomla actuales.

## Decisión
1. El repositorio `pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio` será la fuente única de verdad del nuevo ecosistema Milenio.
2. El repositorio `ehshackleton/glm-platform-migration` se mantiene separado para continuidad y migración Joomla.
3. `main` representa versión estable/aprobada.
4. `dev` representa integración y desarrollo en curso.
5. Cambios significativos deben documentarse y promoverse hacia `main` mediante revisión.

## Consecuencias
- Se reduce riesgo de mezclar operación legado con desarrollo nuevo.
- Se facilita rollback y trazabilidad.
- Las integraciones entre ambos repositorios deben documentarse explícitamente.
