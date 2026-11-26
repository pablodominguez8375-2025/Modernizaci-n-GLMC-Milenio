"""Hermetic tests for the manual cut driver skeleton (temp dirs only; no docker, no host)."""
import fcntl
import hashlib
import importlib.util
import os
import pathlib
import subprocess
import tempfile
import unittest

SRC = pathlib.Path(__file__).resolve().parents[1] / "scripts/qa/operators/qa525-manual-cut-e5c40d3a.py"
spec = importlib.util.spec_from_file_location("cut", SRC)
cut = importlib.util.module_from_spec(spec)
spec.loader.exec_module(cut)
D = cut.DOCKER


class Loader(unittest.TestCase):
    def setUp(self):
        self.t = tempfile.TemporaryDirectory()
        self.addCleanup(self.t.cleanup)
        self.d = os.path.realpath(self.t.name)

    def put(self, name, text, mode=0o644):
        p = os.path.join(self.d, name)
        with open(p, "wb") as h:
            h.write(text)
        os.chmod(p, mode)
        return hashlib.sha256(text).hexdigest()

    def test_loads_pinned_and_executes_only_pinned_bytes(self):
        h = self.put("m.py", b"VALUE = 7\n")
        self.assertEqual(cut.load_libs(self.d, {"m.py": h})["m.py"].VALUE, 7)

    def test_short_reads_are_reassembled_and_overflow_stops(self):
        payload = b"VALUE = 7\n"
        path = os.path.join(self.d, "m.py")
        with open(path, "wb") as h:
            h.write(payload)
        os.chmod(path, 0o644)
        expected = hashlib.sha256(payload).hexdigest()
        original = os.read
        calls = []

        def short_read(fd, size):
            calls.append(size)
            return original(fd, min(size, 2))

        os.read = short_read
        try:
            self.assertEqual(cut.load_libs(self.d, {"m.py": expected})["m.py"].VALUE, 7)
        finally:
            os.read = original
        self.assertGreater(len(calls), 1)

        # the file grows between fstat and the overflow probe: the 1-byte probe returns data => STOP
        def growing(fd, size):
            return b"X" if size == 1 else original(fd, size)

        os.read = growing
        try:
            with self.assertRaises(cut.Stop) as ctx:
                cut.load_libs(self.d, {"m.py": expected})
        finally:
            os.read = original
        self.assertEqual(str(ctx.exception), "CUT_LIB_TOO_LARGE")

        # truncated while reading: empty chunk before the declared size => fixed STOP
        def truncated(fd, size):
            return b"" if size != 1 else original(fd, size)

        os.read = truncated
        try:
            with self.assertRaises(cut.Stop) as ctx:
                cut.load_libs(self.d, {"m.py": expected})
        finally:
            os.read = original
        self.assertEqual(str(ctx.exception), "CUT_LIB_READ_INCOMPLETE")

    def test_mismatch_unpinned_missing_unsafe(self):
        h = self.put("m.py", b"X=1\n")
        with self.assertRaises(cut.Stop) as c:
            cut.load_libs(self.d, {"m.py": "0" * 64})
        self.assertEqual(str(c.exception), "CUT_LIB_PIN_MISMATCH")
        with self.assertRaises(cut.Stop) as c:
            cut.load_libs(self.d, {"m.py": None})
        self.assertEqual(str(c.exception), "CUT_LIB_PIN_MISSING")
        with self.assertRaises(cut.Stop) as c:
            cut.load_libs(self.d, {"nope.py": h})
        self.assertEqual(str(c.exception), "CUT_LIB_UNREADABLE")
        os.chmod(os.path.join(self.d, "m.py"), 0o666)
        with self.assertRaises(cut.Stop) as c:
            cut.load_libs(self.d, {"m.py": h})
        self.assertEqual(str(c.exception), "CUT_LIB_UNSAFE")

    def test_symlink_rejected(self):
        h = self.put("real.py", b"X=1\n")
        os.symlink(os.path.join(self.d, "real.py"), os.path.join(self.d, "m.py"))
        with self.assertRaises(cut.Stop):
            cut.load_libs(self.d, {"m.py": h})

    def test_shipped_pins_complete_and_well_formed(self):
        self.assertEqual(len(cut.LIB_PINS), 7)
        for name, pin in cut.LIB_PINS.items():
            self.assertRegex(pin, r"\A[0-9a-f]{64}\Z", name)
        self.assertEqual(cut.LIB_PINS["qa525-vnext-go-nonce-journal.py"],
                         "65179306bddba301949fce0347bead2080ed8d715dd7b941382ed7e79d1a3562")

    def test_real_libs_load_when_repo_files_match_pins(self):
        scripts = pathlib.Path(__file__).resolve().parents[1] / "scripts/qa"
        if not all((scripts / n).exists() for n in cut.LIB_PINS):
            self.skipTest("repo libraries not present")
        mods = cut.load_libs(str(scripts))
        self.assertTrue(callable(mods["qa525-vnext-schema-cut-executor.py"].run))
        self.assertTrue(callable(mods["qa525-vnext-go-nonce-journal.py"].consume))


