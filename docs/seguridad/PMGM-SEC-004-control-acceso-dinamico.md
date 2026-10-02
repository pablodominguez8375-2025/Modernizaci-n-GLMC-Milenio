# PMGM-SEC-004 — Perfiles, menús, vistas y acciones dinámicos

El administrador autorizado puede crear perfiles técnicos, asignar menús/vistas y seleccionar de forma independiente `view`, `create`, `write`, `edit`, `delete` y `print`. La ausencia de grant es denegación. `print` no implica `export`, firma, aprobación ni una atribución institucional.

La API valida el catálogo y aplica:

- perfiles de sistema protegidos contra edición de grants y baja;
- perfiles custom con baja lógica y asignaciones revocables;
- asignaciones acotables por sujeto, fecha y organización;
- auditoría de creación, edición, grants, asignación, revocación y baja;
- autoridad final de cada módulo de dominio: el perfil técnico no sustituye Constitución, Reglamento, aprobaciones, firmas ni controles de Tesorería/Admisiones.

La UI existente es una superficie de administración y demostración; las operaciones reales deben pasar por `/api/system/access`. La demo sólo usa identidades, menús y vistas ficticios.
