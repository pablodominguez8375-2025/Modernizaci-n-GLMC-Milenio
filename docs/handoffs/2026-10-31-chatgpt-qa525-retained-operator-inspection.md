# QA525 #822 — Retained SHA-pinned source inspector (31-10-2026)

## PRE: claim and risk review

ChatGPT IA from live `dev@97165b52282dca51a313233fbb8f5d59b695455b`, main `6dfb9546a4873baff15955cf86abfd7d47e3d111`. PR #829 and #832 each merged to dev after 7/7 exact-head CI. Current #822 still OPEN; #827 signed release publication not verified; no physical QA access.

**Risk:** the new deterministic launcher points into `/opt/centenario/qa525-signed-stage/<SHA>/scripts/qa/operators/qa525-update.py`. A future privileged installer must prove the actual source bytes remain root-owned, link-free, non-writable by group/world and hash-matching; `render()` cannot do that. Without a retained-source inspection, a pinned path could be present but unsafe or stale.

**Scope of this proposal:** add a read-only, glmdch/root-only inspector of the retained source's precise SHA-pinned path, including all ancestry and file descriptor inode+size continuity. It never imports or executes source, never reads from network, never claims Sigstore/manifest/release verified, never installs or authorizes deploy. The expected source hash is an *input from an independently verified signature* at future installation time and is not independently trusted by this tool. No mutable paths accepted.

**Forbidden:** any physical glmdch access, root modification, Docker/Compose, API/WEB, EF100, Keycloak/SMTP, G6, prod/main/srv01, second VM, PR771/737/766.

## DURANTE / POST

Claim via DRAFT PR before coding. Add fail-closed adversarial tests and required QA525 hermetic workflow paths. Run exact-head CI. Record errors and readback. Only merge to dev when 7/7. Future installer must separately supply actual Sigstore provenance, locks, approval, consumed nonce, durable journal/backup/fsync/rollback and explicit host authorization.

## DURANTE — code and verification gates

- PR #833 DRAFT: https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/pull/833.
- Code `scripts/qa/operators/qa525-launcher-retained-source-inspect.py` at commit `f7ef1ca77d44b829c245e00c679cbdc5cd12badc`. The only path is derived from exact lowercase 40-hex SHA. `trusted_chain` checks all ancestors root-owned, no links/world-group write, stage and SHA directories mode 0700, source regular non-hardlinked. It opens O_RDONLY|O_NOFOLLOW|O_CLOEXEC and validates inode/size/mtime before bounded hash; no import/exec/install. Reports hash match and explicitly `signature_verified_by_inspector=false`.
- Tests `tests/test_qa525_launcher_retained_source_inspect.py` at `ed2fac38dad8409e2fa042f26f0aecf9662d9e1f` reject wrong host, SHA aliases, bad ancestry, links/hardlinks, file race, unexpected hash, permissions and mutating calls; use only simulated root metadata and disposable temp files.
- Workflow QA525 updated in `95feade20a75e62ae3363faa4ad0744adb84fcc8`: PR/push paths, Python compile and tests explicitly included.
- Still no root install or signed release verification; this check is solely necessary consistency, **not proof of signed source**. CI exact-head pending. Do not merge before all seven workflows successful.

## POST — pending

Physical host untouched. Next root installer must independently verify source Sigstore/manifests, persist signed bytes with durable fsync under root-only SHA path, then this inspector can cross-check retained bytes and destination. Must also hold five existing locks and separate approval/nonce plus journal/rollback. Issue #822 remains OPEN. Log all CI/merge outcomes to GitHub and Drive.
