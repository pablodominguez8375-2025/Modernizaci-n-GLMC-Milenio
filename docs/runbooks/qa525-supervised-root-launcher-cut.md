# QA525 — Supervised one-shot ROOT launcher cut / operator runbook

**Status: BLOCKED FOR PHYSICAL EXECUTION.** Approved as a DOCUMENTARY HANDOFF ONLY when reviewed. Source: Issue #822, PR #834, proposed ADR `docs/adr/ADR-qa525-signed-root-bootstrap-and-recovery.md`. This runbook **is not itself authorization** for any privileged action.

## 0. Scope, truth and ownership

- Sole physical QA host: `glmdch` (no additional VM; `srv01` is not a physical fallback).
- Cut **only** the launcher `/usr/local/sbin/pmgm-qa-update`. Do not deploy API/WEB; do not run EF migrations; do not modify Docker Compose, Keycloak or SMTP; never touch production or `main`.
- Source of truth: exact live GitHub `dev` HEAD, seven required PR checks on the exact branch commit, SHA-matching published GitHub **push** release, ZIP digest, Sigstore identity and signed manifest; plus root-owned current host evidence and approved G6 recovery.
- Runtime installer PR #834 is only proposed code. Running that script from a PR branch or copying it to root is **forbidden**. Its current `bootstrap_trusted_sources()` checks ownership/mode, not signed source hashes. No trusted first-execution bootstrap has been evidenced in this chat; thus no root command is authorized.

## 1. Prerequisites gate — all mandatory, no auto-approval

An authorized reviewer records an immutable, timestamped evidence packet with:
1. Exact `dev` commit, PR/merge and CI check IDs; publisher `event=push` and source SHA identical to `dev`. Refuse any workflow_dispatch substitute.
2. Published `qa-signed-<SHA>` tag targeted to same SHA; downloaded ZIP hash independently matching immutable GitHub release metadata, nested bundle identity, GitHub Sigstore attestation against exact repository/source/signer workflow, and signed `MANIFEST.sha256` covering ALL privileged helper modules.
3. A *separately trusted* bootstrap program already installed or authenticated **before** it imports/executes any Python from the ZIP; a signed archive cannot validate its own bootstrap. Every executable's local SHA must match the independently verified manifest before first root execution.
4. Current `glmdch` immutable inventory: hostname/UID, clock, root-owned 0700 stages and journal directory, five unchanged QA lock paths, existing launcher mode 0700 and SHA, current operator path/SHA, no other running QA operator; no unresolved intent.
5. Fresh, verified G6 backup and isolated/offhost restore appropriate to this cut. A historical 29-Oct restore does not establish a current backup.
6. Separate root0600 launcher-only approval packet from the actual operator: exact source SHA, release digest, old/new operator hashes, action/scope/operator, TTL at most 900 s and novel nonce. No shortcut by CI or an old approval.
7. An authorized person explicitly approves the **physical launcher-only** action after inspecting the complete packet. Approval does **not** include API/WEB or archival of previous prepared artifacts.

If any prerequisite is missing, expired, inconsistent, failed, inaccessible, or cannot be proven: record BLOCKED; **do not execute**.

## 2. One authorized transaction (planned, not enabled)

Only after the separately verified bootstrap and gates above are proven should that bootstrap invoke the pinned, root-owned installer once. It must hold all five QA locks; compare live launcher to previously recorded hash; durably write unique intent then immutable backup; persist/rehash complete signed source tree; stage exact SHA-pinned launcher; re-read approval and expiry at last pre-swap boundary; atomically replace + fsync; verify actual live bytes and retained source; persist receipt then clear intent. Failure must not be hidden, retried, or auto-advanced to API/WEB.

Capture without secrets: timestamp, exact dev/release/asset/manifest/installer SHA, G6 receipt hash and restore test result, lock/journal state, nonce *hash only* in public logs, old/new launcher hashes, executor exit code and post-readonly evidence. Do not copy private approval JSON, SMTP/Keycloak credentials or raw secrets to GitHub/Drive.

## 3. Read-only postflight and Go/No-Go

Require every item: single known journal state; actual launcher hash equals approved target; retained source SHA/source digest hashes match signed manifest; newly pinned launcher can be inspected without code execution; five locks released; no API/WEB/DB/identity/configuration drift; public `/health/web`, `/health/ready` and identity endpoint remain as before; G6 restore remains usable. Only then label **LAUNCHER_REPLACED_ONLY**. Do not label QA_DEPLOYED or UAT_PASSED.

If there is a drift or unexpected file, mark STOP + MANUAL_RECONCILIATION_REQUIRED, preserve intent and backup and seek separate authorization before any replacement or rollback.