class Runner(unittest.TestCase):
    def test_only_exact_tuples(self):
        r = cut.make_runner([(D, "image", "inspect", "x")])
        for bad in ((D, "image", "inspect", "y"), (D, "rm", "-f", "x"), ("/bin/sh", "-c", "id"),
                    (D, "image", "inspect", "x", "--extra"), ()):
            with self.assertRaises(cut.Stop) as c:
                r(bad, 5)
            self.assertEqual(str(c.exception), "CUT_ARGV_NOT_ALLOWED")

    def test_allowlist_must_be_docker_strings(self):
        for bad in ([], [("/bin/sh", "-c", "id")], [(D, 1)]):
            with self.assertRaises(cut.Stop):
                cut.make_runner(bad)

    def test_fixed_env_no_shell_stderr_dropped(self):
        seen = {}

        def fake(argv, **kw):
            seen.update(kw, argv=argv)
            return subprocess.CompletedProcess(argv, 0, b"ok", b"SECRETERR")
        orig = subprocess.run
        subprocess.run = fake
        self.addCleanup(setattr, subprocess, "run", orig)
        rc, out = cut.make_runner([(D, "ps")])((D, "ps"), 5, b"in")
        self.assertEqual((rc, out), (0, b"ok"))
        self.assertIs(seen["shell"], False)
        self.assertEqual(seen["env"], cut.ENV)
        self.assertEqual(seen["stderr"], subprocess.DEVNULL)

    def test_stdin_must_be_bytes(self):
        with self.assertRaises(cut.Stop):
            cut.make_runner([(D, "ps")])((D, "ps"), 5, "text")


class PlaneTests(unittest.TestCase):
    def setUp(self):
        self.t = tempfile.TemporaryDirectory()
        self.addCleanup(self.t.cleanup)
        d = os.path.realpath(self.t.name)
        self.paths = [os.path.join(d, "l%d" % i) for i in range(5)]
        for p in self.paths:
            with open(p, "w"):
                pass
            os.chmod(p, 0o600)

    def test_held_and_busy(self):
        with cut.Plane(self.paths) as pl:
            pl.assert_held()
            other = os.open(self.paths[2], os.O_RDONLY)
            with self.assertRaises(BlockingIOError):
                fcntl.flock(other, fcntl.LOCK_EX | fcntl.LOCK_NB)
            os.close(other)
            with self.assertRaises(cut.Stop) as c:
                cut.Plane(self.paths).__enter__()
            self.assertEqual(str(c.exception), "CUT_LOCK_BUSY_OR_MISSING")

    def test_missing_and_unsafe_mode(self):
        os.unlink(self.paths[3])
        with self.assertRaises(cut.Stop):
            cut.Plane(self.paths).__enter__()
        with open(self.paths[3], "w"):
            pass
        os.chmod(self.paths[3], 0o666)
        with self.assertRaises(cut.Stop) as c:
            cut.Plane(self.paths).__enter__()
        self.assertEqual(str(c.exception), "CUT_LOCK_UNSAFE")

    def test_replaced_lock_file_is_plane_lost(self):
        with cut.Plane(self.paths) as pl:
            os.unlink(self.paths[0])
            with open(self.paths[0], "w"):
                pass
            with self.assertRaises(cut.Stop) as c:
                pl.assert_held()
            self.assertEqual(str(c.exception), "CUT_PLANE_LOST")

    def test_released_on_exit(self):
        with cut.Plane(self.paths):
            pass
        with cut.Plane(self.paths):
            pass


