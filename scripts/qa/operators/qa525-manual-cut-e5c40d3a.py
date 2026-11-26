#!/usr/bin/env python3
"""QA525 manual schema-cut DRIVER for e5c40d3a (EF 100->104 + API/WEB).  DRAFT SKELETON: NOT AUTHORISED, NOT A GO.

Thin wiring over the already reviewed libraries (loaded by exact sha256 pin, never imported by name from a search
path): schema-cut-executor, schema-cut-ports, schema-adapter, schema-cut-plan, schema-cut-journal-store/contract and
go-nonce-journal.  It adds ONLY: a pinned library loader, a closed-argv runner (exact tuples, fixed env, no shell),
the five-lock plane, the single-use marker (go-nonce-journal.consume, O_EXCL+fsync, BEFORE any mutation) and the
modes.  It never invokes root-schema-effects (INACTIVE) nor any launcher/GO machinery.  Fixed diagnostic codes only:
no output, argv, path or exception text of a collaborator is ever echoed.

Modes: no argument = A0 (read-only; no marker, no lock held, no write); `EXECUTE_MANUAL_CUT_E5C40D3A` = single run.
Steps whose reviewed implementation is not wired yet (G6 evidence, API/WEB flight, UAT) FAIL CLOSED with a fixed STOP,
so this skeleton cannot mutate anything beyond what is explicitly wired and tested.
"""
import fcntl
import hashlib
import importlib.util
import os
import stat
import subprocess
import sys

SOURCE_SHA = "e5c40d3af861045ea6ae19356d7cd6054924a4d9"
EXECUTE_TOKEN = "EXECUTE_MANUAL_CUT_E5C40D3A"
DOCKER = "/usr/bin/docker"
ENV = {"PATH": "/usr/sbin:/usr/bin:/sbin:/bin", "LC_ALL": "C"}
MAX_OUT = 1 << 20
# sha256 of each reviewed library; ALL seven verified against the signed release e5c40d3a inner zip (attested, checked
# off-host).  go-nonce-journal is byte-identical in da274d65 (host dir, S2c) and e5c40d3a.
LIB_PINS = {
    "qa525-vnext-schema-cut-executor.py": "3d4fec097c192fd9d793a3722ebd92e32276507fd5329d245784397137c77b79",
    "qa525-vnext-schema-cut-ports.py": "5efb0245edd4abbdc73c9462ec6a0205862b2fde25bd5badc19210a9b881df52",
    "qa525-vnext-schema-adapter.py": "059c4e520baabbe9bd908023458289832b27a0d053d056aeebdfea50265115a5",
    "qa525-vnext-schema-cut-journal-store.py": "24c24a10a2cecf0af48ff1f1ce46f7e2d2e4460fc91ceb4343f9614735a885cb",
    "qa525-vnext-schema-cut-journal-contract.py": "646dc440114d5af3235a08a86e13737a8a84924f8a2664572d4d1075e60eb177",
    "qa525-vnext-schema-cut-plan.py": "518ed4016c53b0d6d14069d960d46b6ab625e583bafac3061e8757feccacd471",
    "qa525-vnext-go-nonce-journal.py": "65179306bddba301949fce0347bead2080ed8d715dd7b941382ed7e79d1a3562",
}
LOCKS = (
    "/opt/centenario/qa-api-refresh/operation.lock",
    "/opt/centenario/qa-auto-lite/web-update.lock",
    "/run/lock/pmgm-master-qa-604.lock",
    "/run/lock/pmgm-qa754-web-hotfix.lock",
    "/run/lock/pmgm-qa754-api-activation.lock",
)


class Stop(Exception):
    """Fixed code only."""


def _read_exact(fd, size):
    """Read exactly `size` bytes; callers probe one extra byte for truncation/overflow."""
    chunks = []
    remaining = size
    while remaining:
        chunk = os.read(fd, remaining)
        if not chunk:
            raise Stop("CUT_LIB_READ_INCOMPLETE")
        chunks.append(chunk)
        remaining -= len(chunk)
    return b"".join(chunks)