## 4. Crash and rollback decision table

| Observed state (with root-owned read-only inspection) | Decision | Forbidden action |
| --- | --- | --- |
| Intent present, backup absent/invalid | BLOCKED: forensic reconciliation | Do not replay/download/approve |
| Old live hash, valid backup, intent pending | STOP: interrupted before swap; explicit signed review required | Do not rerun transaction automatically |
| New live hash, valid old backup, intent pending | STOP: swap occurred but receipt incomplete; independent review | Do not report success or blindly roll back |
| Receipt exists but intent persists | STOP: uncertain durable commit; compare every identity field | Do not delete pending marker automatically |
| Receipt committed, launcher matches, no intent | Observe/verify, not a new installation | Do not reuse nonce/approval |
| Unknown live bytes, mismatched old backup or mixed provenance | STOP + preserve host evidence and G6 | Do not overwrite launcher or continue |
| Missing actual Sigstore verification, stale G6, expired approval | BLOCKED before root operation | Do not use public SHA metadata as authorization |

**Supervised rollback is not yet implemented/reviewed:** a future rollback must require independently verified old snapshot, separate root-only approval, five locks, journal/receipt, durable fsync, atomic replacement, no nonce reuse, and tests for process death during reversal. Merely copying back an old file is prohibited. The read-only classifier in PR834 is NOT a recovery executor.

## 5. Continuity and handoff

Record PRE/DURANTE/POST in GitHub Issue #822 and the official Proyecto Centenario Drive state log. Include exact evidence SHA/check IDs, host change/no-change, unresolved issues and next action. Do not claim `glmdch` updated without operator receipt and physical readback. No second VM, no `srv01`, no main/prod or PR #771/#737/#766.

## Pre-import admission — additional no-go condition (1 Nov 2026)

The draft root installer now checks a private `/etc/pmgm/qa525-bootstrap-admission.json` (root-owned, mode 0600) before importing any of its helper modules. A separately authenticated bootstrap must write this packet only AFTER it verifies the exact source, signer workflow, GitHub Sigstore provenance, signed manifest and every imported file hash. The packet must include exact current release SHA/digest and hashes for all privileged sources; it will be cross-checked again against signed preflight identity. A missing, invalid, stale or edited packet stops the installer. The installer **cannot issue its own admission**. Important: even a valid packet does NOT prove that the installer entrypoint itself was trusted before it began executing; that separate pre-execution bootstrap remains a mandatory, unaudited prerequisite. Do NOT copy a sample packet into /etc as a workaround, and do not run downloaded PR Python as root.

## Draft read-only interrupted-cut diagnostic (1 Nov 2026)

The PR #834 installer now contains a separate mutually exclusive CLI flag `--inspect-pending` for **one** unfinished journal transaction. It calls `bootstrap_trusted_sources()` (external admission, root ownership, exact local file SHA) **before** the inspection function, then acquires the five QA locks and returns only `MANUAL_RECONCILIATION_REQUIRED_NO_AUTOREPAIR` with an observed pre/post swap state. If admission is absent, expired by policy, invalid, or files diverge, the diagnostic refuses to read. It never downloads a package, consumes approval, swaps a launcher, authorizes rollback or deploys API/WEB. Strict receipt checks require exact fields including candidate operator SHA-256 and a matching source/digest; unknown journal filenames, equal old/new hashes and unexpected receipt fields are rejected.

**Not a command to run on glmdch yet.** Although the flag is read-only, the privileged Python entrypoint itself must be independently authenticated **before first root execution**, and the external Sigstore bootstrap plus G6/restore/physical operator authorization remain unverified. Do not run downloaded GitHub code by copying this documented option into a shell.

## No-write shared lock policy for read-only classifier — 1 Nov 2026

The `--inspect-pending` path now acquires the same five exclusive advisory `flock` locks with `all_five_locks(read_only=True)` instead of the installer's ordinary create-capable lock path. In diagnostic mode every lock is opened `O_RDONLY | O_NOFOLLOW | O_CLOEXEC` **without `O_CREAT`**; a missing lock blocks with `ROOT_READONLY_SHARED_LOCK_MISSING` and no file creation. The installer path retains its existing `O_RDWR | O_CREAT` behavior. Both modes validate root ownership/mode, absence of hardlinks/symlinks, and fd-vs-path inode equality; both hold all five `LOCK_EX | LOCK_NB` locks before inspecting, with no bypass if another operator owns one.

This is a narrow guarantee about shared lock files and installer journals. Reading source/journal may affect filesystem access-time metadata depending on mount behavior, so do not equate this with full block-device forensics. The CLI remains **BLOCKED for physical use** until its own entrypoint is independently verified before privileged execution, regardless of 42 hermetic tests passing.


