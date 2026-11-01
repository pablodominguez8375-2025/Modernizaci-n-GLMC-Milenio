# QA525 #822 — Deterministic SHA-pinned launcher template (31-10-2026)

**PRE:** ChatGPT IA. Branch `security/qa525-pinned-launcher-template-20261031-gpt` from live `dev@6c121b6e62d77459487de01b52434a95fe9f6a0d`. `main@6dfb9546a4873baff15955cf86abfd7d47e3d111` untouched. Reviewed GitHub #822, PR #829 open DRAFT (CI 5/7), signed preflight, existing QA525 launcher template and sandbox, and the Drive continuity log / adenda 13. Files to be added are disjoint from PR #829. No host access.

## PRE / risk reduction
The legacy `--install-launcher` records an exact path into a root-owned launcher stub using `repr(str(Path(__file__).resolve()))`; the future physical installer must instead pin the entrypoint to the immutable root-owned **signed release SHA**, never a mutable branch alias or an arbitrary caller-provided source path. Template generation must not be confused with signature verification. Therefore ship a **pure, no-IO, deterministic helper** rejecting invalid SHAs and enforcing a fixed root-staging layout, plus adversarial tests for path traversal, command injection, mutable refs, changed hash, and syntactic structure.

**No authority:** the helper must not inspect actual host, read approvals, sign releases, install files, change root paths, Docker, Compose, API/WEB, EF100, Keycloak/SMTP, G6, main, srv01 or second VM. The release/staging paths are a **proposal**, not evidence that the physical host already has them. Future installer must verify Sigstore/manifest *before* persisting package and launcher, and preserve that package for launcher runtime.

## DURANTE / POST
Document commit SHA, workflow exact HEAD and any failure. Integrate only to `dev` if seven CI gates SUCCESS and GitHub mergeable. Do not touch `glmdch` without a signed bootstrap, independent review and specific authorization. Keep Issue #822 OPEN.
