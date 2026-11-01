# Handoff — 2026-10-31 — QA525 #822: bind recovery receipts to signed cut

**Actor:** ChatGPT IA. **Branch:** `security/qa525-recovery-receipt-bind-20261031-gpt`. **Base exact:** `dev@ceeb70d5eee4fae2cd4dedb7ff02848aaca771bc`. **Main:** `6dfb9546a4873baff15955cf86abfd7d47e3d111`. **QA physical:** not accessed; no new deployment or root action.

## PRE — audit and improvement rationale

Read live GitHub open/merged PRs, Issue #822/#827, AGENTS §12, GOV-003 §16, existing `qa525-launcher-replacement-contract.py` and its tests, `qa525-launcher-atomic-sandbox.py`, signed-preflight and current QA operator. Drive delta: latest adenda 13 v0.190 and continuity document, no operative authorization. No other open PR changes the two target files. PR #823 was already merged, but does **not** provide the signed physical launcher installer.

**Gap:** `assess_interrupted_swap` validates the old and new launcher hashes in a durable receipt, but not the signed `source_sha`, `release_asset_sha256`, and single-use `nonce` that the sandbox already validates. A copied receipt for an otherwise identical binary could be accepted for a different signed cut. The function also only accepts a boolean `pending` without verifying the intent record identity. The existing results remain diagnostic, never installer authorization. Severity P1 for reusable policy correctness; P0 gate for any future root installer.

**Bounded change:** tighten the pure, no-I/O recovery contract to require exact signed release identity, nonce and matching intent/receipt when present; add adversarial regressions and document explicit migration of callers. Do not build, install, apply, archive or rollback on glmdch; no Docker, database, Compose, Keycloak, SMTP, EF100, API/WEB, credentials, main, srv01 or second VM. No physical install without complete independent Sigstore chain + host locks/journal/backup + separate approval.

## DURANTE / POST — to complete

Record exact changed files and commits, any failed test and correction, CI exact PR HEAD, final dev SHA if merged, publication proof status separate from CI, Drive adenda and readback. No local syntax test is claimed until executed or CI verifies it.
