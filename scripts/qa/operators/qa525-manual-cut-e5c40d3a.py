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
# Existing G6 v2 evidence checker (blob identical to dev@e5c40d3a; to be re-cotejado against the attested inner zip).
G6_LIB = "qa525-vnext-g6-v2-evidence.py"
G6_PIN = "add8e206d12a5814612db0e77e9438d4a885520c724e1714734b8e5ef4697f32"
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


def make_g6_verifier(g6, facts):
    """verifier(data)->bool for `ports.make_verify_backup`: reuses the EXISTING G6 only if the reviewed evidence checker
    accepts it for THIS cut (pins, receipt, review, restore, 48 h window covering the admission).  Authenticity of the
    archive/off-host hashes, repo id and key epoch is supplied by the caller from physical, independent reads (`facts`);
    this function never reads the host.  Any exception => False (closed STOP in the reviewed library)."""
    need = {"pins", "observed_archive_sha256", "downloaded_offhost_sha256", "expected_restic_repo_id",
            "expected_key_epoch", "admission_expires_at", "clock"}
    if type(facts) is not dict or set(facts) != need or not callable(facts["clock"]):
        raise Stop("CUT_G6_FACTS_INVALID")
    pins = facts["pins"]
    if type(pins) is not dict or set(pins) != {"review", "receipt", "restore"}:
        raise Stop("CUT_G6_FACTS_INVALID")

    def verifier(data):
        try:
            res = g6.inspect(
                review_bytes=data["review"], receipt_bytes=data["receipt"], restore_bytes=data["restore"],
                pins={"review_sha256": pins["review"], "receipt_sha256": pins["receipt"],
                      "restore_sha256": pins["restore"]},
                observed_archive_sha256=facts["observed_archive_sha256"],
                downloaded_offhost_sha256=facts["downloaded_offhost_sha256"],
                expected_restic_repo_id=facts["expected_restic_repo_id"],
                expected_key_epoch=facts["expected_key_epoch"], now=facts["clock"](),
                admission_expires_at=facts["admission_expires_at"])
            return res.get("status") == g6.STATUS and res.get("may_apply") is False
        except Exception:  # noqa: BLE001
            return False
    return verifier


def _read_meta(path, uid, modes, max_bytes):
    """Read one regular file safely and return (bytes, meta-ok).  Never follows symlinks; owner/mode checked on the FD."""
    fd = os.open(path, os.O_RDONLY | os.O_NOFOLLOW | os.O_CLOEXEC | os.O_NONBLOCK)
    try:
        st = os.fstat(fd)
        if not stat.S_ISREG(st.st_mode) or st.st_nlink != 1 or st.st_uid != uid or stat.S_IMODE(st.st_mode) not in modes:
            raise Stop("A0_G6_META_INVALID")
        if st.st_size > max_bytes:
            raise Stop("A0_G6_TOO_LARGE")
        return _read_exact(fd, st.st_size)
    finally:
        os.close(fd)


def _hash_file(path, uid, modes):
    """Streaming sha256 of a (large) regular file; same FD safety checks."""
    fd = os.open(path, os.O_RDONLY | os.O_NOFOLLOW | os.O_CLOEXEC | os.O_NONBLOCK)
    try:
        st = os.fstat(fd)
        if not stat.S_ISREG(st.st_mode) or st.st_nlink != 1 or st.st_uid != uid or stat.S_IMODE(st.st_mode) not in modes:
            raise Stop("A0_G6_META_INVALID")
        h, left = hashlib.sha256(), st.st_size
        while left:
            chunk = os.read(fd, min(1 << 20, left))
            if not chunk:
                raise Stop("A0_G6_ARCHIVE_TRUNCATED")
            h.update(chunk)
            left -= len(chunk)
        if os.read(fd, 1):
            raise Stop("A0_G6_ARCHIVE_GREW")
        return "sha256:" + h.hexdigest()
    finally:
        os.close(fd)