class EndToEndFakeDocker(unittest.TestCase):
    """Real reviewed libraries + driver wiring against a scripted fake docker (no real docker, no host)."""

    def setUp(self):
        import datetime as dt
        import re
        self.dt = dt
        scripts = pathlib.Path(__file__).resolve().parents[1] / "scripts/qa"
        self.mods = cut.load_libs(str(scripts))
        self.ad = self.mods["qa525-vnext-schema-adapter.py"]
        self.pt = self.mods["qa525-vnext-schema-cut-ports.py"]
        self.t = tempfile.TemporaryDirectory()
        self.addCleanup(self.t.cleanup)
        self.root = os.path.realpath(self.t.name) + "/journal"
        os.mkdir(self.root, 0o700)
        dg = lambda b: "sha256:" + hashlib.sha256(b).hexdigest()  # noqa: E731
        self.dg = dg
        self.base = ["20%012d_M" % (260000000000 + i) + "x" for i in range(100)]
        self.after = sorted(self.base + list(self.ad.REQUIRED))
        self.grant, self.gate = b"echo grants\n", b"SELECT 1;\n"
        self.mig = "sha256:" + "a" * 64
        self.api_img, self.web_img = "sha256:" + "b" * 64, "sha256:" + "c" * 64
        self.rev = {"api": "d" * 40, "web": "e" * 40}
        self.cid = "f" * 64
        self.params = {"network": "centenario-qa-lab2_pmgm_srv01_internal", "pg_container": "pg1",
                       "migrator_env_file": "/root/m.env", "grants_env_file": "/root/g.env",
                       "migrator_image_id": self.mig, "baseline_history_ids": self.base,
                       "grant_script": self.grant, "gate_sql": self.gate,
                       "grant_script_sha256": dg(self.grant), "postgate_sha256": dg(self.gate)}
        self.now = int(self.dt.datetime.now(self.dt.timezone.utc).timestamp())
        self.spec = {"pg_container": "pg1", "expires_at": self.now + 7200,
                     "images": {"api": self.api_img, "web": self.web_img}, "revisions": self.rev,
                     "migrator_image_id": self.mig, "baseline_sha256": dg("\n".join(self.base).encode()),
                     "api_container_id": self.cid, "write_roles": ("pmgm_app", "pmgm_runtime")}
        ev = {k: ("evidence-" + k).encode() for k in self.pt.EVIDENCE}
        self.collab = {
            "clock": lambda: self.dt.datetime.now(self.dt.timezone.utc), "abort": lambda: False,
            "read_evidence": lambda n: ev[n], "verifier": lambda d: True,
            "evidence_pins": {k: dg(v) for k, v in ev.items()}, "archive_epoch": self.now - 100,
            "flight": lambda: {"journal": "APPLIED", "health": True, "served_sha": "e" * 40},
            "target_web_sha": "e" * 40, "checks": (lambda: True,),
            "expected_104_sha256": dg("\n".join(self.after).encode())}
        self.auth = {"plan_sha256": dg(b"plan"), "driver_sha256": dg(b"driver"), "policy_sha256": dg(b"policy"),
                     "admission_sha256": dg(b"sponsor-authorization-text")}
        self.hist = list(self.base)
        self.calls = []
        self.fail_grants = False

        def fake(argv, **kw):
            argv = tuple(argv)
            self.calls.append(argv)
            out, rc = b"", 0
            sql = argv[-1] if "-c" in argv else None
            if argv[:3] == (D, "image", "inspect"):
                fmt, img = argv[4], argv[5]
                if fmt == "{{.Id}}":
                    out = (img + "\n").encode()
                else:
                    out = (self.rev["api"] if img == self.api_img else self.rev["web"]).encode() + b"\n"
            elif argv[:2] == (D, "stop"):
                out = b""
            elif argv[:2] == (D, "inspect"):
                out = b"false\n"
            elif argv[:2] == (D, "run"):
                self.hist = list(self.after)
                out = self.ad.MIGRATED
            elif "bash" in argv:
                out = b"" if self.fail_grants else (self.ad.GRANTS_MARKER + "\n").encode()
                rc = 1 if self.fail_grants else 0
            elif sql is not None and "MigrationId" in sql:
                out = ("\n".join(self.hist) + "\n").encode()
            elif sql is not None and "pg_stat_activity" in sql:
                out = b"0\n"
            elif sql is not None and "has_table_privilege" in sql:
                out = self.ad.ACL_OK
            elif argv[-1] == "ON_ERROR_STOP=1":  # gate: psql reading stdin
                out = (self.ad.GATE_PASS + "\n").encode()
            else:
                rc = 99
            return subprocess.CompletedProcess(argv, rc, out, b"")
        orig = subprocess.run
        subprocess.run = fake
        self.addCleanup(setattr, subprocess, "run", orig)

        class P:
            def assert_held(self):
                pass
        self.plane = P()

    def run_cut(self, nonce="1" * 64):
        return cut.run_cut(self.mods, self.plane, self.root, os.geteuid(), self.now, nonce, self.auth,
                           self.spec, self.params, self.collab)

    def test_nonce_not_spent_without_held_plane(self):
        class LostPlane:
            def assert_held(self):
                raise RuntimeError("not held")

        with self.assertRaises(cut.Stop) as ctx:
            cut.run_cut(self.mods, LostPlane(), self.root, os.geteuid(), self.now, "2" * 64,
                         self.auth, self.spec, self.params, self.collab)
        self.assertEqual(str(ctx.exception), "CUT_PLANE_NOT_HELD")
        self.assertEqual(self.calls, [])
        self.assertEqual(os.listdir(self.root), [])

    def test_full_sequence_completes_with_reviewed_libs(self):
        res = self.run_cut()
        self.assertEqual(res["phases"], ("RESERVED", "BACKUP_VERIFIED", "WRITES_QUIESCED", "MIGRATION_STARTED",
                                         "SCHEMA_104", "GRANTS_VERIFIED", "POSTGATE_PASS", "APP_STARTED",
                                         "APP_APPLIED", "UAT_PASS"))
        self.assertIs(res["may_apply"], False)
        self.assertEqual(sum(1 for c in self.calls if c[:2] == (D, "run")), 1)  # exactly one migrator run

    def test_second_attempt_with_same_ticket_does_nothing(self):
        self.run_cut()
        n = len(self.calls)
        with self.assertRaises(Exception):
            self.run_cut()
        self.assertEqual(len(self.calls), n)  # marker refused BEFORE any effect (allowlist/steps build is effect-free)

    def test_backup_rejected_stops_before_any_quiesce_or_ddl(self):
        self.collab["verifier"] = lambda d: False
        with self.assertRaises(Exception) as c:
            self.run_cut()
        self.assertIn("BACKUP", str(c.exception))
        self.assertFalse(any(x[:2] in ((D, "stop"), (D, "run")) for x in self.calls))

    def test_grants_failure_is_partial_schema_stop_without_retry(self):
        self.fail_grants = True
        with self.assertRaises(Exception) as c:
            self.run_cut()
        self.assertIn("SCHEMA", str(c.exception))
        self.assertEqual(sum(1 for x in self.calls if x[:2] == (D, "run")), 1)
        self.assertFalse(any(x[:2] == (D, "run") for x in self.calls[self.calls.index(
            next(x for x in self.calls if "bash" in x)):]))  # nothing re-run after the failure

    # ---- A0 (read-only) -------------------------------------------------------------------------------------
    def a0(self, **over):
        outs = {"timer": (3, b"inactive\n"), "update_procs": (1, b"0\n"),
                "containers": (0, b"abcdef123456 pg1 running\nabcdef123457 api running\n")}
        outs.update(over.pop("outs", {}))
        argv_key = {v: k for k, v in cut.A0_ARGV.items()}

        def run(argv):
            return outs[argv_key[tuple(argv)]]
        fs = {"file_ok": lambda path, mode, uid: True, "env_dir": self.root + "/absent-env",
              "abort": self.root + "/absent-abort"}
        fs.update(over.pop("fs", {}))
        return cut.a0_report(run, self.mods, self.spec, self.params, "a" * 64, lock_paths=self.locks(),
                             uid=os.geteuid(), fs=fs)

    def locks(self):
        paths = []
        for i in range(5):
            p = self.root + "/lock%d" % i
            open(p, "w").close()
            os.chmod(p, 0o600)
            paths.append(p)
        return paths

    def test_a0_passes_and_issues_no_mutating_command(self):
        rep = self.a0()
        self.assertEqual(rep["overall"], "A0_PASS_READ_ONLY_EVIDENCE", rep)
        self.assertFalse(any(c[:2] in ((D, "stop"), (D, "run"), (D, "start"), (D, "rm")) for c in self.calls))

    def test_a0_stops_on_active_timer_updater_history_drift_and_leftovers(self):
        self.assertEqual(self.a0(outs={"timer": (0, b"active\n")})["timer_inactive"], "STOP:TIMER_ACTIVE")
        self.assertEqual(self.a0(outs={"update_procs": (0, b"2\n")})["no_update_processes"],
                         "STOP:UPDATER_PROCESS_PRESENT")
        self.hist = self.hist[:-1]
        self.assertEqual(self.a0()["ef_history_100"], "STOP:EF_NOT_PINNED_100")
        self.hist = list(self.base)
        open(self.root + "/absent-abort", "w").close()
        self.assertEqual(self.a0()["abort_file_absent"], "STOP:ABORT_FILE_PRESENT")
        os.unlink(self.root + "/absent-abort")
        self.assertEqual(self.a0(outs={"containers": (0, b"abcdef123457 api running\n")})["containers"],
                         "STOP:PG_CONTAINER_MISSING")
        self.assertEqual(self.a0(fs={"file_ok": lambda *a: False})["uat_credential_mode"],
                         "STOP:UAT_CREDENTIAL_NOT_ROOT_0600")

    def test_a0_stops_when_a_legacy_lock_is_held(self):
        paths = self.locks()
        fd = os.open(paths[2], os.O_RDONLY)
        self.addCleanup(os.close, fd)
        fcntl.flock(fd, fcntl.LOCK_EX | fcntl.LOCK_NB)
        out = cut.a0_report(lambda a: (0, b""), self.mods, self.spec, self.params, "a" * 64, lock_paths=paths,
                            uid=os.geteuid(), fs={"file_ok": lambda *a: True})
        self.assertNotEqual(out["locks_free"], "PASS")
        self.assertEqual(out["overall"], "A0_STOP")

    def test_incomplete_collaborators_fail_closed(self):
        del self.collab["flight"]
        with self.assertRaises(cut.Stop) as c:
            self.run_cut()
        self.assertEqual(str(c.exception), "CUT_COLLABORATORS_INCOMPLETE")
        self.assertEqual(self.calls, [])

    def test_runner_blocks_anything_outside_the_derived_allowlist(self):
        r = cut.make_runner(cut.build_allowlist(self.mods, self.spec, self.params))
        with self.assertRaises(cut.Stop):
            r((D, "rm", "-f", self.cid), 5)
        with self.assertRaises(cut.Stop):
            r((D, "stop", "--time", "30", "0" * 64), 5)