def load_libs(lib_dir, pins=None):
    """Load every reviewed library by exact pin from `lib_dir` (absolute, root-owned). Unpinned (None) => STOP."""
    pins = LIB_PINS if pins is None else pins
    if not isinstance(lib_dir, str) or not os.path.isabs(lib_dir):
        raise Stop("CUT_LIB_DIR_INVALID")
    mods = {}
    for name, want in sorted(pins.items()):
        if want is None:
            raise Stop("CUT_LIB_PIN_MISSING")
        path = os.path.join(lib_dir, name)
        try:
            fd = os.open(path, os.O_RDONLY | os.O_NOFOLLOW | os.O_NONBLOCK | os.O_CLOEXEC)
        except OSError:
            raise Stop("CUT_LIB_UNREADABLE") from None
        try:
            st = os.fstat(fd)
            if not stat.S_ISREG(st.st_mode) or st.st_nlink != 1 or st.st_mode & 0o022 or st.st_size > (1 << 21):
                raise Stop("CUT_LIB_UNSAFE")
            data = _read_exact(fd, st.st_size)
            if os.read(fd, 1):
                raise Stop("CUT_LIB_TOO_LARGE")
        finally:
            os.close(fd)
        if len(data) != st.st_size or hashlib.sha256(data).hexdigest() != want:
            raise Stop("CUT_LIB_PIN_MISMATCH")
        spec = importlib.util.spec_from_loader("qa525_" + name[:-3].replace("-", "_"), loader=None)
        mod = importlib.util.module_from_spec(spec)
        exec(compile(data, name, "exec"), mod.__dict__)  # noqa: S102 - exact pinned bytes only, never a search path
        mods[name] = mod
    return mods


def make_runner(allowed):
    """Closed-argv runner: only EXACT tuples in `allowed` (frozenset of tuples starting with /usr/bin/docker)."""
    allowed = frozenset(tuple(a) for a in allowed)
    if not allowed or any(a[0] != DOCKER or any(type(x) is not str for x in a) for a in allowed):
        raise Stop("CUT_ALLOWLIST_INVALID")

    def runner(argv, timeout, stdin=None):
        argv = tuple(argv)
        if argv not in allowed:
            raise Stop("CUT_ARGV_NOT_ALLOWED")
        if stdin is not None and type(stdin) is not bytes:
            raise Stop("CUT_STDIN_INVALID")
        proc = subprocess.run(argv, input=stdin, stdout=subprocess.PIPE, stderr=subprocess.DEVNULL, env=ENV,
                              shell=False, timeout=timeout, check=False)
        return proc.returncode, proc.stdout[:MAX_OUT + 1]
    return runner


class Plane:
    """Five legacy locks held LOCK_EX|LOCK_NB for the whole run; `assert_held` re-validates fd vs path identity."""

    def __init__(self, paths=LOCKS):
        self.paths = tuple(paths)
        self.fds = []

    def __enter__(self):
        try:
            for path in self.paths:
                fd = os.open(path, os.O_RDONLY | os.O_NOFOLLOW | os.O_CLOEXEC | os.O_NONBLOCK)
                self.fds.append((fd, path))
                st = os.fstat(fd)
                if not stat.S_ISREG(st.st_mode) or st.st_nlink != 1 or st.st_mode & 0o077:
                    raise Stop("CUT_LOCK_UNSAFE")
                fcntl.flock(fd, fcntl.LOCK_EX | fcntl.LOCK_NB)
        except Stop:
            self.close()
            raise
        except OSError:
            self.close()
            raise Stop("CUT_LOCK_BUSY_OR_MISSING") from None
        return self

    def assert_held(self):
        if len(self.fds) != len(self.paths):
            raise Stop("CUT_PLANE_LOST")
        for fd, path in self.fds:
            try:
                a, b = os.fstat(fd), os.stat(path, follow_symlinks=False)
            except OSError:
                raise Stop("CUT_PLANE_LOST") from None
            if (a.st_dev, a.st_ino) != (b.st_dev, b.st_ino) or a.st_nlink != 1:
                raise Stop("CUT_PLANE_LOST")

    def close(self):
        for fd, _ in self.fds:
            try:
                os.close(fd)
            except OSError:
                pass
        self.fds = []

    def __exit__(self, *exc):
        self.close()
        return False