def a0_g6_check(g6, paths, pins, expected, offhost_sha256, admission_expires_at, clock, uid=0,
                modes=(0o600, 0o640, 0o644)):
    """Closed physical G6 check (A0 only).  Reads review/receipt/restore + the encrypted archive from `paths`
    (dict review/receipt/restore/archive -> absolute path, fixed by the reviewed A0 params, never invented), requiring
    root-owned regular files with safe modes, hashing the ARCHIVE itself (not trusting any report), and feeding the
    reviewed evidence checker.  `offhost_sha256` is the independent off-host hash (Drive download) supplied by the
    caller from a different source; `expected` = {restic_repo_id, key_epoch}.  Returns 'PASS' or 'STOP:<code>'."""
    try:
        if type(paths) is not dict or set(paths) != {"review", "receipt", "restore", "archive"}:
            return "STOP:A0_G6_PATHS_INVALID"
        data = {k: _read_meta(paths[k], uid, modes, g6.MAX_BYTES) for k in ("review", "receipt", "restore")}
        archive = _hash_file(paths["archive"], uid, modes)
        facts = {"pins": pins, "observed_archive_sha256": archive, "downloaded_offhost_sha256": offhost_sha256,
                 "expected_restic_repo_id": expected["restic_repo_id"], "expected_key_epoch": expected["key_epoch"],
                 "admission_expires_at": admission_expires_at, "clock": clock}
        if make_g6_verifier(g6, facts)(data) is not True:
            return "STOP:A0_G6_EVIDENCE_REJECTED"
        return "PASS"
    except Stop as exc:
        return "STOP:" + str(exc)
    except OSError:
        return "STOP:A0_G6_UNREADABLE"
    except Exception:  # noqa: BLE001
        return "STOP:A0_G6_UNEXPECTED"


A0_G6_PARAMS_FIELDS = frozenset({"schema", "paths", "pins", "restic_repo_id", "key_epoch", "offhost_sha256"})


def load_a0_g6_params(path, uid=0):
    """Reviewed, root-owned, canonical JSON written AFTER a G6 run; absent/invalid => closed STOP (never invented)."""
    import json
    try:
        raw = _read_meta(path, uid, (0o600, 0o640, 0o644), 65536)
    except FileNotFoundError:
        raise Stop("A0_G6_PARAMS_ABSENT") from None
    except OSError:
        raise Stop("A0_G6_PARAMS_UNREADABLE") from None
    try:
        rec = json.loads(raw.decode("utf-8"))
    except (ValueError, UnicodeError):
        raise Stop("A0_G6_PARAMS_INVALID") from None
    canon = (json.dumps(rec, sort_keys=True, separators=(",", ":"), ensure_ascii=True) + "\n").encode("ascii") \
        if type(rec) is dict else b""
    if (type(rec) is not dict or set(rec) != A0_G6_PARAMS_FIELDS or rec["schema"] != "qa525-a0-g6-params-v1"
            or canon != raw or type(rec["paths"]) is not dict or set(rec["paths"]) != {"review", "receipt", "restore", "archive"}
            or any(type(v) is not str or not v.startswith("/") for v in rec["paths"].values())
            or type(rec["pins"]) is not dict or set(rec["pins"]) != {"review", "receipt", "restore"}
            or not all(type(rec[k]) is str for k in ("restic_repo_id", "key_epoch", "offhost_sha256"))):
        raise Stop("A0_G6_PARAMS_INVALID")
    return rec


# ---- A0: read-only pre-check.  Closed output; no env, no secrets, no free text from the host is echoed. ----------
A0_PROJECT = "centenario-qa-lab2"
A0_TIMER = "pmgm-qa-lite.timer"
A0_ABORT = "/root/.qa525-schema-cut-abort"
A0_ENV_DIR = "/run/qa525-schema-env"
A0_UAT = "/root/.qa525-uat-credential"
A0_ARGV = {
    "containers": (DOCKER, "ps", "-a", "--filter", "label=com.docker.compose.project=" + A0_PROJECT,
                   "--format", "{{.ID}} {{.Names}} {{.State}}"),
    "timer": ("/usr/bin/systemctl", "is-active", A0_TIMER),
    "update_procs": ("/usr/bin/pgrep", "-c", "-f", "pmgm-qa-update|qa-lite-install|run-with-backup"),
}
_HEX64 = __import__("re").compile(r"[0-9a-f]{64}\Z")
_NAME = __import__("re").compile(r"[A-Za-z0-9][A-Za-z0-9_.-]{0,62}\Z")


