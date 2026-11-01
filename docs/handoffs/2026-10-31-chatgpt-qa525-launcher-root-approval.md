# QA525 #822 — Root-local launcher approval gate (31-10-2026)

**Actor:** ChatGPT IA. **PRE source:** dev@ceeb70d5eee4fae2cd4dedb7ff02848aaca771bc, main@6dfb9546a4873baff15955cf86abfd7d47e3d111. **QA physical:** no access or changes. **Scope:** launcher-only approval reader with no installation entrypoint.

## PRE / improvement review
Issue #822 requires a distinct root0600 approval before replacement. The existing API+WEB approval reader `qa525-supervised-cut-approval.py` already provides root-owned, no-symlink, 0600, TTL/nonce patterns but has a different scope/action; reusing its *file* would conflate independent permissions. The pure launcher policy `qa525-launcher-replacement-contract.py` checks signed SHA/digest and issuer claims, but does not read a root file. PR #829 binds recovery receipts, PR #830 adds legacy status; neither modifies this new reader/workflow. Drive delta reviewed (adenda 13 and QA continuity).

**Decision:** dedicated root-local file `/etc/pmgm/qa525-launcher-replace.json`; import-safe read-only module with a strict one-use approval schema, owner/path checks and duplicate JSON-key rejection; no CLI for accepting approval or replacing launcher. Fail closed on any mismatch. Signing and approval remain separate. A future installer must hold the five existing locks, independently verify release Sigstore/manifest and host state, check nonce/journal and pass the pure contract before any mutation.

**Exclusions:** host execution, root mutation, Docker/API/WEB/Compose/EF100/Keycloak/SMTP, existing approvals, G6, main, srv01, second VM, PR771/737/766.

## DURANTE / POST
Document exact commits, tests/CI and any failures; integrate only to dev after the exact HEAD gates; mirror GitHub and Drive. No physical deployment or new release assertion.