class Modes(unittest.TestCase):
    def test_fail_closed_skeleton(self):
        self.assertEqual(cut.main([]), 1)
        self.assertEqual(cut.main([cut.EXECUTE_TOKEN]), 1)
        self.assertEqual(cut.main(["--other"]), 2)
        self.assertEqual(cut.main([cut.EXECUTE_TOKEN, "x"]), 2)

    def test_unwired_step_stops(self):
        with self.assertRaises(cut.Stop) as c:
            cut.unwired("FLIGHT")()
        self.assertEqual(str(c.exception), "CUT_STEP_NOT_WIRED_FLIGHT")

class Flight(unittest.TestCase):
    SHA = "e" * 40

    def make(self, api_ok=True, web_ok=True, public=None, restarts=0):
        spec = {"images": {"api": "sha256:" + "b" * 64, "web": "sha256:" + "c" * 64}}
        st = {"api": {"cid": "old-a", "image": "old", "state": "running", "restarting": False, "restarts": 0, "ip": "10.0.0.6"},
              "web": {"cid": "old-w", "image": "old", "state": "running", "restarting": False, "restarts": 0, "ip": "10.0.0.7"}}
        self.log, self.t = [], [0]

        class IO:
            def inspect(_s, svc):
                return dict(st[svc])

            def compose_up(_s, svc):
                self.log.append(svc)
                ok = api_ok if svc == "api" else web_ok
                if ok:
                    st[svc].update(cid="new-" + svc, image=spec["images"][svc], restarts=restarts)
                return True

            def http(_s, ip, port, path):
                if path == "/qa-version.json":
                    return 200, b'{"source_sha":"' + self.SHA.encode() + b'"}'
                return 200, b"ok"

            def public_version(_s):
                return public or self.SHA

        def clock():
            return self.t[0]

        def sleep(n):
            self.t[0] += n
        return cut.make_flight(IO(), spec, self.SHA, clock, sleep)

    def test_happy_path_swaps_api_then_web_once(self):
        self.assertEqual(self.make()(), {"journal": "APPLIED", "health": True, "served_sha": self.SHA})
        self.assertEqual(self.log, ["api", "web"])

    def test_health_probe_uses_same_verified_instance_snapshot(self):
        spec = {"images": {"api": "sha256:" + "b" * 64, "web": "sha256:" + "c" * 64}}
        ips = []

        class IO:
            def __init__(self):
                self.n = {"api": 0, "web": 0}
            def inspect(self, svc):
                self.n[svc] += 1
                if self.n[svc] == 1:
                    return {"cid": "old-" + svc, "image": "old", "state": "running",
                            "restarting": False, "restarts": 0, "ip": "10.0.0.1"}
                if self.n[svc] == 2:
                    return {"cid": "new-" + svc, "image": spec["images"][svc], "state": "running",
                            "restarting": False, "restarts": 0, "ip": "10.0.0.2"}
                return {"cid": "other-" + svc, "image": spec["images"][svc], "state": "running",
                        "restarting": False, "restarts": 0, "ip": "10.0.0.99"}
            def compose_up(self, svc):
                return True
            def http(self, ip, port, path):
                ips.append((ip, path))
                return 200, b'{"source_sha":"' + sha.encode() + b'"}'
            def public_version(self):
                return sha

        sha, tick = self.SHA, [0]
        io = IO()

        def clock():
            tick[0] += 1
            return tick[0]
        flight = cut.make_flight(io, spec, sha, clock, lambda n: None)
        self.assertEqual(flight()["journal"], "APPLIED")
        # the first health probe of each service goes to the IP of the SAME snapshot that was verified
        self.assertEqual(ips[0], ("10.0.0.2", "/health/ready"))
        self.assertEqual(ips[1], ("10.0.0.2", "/health/web"))

    def test_unhealthy_instance_fails_without_second_swap_or_rollback(self):
        self.assertEqual(self.make(api_ok=False)()["journal"], "FAILED")
        self.assertEqual(self.log, ["api"])  # web never touched, no rollback, no retry of the swap

    def test_restarting_instance_is_rejected(self):
        self.assertEqual(self.make(restarts=1)()["journal"], "FAILED")

    def test_public_version_mismatch_fails(self):
        self.assertEqual(self.make(public="f" * 40)()["journal"], "FAILED")

    def test_invalid_target_sha_stops(self):
        with self.assertRaises(cut.Stop):
            cut.make_flight(None, {}, "xyz", lambda: 0, lambda n: None)

