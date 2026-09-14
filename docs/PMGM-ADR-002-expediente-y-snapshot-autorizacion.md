# PMGM-ADR-002 — Expediente único y snapshot de autorización

**Estado:** Propuesto  
**Fecha:** 2026-09-11

## Decisión

Los trámites de insinuación y ceremonia se modelarán como expedientes con historial de eventos. La autorización de una ceremonia guardará un snapshot de las evidencias que estaban vigentes al momento de autorizar.

## Motivo

Las validaciones institucionales pueden cambiar con el tiempo. Una autorización emitida debe poder demostrar qué requisitos estaban conformes cuando fue aprobada, sin depender del estado actual de Tesorería, Hospitalaria, Régimen Interior u otras unidades.

## Consecuencias

- no se sobrescriben decisiones históricas;
- las correcciones se registran como nuevos eventos;
- el estado actual se deriva del historial;
- la plancha referencia el snapshot de autorización;
- auditoría y UAT pueden reconstruir la decisión;
- los datos personales no se duplican innecesariamente entre expediente y ficha maestra.

## Rechazado

No se utilizará un único campo booleano `aprobado` como fuente histórica del trámite.
