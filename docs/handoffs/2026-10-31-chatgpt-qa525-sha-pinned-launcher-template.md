# QA525 #822 — Deterministic SHA-pinned launcher template (31-10-2026)

**PRE:** ChatGPT IA. Branch `security/qa525-pinned-launcher-template-20261031-gpt` from live `dev@6c121b6e62d77459487de01b52434a95fe9f6a0d`. `main@6dfb9546a4873baff15955cf86abfd7d47e3d111` untouched. Reviewed GitHub #822, PR #829 open DRAFT (CI 5/7), signed preflight, existing QA525 launcher template and sandbox, and the Drive continuity log / adenda 13. Files to be added are disjoint from PR #829. No host access.

## PRE / risk reduction
The legacy `--install-launcher` records an exact path into a root-owned launcher stub using `repr(str(Path(__file__).resolve()))`; the future physical installer must instead pin the entrypoint to the immutable root-owned **signed release SHA**, never a mutable branch alias or an arbitrary caller-provided source path. Template generation must not be confused with signature verification. Therefore ship a **pure, no-IO, deterministic helper** rejecting invalid SHAs and enforcing a fixed root-staging layout, plus adversarial tests for path traversal, command injection, mutable refs, changed hash, and syntactic structure.

**No authority:** the helper must not inspect actual host, read approvals, sign releases, install files, change root paths, Docker, Compose, API/WEB, EF100, Keycloak/SMTP, G6, main, srv01 or second VM. The release/staging paths are a **proposal**, not evidence that the physical host already has them. Future installer must verify Sigstore/manifest *before* persisting package and launcher, and preserve that package for launcher runtime.

## DURANTE / POST
Document commit SHA, workflow exact HEAD and any failure. Integrate only to `dev` if seven CI gates SUCCESS and GitHub mergeable. Do not touch `glmdch` without a signed bootstrap, independent review and specific authorization. Keep Issue #822 OPEN.

## DURANTE — implementación exacta y pruebas previstas

- PR **#832 DRAFT** creada desde dev@6c121b6e62d77459487de01b52434a95fe9f6a0d, pendiente de CI.
- Nuevo `scripts/qa/operators/qa525-launcher-pinned-template.py` (commit `9592dbee89f162e0d61b51d41e6ca6d275066866`): `render(source_sha)` construye bytes exactos compatibles con la plantilla histórica, apuntando únicamente a `/opt/centenario/qa525-signed-stage/<SOURCE_SHA>/scripts/qa/operators/qa525-update.py` con SHA minúsculo hex de 40 caracteres. `inspect_template` verifica igualdad exacta, sintaxis Python y SHA-256 de los bytes, reporta explícitamente `signature_verified_by_template=false`, `may_install=false`, `may_apply=false`. No lee, ejecuta ni instala el operador.
- Nueva `tests/test_qa525_launcher_pinned_template.py` (commit `c774f8dce9d121064a9072e77dcc6b3602d29e3c`): SHA/compatibilidad reproducible, comparación entre cortes, no mutación, ruta/traversal, intérprete malicioso, CLI inyectada, tipos incorrectos y AST del stub.
- Workflow QA525 modificado (commit `e15f73242e93023a83efb49b57de776252870d2c`) para disparar el pipeline con ambos archivos, compilar y ejecutar test hermético; no ejecuta Docker/host de QA desde el paso nuevo.
- El futuro instalador debe **autenticar y retener** los bytes del paquete firmado en ese árbol privado antes de usar el launcher. La existencia de la ruta propuesta no fue verificada en `glmdch`; nunca interpretar `inspect_template` como Sigstore, revisión del paquete o aprobación.

## POST pendiente

Esperar CI exact-head; corregir cualquier error real y repetir. No integrar a `dev` ni afirmar release/instalación/G6 hasta gates y verificaciones separadas. PR #829 continúa su propia validación, actualmente 5/7 en ronda nueva.
