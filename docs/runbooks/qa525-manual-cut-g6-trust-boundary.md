# QA525 manual cut — G6 mode: trust boundary (route C)

Status: documentation of the driver `scripts/qa/operators/qa525-manual-cut-e5c40d3a.py`. Not an authorization. G6 mode is OFF in `main`, A0 is read-only, EXECUTE is fail-closed, `QA_DEPLOY=NO_GO`.

The driver does NOT call or fabricate the bootstrap `Verified` object. Its equivalent authority is composed, in this order, before any dispatch:

1. Exact pinned bytes: every G6 module and library is loaded only from the fixed pin table (`LIB_PINS`, `G6_PINS`); unknown names or any hash mismatch → STOP.
2. Validated admission/GO: scope `G6_V2_ONLY`, one command per GO, `g6_operation_sha256` and `g6_command` must match the canonical operation built from the pinned runner grammar.
3. Host/root checks and the environment built from pinned bytes (`build_g6_env`), never from the host tree.
4. Locks (the 5 legacy locks) held by the plane (`assert_held`).
5. Nonce consumed after locks and before effects (`.spent` marker per command).
6. Journal marker written before dispatch.

Per command (preflight, init, backup, restore, review): its own nonce, its own GO and its own literal Sponsor authorization; never chained. Restic keys never appear in stdout/stderr, journal markers or the `qa525-a0-g6-params-v1` file. Params file: exclusive create, 0600, no-follow, fsync, never overwritten, bound to operation/command/nonce/GO.

Negative coverage: `tests/test_qa525_manual_cut_g6_adapter.py` and `tests/test_qa525_manual_cut_driver.py` (incomplete/crossed admission, inconsistent or unpinned modules → STOP before dispatch, no nonce consumed).
