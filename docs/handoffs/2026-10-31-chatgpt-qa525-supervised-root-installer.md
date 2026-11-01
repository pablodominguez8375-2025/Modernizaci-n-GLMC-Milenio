# QA525 #822 — Supervised root launcher installer (31-10-2026)

## PRE / ownership and safety
ChatGPT IA. Claimed branch from live dev@97165b52282dca51a313233fbb8f5d59b695455b; main@6dfb9546a4873baff15955cf86abfd7d47e3d111. Issue #822 OPEN. #827 signed publication OPEN. PR #833 in separate review changes root-source inspector and QA525 workflow; this PR must avoid those same paths until #833 merged and CI succeeds.

**Goal:** develop the real one-shot replacement entrypoint as **source code for independent review**, never execute it on glmdch in this session. Fail closed without root/glmdch, exact fresh public GitHub signature/manifest/source gate, separate root0600 launcher-only approval (TTL/nonce), five shared QA operator locks, existing trusted old launcher, root-owned retained package tree, durable intent and backup, atomic swap and receipt plus fsync, no automatic crash reconciliation. Signed external bootstrap and physical host authorization remain essential.

**Hot paths:** only a new installer entrypoint and its tests/handoff in this branch. Do not edit the PR #833 workflow until it merges. No app/API/WEB, database/EF100, Docker/Compose, Keycloak/SMTP, G6, prod/main/srv01, second VM or PR771/737/766.

## Trust boundary
A raw Python script from GitHub must **never** run with sudo. Physical execution requires the signed package to have been independently verified and root-owned before loading this module, then runtime checks independently verify current HEAD/release/GitHub Sigstore and package manifest. The installer must never claim that CI alone proves release or host readiness.

## DURANTE / POST
Record exact files, commits, adverse tests, CI and postmerge SHA, G6 and host verification limitations; GitHub + Drive mirror. Remain DRAFT if any check fails. #822 remains open until a successful authorized physical rollout and UAT.

## DURANTE — tangible code, tests and security review

- PR **#834 DRAFT** https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/pull/834, based on dev@97165b52282dca51a313233fbb8f5d59b695455b. PR #833 still owns the existing QA525 hermetic workflow and root-retained-source inspector; this PR uses a **new separate workflow** to avoid file collision.
- New `scripts/qa/operators/qa525-launcher-supervised-root-install.py` initially commit `ddf261262bc1b69be834b3e4d5179d9900886594`. Explicit only `--replace-approved`; require actual root/glmdch, validate root-owned trusted source paths before imports, use five exact QA operators' shared locks, read old launcher's SHA, independent signed preflight and re-download+manifest verification, distinct `/etc/pmgm/qa525-launcher-replace.json` root0600 approval and nonce gate, retain the signed package under fixed root-only SHA, durable intent + backup + staged launcher via O_EXCL/fsync, replace launcher atomically, fsync, durable receipt, no API/WEB/EF100/Compose or Docker. If an intent remains on any interruption, abort all retries and require human reconciliation; **no automatic rollback is implemented yet**. Do not physically run pending independent source-authentication/security review and G6.
- Security improvements commit `9c3e47f46781c45c612688bbb15de819bb47a86f`: re-read entire approval after second signed download and require SAME one-time nonce. Further commit `f9b72dfaa9fcad09e34b717120aff18d06bbdbf0`: revalidate exact approval and expiry immediately before atomic swap.
- New adversarial tests `tests/test_qa525_launcher_supervised_root_install.py` commit `949918e05b31b6fe108f5697b03017098669496f`, check wrong host, missing signature, denied approval, rejected policy, exclusive durable file, pending journal and shared five lock names. New independent workflow `.github/workflows/qa525-root-launcher-installer-review.yml` commit `fcaeaf87dc23aec6931e5bdfe270e1929209ba05`.
- Initial root-installer review workflow #41265300532 had 8/9 tests PASS, 1 FAIL in an overbroad static assertion: documentation that *forbids* Docker matched `assertNotIn("docker", script.lower())`. Corrected in test commit `492c47bc6baf3eaaf7970b7973755004ca05aeba` to look for executable Docker/subprocess calls; no implementation weakening.
- Next review workflow #41265348839 also had 8/9 PASS, 1 FAIL because static AST test counted direct Python functions `write_once` as method calls. Corrected in `6a67915a1eb93bdf71248446d426873c7c058932` by checking attribute and direct function invocations separately. All failures transparently retained in CI history, no host changes.
- CI of final source commit `f9b72dfaa9fcad09e34b717120aff18d06bbdbf0` must be independently checked, including new offline reviewer and required global PR gates. Do not inherit SUCCESS from previous HEAD.

## POST status / explicit remaining blockers

This is a **review-only source candidate**, not a certified signed installer or authorized physical cut. Outstanding for production-quality readiness: complete hermetic process-death rollback/reconciliation tests with real file barriers (not only mocked gate tests), independent review of root and Sigstore bootstrap, successful exact-head CI, actual signed published QA release (#827), root-owned installed package/lock/state validation on the physical single host, fresh G6 backup plus restore/offhost evidence, and sponsor authorization for launcher-only replacement. Never merge or physically run merely on the basis of the static tests. No second VM, no production/main/srv01/PR771/737/766, no EF100 rerun.