## Audit update — external bootstrap admission freshness and physical evidence (1 Nov 2026)

**Independent verification already observed:** the operator on `glmdch`, without sudo, reported `sha256sum --check: OK`, `ZIP_COMPONENTES_CONTROLADOS=PASS` and `gh attestation verify ... --source-digest f457628d7ecd185bde5408f6324725ec57372bfb --deny-self-hosted-runners: PASS` against the inner installable in release `qa-signed-f457628d...`. This verifies that **specific published installable's provenance only**. It does **not** attest the draft installer in PR #834, validate every nested signed-manifest member, verify the entire root bootstrap, or grant any launcher/API/WEB action. The downloaded archive was in a disposable unprivileged directory and removed afterward.

**G6 evidence refined:** seven isolated restores dated 29–31 October reported `G6_SAME_HOST_ISOLATED_RESTORE_PASS_NOT_DISASTER_RECOVERY`, with `production_touched=false`; their reports explicitly say `disaster_restore_other_host=PENDING`, `atomic_cross_resource_snapshot=NOT_PROVEN`, `iam_restore=NOT_INCLUDED`. Therefore same-host recovery PASS cannot be described as independent disaster recovery or current cut readiness.

**New external-admission interface in draft PR #834:** an independently authenticated bootstrap, **only after** validating a future signed release containing the exact installer and all twelve `SOURCES`, must issue a root-owned `0600` `/etc/pmgm/qa525-bootstrap-admission.json` with exactly the prior signed identity fields PLUS two strict integer UNIX timestamps: `issued_at` and `expires_at`. Require `issued_at <= current_UTC_epoch < expires_at` and `0 < expires_at - issued_at <= 900` seconds. Booleans, missing fields, future issuance, expired timestamps and longer validity fail closed before helper import. `replace_approved()` rejects an expired external admission **again immediately before the atomic swap**, preserving the journal if already written. The independent verifier must never treat this signed-source admission as launcher replacement authorization; the separate root0600 approval's nonce and TTL are still mandatory.

**Remaining pre-root bootstrap STOP:** this branch cannot authenticate its own interpreter or first line of code. There is still no independently provisioned and locally audited root trust anchor for running PR #834 itself. An external verifier must establish signer identity, exact executable and signed manifest **before first root Python execution**, and an authorized operator must review the static privileged bootstrap path. Do not run the draft installer with sudo, do not create a sample admission packet to satisfy its shape, do not elevate the unprivileged verification directory, and do not bypass recovery/approval gates. No physical launcher/API/WEB change has been made.


## Verifier integration — unprivileged signed-source admission PREPARATION (1 Nov 2026)

PR #834 adds `scripts/qa/operators/qa525-attested-installer-audit.py` and hermetic tests `tests/test_qa525_attested_installer_audit.py`, required by the PR-specific offline CI workflow. This **never runs as root** (rejects UID 0), never imports/executes signed source and never writes `/etc/pmgm` or a bootstrap-admission file. It takes a **previously downloaded local** envelope ZIP, exact source SHA and expected envelope SHA-256; hashes the complete outer ZIP, checks only the three canonical outer members, authenticates the inner installable with GitHub Sigstore (exact repository, signer workflow, source digest and denial of self-hosted runners), parses the signed complete `MANIFEST.sha256`, rejects noncanonical paths/symlinks/duplicates/extras, verifies SHA-256 of **every** nested package file, and requires the **12 precise** installer/helper source files before returning `SIGNED_INSTALLER_AUDITED_UNPRIVILEGED_NO_INSTALL` with their hashes. Output is evidence, **not a trusted root bootstrap**, rollout permission or an admission packet.

Negative tests cover incorrect outer digest before `gh`, failed Sigstore attestation, missing signed installer, modified helper after manifest, mismatched source SHA, altered envelope members, traversal, symlink and accidental UID0. The checker only becomes applicable to the installer after a **future** signed, exact-dev release actually includes the merged PR #834 code; the already authenticated 1 Nov `f457628d...` release **does not include the draft installer**, so cannot authorize it.

**Critical remaining STOP:** source hashes printed by an unprivileged verifier are not root trust by themselves. A **separately reviewed and authenticated** promotion mechanism must bind those hashes to the first root command, securely recheck every staged byte under exclusive host locks, and refuse any race or identity drift. No signed release verification, CI or JSON report can authorize installing, running, or copying a PR branch's downloaded Python with sudo. No launcher/API/WEB operation should be attempted before that independent trusted bootstrap, G6/rollback gates and explicit human approval.