class G6Verifier(unittest.TestCase):
    def setUp(self):
        import datetime as dt
        import json
        scripts = pathlib.Path(__file__).resolve().parents[1] / "scripts/qa"
        self.g6 = cut.load_libs(str(scripts), {cut.G6_LIB: cut.G6_PIN})[cut.G6_LIB]
        self.dt = dt
        canon = lambda o: (json.dumps(o, sort_keys=True, separators=(",", ":")) + "\n").encode()  # noqa: E731
        self.start = dt.datetime(2026, 11, 25, 6, 0, 0, tzinfo=dt.timezone.utc)
        ts = self.start.strftime("%Y%m%dT%H%M%SZ")
        name = "centenario-qa-g6-v2-CIFRADO-%s.tar.gz" % ts
        sha, repo, epoch = "a" * 64, "b" * 64, "c" * 32
        review = {"schema": "qa525-g6-v2-review", "g6_archive_name": name, "g6_archive_sha256": sha,
                  "g6_restore_report_name": "isolated-restore-v2-%s.report.json" % ts,
                  "g6_offhost_sha256_verified": True, "g6_drive_file_id": "D" * 20, "restic_repo_id": repo,
                  "key_epoch": epoch}
        receipt = {"schema": "qa525-g6-v2-receipt", "archive_name": name, "sha256": sha,
                   "status": "ENCRYPTED_LOCAL_ARCHIVE_READY_OFFHOST_PENDING", "restic_repo_id": repo,
                   "key_epoch": epoch}
        restore = {"schema": "qa525-g6-v2-restore", "result": "G6_SAME_HOST_ISOLATED_RESTORE_PASS_NOT_DISASTER_RECOVERY",
                   "archive_filename": name, "archive_sha256": sha, "restic_repo_id": repo, "key_epoch": epoch,
                   "postgres_tables_by_database": {"pmgm": 5, "pmgm_keycloak": 3}, "silo_objects_verified": 1,
                   "production_touched": False}
        self.data = {"review": canon(review), "receipt": canon(receipt), "restore": canon(restore)}
        d = lambda b: "sha256:" + hashlib.sha256(b).hexdigest()  # noqa: E731
        self.facts = {"pins": {k: d(v) for k, v in self.data.items()},
                      "observed_archive_sha256": "sha256:" + sha, "downloaded_offhost_sha256": "sha256:" + sha,
                      "expected_restic_repo_id": repo, "expected_key_epoch": epoch,
                      "admission_expires_at": self.start + dt.timedelta(hours=40),
                      "clock": lambda: self.start + dt.timedelta(hours=20)}

    def test_accepts_coherent_evidence_inside_window(self):
        self.assertIs(cut.make_g6_verifier(self.g6, self.facts)(self.data), True)

    def test_rejects_stale_window_hash_drift_and_tampered_evidence(self):
        f = dict(self.facts, clock=lambda: self.start + self.dt.timedelta(hours=49))
        self.assertIs(cut.make_g6_verifier(self.g6, f)(self.data), False)
        f = dict(self.facts, admission_expires_at=self.start + self.dt.timedelta(hours=48))  # not strictly inside
        self.assertIs(cut.make_g6_verifier(self.g6, f)(self.data), False)
        f = dict(self.facts, downloaded_offhost_sha256="sha256:" + "f" * 64)
        self.assertIs(cut.make_g6_verifier(self.g6, f)(self.data), False)
        bad = dict(self.data, restore=self.data["restore"].replace(b"5", b"6", 1))
        self.assertIs(cut.make_g6_verifier(self.g6, self.facts)(bad), False)

    def test_invalid_facts_stop(self):
        with self.assertRaises(cut.Stop):
            cut.make_g6_verifier(self.g6, {"pins": {}})



