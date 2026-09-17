# PMGM-GOV-002 — Continuidad multichat, multi-IA y gobierno de ramas

**Estado:** vigente al integrarse en `dev`  
**Fecha:** 2026-09-17  
**Alcance:** continuidad técnica/funcional del Proyecto Centenario

## 1. Decisión

El Proyecto Centenario puede ser continuado desde cualquier chat, IA o herramienta de desarrollo. La continuidad no depende de conservar una conversación específica: depende de reconstruir el estado desde las fuentes persistentes del proyecto.

Este documento complementa `PMGM-GOV-001`. Cuando una referencia histórica señale que un chat concreto es el único hilo maestro, debe interpretarse como una práctica de coordinación de ese momento, **no como una restricción de continuidad**. La fuente persistente prevalece.

## 2. Ramas

- `dev`: rama activa de desarrollo e integración.
- `main`: rama estable y de promoción controlada.
- `feature/*`: cambios aislados nacidos desde el HEAD vigente de `dev`.
- `release/*`: cortes de estabilización/RC cuando correspondan.

Toda IA/desarrollador debe verificar las ramas antes de trabajar. No debe asumir que `main` contiene el desarrollo más reciente.

Checkpoint conocido al aprobar esta regla: `dev` en `12b37f465220078e3d7472b3c019965c7cce85ae`, merge de PR #87. El checkpoint no sustituye el HEAD vivo.

## 3. Fuentes persistentes

Para retomar:

1. GitHub `dev`: código integrado activo, pruebas, migraciones, ADR, workflows y documentación técnica.
2. Google Drive: Línea Base Maestra y documentación oficial/normativa.
3. GitHub `main`: rama estable/promoción y referencia de releases consolidados.
4. Chats: contexto histórico, nunca sustituto del estado versionado.

## 4. Reconciliación

Si `main` y `dev` divergen, no se debe hacer merge ciego ni escoger una rama por nombre. Se debe identificar qué commits pertenecen a desarrollo activo, cuáles son documentación/estabilidad, revisar conflictos y promover de forma explícita.

Si GitHub y Drive difieren en una regla funcional o normativa, se compara fuente, fecha y aprobación antes de modificar el código afectado.

## 5. Flujo de desarrollo

`Requisito → fuente/norma → feature desde dev → código + pruebas + documentación → PR a dev → CI exact-head → merge dev → Demo Pages + artefacto/QA → UAT → release/main según aprobación`

No se requiere autorización adicional para continuar una tarea ya aprobada dentro de su alcance; sí se requiere decisión cuando el cambio altera arquitectura, normativa, alcance o una decisión previamente aprobada.

## 6. Handoff

Cada intervención debe dejar commit/PR, pruebas, estado de demo, estado QA, documentación y pendientes. La siguiente intervención vuelve a validar el estado vivo antes de continuar.

## 7. Regla de no regresión

Nunca sustituir el código activo de `dev` por prototipos, código simplificado o implementaciones paralelas provenientes de chats o ramas desactualizadas. Reutilizar y extender el sistema existente.
