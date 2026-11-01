# QA525 #822 — Retained SHA-pinned source inspector (31-10-2026)

## PRE: claim and risk review

ChatGPT IA from live `dev@97165b52282dca51a313233fbb8f5d59b695455b`, main `6dfb9546a4873baff15955cf86abfd7d47e3d111`. PR #829 and #832 each merged to dev after 7/7 exact-head CI. Current #822 still OPEN; #827 signed release publication not verified; no physical QA access.

**Risk:** the new deterministic launcher points into `/opt/centenario/qa525-signed-stage/<SHA>/scripts/qa/operators/qa525-update.py`. A future privileged installer must prove the actual source bytes remain root-owned, link-free, non-writable by group/world and hash-matching; `render()` cannot do that. Without a retained-source inspection, a pinned path could be present but unsafe or stale.

**Scope of this proposal:** add a read-only, glmdch/root-only inspector of the retained source's precise SHA-pinned path, including all ancestry and file descriptor inode+size continuity. It never imports or executes source, never reads from network, never claims Sigstore/manifest/release verified, never installs or authorizes deploy. The expected source hash is an *input from an independently verified signature* at future installation time and is not independently trusted by this tool. No mutable paths accepted.

**Forbidden:** any physical glmdch access, root modification, Docker/Compose, API/WEB, EF100, Keycloak/SMTP, G6, prod/main/srv01, second VM, PR771/737/766.

## DURANTE / POST

Claim via DRAFT PR before coding. Add fail-closed adversarial tests and required QA525 hermetic workflow paths. Run exact-head CI. Record errors and readback. Only merge to dev when 7/7. Future installer must separately supply actual Sigstore provenance, locks, approval, consumed nonce, durable journal/backup/fsync/rollback and explicit host authorization.