def _file_mode_ok(path, mode, uid=0):
    """Existence + root-owned + exact mode, regular, single link.  Never reads content."""
    try:
        st = os.lstat(path)
    except OSError:
        return False
    return (stat.S_ISREG(st.st_mode) and st.st_uid == uid and st.st_nlink == 1
            and stat.S_IMODE(st.st_mode) == mode)


def a0_report(run, mods, spec, params, driver_sha256, lock_paths=LOCKS, uid=0, fs=None):
    """`run(argv)->(rc, bytes)` for the FIXED A0_ARGV table only; docker ones go through the closed runner.
    Returns {check: 'PASS'|'STOP:<code>'} plus an `overall`.  Never raises for a failed check; no host text echoed."""
    fs = fs or {}
    out = {}

    def check(name, fn):
        try:
            res = fn()
            out[name] = "PASS" if res is True else "STOP:" + (res if type(res) is str else "FAILED")
        except Stop as exc:
            out[name] = "STOP:" + str(exc)
        except Exception:  # noqa: BLE001
            out[name] = "STOP:A0_UNEXPECTED"

    check("driver_sha256", lambda: bool(_HEX64.fullmatch(driver_sha256 or "")))
    check("libs_pinned", lambda: all(m is not None for m in mods.values()) and len(mods) == len(LIB_PINS))

    def locks_free():
        with Plane(lock_paths) as probe:
            probe.assert_held()
        return True
    check("locks_free", locks_free)

    def timer_inactive():
        rc, data = run(A0_ARGV["timer"])
        return data.strip() in (b"inactive", b"failed", b"unknown") or "TIMER_ACTIVE"
    check("timer_inactive", timer_inactive)

    def no_updaters():
        rc, data = run(A0_ARGV["update_procs"])
        return data.strip() == b"0" or "UPDATER_PROCESS_PRESENT"
    check("no_update_processes", no_updaters)

    def containers():
        rc, data = run(A0_ARGV["containers"])
        rows = [ln.split(" ") for ln in data.decode("ascii", "replace").splitlines() if ln]
        if rc != 0 or any(len(r) != 3 for r in rows):
            return "CONTAINERS_UNEXPECTED"
        names = {r[1]: r[2] for r in rows}
        if spec["pg_container"] not in names:
            return "PG_CONTAINER_MISSING"
        return names[spec["pg_container"]] == "running" or "PG_NOT_RUNNING"
    check("containers", containers)

    allowed = build_allowlist(mods, spec, params)
    docker = make_runner(allowed)
    pt = mods["qa525-vnext-schema-cut-ports.py"]

    def images():
        for image in (spec["migrator_image_id"],):
            rc, data = docker((DOCKER, "image", "inspect", "--format", "{{.Id}}", image), 60)
            if rc != 0 or data != (image + "\n").encode():
                return "MIGRATOR_IMAGE_MISSING"
        for service, image in spec["images"].items():
            rc, data = docker((DOCKER, "image", "inspect", "--format", pt.REVISION_FORMAT, image), 60)
            if rc != 0 or data != (spec["revisions"][service] + "\n").encode():
                return "IMAGE_REVISION_MISMATCH"
        return True
    check("images", images)

    def ef100():
        rc, data = docker(pt._psql(spec["pg_container"], pt.HISTORY_SQL), 60)
        lines = data.decode("ascii", "replace").split("\n")[:-1]
        digest = "sha256:" + hashlib.sha256("\n".join(lines).encode("ascii", "replace")).hexdigest()
        return rc == 0 and len(lines) == 100 and digest == spec["baseline_sha256"] or "EF_NOT_PINNED_100"
    check("ef_history_100", ef100)

    check("migrator_env_mode", lambda: fs.get("file_ok", _file_mode_ok)(params["migrator_env_file"], 0o600, uid)
          or "MIGRATOR_ENV_NOT_ROOT_0600")
    check("grants_env_mode", lambda: fs.get("file_ok", _file_mode_ok)(params["grants_env_file"], 0o600, uid)
          or "GRANTS_ENV_NOT_ROOT_0600")
    check("uat_credential_mode", lambda: fs.get("file_ok", _file_mode_ok)(A0_UAT, 0o600, uid)
          or "UAT_CREDENTIAL_NOT_ROOT_0600")
    check("schema_env_dir_absent", lambda: not os.path.lexists(fs.get("env_dir", A0_ENV_DIR)) or "SCHEMA_ENV_DIR_PRESENT")
    check("abort_file_absent", lambda: not os.path.lexists(fs.get("abort", A0_ABORT)) or "ABORT_FILE_PRESENT")
    out["overall"] = "A0_PASS_READ_ONLY_EVIDENCE" if all(v == "PASS" for v in out.values()) else "A0_STOP"
    return out


