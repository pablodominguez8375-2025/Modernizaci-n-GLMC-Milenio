"""QA525 G6 v2 physical captor -- hermetic: fake runner / fake executables, no docker, no PG."""
import datetime as dt
import importlib.util
import os
from pathlib import Path
import stat
import tempfile
import unittest
from unittest import mock

ROOT = Path(__file__).resolve().parents[1]


def load(name, rel):
    spec = importlib.util.spec_from_file_location(name, ROOT / rel)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


cap = load("qa525_g6_captor_under_test", "scripts/qa/qa525-vnext-g6-v2-captor.py")
prod = load("qa525_g6_captor_producer", "scripts/qa/qa525-vnext-g6-v2-producer.py")
# Reuses the producer test fixtures (FakeRestic, KEY, REPO_ID, NOW); keep that file's names stable.
fx = load("qa525_g6_captor_producer_fixtures", "tests/test_qa525_vnext_g6_v2_producer.py")
DOCKER = "/usr/bin/docker"


def listing(db="pmgm", toc=40, fmt="CUSTOM", extra=""):
    head = ";\n; Archive created at 2026-11-08 02:00:00 UTC\n;     dbname: %s\n" % db
    head += ";     TOC Entries: %d\n;     Format: %s (c)\n" % (toc, fmt) + extra
    return head + "".join("%d; 1259 %d TABLE public t%d owner\n" % (i, i, i)
                          for i in range(1, toc - 2))


class CaptorFakeRunner:
    """Emulates docker: answers inspect, streams dump bytes, lists a TOC."""

    def __init__(self, **o):
        self.calls, self.o = [], o

    def __call__(self, argv, *, stdin_path=None, stdout_fd=None, capture_limit=0):
        self.calls.append(list(argv))
        tail = argv[3:]
        if tail[0] == "inspect":
            return 0, self.o.get("inspect", b"centenario-qa-lab2|postgres|true\n")
        if "pg_dump" in tail:
            db = tail[-1]
            if self.o.get("dump_fail") == db:
                return 1, b""
            os.write(stdout_fd, b"" if self.o.get("empty") == db else b"PGDMP-" + db.encode())
            return 0, b""
        if "pg_dumpall" in tail:
            os.write(stdout_fd, b"CREATE ROLE x;\n")
            return self.o.get("globals_code", 0), b""
        if "pg_restore" in tail:
            data = Path(stdin_path).read_bytes()
            db = data.decode()[6:]
            return self.o.get("list_code", 0), self.o.get("list", listing(db)).encode() \
                if not callable(self.o.get("list")) else self.o["list"](db).encode()
        raise AssertionError(argv)


def silo_ok(dest):
    os.mkdir(dest, 0o700)
    Path(dest, "obj1").write_bytes(b"x")
    os.chmod(os.path.join(dest, "obj1"), 0o600)


