# QA525 #822 — Root-local launcher approval gate (31-10-2026)

**Actor:** ChatGPT IA. **PRE source:** dev@ceeb70d5eee4fae2cd4dedb7ff02848aaca771bc, main@6dfb9546a4873baff15955cf86abfd7d47e3d111. **QA physical:** no access or changes. **Scope:** launcher-only approval reader with no installation entrypoint.

## PRE / improvement review
Issue #822 requires a distinct root0600 approval before replacement. The existing API+WEB approval reader `qa525-supervised-cut-approval.py` already provides root-owned, no-symlink, 0600, TTL/nonce patterns but has a different scope/action; reusing its *file* would conflate independent permissions. The pure launcher policy `qa525-launcher-replacement-contract.py` checks signed SHA/digest and issuer claims, but does not read a root file. PR #829 binds recovery receipts, PR #830 adds legacy status; neither modifies this new reader/workflow. Drive delta reviewed (adenda 13 and QA continuity).

**Decision:** dedicated root-local file `/etc/pmgm/qa525-launcher-replace.json`; import-safe read-only module with a strict one-use approval schema, owner/path checks and duplicate JSON-key rejection; no CLI for accepting approval or replacing launcher. Fail closed on any mismatch. Signing and approval remain separate. A future installer must hold the five existing locks, independently verify release Sigstore/manifest and host state, check nonce/journal and pass the pure contract before any mutation.

**Exclusions:** host execution, root mutation, Docker/API/WEB/Compose/EF100/Keycloak/SMTP, existing approvals, G6, main, srv01, second VM, PR771/737/766.

## DURANTE / POST
Document exact commits, tests/CI and any failures; integrate only to dev after the exact HEAD gates; mirror GitHub and Drive. No physical deployment or new release assertion.

## DURANTE — implementation and evidence

- Claim PR #831 DRAFT from exact dev@ceeb70d5eee4fae2cd4dedb7ff02848aaca771bc; task bound to Issue #822. This PR does not overlap the files edited by PR #829 or #830, except its own QA525 workflow, neither of which changes the workflow.
- New `scripts/qa/operators/qa525-launcher-root-approval.py` reads **only** `/etc/pmgm/qa525-launcher-replace.json`, with root/glmdch, root-owned ancestor chain, O_RDONLY|O_NOFOLLOW, inode/size and mode0600 checks, hardlink/symlink exclusion, 4096-byte limit and strict duplicate-key rejection. Exactly-scoped fields, identity/digest/nonce, TTL ≤900 seconds and operator action are validated. The library cannot install and has no installation CLI.
- `tests/test_qa525_launcher_root_approval.py` exercises exact valid schema, false/invalid/expired act, wrong host/UID, symlink/hardlink/world writable ancestry, duplicate keys, data read, and no mutation APIs. A pre-push code review noticed an invalid AST test comprehension, fixed in subsequent test commit; this is not a CI failure and is recorded to explain the correction.
- `.github/workflows/qa525-supervised-dual-transaction.yml` explicitly adds both code/test paths for push and PR, compiles both and invokes hermetic root-approval tests. This avoids a misleading green CI when new files were omitted from `paths`.

**Changes committed on branch:** ccf568556a80f88928fdf531f8bbc89dc2134796 (claim); 02a95fd9c6e3aa1302a555e982db680296e35238 (reader); e532bfdd9889832fe9523ca57948c8e61b2c18e1 (adversarial tests); c9d0eae7a0b682ae0b517107a8635030758bc391 (AST test fix); 7cad5cc9196dd72cac719ca6bb9137eaedb32e98 (CI paths and tests). CI exact-head initiated, PENDING at first inspection; cannot merge until full success. No physical host access, signed release verification or G6 proof.

## POST pending

The root approval reader **cannot authenticate a human identity**, independently inspect live host, consume nonce, or approve installation. A future installer must be externally authenticated before execution, verify exact release Sigstore/manifest, acquire existing five locks, re-read approval and live state, validate separately against pure contract, journal/backup, and fail closed before any swap. No main/srv01/EF100/Compose/API/WEB/Keycloak/SMTP changes, no use of obsolete PR771/737/766. Record CI and readback in GitHub and Drive.