def build_allowlist(mods, spec, params):
    """EXACT argv tuples the reviewed libraries may issue for this cut (derived from their own builders)."""
    ad = mods["qa525-vnext-schema-adapter.py"]
    pt = mods["qa525-vnext-schema-cut-ports.py"]
    allowed = set(ad.plan_commands(params).values())
    pg = spec["pg_container"]
    allowed.add(pt._psql(pg, pt.HISTORY_SQL))
    allowed.add((DOCKER, "image", "inspect", "--format", "{{.Id}}", spec["migrator_image_id"]))
    for image in spec["images"].values():
        allowed.add((DOCKER, "image", "inspect", "--format", pt.REVISION_FORMAT, image))
    roles = ",".join("'" + r + "'" for r in spec["write_roles"])
    sessions = ("SELECT count(*) FROM pg_stat_activity WHERE datname = 'pmgm' AND usename IN (" + roles
                + ") AND pid <> pg_backend_pid()")
    allowed.add(pt._psql(pg, sessions))
    allowed.add((DOCKER, "stop", "--time", "30", spec["api_container_id"]))
    allowed.add((DOCKER, "inspect", "--format", "{{.State.Running}}", spec["api_container_id"]))
    return frozenset(allowed)


def build_steps(mods, runner, spec, params, collab):
    """Wire the reviewed ports.  `collab` supplies what only the host knows: clock, abort, verify_backup inputs,
    flight and uat checks.  Any missing collaborator => the step FAILS CLOSED (never silently skipped)."""
    pt = mods["qa525-vnext-schema-cut-ports.py"]
    ad = mods["qa525-vnext-schema-adapter.py"]
    need = ("clock", "abort", "read_evidence", "verifier", "evidence_pins", "archive_epoch", "flight",
            "target_web_sha", "checks", "expected_104_sha256")
    if type(collab) is not dict or any(k not in collab for k in need):
        raise Stop("CUT_COLLABORATORS_INCOMPLETE")
    return {
        "preflight": pt.make_preflight(runner, collab["clock"], collab["abort"], spec),
        "verify_backup": pt.make_verify_backup(collab["read_evidence"], collab["verifier"], collab["clock"],
                                               collab["evidence_pins"], collab["archive_epoch"]),
        "quiesce_writes": pt.make_quiesce_writes(runner, spec),
        "schema": pt.make_schema(ad, params, runner),
        "revalidate": pt.make_revalidate(runner, collab["clock"], collab["abort"], spec,
                                         collab["expected_104_sha256"]),
        "apply_api_web": pt.make_apply_api_web(collab["flight"], collab["target_web_sha"]),
        "uat": pt.make_uat(collab["checks"]),
    }


def _require_plane(plane):
    """Require the complete lock plane before spending the nonce or journaling."""
    try:
        plane.assert_held()
    except Exception:
        raise Stop("CUT_PLANE_NOT_HELD") from None


def run_cut(mods, plane, root, uid, now, nonce, authorization, spec, params, collab):
    """Single attempt.  Order: allowlist+steps built (no effect) -> marker consumed (O_EXCL+fsync) -> sequencer.
    The marker is spent BEFORE the first journal write or effect; any later failure never re-opens it."""
    nj = mods["qa525-vnext-go-nonce-journal.py"]
    store = mods["qa525-vnext-schema-cut-journal-store.py"]
    contract = mods["qa525-vnext-schema-cut-journal-contract.py"]
    ex = mods["qa525-vnext-schema-cut-executor.py"]
    runner = make_runner(build_allowlist(mods, spec, params))
    steps = build_steps(mods, runner, spec, params, collab)
    plan_sha = authorization["plan_sha256"]
    bound5 = {"app": "sha256:" + hashlib.sha256(SOURCE_SHA.encode()).hexdigest(), "ops": authorization["driver_sha256"],
              "policy": authorization["policy_sha256"], "plan": plan_sha, "admission": authorization["admission_sha256"]}
    _require_plane(plane)
    nj.consume(root, nonce, bound5, uid=uid, now=now, expires_at=now + 3600)  # spent only after locks are held
    jbinding = {"nonce": nonce, "go_sha256": authorization["admission_sha256"], "plan_sha256": plan_sha}

    def journal(phase):
        return store.append(root, jbinding, phase, uid=uid, plane=plane, contract=contract, nonce_journal=nj)
    return ex.run(journal, plane, steps)


def unwired(name):
    def step(*_a):
        raise Stop("CUT_STEP_NOT_WIRED_" + name)
    return step


def main(argv=None):
    argv = sys.argv[1:] if argv is None else argv
    if argv not in ([], [EXECUTE_TOKEN]):
        return 2
    if argv == [EXECUTE_TOKEN]:
        sys.stdout.write("QA525_MANUAL_CUT=STOP CUT_EXECUTE_NOT_AUTHORISED_SKELETON\n")  # fail closed until reviewed
        return 1
    sys.stdout.write("QA525_MANUAL_CUT_A0=NOT_WIRED_SKELETON\n")
    return 1


if __name__ == "__main__":
    sys.exit(main())
