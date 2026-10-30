# Handoff QA525 — corrección del probe post-DB — 2026-10-30

## Estado

Implementación terminada en rama; PR #803 pendiente de gates exact-head e integración a `dev`.

- Issue: #525, reclamado con `agente:chatgpt`.
- Rama: `fix/qa525-post-db-catalog-20261030-gpt`.
- Base inicial: `dev@81554d0b1a5a31273f655dd2b437ba4c70457ce7`.
- `main@6dfb9546a4873baff15955cf86abfd7d47e3d111` no se modificó.

## Evidencia física que originó el cambio

En el único host `glmdch`, después de aplicar la migración aditiva #801:

- historial EF: 99;
- esquema exacto: 2 tablas, 3 índices, 22 columnas y 3 constraints;
- G6 preDB y postDB: copia Drive byte idéntica y restauración aislada PASS;
- el probe firmado falló por representación del catálogo, no por la base.

Defectos reproducidos:

1. `pg_constraint.contype` es el tipo interno `"char"`; la concatenación sin cast produjo SQLSTATE `42725`.
2. PostgreSQL convirtió booleanos concatenados a `true/false`, mientras el contrato esperaba `t/f`.
3. PostgreSQL truncó de forma válida la FK a 63 bytes: `FK_member_history_proposal_items_member_history_proposals_Propo`.

## Cambios

- `scripts/qa/operators/qa525-post-db-readonly.py`
  - cast explícito `con.contype::text`;
  - serialización canónica `t/f` mediante `CASE` para `indisunique` y `attnotnull`;
  - consultas conservan `BEGIN READ ONLY`, allowlist cerrada y límites de resultado.
- `scripts/qa/operators/qa525-member-history-schema-gate.py`
  - contrato fijado al identificador físico PostgreSQL de 63 bytes.
- `tests/test_qa525_post_db_readonly.py`
  - regresión independiente para casts, booleanos y longitud/nombre físico de FK.

No hay migraciones ni cambios de datos.

## Pruebas

`python3 -m unittest -v tests.test_qa525_post_db_readonly`: **10/10 PASS**.

Pendiente: CI, Showcase, instalable, Silo y publicación firmada del HEAD que se integre.

## Exclusiones y continuidad

- No desplegar el corte anterior con el probe defectuoso.
- No repetir la migración #801.
- No tocar `main`, producción, srv01 ni PR #771/#737/#766.
- Después del merge y gates postmerge: exigir release `qa-signed-<SHA>` verificable, staging físico de firma y revalidar el probe corregido antes del aplicador API+WEB supervisado.