class G6CaptorBase(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.base = os.path.realpath(self.tmp.name)
        self.stage = os.path.join(self.base, "stage")
        os.mkdir(self.stage, 0o700)
        self.uid = os.getuid()

    def make(self, runner=None, silo=silo_ok, quiescent=lambda: True):
        self.runner = runner or CaptorFakeRunner()
        return cap.build_captor(runner=self.runner, copy_silo=silo, silo_quiescent=quiescent,
                                uid=self.uid)

    def code(self, call, *a, **k):
        with self.assertRaises(cap.Stop) as ctx:
            call(*a, **k)
        return str(ctx.exception)


class G6CaptorHappyAndArgvTests(G6CaptorBase):
    def test_stage_gets_exactly_the_four_artifacts_and_toc_counts(self):
        counts = self.make()(self.stage)
        self.assertEqual(counts, {"pmgm": 40, "pmgm_keycloak": 40})
        self.assertEqual(set(os.listdir(self.stage)),
                         {"pmgm.dump", "pmgm_keycloak.dump", "globals.sql", "silo"})
        for name in ("pmgm.dump", "pmgm_keycloak.dump", "globals.sql"):
            self.assertEqual(stat.S_IMODE(os.lstat(os.path.join(self.stage, name)).st_mode), 0o600)

    def test_argv_are_fixed_and_golden(self):
        self.make()(self.stage)
        h = [DOCKER, "--host", "unix:///var/run/docker.sock"]
        c = "centenario-qa-lab2-postgres-1"
        expected = [
            h + ["inspect", "--format",
                 '{{index .Config.Labels "com.docker.compose.project"}}|'
                 '{{index .Config.Labels "com.docker.compose.service"}}|{{.State.Running}}', c],
            h + ["exec", "--user", "postgres", c, "pg_dump", "--username", "pmgm_app",
                 "--format=custom", "--no-owner", "--no-acl", "--dbname", "pmgm"],
            h + ["exec", "-i", "--user", "postgres", c, "pg_restore", "--list"],
            h + ["exec", "--user", "postgres", c, "pg_dump", "--username", "pmgm_app",
                 "--format=custom", "--no-owner", "--no-acl", "--dbname", "pmgm_keycloak"],
            h + ["exec", "-i", "--user", "postgres", c, "pg_restore", "--list"],
            h + ["exec", "--user", "postgres", c, "pg_dumpall", "--username", "pmgm_app",
                 "--globals-only", "--no-role-passwords"],
        ]
        self.assertEqual(self.runner.calls, expected)

    def test_database_role_is_explicitly_pinned_for_every_server_dump(self):
        self.assertEqual(cap.POSTGRES_ROLE, "pmgm_app")
        for argv in (cap.argv_pg_dump(DOCKER, cap.POSTGRES_CONTAINER, "pmgm"),
                     cap.argv_pg_dump(DOCKER, cap.POSTGRES_CONTAINER, "pmgm_keycloak"),
                     cap.argv_globals(DOCKER, cap.POSTGRES_CONTAINER)):
            with self.subTest(argv=argv):
                self.assertIn("--username", argv)
                pos = argv.index("--username")
                self.assertEqual(argv[pos + 1], cap.POSTGRES_ROLE)
                self.assertNotEqual(argv[pos + 1], "postgres")

    def test_unexpected_container_or_label_or_stopped_is_refused_before_any_dump(self):
        for out in (b"centenario-qa-lab|postgres|true\n", b"centenario-qa-lab2|api|true\n",
                    b"centenario-qa-lab2|postgres|false\n", b"", b"x|y"):
            with self.subTest(out=out):
                os.rmdir(self.stage); os.mkdir(self.stage, 0o700)
                self.assertEqual(self.code(self.make(CaptorFakeRunner(inspect=out)), self.stage),
                                 "G6CAP_CONTAINER_UNEXPECTED")
                self.assertEqual(os.listdir(self.stage), [])
        self.assertEqual(self.code(cap.build_captor, runner=lambda *a, **k: (0, b""),
                                   copy_silo=silo_ok, silo_quiescent=lambda: True, uid=self.uid, container="other"),
                         "G6CAP_CONFIG_INVALID")

    def test_producer_accepts_the_captor_output_end_to_end(self):
        root = os.path.join(self.base, "g6"); os.mkdir(root, 0o700)
        restic = fx.G6V2ProducerFakeRestic()
        prod.init_repo(root, runner=restic, key_bytes=fx.KEY, uid=self.uid, now=fx.NOW)
        out = prod.backup(root, runner=restic, captor=self.make(), uid=self.uid, now=fx.NOW,
                          expected_repo_id=fx.REPO_ID, expected_key_epoch=prod.key_epoch(fx.KEY))
        self.assertTrue(out["archive_sha256"])
        self.assertEqual(os.listdir(os.path.join(root, "stages-v2")), [])


class G6CaptorFailClosedTests(G6CaptorBase):
    def test_dump_failure_or_empty_dump_stops(self):
        for opts, want in (({"dump_fail": "pmgm"}, "G6CAP_DUMP_FAILED"),
                           ({"dump_fail": "pmgm_keycloak"}, "G6CAP_DUMP_FAILED"),
                           ({"empty": "pmgm"}, "G6CAP_DUMP_INVALID"),
                           ({"globals_code": 2}, "G6CAP_DUMP_FAILED")):
            with self.subTest(opts=opts):
                os.rmdir(self.stage) if not os.listdir(self.stage) else \
                    __import__("shutil").rmtree(self.stage)
                os.mkdir(self.stage, 0o700)
                self.assertEqual(self.code(self.make(CaptorFakeRunner(**opts)), self.stage), want)

    def test_restore_list_gate_rejects_wrong_db_empty_toc_and_garbage(self):
        bad = {"wrong-db": lambda db: listing("pmgm_keycloak" if db == "pmgm" else "pmgm"),
               "tiny-toc": lambda db: listing(db, toc=5),
               "not-custom": lambda db: listing(db, fmt="TAR"),
               "garbage": lambda db: "garbage",
               "two-dbnames": lambda db: listing(db, extra=";     dbname: other\n")}
        for label, fn in bad.items():
            with self.subTest(label):
                __import__("shutil").rmtree(self.stage); os.mkdir(self.stage, 0o700)
                self.assertEqual(self.code(self.make(CaptorFakeRunner(list=fn)), self.stage),
                                 "G6CAP_LIST_INVALID")
        __import__("shutil").rmtree(self.stage); os.mkdir(self.stage, 0o700)
        self.assertEqual(self.code(self.make(CaptorFakeRunner(list_code=1)), self.stage),
                         "G6CAP_LIST_INVALID")

    def test_parse_restore_list_counts_and_bounds(self):
        self.assertEqual(cap.parse_restore_list(listing("pmgm", toc=40), "pmgm"), 40)
        for text in (listing("pmgm", toc=40).replace("TOC Entries: 40", "TOC Entries: 5"),
                     listing("pmgm", toc=40).replace("TOC Entries: 40", "TOC Entries: 3"),
                     "", None, 5):
            self.assertEqual(self.code(cap.parse_restore_list, text, "pmgm"), "G6CAP_LIST_INVALID")
        self.assertEqual(self.code(cap.parse_restore_list, listing("pmgm"), "other"),
                         "G6CAP_LIST_INVALID")

    def test_stage_must_be_a_private_empty_directory_owned_by_the_caller(self):
        Path(self.stage, "leftover").write_bytes(b"x")
        self.assertEqual(self.code(self.make(), self.stage), "G6CAP_STAGE_INVALID")
        os.unlink(os.path.join(self.stage, "leftover"))
        os.chmod(self.stage, 0o755)
        self.assertEqual(self.code(self.make(), self.stage), "G6CAP_STAGE_INVALID")
        os.chmod(self.stage, 0o700)
        self.assertEqual(self.code(self.make(), os.path.join(self.base, "missing")),
                         "G6CAP_STAGE_INVALID")
        link = os.path.join(self.base, "link"); os.symlink(self.stage, link)
        self.assertEqual(self.code(self.make(), link), "G6CAP_STAGE_INVALID")
        self.assertEqual(self.code(cap.build_captor, runner=lambda *a, **k: (0, b""),
                                   copy_silo=silo_ok, silo_quiescent=lambda: True, uid="0"), "G6CAP_CONFIG_INVALID")

    def test_empty_or_missing_silo_stops(self):
        def empty(dest): os.mkdir(dest, 0o700)

        def zero(dest): os.mkdir(dest, 0o700); Path(dest, "z").write_bytes(b"")

        def nothing(dest): pass
        for port in (empty, zero, nothing):
            with self.subTest(port=port.__name__):
                __import__("shutil").rmtree(self.stage); os.mkdir(self.stage, 0o700)
                self.assertEqual(self.code(self.make(silo=port), self.stage), "G6CAP_SILO_EMPTY")

    def test_quiescence_port_is_required_true_before_and_after_and_not_merely_truthy(self):
        for label, port in (("false", lambda: False), ("truthy", lambda: 1), ("none", lambda: None)):
            with self.subTest(label):
                __import__("shutil").rmtree(self.stage); os.mkdir(self.stage, 0o700)
                self.assertEqual(self.code(self.make(quiescent=port), self.stage),
                                 "G6CAP_SILO_NOT_QUIESCENT")
        answers = iter([True, False])
        __import__("shutil").rmtree(self.stage); os.mkdir(self.stage, 0o700)
        self.assertEqual(self.code(self.make(quiescent=lambda: next(answers)), self.stage),
                         "G6CAP_SILO_NOT_QUIESCENT")
        self.assertEqual(self.code(cap.build_captor, runner=lambda *a, **k: (0, b""),
                                   copy_silo=silo_ok, silo_quiescent=None, uid=self.uid),
                         "G6CAP_CONFIG_INVALID")


class G6CaptorNewFileTests(G6CaptorBase):
    def test_new_file_never_overwrites_or_follows_a_link(self):
        existing = os.path.join(self.base, "e"); Path(existing).write_bytes(b"keep")
        with self.assertRaises(FileExistsError):
            cap._new_file(existing)
        link = os.path.join(self.base, "l"); os.symlink(existing, link)
        with self.assertRaises(OSError):
            cap._new_file(link)
        self.assertEqual(Path(existing).read_bytes(), b"keep")
        fresh = os.path.join(self.base, "n")
        os.close(cap._new_file(fresh))
        self.assertEqual(stat.S_IMODE(os.lstat(fresh).st_mode), 0o600)


class G6CaptorSiloCopyTests(G6CaptorBase):
    def setUp(self):
        super().setUp()
        self.src = os.path.join(self.base, "src"); os.mkdir(self.src)
        Path(self.src, "a").write_bytes(b"1")
        os.mkdir(os.path.join(self.src, "d")); Path(self.src, "d", "b").write_bytes(b"22")
        self.dst = os.path.join(self.base, "dst")

    def test_copies_regular_files_private_and_counts(self):
        self.assertEqual(cap.copy_tree_nofollow(self.src, self.dst), 2)
        self.assertEqual(Path(self.dst, "d", "b").read_bytes(), b"22")
        self.assertEqual(stat.S_IMODE(os.lstat(os.path.join(self.dst, "a")).st_mode), 0o600)
        self.assertEqual(stat.S_IMODE(os.lstat(os.path.join(self.dst, "d")).st_mode), 0o700)

    def one_file_tree(self, data=b"0123456789"):
        for name in os.listdir(self.src):
            full = os.path.join(self.src, name)
            os.unlink(full) if not os.path.isdir(full) else __import__("shutil").rmtree(full)
        self.target = os.path.join(self.src, "obj")
        Path(self.target).write_bytes(data)

    def mid_read(self, mutate):
        """Run copy with 2-byte blocks; call mutate() right after the first real read."""
        real, state = os.read, {"n": 0}

        def wrapped(fd, n):
            blob = real(fd, n)
            state["n"] += 1
            if state["n"] == 1:
                mutate()
            return blob
        with mock.patch.object(cap, "CHUNK", 2), mock.patch.object(cap.os, "read", wrapped):
            return self.code(cap.copy_tree_nofollow, self.src, self.dst)

    def test_short_writes_are_looped_until_every_byte_lands(self):
        self.one_file_tree()
        real = os.write

        def trickle(fd, data):
            return real(fd, bytes(data[:3]))
        with mock.patch.object(cap.os, "write", trickle):
            self.assertEqual(cap.copy_tree_nofollow(self.src, self.dst), 1)
        self.assertEqual(Path(self.dst, "obj").read_bytes(), b"0123456789")

    def test_zero_progress_write_is_refused(self):
        self.one_file_tree()
        with mock.patch.object(cap.os, "write", lambda fd, data: 0):
            self.assertEqual(self.code(cap.copy_tree_nofollow, self.src, self.dst),
                             "G6CAP_SILO_COPY_MISMATCH")

    def test_source_truncated_grown_or_rewritten_during_read_is_refused(self):
        for label, mutate in (
                ("truncate", lambda: os.truncate(self.target, 4)),
                ("grow", lambda: open(self.target, "ab").write(b"more")),
                ("rewrite-same-size", lambda: (open(self.target, "r+b").write(b"XXXXXXXXXX")))):
            with self.subTest(label):
                self.one_file_tree()
                if os.path.exists(self.dst):
                    __import__("shutil").rmtree(self.dst)
                self.assertEqual(self.mid_read(mutate), "G6CAP_SILO_CHANGED")

    def test_source_replaced_by_another_file_during_read_is_refused(self):
        self.one_file_tree()

        def swap():
            Path(self.target + ".new").write_bytes(b"0123456789")
            os.replace(self.target + ".new", self.target)
        self.assertEqual(self.mid_read(swap), "G6CAP_SILO_CHANGED")

    def test_destination_corruption_is_caught_by_the_rehash(self):
        self.one_file_tree()
        real = os.write

        def lossy(fd, data):
            real(fd, bytes(data[:-1]) + b"!")
            return len(data)
        with mock.patch.object(cap.os, "write", lossy):
            self.assertEqual(self.code(cap.copy_tree_nofollow, self.src, self.dst),
                             "G6CAP_SILO_COPY_MISMATCH")

    def test_endlessly_growing_source_stops_after_the_declared_size(self):
        self.one_file_tree()
        real, calls = os.read, {"n": 0}

        def growing(fd, n):
            calls["n"] += 1
            with open(self.target, "ab") as grow:
                grow.write(b"+")
            return real(fd, n)
        with mock.patch.object(cap, "CHUNK", 2), mock.patch.object(cap.os, "read", growing):
            self.assertEqual(self.code(cap.copy_tree_nofollow, self.src, self.dst),
                             "G6CAP_SILO_CHANGED")
        self.assertLessEqual(calls["n"], 7)

    def test_object_changed_between_listing_and_open_is_refused(self):
        self.one_file_tree()
        stale = os.lstat(self.target)
        Path(self.target).write_bytes(b"0123456789")
        os.utime(self.target, ns=(1, 1))
        os.mkdir(self.dst)
        self.assertEqual(self.code(cap._copy_object, self.target, os.path.join(self.dst, "o"), stale),
                         "G6CAP_SILO_CHANGED")

    def test_hard_linked_object_is_refused(self):
        self.one_file_tree()
        os.link(self.target, os.path.join(self.base, "other-name"))
        self.assertEqual(self.code(cap.copy_tree_nofollow, self.src, self.dst), "G6CAP_SILO_CHANGED")

    def test_symlink_fifo_and_oversize_are_refused(self):
        os.symlink("/etc/passwd", os.path.join(self.src, "ln"))
        self.assertEqual(self.code(cap.copy_tree_nofollow, self.src, self.dst),
                         "G6CAP_SILO_UNSAFE_ENTRY")
        os.unlink(os.path.join(self.src, "ln"))
        os.mkfifo(os.path.join(self.src, "pipe"))
        self.assertEqual(self.code(cap.copy_tree_nofollow, self.src, self.dst + "2"),
                         "G6CAP_SILO_UNSAFE_ENTRY")
        os.unlink(os.path.join(self.src, "pipe"))
        self.assertEqual(self.code(cap.copy_tree_nofollow, self.src, self.dst + "3", max_files=1),
                         "G6CAP_SILO_TOO_LARGE")
        self.assertEqual(self.code(cap.copy_tree_nofollow, self.src, self.dst + "4", max_bytes=2),
                         "G6CAP_SILO_TOO_LARGE")
        self.assertEqual(self.code(cap.copy_tree_nofollow, "relative", self.dst + "5"),
                         "G6CAP_SILO_INVALID")
        self.assertEqual(self.code(cap.copy_tree_nofollow, os.path.join(self.src, "a"),
                                   self.dst + "6"), "G6CAP_SILO_INVALID")


class G6CaptorFixedRunnerTests(G6CaptorBase):
    def script(self, body):
        path = os.path.join(self.base, "fakedocker")
        Path(path).write_text("#!/bin/sh\n" + body + "\n")
        os.chmod(path, 0o755)
        return path

    def test_refuses_foreign_binary_and_bad_arguments(self):
        run = cap.FixedRunner(docker=self.script("echo hi"))
        for argv in ([ "/bin/sh", "-c", "id"], [], "docker", ["x\0y"]):
            self.assertEqual(self.code(run, argv, capture_limit=10), "G6CAP_RUNNER_REFUSED")
        self.assertEqual(self.code(run, [run.docker], capture_limit=0), "G6CAP_RUNNER_REFUSED")
        self.assertEqual(self.code(cap.FixedRunner, docker="docker"), "G6CAP_RUNNER_INVALID")
        self.assertEqual(self.code(cap.FixedRunner, docker="/x", timeout=0), "G6CAP_RUNNER_INVALID")

    def test_captures_output_and_streams_to_fd(self):
        run = cap.FixedRunner(docker=self.script('printf abc; exit 0'))
        self.assertEqual(run([run.docker], capture_limit=100), (0, b"abc"))
        out = os.path.join(self.base, "o")
        fd = os.open(out, os.O_WRONLY | os.O_CREAT, 0o600)
        self.assertEqual(run([run.docker], stdout_fd=fd)[0], 0)
        os.close(fd)
        self.assertEqual(Path(out).read_bytes(), b"abc")

    def test_nonzero_exit_oversize_and_timeout(self):
        run = cap.FixedRunner(docker=self.script('printf zz; exit 3'))
        self.assertEqual(run([run.docker], capture_limit=100), (3, b"zz"))
        big = cap.FixedRunner(docker=self.script('head -c 5000 /dev/zero'))
        self.assertEqual(self.code(big, [big.docker], capture_limit=1000), "G6CAP_OUTPUT_TOO_LARGE")
        slow = cap.FixedRunner(docker=self.script('sleep 30'), timeout=1)
        self.assertEqual(self.code(slow, [slow.docker], capture_limit=10), "G6CAP_TIMEOUT")

    def test_stdin_is_a_file_without_following_links_and_env_is_minimal(self):
        src = os.path.join(self.base, "in"); Path(src).write_bytes(b"DATA")
        run = cap.FixedRunner(docker=self.script('cat; env | grep -c SECRET'))
        os.environ["SECRET_TOKEN"] = "x"
        self.addCleanup(os.environ.pop, "SECRET_TOKEN", None)
        code, out = run([run.docker], stdin_path=src, capture_limit=100)
        self.assertEqual(out, b"DATA0\n")
        link = os.path.join(self.base, "lnk"); os.symlink(src, link)
        self.assertEqual(self.code(run, [run.docker], stdin_path=link, capture_limit=10),
                         "G6CAP_INPUT_UNAVAILABLE")


class G6CaptorStaticTests(unittest.TestCase):
    def test_no_shell_no_network_no_secret_material(self):
        text = (ROOT / "scripts/qa/qa525-vnext-g6-v2-captor.py").read_text()
        for forbidden in ("shell=True", "os.system", "import socket", "urllib", "password=",
                          "PGPASSWORD", "docker cp", '"run"', "--privileged"):
            self.assertNotIn(forbidden, text)
        with self.assertRaises(SystemExit):
            exec(compile(text, "x", "exec"), {"__name__": "__main__"})


if __name__ == "__main__":
    unittest.main()
