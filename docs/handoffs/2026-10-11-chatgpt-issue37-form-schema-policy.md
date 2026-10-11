# Issue #37 — Política base de formularios configurables — 11-10-2026

Rama: feature/issue37-form-schema-core-20261011-gpt desde dev@e8e33c76994e1c252b7ed4f84019ef260215efe3; main sin modificar.

Alcance reservado: FormSchemaPolicy.cs y FormSchemaPolicyTests.cs, este handoff. Sin tocar PR #444, UI de Claude, migraciones ni workflows.

Fase 1: versiones, protección de campos núcleo, tipado, clasificación, dependencias y validación fail-closed con tests. No habilita aún editor ni formularios dinámicos.

Pendiente: modelos/persistencia, API con RBAC, auditoría, snapshots, vistas responsive y demo; reservar migraciones antes de usarlas.

Integrar sólo CI/Showcase/QA exact-head SUCCESS, dev actualizado, y crear PR documental START-HERE después de fusionar.

No modificar main ni instalar srv01 (Issue #97).
