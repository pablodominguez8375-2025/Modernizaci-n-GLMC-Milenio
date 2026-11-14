# QA525 V3.2 — infraestructura auto-web inactiva

## Estado

- Issue #1020; PR DRAFT #1021; referencia de arquitectura #1010.
- Base inicial: `dev@6e74c422e14e310f2e463a821a34ad37a2a6b825`; rebase final pendiente sobre `dev@8e851bc3d2f1615a94009591f0781f88d6d3fe6f`.
- `main@6dfb9546a4873baff15955cf86abfd7d47e3d111`, sin cambios.
- Implementado en rama, todavía no integrado a `dev`.

## Entrega

- `qa525-vnext-autoweb-journal.py`: ventana de hasta cinco secuencias con nombres idénticos al contrato, secuencia consecutiva, enlace por digest, release/nonce nuevos y rechazo de BLOCKED, recuperación, vuelo activo o residuo.
- `qa525-vnext-autoweb-root-plane.py`: adapta la clase `RootPlane` verificada, rechaza token sintético, limita efectos y revalida locks + lease antes y después de cada efecto. Excepción o pérdida post-efecto es indeterminada/recuperación, nunca éxito.
- `qa525-vnext-autoweb-terminal.py`: liga policy/sequence/release/nonce, stage-store, plano/lease, firma y mediciones. ACTIVE exige imagen nueva; BLOCKED no crítico acredita baseline restaurado y crítico acredita que no fue restaurado.
- `docs/qa/QA525-AUTOWEB-V3.2-PROCEDIMIENTO-CORTO.md`: flujo normal de cero intervención por release, controles consolidados y excepciones mínimas.
- Tres suites adversariales: 23 tests locales PASS.

## Límites

Bibliotecas puras e inactivas: sin filesystem, red, procesos, Docker, firma propia, launcher, workflow, unit o timer. No crean autoridad APPLY y no reemplazan #1012/#1019. El orquestador `qa525-vnext-auto.py` sigue bloqueado hasta que contrato, workflow y puerto WEB queden 0 P0/P1.

No se tocó glmdch, G6, API/WEB real, BD, identidad, migraciones, `main` ni producción. Demo Pages e instalable QA: sin cambios por este PR.

## CI y revisión

- Local: `python3 -m unittest -v tests.test_qa525_vnext_autoweb_terminal tests.test_qa525_vnext_autoweb_journal tests.test_qa525_vnext_autoweb_root_plane` → 23 PASS.
- Pendiente: GitHub Actions exact-head, revisión adversarial Claude y 0 P0/P1 antes de cualquier integración a `dev`.
