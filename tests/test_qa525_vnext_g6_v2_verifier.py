"""QA525 G6 v2 isolated restore verifier -- hermetic: fake docker runner, no containers."""
import importlib.util
import os
from pathlib import Path
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[1]


def load(name, rel):
    spec = importlib.util.spec_from_file_location(name, ROOT / rel)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


ver = load("qa525_g6_verifier_under_test", "scripts/qa/qa525-vnext-g6-v2-verifier.py")
IMAGE = "postgres@sha256:" + "a" * 64
STAMP = "20261108T120000Z"
NAME = "pmgm-g6v2-verify-20261108t120000z"
DOCKER = "/usr/bin/docker"


class VerifierFakeDocker:
    """Tiny docker emulation: tracks one container, answers psql/pg_restore."""

    def __init__(self, **o):
        self.name = o.pop("name", NAME)
        self.o, self.calls, self.exists = o, [], bool(o.get("preexisting"))
        self.ready_after, self.restored, self.stdins = o.get("ready_after", 0), [], []

    def __call__(self, argv, *, stdin_path=None, stdout_fd=None, capture_limit=0):
        assert argv[0] == DOCKER and capture_limit > 0 and stdout_fd is None
        self.calls.append(list(argv))
        tail = argv[3:]
        verb = tail[0]
        if verb == "inspect":
            return (0, self.name.encode()) if self.exists else (1, b"")
        if verb == "run":
            if self.o.get("run_fail"):
                return 1, b""
            self.exists = True
            return 0, b"containerid\n"
        if verb == "rm":
            if not self.o.get("rm_stuck"):
                self.exists = False
            return 0, b""
        assert verb == "exec"
        cmd = tail[tail.index(self.name) + 1:]
        if cmd[0] == "pg_isready":
            self.ready_after -= 1
            return (0 if self.ready_after < 0 else 2), b""
        if cmd[0] == "createdb":
            return self.o.get("createdb_code", 0), b""
        if cmd[0] == "pg_restore":
            self.stdins.append(stdin_path)
            db = cmd[cmd.index("--dbname") + 1]
            if self.o.get("restore_fail") == db:
                return 1, b""
            self.restored.append(db)
            return 0, b""
        if cmd[0] == "psql" and "show server_version_num" in cmd:
            return 0, self.o.get("version", b"170005\n")
        if cmd[0] == "psql":
            db = cmd[cmd.index("-d") + 1]
            return 0, self.o.get("tables", {}).get(db, b"12\n")
        raise AssertionError(argv)


