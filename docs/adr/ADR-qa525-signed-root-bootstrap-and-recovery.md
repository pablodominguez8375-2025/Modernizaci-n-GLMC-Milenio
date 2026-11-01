# ADR — QA525: trust boundary before first privileged launcher replacement

**Status:** PROPOSED / SECURITY BLOCKER. **Scope:** Issue #822, PR #834.
**Decision owner:** Product Owner; this draft is for engineering and security review.
**Observed baseline:** dev@171e544a2fc8b51c62bc9a022e9509a5ea2c24ae; main@6dfb9546a4873baff15955cf86abfd7d47e3d111. PR #835 (approval PosixPath fix) merged into dev. PR #834 code tests 7/7 for previous exact branch SHA d21e87fa8a78c9969ed87be2ad655158c9ac6617, but source remains DRAFT. No physical glmdch inspection in this work.

## Problem: the first-execution trust paradox

The root installer currently calls `bootstrap_trusted_sources()` before importing its helper Python modules. That function checks root ownership and non-writable ancestry. **It does not authenticate the SHA-256 of the installer or its helpers against a separately verified signed source.** A local file that passes ownership/mode checks can still be stale, mismatched or replaced by a privileged actor. Calling its own signature checker from code whose authenticity has not yet been established is circular and cannot fix this gap.

A new executable obtained directly from a mutable Git branch, a comment, a chat, or `curl | sudo` must NEVER be allowed to bootstrap its own trust. Running `gh attestation verify` *after* importing downloaded Python code also fails this boundary.

## Decision / fail-closed safety contract

**Before any root import or execution of installer content**, an operator must have a separately reviewed, immutable, previously trusted bootstrap. The bootstrap itself must not come from the just-downloaded PR/release and must be independently pinned and protected. Until that prerequisite is demonstrated on the sole host `glmdch`, **DO NOT RUN PR #834**.

The independently trusted bootstrap must, in order:

1. Prove exact live `dev` SHA and require success of the publisher and all relevant `push` workflows. Never count `workflow_dispatch` as `push`.
2. Acquire an immutable GitHub release `qa-signed-<SHA>`, record the asset's canonical digest, and verify the downloaded ZIP bytes against that digest. A GitHub REST `digest` field alone is metadata, not independent proof.
3. Validate Sigstore attestation with the **expected repository, signer workflow, source digest and denied self-hosted runners**, using a pre-existing trusted verifier. Verify nested archive manifests/required source identity without importing any package Python.
4. Compare every installer and helper file's actual bytes against the verified signed manifest; reject missing/extra/mismatched/mutable, symlink or hardlink artifacts, mismatched SHA, unexpected interpreter and stale branch artifacts.
5. Persist independently checked files to an isolated, immutable, root-owned 0700 staging tree and fsync all contained files/dirs. The executable entrypoint used for installation must be taken only from this verified tree. Preserve evidence of the trust-root version and checks as an audit receipt, not an implicit approval.
6. Separately confirm `glmdch` UID0, the five established QA locks, fresh successful G6/offhost restore, approved launcher-only root0600 act (exact SHA/digest/old/new hashes and single-use nonce TTL<=900), and the existing installer's reversible journal/rollback plan. **None of the signature steps grants deployment permission.**

These steps describe required properties, not a host-executable command. A draft Python file copied out of this PR is not a trusted bootstrap.

## Crash/rollback threat model and required tests

