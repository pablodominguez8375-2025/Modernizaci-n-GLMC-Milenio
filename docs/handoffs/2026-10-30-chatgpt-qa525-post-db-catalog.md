# Handoff QA525 — corrección del probe post-DB — 2026-10-30

Estado: EN CURSO.

- Issue: #525.
- Rama: `fix/qa525-post-db-catalog-20261030-gpt`.
- Base exacta: `dev@81554d0b1a5a31273f655dd2b437ba4c70457ce7`.
- Alcance: corregir exclusivamente la serialización del catálogo PostgreSQL del probe read-only posterior a la migración #801.
- Archivos previstos:
  - `scripts/qa/operators/qa525-post-db-readonly.py`
  - `scripts/qa/operators/qa525-member-history-schema-gate.py` si se requiere alinear el nombre físico truncado.
  - `tests/test_qa525_post_db_readonly.py`
  - este handoff.
- Evidencia física de glmdch: EF99, 2 tablas, 3 índices, 22 columnas y 3 constraints PASS mediante consultas READ ONLY; G6 postDB restaurado PASS.
- Defectos reproducidos: SQLSTATE 42725 por `con.contype`, booleanos `true/false` frente a `t/f` y FK física truncada por PostgreSQL a 63 bytes.
- Exclusiones: sin migraciones, sin despliegue, sin main, producción ni srv01.