class G6VerifierBase(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.data = os.path.join(os.path.realpath(self.tmp.name), "data")
        os.makedirs(os.path.join(self.data, "silo", "bucket"))
        for rel in ("pmgm.dump", "pmgm_keycloak.dump", "silo/bucket/o1", "silo/o2"):
            Path(self.data, rel).write_bytes(b"x")

    def build(self, **opts):
        self.fake = VerifierFakeDocker(**opts)
        return ver.build_verifier(runner=self.fake, image=IMAGE, stamp=STAMP,
                                  sleep=lambda s: None)

    def code(self, call, *a, **k):
        with self.assertRaises(ver.Stop) as ctx:
            call(*a, **k)
        return str(ctx.exception)


class G6VerifierHappyTests(G6VerifierBase):
    def test_returns_positive_counts_and_removes_the_container(self):
        tables, silo = self.build(tables={"pmgm": b"12\n", "pmgm_keycloak": b"7\n"})(self.data)
        self.assertEqual((tables, silo), ({"pmgm": 12, "pmgm_keycloak": 7}, 2))
        self.assertFalse(self.fake.exists)
        self.assertEqual(self.fake.restored, ["pmgm", "pmgm_keycloak"])
        self.assertEqual(self.fake.stdins, [os.path.join(self.data, "pmgm.dump"),
                                            os.path.join(self.data, "pmgm_keycloak.dump")])

    def test_run_argv_is_isolated_and_mounts_nothing_from_the_host(self):
        self.build()(self.data)
        run = next(c for c in self.fake.calls if c[3] == "run")
        joined = " ".join(run)
        for needed in ("--network none", "--read-only", "--cap-drop ALL",
                       "--security-opt no-new-privileges", "--pids-limit 256", "--memory 2g",
                       "--user 999:999", "POSTGRES_HOST_AUTH_METHOD=trust"):
            self.assertIn(needed, joined)
        for token in ("-v", "--volume", "--mount", "--privileged", "--pid", "--ipc", "-p",
                      "--publish", "--net", "--device", "--cap-add", "--userns", "--add-host"):
            self.assertNotIn(token, run)
        self.assertNotIn("docker.sock:", joined)
        self.assertNotIn(self.data, joined)
        self.assertEqual(run[run.index("--network") + 1], "none")
        self.assertEqual(run[-1], IMAGE)
        self.assertEqual(run[:3], [DOCKER, "--host", "unix:///var/run/docker.sock"])

    def test_restore_and_count_argv_are_golden(self):
        self.build()(self.data)
        h = [DOCKER, "--host", "unix:///var/run/docker.sock"]
        restores = [c for c in self.fake.calls if "pg_restore" in c]
        self.assertEqual(restores[0], h + ["exec", "-i", NAME, "pg_restore", "--exit-on-error",
                                           "--no-owner", "--no-acl", "-U", "postgres",
                                           "--dbname", "pmgm"])
        counts = [c for c in self.fake.calls if c[-1] == ver.TABLES_SQL]
        self.assertEqual(counts[1], h + ["exec", NAME, "psql", "-U", "postgres", "-d",
                                         "pmgm_keycloak", "-At", "-c", ver.TABLES_SQL])
        self.assertIn("pg_class", ver.TABLES_SQL)
        self.assertIn("relkind in ('r','p')", ver.TABLES_SQL)

    def test_waits_for_readiness_then_checks_server_version(self):
        self.build(ready_after=3)(self.data)
        kinds = [c[c.index(NAME) + 1] for c in self.fake.calls
                 if c[3] == "exec"]
        self.assertEqual(kinds.count("pg_isready"), 4)
        self.assertLess(kinds.index("pg_isready"), kinds.index("psql"))

    def test_every_argv_starts_with_the_pinned_docker_and_has_no_shell_text(self):
        self.build()(self.data)
        for argv in self.fake.calls:
            self.assertEqual(argv[:3], [DOCKER, "--host", "unix:///var/run/docker.sock"])
            self.assertTrue(all(type(a) is str and "\0" not in a and ";" not in a.replace(
                "show server_version_num", "") or a == ver.TABLES_SQL for a in argv))


class G6VerifierFailClosedTests(G6VerifierBase):
    def cleaned(self, fn, want):
        self.assertEqual(self.code(fn, self.data), want)
        self.assertFalse(self.fake.exists)

    def test_failures_never_leave_the_container_behind(self):
        for opts, want in (({"restore_fail": "pmgm"}, "G6VER_COMMAND_FAILED"),
                           ({"restore_fail": "pmgm_keycloak"}, "G6VER_COMMAND_FAILED"),
                           ({"createdb_code": 1}, "G6VER_COMMAND_FAILED"),
                           ({"ready_after": 10**6}, "G6VER_NOT_READY"),
                           ({"version": b"160004\n"}, "G6VER_RESULT_INVALID"),
                           ({"version": b"garbage"}, "G6VER_RESULT_INVALID"),
                           ({"tables": {"pmgm": b"0\n", "pmgm_keycloak": b"3\n"}}, "G6VER_RESULT_INVALID"),
                           ({"tables": {"pmgm": b"3\n", "pmgm_keycloak": b"x\n"}}, "G6VER_RESULT_INVALID"),
                           ({"tables": {"pmgm": b"-1\n"}}, "G6VER_RESULT_INVALID")):
            with self.subTest(opts=opts):
                fn = self.build(**opts)
                self.cleaned(fn, want)

    def test_start_failure_and_stuck_container(self):
        self.assertEqual(self.code(self.build(run_fail=True), self.data), "G6VER_COMMAND_FAILED")
        self.assertEqual(self.code(self.build(rm_stuck=True), self.data), "G6VER_CLEANUP_FAILED")

    def test_cleanup_failure_is_never_a_silent_pass(self):
        # even when everything else succeeded, a container that stays means no PASS
        self.assertEqual(self.code(self.build(rm_stuck=True), self.data), "G6VER_CLEANUP_FAILED")

    def test_name_collision_stops_before_starting_anything(self):
        fn = self.build(preexisting=True)
        self.assertEqual(self.code(fn, self.data), "G6VER_NAME_IN_USE")
        self.assertFalse(any(c[3] in ("run", "rm") for c in self.fake.calls))

    def test_silo_must_have_regular_nonempty_objects_and_no_links(self):
        fn = self.build()
        Path(self.data, "silo", "bucket", "o1").write_bytes(b"")
        Path(self.data, "silo", "o2").write_bytes(b"")
        self.assertEqual(self.code(fn, self.data), "G6VER_SILO_INVALID")
        self.assertEqual(self.fake.calls, [])
        Path(self.data, "silo", "o2").write_bytes(b"x")
        os.symlink("/etc/passwd", os.path.join(self.data, "silo", "ln"))
        self.assertEqual(self.code(fn, self.data), "G6VER_SILO_UNSAFE_ENTRY")
        os.unlink(os.path.join(self.data, "silo", "ln"))
        os.mkfifo(os.path.join(self.data, "silo", "fifo"))
        self.assertEqual(self.code(fn, self.data), "G6VER_SILO_UNSAFE_ENTRY")
        self.assertEqual(self.fake.calls, [])

    def test_missing_silo_or_bad_data_dir(self):
        fn = self.build()
        for bad in (None, "relative", "/x\0y", os.path.join(self.data, "nope")):
            self.assertIn(self.code(fn, bad), ("G6VER_DATA_DIR_INVALID", "G6VER_SILO_INVALID"))
        self.assertEqual(self.fake.calls, [])

    def test_config_is_validated(self):
        for image in ("postgres:17", "postgres@sha256:" + "A" * 64, None,
                      "postgres@sha256:" + "a" * 63, "../x@sha256:" + "a" * 64):
            self.assertEqual(self.code(ver.build_verifier, runner=lambda *a, **k: (0, b""),
                                       image=image, stamp=STAMP), "G6VER_CONFIG_INVALID")
        for stamp in ("20261108", "x", None, "20261108T120000Z; rm -rf /"):
            self.assertEqual(self.code(ver.build_verifier, runner=lambda *a, **k: (0, b""),
                                       image=IMAGE, stamp=stamp), "G6VER_CONFIG_INVALID")
        self.assertEqual(self.code(ver.build_verifier, runner=None, image=IMAGE, stamp=STAMP),
                         "G6VER_CONFIG_INVALID")


class G6VerifierProducerIntegrationTests(unittest.TestCase):
    def test_producer_restore_accepts_the_verifier_contract(self):
        prod = load("qa525_g6_verifier_producer", "scripts/qa/qa525-vnext-g6-v2-producer.py")
        fx = load("qa525_g6_verifier_producer_fixtures", "tests/test_qa525_vnext_g6_v2_producer.py")
        with tempfile.TemporaryDirectory() as tmp:
            base = os.path.realpath(tmp)
            root = os.path.join(base, "g6"); os.mkdir(root, 0o700)
            restic, uid = fx.G6V2ProducerFakeRestic(), os.getuid()
            epoch = prod.key_epoch(fx.KEY)
            prod.init_repo(root, runner=restic, key_bytes=fx.KEY, uid=uid, now=fx.NOW)
            out = prod.backup(root, runner=restic, captor=fx.good_captor, uid=uid, now=fx.NOW,
                              expected_repo_id=fx.REPO_ID, expected_key_epoch=epoch)
            import shutil
            downloads = Path(root, "downloads-v2"); downloads.mkdir(mode=0o700)
            shutil.copyfile(Path(root, "exports-v2", out["archive_name"]),
                            downloads / out["archive_name"])
            (downloads / out["archive_name"]).chmod(0o600)
            shutil.rmtree(Path(root, "repo-v2"))
            own = ver.container_name(out["stamp"])
            self.assertEqual(own, "pmgm-g6v2-verify-" + out["stamp"].lower())
            fake = VerifierFakeDocker(name=own, tables={"pmgm": b"9\n", "pmgm_keycloak": b"4\n"})
            verifier = ver.build_verifier(runner=fake, image=IMAGE, stamp=out["stamp"],
                                          sleep=lambda s: None)
            import datetime as dt
            report = prod.restore(
                root, runner=restic, verifier=verifier, archive_name=out["archive_name"],
                archive_sha256=out["archive_sha256"], snapshot_id=out["snapshot_id"],
                expected_manifest_sha256=out["manifest_sha256"], expected_repo_id=fx.REPO_ID,
                expected_key_epoch=epoch, forbidden_prefixes=[os.path.join(base, "production")],
                uid=uid, now=fx.NOW + dt.timedelta(hours=1))
            self.assertEqual(report["postgres_tables_by_database"], {"pmgm": 9, "pmgm_keycloak": 4})
            self.assertFalse(fake.exists)


class G6VerifierStaticTests(unittest.TestCase):
    def test_no_shell_no_network_no_secret_material(self):
        text = (ROOT / "scripts/qa/qa525-vnext-g6-v2-verifier.py").read_text()
        for forbidden in ("shell=True", "os.system", "import socket", "urllib", "PGPASSWORD",
                          "--privileged", "docker cp", "--net host", "import subprocess"):
            self.assertNotIn(forbidden, text)
        with self.assertRaises(SystemExit):
            exec(compile(text, "x", "exec"), {"__name__": "__main__"})


if __name__ == "__main__":
    unittest.main()
