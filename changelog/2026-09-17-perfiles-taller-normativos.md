# 2026-09-17 — Perfiles normativos de cargos de Taller

## Contexto

Se corrige la continuidad del desarrollo para trabajar sobre `dev` en el commit `12b37f465220078e3d7472b3c019965c7cce85ae` (merge del PR #87), evitando usar como base de programación la rama `main` divergente.

## Cambios

- Normaliza los ocho cargos institucionales de Taller en autorización.
- Incorpora constantes explícitas para Hospitalaria, Primer Vigilante, Segundo Vigilante e Inmediato Ex-Venerable Maestro.
- Conserva `lodge_past_master` como claim técnico compatible, con significado funcional Inmediato Ex-Venerable Maestro.
- Implementa autorización de docencia por grado y límite de Taller.
- Reconoce a los ocho cargos institucionales como miembros funcionales del Consejo de Administración.
- Mantiene `lodge_admin` como perfil técnico, separado de los cargos institucionales.
- Actualiza la parametrización de Sistema con responsabilidades alineadas a Constitución/Reglamento.
- Amplía la demo/diseñador de perfiles con los ocho cargos, vistas y acciones.
- Agrega pruebas backend y frontend contra escalamiento y cruces de competencia.

## Fuentes

- Constitución y Reglamento General vigente GLMCh.
- Línea Base Maestra Proyecto Centenario.
- Matriz funcional normativa de cargos de Taller v1.0.
- `docs/PMGM-BASE-001-estado-maestro.md`.

## Estado

Implementado en rama de feature y pendiente de CI/PR hacia `dev`.