class A0G6Physical(G6Verifier):
    def setUp(self):
        super().setUp()
        self.t = tempfile.TemporaryDirectory()
        self.addCleanup(self.t.cleanup)
        self.d = os.path.realpath(self.t.name)
        self.paths = {}
        for k, v in self.data.items():
            self.paths[k] = self.put(k + ".json", v)
        arch = b"encrypted-archive-bytes"
        self.arch_sha = "sha256:" + hashlib.sha256(arch).hexdigest()
        self.paths["archive"] = self.put("a.tar.gz", arch)
        self.exp = {"restic_repo_id": self.facts["expected_restic_repo_id"], "expected_key_epoch": None,
                    "key_epoch": self.facts["expected_key_epoch"]}

    def put(self, name, raw):
        p = os.path.join(self.d, name)
        with open(p, "wb") as h:
            h.write(raw)
        os.chmod(p, 0o600)
        return p

    def run_check(self, offhost=None, paths=None, uid=None):
        return cut.a0_g6_check(self.g6, paths or self.paths, self.facts["pins"], self.exp,
                               offhost or self.facts["downloaded_offhost_sha256"], self.facts["admission_expires_at"],
                               self.facts["clock"], os.geteuid() if uid is None else uid)

    def rebind(self, raw):
        # review/receipt/restore name an archive sha "a"*64: align the fake archive with it
        self.facts["observed_archive_sha256"] = self.arch_sha

    def test_archive_hash_is_physically_computed_not_reported(self):
        # the evidence claims archive sha 'aaaa…'; the physical file hashes differently => STOP (not self-certified)
        self.assertEqual(self.run_check(), "STOP:A0_G6_EVIDENCE_REJECTED")

    def test_wrong_owner_and_symlink_and_missing_stop_closed(self):
        self.assertEqual(self.run_check(uid=12345), "STOP:A0_G6_META_INVALID")
        link = os.path.join(self.d, "l.json")
        os.symlink(self.paths["review"], link)
        self.assertEqual(self.run_check(paths=dict(self.paths, review=link)), "STOP:A0_G6_UNREADABLE")
        self.assertEqual(self.run_check(paths=dict(self.paths, restore=self.d + "/nope")), "STOP:A0_G6_UNREADABLE")
        self.assertEqual(self.run_check(paths={"review": 1}), "STOP:A0_G6_PATHS_INVALID")

    def test_pass_when_physical_archive_matches_evidence_and_offhost(self):
        import json
        real = self.arch_sha[len("sha256:"):]
        canon = lambda o: (json.dumps(o, sort_keys=True, separators=(",", ":")) + "\n").encode()  # noqa: E731
        for k in ("review", "receipt", "restore"):
            rec = json.loads(self.data[k])
            for f in ("g6_archive_sha256", "sha256", "archive_sha256"):
                if f in rec:
                    rec[f] = real
            self.data[k] = canon(rec)
            self.paths[k] = self.put(k + ".json", self.data[k])
        self.facts["pins"] = {k: "sha256:" + hashlib.sha256(v).hexdigest() for k, v in self.data.items()}
        self.assertEqual(self.run_check(offhost=self.arch_sha), "PASS")
        self.assertEqual(self.run_check(offhost="sha256:" + "9" * 64), "STOP:A0_G6_EVIDENCE_REJECTED")

    def test_group_writable_evidence_is_rejected(self):
        os.chmod(self.paths["review"], 0o666)
        self.assertEqual(self.run_check(), "STOP:A0_G6_META_INVALID")