| Threat/failure | Minimum required evidence | Fail-closed outcome |
| --- | --- | --- |
| Mutable branch or unsigned downloaded code | Trusted bootstrap verifies asset, Sigstore and signed manifest BEFORE import | Reject without root execution |
| Altered GitHub release or wrong SHA | Exact HEAD, asset digest and source attestation checked twice | Reject and retain old launcher |
| Local root-owned stale helper | SHA of each helper equals signed manifest under immutable staging | Reject; owner/mode alone insufficient |
| Interrupted after durable intent, before backup | Intent present / backup absent | Block; manual verified reconciliation |
| Interrupted before atomic swap | Old live bytes and backup match intent | No auto-retry; require explicit operator decision |
| Interrupted after atomic swap, before receipt | New live bytes; old backup; intent present | No unreviewed success; manual reconciliation |
| Changed live bytes or bad backup | Independently measured live+old hashes mismatch intent | Block; preserve evidence, no blind rollback |
| Expired/modified approval or replay nonce | Same root0600 packet re-read at last pre-swap boundary; historical receipt | Block without consuming extra approvals |
| Partial fsync/power loss | Actual subprocess SIGKILL tests + isolated persistent-restore drill | Keep journal, block unsafe progress |
| Keycloak/SMTP/EF100/Compose drift | Explicit no-change pre/post inventory | Stop; never expand launcher approval into API/WEB |
| Missing or stale G6 / rollback target | Fresh backup and offhost restore evidence at required cut | Block physical apply |

**Rollback plan still incomplete:** PR #834's `inspect_pending_reconciliation()` is read-only and reports PRE/POST_ATOMIC_SWAP but does not perform a supervised dual rollback or prove post-crash recovery under host conditions. Tests covering `SIGKILL` with disposable files do not replace a real host recovery drill. A separate root-only, pre-authenticated, nonce-gated supervised rollback design and tests must be reviewed before physical use.

## Alternatives considered

- Trust file ownership and `chmod 0700` alone: **REJECTED** (authenticity cannot be inferred from permissions).
- Execute PR installer first, then validate signed release: **REJECTED** (code executes before provenance).
- Skip Sigstore because GitHub has SHA-256 metadata: **REJECTED** (asset integrity metadata is not cryptographic provenance).
- Auto-rollback on a power-loss journal with no human review: **REJECTED** (can replay an unknown partially committed transaction).
- Reuse stale QA release after a new dev merge: **REJECTED** (violates exact HEAD identity).

## Exit criteria and governance

This ADR can become ACCEPTED only after independent bootstrap code and its fixed trust anchor are reviewed and evidenced on `glmdch`; exact-head 7/7 tests plus original signed push publication, a testable supervised dual rollback with operator controls, fresh G6 and restore, QA pilot with read-only pre/post diff, and sponsor's explicit physical authorization. Completing or merging a PR is not installation, and launcher installation is not API/WEB deployment or UAT.

No main, production, srv01 physical, second VM, migration EF100 rerun, Compose/Keycloak/SMTP modification or PR #771/#737/#766.

## Implementation update — pre-import admission check (1 Nov 2026)

PR #834 now includes defense-in-depth code from `608d52ac2d9f486c0c7c09eb5e5d5dedb0f2bd1a`: the installer refuses to dynamically import its privileged helper modules unless an **already independently provisioned**, root-owned mode 0600 admission exists at `/etc/pmgm/qa525-bootstrap-admission.json`. It requires exact fields `status, source_sha, release_asset_sha256, manifest_sha256, trusted_verifier_sha256, files`; `files` must list **every** of the hardcoded privileged source paths and map each to its actual SHA-256. It reads the files with no-symlink root-owner checks, rejects missing/modified/extra paths, and compares the admitted SHA/release digest to the source identity obtained from the signed preflight before writing a launcher transaction. It does NOT write/refresh the receipt and has no CLI override.

**Essential distinction:** An admission packet bearing `status=EXTERNAL_SIGSTORE_BOOTSTRAP_ADMISSION_ONLY` is **not a signature** or proof of the verifier's authority by itself. This program still executes some of its own Python before reading the admission; the *caller must have authenticated this entrypoint separately before first root execution*. No trusted bootstrap has been installed or witnessed on glmdch. Therefore **the initial root bootstrap problem remains unresolved**, and the physical installer is explicitly blocked despite these strengthened checks. The verifier's hash inside the packet is an audit binding, not a trusted root of authority by assertion.

Tests added in `b98245569c647396cc8df77c50b067d32cbe06bf`: simulated root-owned admission using real temporary file bytes, exact expected helper set, reject tampered source, missing helper, invalid admission status/sha, changed release identity and missing admission before any helper import. They never run on glmdch or grant installation. Build/CI must be evaluated for the final exact PR HEAD.
