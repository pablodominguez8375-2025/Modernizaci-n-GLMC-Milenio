# Handoff #812 — Taller 00 reservado para soporte (2026-10-30)

- Sponsor: Taller 00 fijo, exclusivo del personal técnico (Máster y superusuarios), sin adscripción a logias institucionales ni actividad contable, ceremonial o secretarial.
- GitHub: Issue #812, #604, #525. Inicio dev `0b1b31e59ee1c65364d3a82885815e0c4e1ce865`; main `6dfb9546a4873baff15955cf86abfd7d47e3d111`. Rama `fix/support-workshop-00-20261030-gpt`.
- QA físico: `support_orgs=0, support_number_00=0` en PostgreSQL glmdch con transacción READ ONLY del Sponsor. No se insertó ni desplegó nada.
- Esta PR pretende crear una organización técnica `Type=support,Number=00` en una migración aditiva idempotente y bloquear fail-closed perfiles de Taller sin organización única; no asignar cargos ni membresías a Master, ni tocar Keycloak.
- **Front-end Claude (GOV-003):** `frontend/src/api/pmgmApi.ts` método `startSupportView` omite Content-Type JSON; requiere sustituir `request` por `postJson`, y agregar regresión `pmgmApi.test.ts` para perfil y usuario. Separar PR frontend. HTTP 415 pendiente hasta integración.
- Estado inicial DRAFT, pruebas/CI y revisión pendientes. NO merge o deploy sin gates exactos, firma de release y actualización supervisada de #525. PR #771/#737/#766 y scripts srv01 prohibidos para QA. Sin promoción main.
- Cierre: completar evidencia de CI, postmerge SHA, releases e informe a hilo montaje QA, sin asumir que este handoff equivalga a instalación física.