class A0G6Params(unittest.TestCase):
    def setUp(self):
        self.t = tempfile.TemporaryDirectory()
        self.addCleanup(self.t.cleanup)
        self.p = os.path.join(os.path.realpath(self.t.name), "params.json")

    def write(self, rec, canonical=True):
        import json
        raw = json.dumps(rec, sort_keys=True, separators=(",", ":")) + ("\n" if canonical else " \n")
        with open(self.p, "w") as h:
            h.write(raw)
        os.chmod(self.p, 0o600)

    def good(self):
        d = "sha256:" + "a" * 64
        return {"schema": "qa525-a0-g6-params-v1", "paths": {k: "/x/" + k for k in ("review", "receipt", "restore", "archive")},
                "pins": {"review": d, "receipt": d, "restore": d}, "restic_repo_id": "b" * 64, "key_epoch": "c" * 32,
                "offhost_sha256": d}

    def test_absent_params_stop_closed(self):
        with self.assertRaises(cut.Stop) as c:
            cut.load_a0_g6_params(self.p, os.geteuid())
        self.assertEqual(str(c.exception), "A0_G6_PARAMS_ABSENT")

    def test_valid_params_load_and_noncanonical_or_extra_fields_stop(self):
        self.write(self.good())
        self.assertEqual(cut.load_a0_g6_params(self.p, os.geteuid())["key_epoch"], "c" * 32)
        self.write(self.good(), canonical=False)
        with self.assertRaises(cut.Stop):
            cut.load_a0_g6_params(self.p, os.geteuid())
        rec = self.good()
        rec["extra"] = 1
        self.write(rec)
        with self.assertRaises(cut.Stop):
            cut.load_a0_g6_params(self.p, os.geteuid())

    def test_wrong_owner_stops(self):
        self.write(self.good())
        with self.assertRaises(cut.Stop):
            cut.load_a0_g6_params(self.p, 12345)