# ---- API/WEB flight: per-instance verification, forward only (no automatic rollback, no retry of the swap). ------
FLIGHT_DEADLINE_S = 480
FLIGHT_POLL_S = 2


def make_flight(io, spec, target_sha, clock, sleep):
    """`io` supplies the effectful primitives (all injected, all read/ordered by the caller's allowlist):
      io.inspect(service)   -> {"cid","image","state","restarting","restarts","ip"}
      io.compose_up(service)-> bool   (force-recreate with the image-only override of the TARGET image; no pull/build)
      io.http(ip, port, path)-> (status:int, body:bytes)    direct to the container IP, never the public endpoint
      io.public_version()   -> str    (public qa-version source_sha)
    Returns the {"journal","health","served_sha"} dict the reviewed port expects.  Any doubt => journal "FAILED"."""
    if not HEX40_RE.fullmatch(target_sha or ""):
        raise Stop("CUT_FLIGHT_TARGET_INVALID")
    fail = {"journal": "FAILED", "health": False, "served_sha": ""}

    def wait_for(fn):
        end = clock() + FLIGHT_DEADLINE_S
        while True:
            try:
                if fn() is True:
                    return True
            except Stop:
                raise
            except Exception:  # noqa: BLE001
                pass
            if clock() >= end:
                return False
            sleep(FLIGHT_POLL_S)

    def verified_instance(service, old_cid, image):
        st = io.inspect(service)
        if (type(st) is dict and st["cid"] != old_cid and st["image"] == image
                and st["state"] == "running" and st["restarting"] is False and st["restarts"] == 0):
            return st
        return None

    def probe_api(old_cid):
        st = verified_instance("api", old_cid, spec["images"]["api"])
        if st is None:
            return False
        code, _ = io.http(st["ip"], 8080, "/health/ready")
        return code == 200

    def probe_web(old_cid):
        st = verified_instance("web", old_cid, spec["images"]["web"])
        if st is None:
            return False
        code, _ = io.http(st["ip"], 80, "/health/web")
        code2, body = io.http(st["ip"], 80, "/qa-version.json")
        return code == 200 and code2 == 200 and target_sha.encode() in body

    def flight():
        old_api, old_web = io.inspect("api")["cid"], io.inspect("web")["cid"]
        if not io.compose_up("api") or not wait_for(lambda: probe_api(old_api)):
            return dict(fail)
        if not io.compose_up("web") or not wait_for(lambda: probe_web(old_web)):
            return dict(fail)
        if not wait_for(lambda: probe_api(old_api)) or io.public_version() != target_sha:
            return dict(fail)
        return {"journal": "APPLIED", "health": True, "served_sha": io.public_version()}
    return flight


HEX40_RE = __import__("re").compile(r"[0-9a-f]{40}\Z")


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
