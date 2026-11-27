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

## `main --g6` (habilitado por el commit de habilitación: `G6_MODE_ENABLED=True`; antes OFF por defecto)
Grammar (closed): `--g6 <preflight|init|backup|restore|review> --authorization-sha256 <hex64> [--name value]...`. One command per process.
Order before dispatch: mode flag → closed parse → pinned modules → operation hash → env (host/root checks) → 5 locks held →
authorization-text marker `auth-<hex64>.spent` (O_EXCL+fsync; same text cannot be reused) → fresh random nonce consumed (O_EXCL+fsync)
→ dispatch. After a successful `review` only: `qa525-a0-g6-params-v1.json` is written exclusively (0600) bound to
operation/command/nonce/GO. Journal root `/var/lib/pmgm-qa525-manual-cut-e5c40d3a/journal` and lib dir
`/opt/centenario/qa525-manual-cut-e5c40d3a/lib` must pre-exist (created by their own authorised gates; never by the driver).

## Gate G6-0 (design only, NOT authorised): retire the 13-nov G6 tree
`init` refuses to run while `repo-v2`, `key-v2`, `state-v2` exist. Candidate: preflight that the `.retired-20261113` target does not
exist and owner/mode are root/0700; one reversible `mv`, no delete/overwrite; verify afterwards with the A0-G6 inventory.
Requires its own literal Sponsor authorization; does not chain G6-1..G6-6.

## Actualización (opción A, dictamen 7225915717)
La frontera ahora incluye un GO firmado con la clave OPS (ver `qa525-g6-signed-go.md`): `--go/--go-sig`, nonce del GO, TTL ≤ 900 s. `--authorization-sha256` se conserva solo como marcador `auth-<hex64>.spent` por texto literal.