class G6Mode(unittest.TestCase):
    NJ = "qa525-vnext-go-nonce-journal.py"

    def setUp(self):
        self.scripts = pathlib.Path(__file__).resolve().parents[1] / "scripts/qa"
        pins = dict(cut.G6_PINS)
        pins[self.NJ] = cut.LIB_PINS[self.NJ]
        self.mods = cut.load_libs(str(self.scripts), pins)
        self.run = self.mods[cut.G6_RUN]
        self.t = tempfile.TemporaryDirectory()
        self.addCleanup(self.t.cleanup)
        self.root = os.path.realpath(self.t.name) + "/j"
        os.mkdir(self.root, 0o700)
        _, self.op = cut.g6_operation(self.run, "preflight", [])
        d = lambda s: "sha256:" + hashlib.sha256(s).hexdigest()  # noqa: E731
        self.auth = {"driver_sha256": d(b"driver"), "policy_sha256": d(b"policy"), "admission_sha256": d(b"adm"),
                     "g6_operation_sha256": self.op, "g6_command": "preflight"}
        self.calls = []

        class P:
            def __init__(s, ok=True):
                s.ok = ok

            def assert_held(s):
                if not s.ok:
                    raise RuntimeError("no locks")
        self.P = P
        self.dispatch = lambda command, opts, env: (self.calls.append((command, opts)) or 0)
        self.now = int(__import__("time").time())

    def go(self, nonce="1" * 64, plane=None, auth=None, command="preflight", options=None):
        return cut.run_g6(self.mods, plane or self.P(), self.root, os.geteuid(), self.now, nonce, auth or self.auth,
                          command, [] if options is None else options, object(), self.dispatch)

    def test_operation_matches_reviewed_builder_and_published_pin(self):
        self.assertEqual(self.op, "2f9ba6141fa4dbc7736d4f772ed4a6e2d5b87e9e2971fe73e23eeb8011d346c8")
        spec = importlib.util.spec_from_file_location("b", self.scripts / "qa525-g6-operation-build.py")
        b = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(b)
        for cmd, opts in (("preflight", []), ("backup", [("restic-sha256", "a" * 64), ("repo-id", "b" * 64),
                                                        ("key-epoch", "c" * 32)])):
            self.assertEqual(cut.g6_operation(self.run, cmd, opts), b.build(cmd, opts, runner=self.run))

    def test_invalid_operations_are_rejected(self):
        for cmd, opts in (("nope", []), ("init", []), ("preflight", [("bogus", "x")]), ("preflight", [("image", "bad")])):
            with self.assertRaises(cut.Stop):
                cut.g6_operation(self.run, cmd, opts)

    def test_altered_g6_module_is_rejected_by_pin(self):
        d = os.path.realpath(self.t.name) + "/lib"
        os.mkdir(d)
        for name in cut.G6_PINS:
            raw = (self.scripts / name).read_bytes()
            if name == "qa525-vnext-g6-v2-captor.py":
                raw += b"\n# tampered\n"
            with open(os.path.join(d, name), "wb") as h:
                h.write(raw)
            os.chmod(os.path.join(d, name), 0o644)
        with self.assertRaises(cut.Stop) as c:
            cut.load_libs(d, cut.G6_PINS)
        self.assertEqual(str(c.exception), "CUT_LIB_PIN_MISMATCH")

    def test_env_requires_expected_host_and_root_and_carries_pinned_hashes(self):
        with self.assertRaises(cut.Stop):
            cut.build_g6_env(self.mods, 0, "other-host", 0)
        with self.assertRaises(cut.Stop):
            cut.build_g6_env(self.mods, 0, "glmdch", 1000)
        env = cut.build_g6_env(self.mods, 0, "glmdch", 0)
        self.assertEqual(env.module_hashes["entrypoint"], cut.G6_PINS[cut.G6_RUN])
        self.assertEqual(env.module_hashes["captor"], cut.G6_PINS["qa525-vnext-g6-v2-captor.py"])

    def test_one_command_per_nonce_and_effects_only_after_marker(self):
        self.assertEqual(self.go(), 0)
        self.assertEqual(self.calls, [("preflight", {})])
        with self.assertRaises(Exception):
            self.go()  # same nonce: refused, no second dispatch
        self.assertEqual(len(self.calls), 1)
        self.assertEqual(self.go(nonce="2" * 64), 0)  # a fresh nonce is a new authorization

    def test_missing_locks_operation_or_command_mismatch_do_nothing(self):
        with self.assertRaises(cut.Stop) as c:
            self.go(plane=self.P(ok=False))
        self.assertEqual(str(c.exception), "CUT_PLANE_NOT_HELD")
        with self.assertRaises(cut.Stop):
            self.go(auth=dict(self.auth, g6_operation_sha256="0" * 64))
        with self.assertRaises(cut.Stop):
            self.go(auth=dict(self.auth, g6_command="backup"))
        self.assertEqual(self.calls, [])
        self.assertEqual(os.listdir(self.root), [])  # no marker was spent by the refused attempts
        self.assertEqual(self.go(), 0)  # and the same nonce still works afterwards


if __name__ == "__main__":
    unittest.main()
