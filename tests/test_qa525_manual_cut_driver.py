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


if __name__ == "__main__":
    unittest.main()
