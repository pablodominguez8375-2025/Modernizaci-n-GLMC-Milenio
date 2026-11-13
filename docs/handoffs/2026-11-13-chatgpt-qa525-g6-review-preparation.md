# QA525 G6 review preparation — 164a07ec

Issue #525 / PR #1007. PREPARATION ONLY / NO MERGE / NO EXECUTION.

Base dev and observed current dev: 164a07ecd36eb45428b6aeac4854a17e339ca525. main: 6dfb9546a4873baff15955cf86abfd7d47e3d111. Neither branch modified.

Isolated restore completed with exit 0 and production_touched=false. Operator readback: pmgm=120 tables, pmgm_keycloak=100 tables, silo_objects_verified=34; downloaded archive SHA256 b0c77c1c26ba48e1c6942abab3ea5d5b1a9aaa3228cbd65753bc274a775276fe; review absent. Receipt/report custody checked by operator read-only block; full receipt output not included in latest chat excerpt, so GO-H independently revalidates it before consent or GO installation.

## Final implementation
Four NEW scripts under ops/qa525/164a07ec/review derive from reviewed PR #1006 HEAD 336073c0b9ce5fdced5e803c822865abfe0037ed, without modifying that branch or any runtime module. Separate command=review, kit directory, root inbox, config, admission, manifest PIN and short-lived signed GO. Exact runner argv includes archive-name, fixed Drive file ID, repo-id and key-epoch. GO-H pre-validates canonical receipt/restore, root custody, ciphertext hash, absence of existing review/plaintext/verifier, service health and 48-hour window with 30-minute margin. Post-validation requires unchanged inputs and a canonical review tied to the exact Drive ID.

Two Drive IDs resolved by downloading and hashing actual ciphertext bytes: 1THaJaMm0sKRi1_VGp1eqOBuyzgQP54aT and 1j7SvSnrJ3oR0Xzw_EnBVPT7FEkx3Co1H. Both 262909 bytes with the archive hash above. The former is fixed in this chain per Claude contract #525 comment 6867526741. No files moved, deleted or shared.

## Raw script hashes
```text
450ab11045f24406fd253b2063e22de517dd0b58379e31a9d33f3fd8b9c4d233  ops/qa525/164a07ec/review/qa525-g6-review-go-termux-164a07ec.sh
65a546155f38b248883e19aa9ac264d32eb6c6a9702294022d006361c96d4256  ops/qa525/164a07ec/review/qa525-g6-review-kit-B-164a07ec.sh
a89dd00d1345760bbc8abedb026c485d31cb6f10242ee484b824fb8bea3cfbdf  ops/qa525/164a07ec/review/qa525-g6-review-kit-A-164a07ec.sh
25582663f2b50d56b02493141f9d3aef755063a5e2c4202e19f661bd7e6b4abc  ops/qa525/164a07ec/review/qa525-g6-review-go-host-164a07ec.sh
```

## Validation
Ten local sandbox tests PASS: shell syntax/embedded Python, exact canonical evidence, malicious or inconsistent evidence, symlinks/hardlinks/mode, old commands, wrong cut, expired/short/future GO, real ephemeral SSH signing/verification, exact one-time finalize_review from the actual dev producer against fixtures, unchanged inputs and O_EXCL rejection of repeat writes.

NOT proved here: full privileged GO-H orchestration, physical root custody, actual Docker/Restic, real Termux/sudo/PTY/signals/FD pipeline or Kit A/B on the real release. Sandbox reader substitutes UID/GID in test copies only. Runtime producer was read from dev unchanged and is not modified by this PR.

## Pending
Independent review of the exact PR HEAD, including full mocked host orchestration, required before presenting root execution. Then Kit A/B/read-only physical preparation, separate Sponsor authorization and fresh review GO. No global deployment GO, merge, restore repeat, backup repeat, identity/production/API/WEB/dev/main changes authorized.

Docs, tests and PR are the deliverable. Pages and QA installable remain at their existing cut; no application/UI change or deployment. Final record also appended to Drive Estado Maestro.
