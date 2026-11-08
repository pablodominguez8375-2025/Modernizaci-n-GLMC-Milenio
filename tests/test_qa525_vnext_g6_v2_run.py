"""QA525 G6 v2 root entrypoint -- hermetic: fake docker/restic, tmp dirs, never root-only paths."""
import contextlib
import datetime as dt
import hashlib
import importlib.util
import io
import json
import os
from pathlib import Path
import shutil
import tempfile
import unittest
from unittest import mock

ROOT = Path(__file__).resolve().parents[1]


def load(name, rel):
    spec = importlib.util.spec_from_file_location(name, ROOT / rel)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


run = load("qa525_g6_run_under_test", "scripts/qa/qa525-vnext-g6-v2-run.py")
prod = load("qa525_g6_run_producer", "scripts/qa/qa525-vnext-g6-v2-producer.py")
cap = load("qa525_g6_run_captor", "scripts/qa/qa525-vnext-g6-v2-captor.py")
ver = load("qa525_g6_run_verifier", "scripts/qa/qa525-vnext-g6-v2-verifier.py")
ptest = load("qa525_g6_run_ptest", "tests/test_qa525_vnext_g6_v2_producer.py")
ctest = load("qa525_g6_run_ctest", "tests/test_qa525_vnext_g6_v2_captor.py")
vtest = load("qa525_g6_run_vtest", "tests/test_qa525_vnext_g6_v2_verifier.py")
UID = os.getuid()
NOW = ptest.NOW
SOURCE = (ROOT / "scripts/qa/qa525-vnext-g6-v2-run.py").read_text()
PIN = "5" * 64
EPOCH = "e" * 32
DRIVE = "1AbCdEfGhIjKlMnOpQrStUv"


class RunFakeDocker(ctest.CaptorFakeRunner):
    """Captor fake plus the MinIO identity and an image look-up."""

    SILO = None  # set per test by G6RunBase.setUp

    def __init__(self, minio="false", image=None, mounts=None, **o):
        super().__init__(**o)
        self.minio, self.image, self.mounts = minio, image, mounts

    def state(self):
        if isinstance(self.minio, list):  # successive answers, the last one repeats
            return self.minio.pop(0) if len(self.minio) > 1 else self.minio[0]
        return self.minio

    def __call__(self, argv, *, stdin_path=None, stdout_fd=None, capture_limit=0):
        tail = argv[3:]
        if tail[0] == "inspect" and tail[-1].endswith("-minio-1"):
            self.calls.append(list(argv))
            if "{{json .Mounts}}" in tail:
                mounts = self.mounts if self.mounts is not None else [
                    {"Type": "volume", "Destination": "/data", "Source": self.SILO}]
                return 0, json.dumps(mounts).encode()
            state = self.state()
            if state == "missing":
                return 1, b""
            return 0, ("centenario-qa-lab2|minio|%s\n" % state).encode()
        if tail[0] == "image":
            self.calls.append(list(argv))
            return (0, self.image) if self.image is not None else (1, b"")
        return super().__call__(argv, stdin_path=stdin_path, stdout_fd=stdout_fd,
                                capture_limit=capture_limit)


class SystemctlFake:
    """Answers `systemctl show` like systemd would for the operator-armed restart timer."""

    def __init__(self, test, *, active="active", sub="waiting", offset=660,
                 argv=None, unit="pmgm-g6-minio-restart.service", code=0):
        self.test, self.active, self.sub, self.offset = test, active, sub, offset
        self.argv, self.unit, self.code, self.calls = argv, unit, code, []

    def __call__(self, argv, *, stdin_path=None, stdout_fd=None, capture_limit=0):
        self.calls.append(list(argv))
        verbs = {a for a in argv[1:] if a in ("show",)}
        assert verbs == {"show"} and argv[0] == "/usr/bin/systemctl", argv  # read-only only
        if self.code:
            return self.code, b""
        if argv[3] == "pmgm-g6-minio-restart.timer":
            next_us = int((self.test.tick[0] + self.offset) * 1e6)
            return 0, ("ActiveState=%s\nSubState=%s\nUnit=%s\nNextElapseUSecMonotonic=%d\n"
                       % (self.active, self.sub, self.unit, next_us)).encode()
        argv_text = self.argv or "/usr/bin/docker start centenario-qa-lab2-minio-1"
        return 0, ("ExecStart={ path=/usr/bin/docker ; argv[]=%s ; ignore_errors=no ; "
                   "start_time=[n/a] ; status=0/0 }\n" % argv_text).encode()


class G6RunBase(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.base = os.path.realpath(self.tmp.name)
        self.state = os.path.join(self.base, "g6")
        os.mkdir(self.state, 0o700)
        self.silo = os.path.join(self.base, "silo")
        os.makedirs(os.path.join(self.silo, "bucket"))
        Path(self.silo, "bucket", "obj1").write_bytes(b"object-one")
        Path(self.silo, "obj2").write_bytes(b"object-two")
        self.restic_bin = os.path.join(self.base, "restic")
        Path(self.restic_bin).write_bytes(b"fake-restic")
        os.chmod(self.restic_bin, 0o755)
        self.lines = []
        self.restic = ptest.G6V2ProducerFakeRestic()
        self.clock = NOW
        self.key = b"K" * 40
        RunFakeDocker.SILO = self.silo
        self.slept, self.tick = [], [0.0]

    def env(self, runner, **over):
        args = dict(
            producer=prod, captor=cap, verifier=ver, uid=UID, now=lambda: self.clock,
            docker_runner=runner, make_restic=lambda sha: self.restic,
            read_key=lambda: self.key, emit=self.lines.append, state_root=self.state,
            silo_source=self.silo, restic=self.restic_bin,
            forbidden=[os.path.join(self.base, "prod")],
            module_hashes={"producer": "1" * 64, "entrypoint": "2" * 64},
            sleep=self.slept.append, monotonic=self.monotonic,
            systemctl_runner=SystemctlFake(self), systemctl="/usr/bin/systemctl")
        args.update(over)
        return run.Env(**args)

    def monotonic(self):
        self.tick[0] += 1.0
        return self.tick[0]

    def go(self, command, env, **opts):
        self.lines.clear()
        code = run.dispatch(command, opts, env)
        return code, {l.split("=", 1)[0]: l.split("=", 1)[1] for l in self.lines}


class G6RunGrammarTests(unittest.TestCase):
    def test_closed_grammar_accepts_only_exact_commands_and_options(self):
        cmd, opts = run.parse(["backup", "--restic-sha256", PIN, "--repo-id", "a" * 64,
                               "--key-epoch", EPOCH])
        self.assertEqual((cmd, sorted(opts)), ("backup", ["key-epoch", "repo-id",
                                                          "restic-sha256"]))
        for bad in ([], ["nope"], ["backup"], ["preflight", "x"], ["preflight", "--image"],
                    ["preflight", "--restic-sha"], ["preflight", "--bogus", "1"],
                    ["backup", "--restic-sha256", PIN, "--repo-id", "a" * 64],
                    ["preflight", "--image", "postgres@sha256:" + "a" * 64,
                     "--image", "postgres@sha256:" + "a" * 64],
                    ["preflight", "restic-sha256", PIN]):
            with self.assertRaises(run.Stop) as ctx:
                run.parse(bad)
            self.assertEqual(str(ctx.exception), "G6RUN_USAGE", bad)

    def test_every_value_is_matched_against_its_strict_pattern(self):
        for opt, value in (("restic-sha256", "A" * 64), ("restic-sha256", PIN[:-1]),
                           ("image", "postgres:17"), ("image", "postgres@sha256:" + "A" * 64),
                           ("image", "../x@sha256:" + "a" * 64)):
            with self.assertRaises(run.Stop) as ctx:
                run.parse(["preflight", "--" + opt, value])
            self.assertEqual(str(ctx.exception), "G6RUN_VALUE_INVALID")
        with self.assertRaises(run.Stop) as ctx:
            run.parse(["review", "--archive-name", "../centenario-qa-g6-v2-CIFRADO-"
                       "20261108T120000Z.tar.gz", "--drive-file-id", DRIVE,
                       "--repo-id", "a" * 64, "--key-epoch", EPOCH])
        self.assertEqual(str(ctx.exception), "G6RUN_VALUE_INVALID")

    def test_main_prints_one_fixed_code_for_bad_usage_and_no_root(self):
        out = io.StringIO()
        with contextlib.redirect_stdout(out):
            self.assertEqual(run.main(["bogus"]), 1)
        self.assertEqual(out.getvalue(), "G6RUN_STOP=G6RUN_USAGE\n")
        out = io.StringIO()
        with contextlib.redirect_stdout(out), \
                mock.patch.object(run.os, "geteuid", return_value=1000), \
                mock.patch.object(run.socket, "gethostname", return_value="glmdch"):
            self.assertEqual(run.main(["preflight"]), 1)
        self.assertEqual(out.getvalue(), "G6RUN_STOP=G6RUN_ROOT_REQUIRED\n")


class G6RunQuiesceTests(G6RunBase):
    def test_only_a_positively_stopped_minio_of_this_project_is_quiescent(self):
        for state, expected in (("false", True), ("true", False), ("missing", False),
                                ("restarting", False)):
            runner = RunFakeDocker(minio=state)
            self.assertIs(run.minio_quiescent(self.env(runner)), expected, state)

        def foreign(argv, **kw):
            return 0, b"other-project|minio|false\n"
        self.assertIs(run.minio_quiescent(self.env(foreign)), False)

    def test_the_program_never_issues_a_mutating_docker_verb(self):
        runner = RunFakeDocker(minio=["true", "false"])
        env = self.env(runner)
        self.go("init", env, **{"restic-sha256": PIN})
        _, out = self.go("backup", env, **{"restic-sha256": PIN, "repo-id": ptest.REPO_ID,
                                           "key-epoch": prod.key_epoch(self.key)})
        verbs = {c[3] for c in runner.calls}
        self.assertTrue(verbs <= {"inspect", "exec"}, verbs)


class G6RunCycleTests(G6RunBase):
    def cycle(self):
        env = self.env(RunFakeDocker(minio=["true", "false"]))
        code, init = self.go("init", env, **{"restic-sha256": PIN})
        self.assertEqual(code, 0)
        pins = {"restic-sha256": PIN, "repo-id": init["RESTIC_REPO_ID"],
                "key-epoch": init["KEY_EPOCH"]}
        code, backup = self.go("backup", env, **pins)
        self.assertEqual(code, 0, self.lines)
        return env, pins, backup

    def test_init_backup_restore_review_end_to_end_with_fakes(self):
        env, pins, backup = self.cycle()
        self.assertEqual(backup["STAMP"], "20261108T120000Z")
        exports = os.path.join(self.state, "exports-v2", backup["ARCHIVE_NAME"])
        downloads = os.path.join(self.state, "downloads-v2")
        os.makedirs(downloads, mode=0o700, exist_ok=True)
        shutil.copyfile(exports, os.path.join(downloads, backup["ARCHIVE_NAME"]))
        os.chmod(os.path.join(downloads, backup["ARCHIVE_NAME"]), 0o600)
        self.clock = NOW + dt.timedelta(hours=2)
        fake = vtest.VerifierFakeDocker()
        renv = self.env(fake)
        code, restored = self.go("restore", renv, **pins, **{
            "image": vtest.IMAGE, "archive-name": backup["ARCHIVE_NAME"],
            "archive-sha256": backup["ARCHIVE_SHA256"], "snapshot-id": backup["SNAPSHOT_ID"],
            "manifest-sha256": backup["MANIFEST_SHA256"]})
        self.assertEqual(code, 0, self.lines)
        self.assertEqual(restored["RESULT"],
                         "G6_SAME_HOST_ISOLATED_RESTORE_PASS_NOT_DISASTER_RECOVERY")
        self.assertEqual(restored["PRODUCTION_TOUCHED"], "false")
        self.assertEqual((restored["PMGM_TABLES"], restored["PMGM_KEYCLOAK_TABLES"]),
                         ("12", "12"))
        self.assertEqual(fake.verbs()[0], "container")
        self.assertEqual(fake.verbs()[-1], "container")  # cleanup confirmed by ID last
        code, review = self.go("review", renv, **{
            "archive-name": backup["ARCHIVE_NAME"], "drive-file-id": DRIVE,
            "repo-id": pins["repo-id"], "key-epoch": pins["key-epoch"]})
        self.assertEqual(code, 0, self.lines)
        self.assertEqual(review["G6_ARCHIVE_SHA256"], backup["ARCHIVE_SHA256"])
        self.assertEqual(review["G6_OFFHOST_SHA256_VERIFIED"], "true")
        # Every printed value is a public identifier: never the Restic key.
        self.assertNotIn(self.key.decode(), "\n".join(self.lines))

    def test_backup_with_a_running_minio_stops_and_leaves_no_archive(self):
        env, pins, _ = self.cycle()
        before = set(os.listdir(os.path.join(self.state, "exports-v2")))
        self.clock = NOW + dt.timedelta(minutes=5)
        code, out = self.go("backup", self.env(RunFakeDocker(minio="true")), **pins)
        self.assertEqual((code, out["G6RUN_STOP"]), (1, "G6CAP_SILO_NOT_QUIESCENT"))
        self.assertEqual(set(os.listdir(os.path.join(self.state, "exports-v2"))), before)
        self.assertEqual(os.listdir(os.path.join(self.state, "stages-v2")), [])

    def test_backup_refuses_the_wrong_pins_before_touching_docker(self):
        env, pins, _ = self.cycle()
        runner = RunFakeDocker(minio="false")
        self.clock = NOW + dt.timedelta(minutes=5)
        code, out = self.go("backup", self.env(runner), **{**pins, "repo-id": "c" * 64})
        self.assertEqual(code, 1)
        self.assertEqual(runner.calls, [])
        self.assertTrue(out["G6RUN_STOP"].startswith("G6P_"))

    def test_restore_inside_a_forbidden_prefix_is_refused(self):
        env, pins, backup = self.cycle()
        os.makedirs(os.path.join(self.state, "downloads-v2"), mode=0o700, exist_ok=True)
        shutil.copyfile(os.path.join(self.state, "exports-v2", backup["ARCHIVE_NAME"]),
                        os.path.join(self.state, "downloads-v2", backup["ARCHIVE_NAME"]))
        os.chmod(os.path.join(self.state, "downloads-v2", backup["ARCHIVE_NAME"]), 0o600)
        self.clock = NOW + dt.timedelta(hours=2)
        fake = vtest.VerifierFakeDocker()
        code, out = self.go("restore", self.env(fake, forbidden=[self.base]), **pins, **{
            "image": vtest.IMAGE, "archive-name": backup["ARCHIVE_NAME"],
            "archive-sha256": backup["ARCHIVE_SHA256"], "snapshot-id": backup["SNAPSHOT_ID"],
            "manifest-sha256": backup["MANIFEST_SHA256"]})
        self.assertEqual((code, out), (1, {"G6RUN_STOP": "G6P_RESTORE_TARGET_FORBIDDEN"}))
        self.assertEqual(fake.calls, [])


class G6RunHostAndWatchdogTests(G6RunBase):
    def prepare(self):
        env = self.env(RunFakeDocker(minio="false"))
        init = self.go("init", env, **{"restic-sha256": PIN})[1]
        self.clock = NOW + dt.timedelta(minutes=5)
        return {"restic-sha256": PIN, "repo-id": init["RESTIC_REPO_ID"],
                "key-epoch": init["KEY_EPOCH"]}

    def attempt(self, pins, **over):
        self.tick[0] = 0.0
        env = self.env(RunFakeDocker(minio=["true", "false"]), **over)
        return self.go("backup", env, **pins)

    def test_other_host_is_refused_before_any_module_is_loaded(self):
        loaded = []
        with mock.patch.object(run.socket, "gethostname", return_value="otro-host"), \
                mock.patch.object(run, "_load_module", side_effect=loaded.append), \
                mock.patch.object(run.os, "geteuid", return_value=0):
            with self.assertRaises(run.Stop) as err:
                run.real_env("backup")
        self.assertEqual(str(err.exception), "G6RUN_HOST_REFUSED")
        self.assertEqual(loaded, [])

    def test_armed_watchdog_lets_the_window_open_and_is_read_only(self):
        pins = self.prepare()
        sysctl = SystemctlFake(self)
        code, out = self.attempt(pins, systemctl_runner=sysctl)
        self.assertEqual(code, 0, self.lines)
        self.assertEqual(len(sysctl.calls), 2)

    def test_missing_or_wrong_watchdog_stops_before_asking_to_stop_minio(self):
        pins = self.prepare()
        cases = {
            "no_runner": (None, "G6RUN_WATCHDOG_MISSING"),
            "unit_absent": (SystemctlFake(self, code=1), "G6RUN_WATCHDOG_MISSING"),
            "inactive": (SystemctlFake(self, active="inactive", sub="dead"),
                         "G6RUN_WATCHDOG_MISSING"),
            "other_target": (SystemctlFake(self, argv="/usr/bin/docker start otra-cosa"),
                             "G6RUN_WATCHDOG_WRONG"),
            "echo_only": (SystemctlFake(self, argv="/bin/echo prueba"), "G6RUN_WATCHDOG_WRONG"),
            "extra_args": (SystemctlFake(self, argv="/usr/bin/docker start "
                                         "centenario-qa-lab2-minio-1 --x"), "G6RUN_WATCHDOG_WRONG"),
            "weird_unit": (SystemctlFake(self, unit="../x"), "G6RUN_WATCHDOG_WRONG"),
            "fires_too_soon": (SystemctlFake(self, offset=120), "G6RUN_WATCHDOG_TIMING"),
            "fires_too_late": (SystemctlFake(self, offset=7200), "G6RUN_WATCHDOG_TIMING"),
        }
        for label, (runner, expected) in cases.items():
            code, out = self.attempt(pins, systemctl_runner=runner)
            self.assertEqual((label, code, out["G6RUN_STOP"]), (label, 1, expected))
            self.assertNotIn("G6RUN_ACTION", out)  # never asked the operator to stop MinIO

    def test_window_is_aborted_if_the_watchdog_would_have_fired(self):
        pins = self.prepare()
        ticks = iter([0.0, 0.0, 100.0, 600.0, 650.0, 650.0])
        code, out = self.attempt(pins, systemctl_runner=SystemctlFake(self, offset=610),
                                 monotonic=lambda: next(ticks))
        self.assertEqual((code, out["G6RUN_STOP"]), (1, "G6RUN_WINDOW_EXCEEDED"))

    def test_sigkill_case_is_covered_because_the_watchdog_lives_in_systemd(self):
        self.assertIn("WATCHDOG_TIMER", SOURCE)
        self.assertNotIn("import subprocess", SOURCE)


class G6RunSiloBindingTests(G6RunBase):
    def test_silo_must_be_the_single_volume_mounted_at_data(self):
        good = {"Type": "volume", "Destination": "/data", "Source": self.silo}
        cases = {
            "ok": ([good], True),
            "other_source": ([dict(good, Source=self.silo + "-other")], False),
            "bind_not_volume": ([dict(good, Type="bind")], False),
            "wrong_destination": ([dict(good, Destination="/other")], False),
            "two_data_mounts": ([good, dict(good, Source="/x")], False),
            "no_mounts": ([], False),
            "not_a_list": ({"Destination": "/data"}, False),
        }
        for label, (mounts, expected) in cases.items():
            env = self.env(RunFakeDocker(mounts=mounts))
            self.assertIs(run.silo_bound(env), expected, label)

    def test_symlinked_source_is_not_bound(self):
        link = os.path.join(self.base, "link")
        os.symlink(self.silo, link)
        mounts = [{"Type": "volume", "Destination": "/data", "Source": link}]
        env = self.env(RunFakeDocker(mounts=mounts), silo_source=link)
        self.assertIs(run.silo_bound(env), False)

    def test_another_mount_with_fake_data_on_the_conventional_path_is_refused(self):
        env, pins, _ = G6RunCycleTests.cycle(self)
        before = set(os.listdir(os.path.join(self.state, "exports-v2")))
        self.clock = NOW + dt.timedelta(minutes=5)
        runner = RunFakeDocker(minio=["true", "false"], mounts=[
            {"Type": "volume", "Destination": "/data", "Source": "/somewhere/real/_data"}])
        code, out = self.go("backup", self.env(runner), **pins)
        self.assertEqual((code, out), (1, {"G6RUN_STOP": "G6RUN_SILO_UNBOUND"}))
        self.assertEqual(set(os.listdir(os.path.join(self.state, "exports-v2"))), before)
        self.assertNotIn("G6RUN_ACTION=STOP_MINIO_NOW", self.lines)

    def test_drift_between_preflight_and_backup_is_refused(self):
        env, pins, _ = G6RunCycleTests.cycle(self)
        self.assertIs(run.silo_bound(self.env(RunFakeDocker())), True)  # preflight sees it bound
        moved = [{"Type": "volume", "Destination": "/data",
                  "Source": "/var/lib/docker/volumes/other_volume/_data"}]
        self.clock = NOW + dt.timedelta(minutes=5)
        code, out = self.go("backup", self.env(RunFakeDocker(minio=["true", "false"],
                                                             mounts=moved)), **pins)
        self.assertEqual((code, out["G6RUN_STOP"]), (1, "G6RUN_SILO_UNBOUND"))
        self.assertNotIn("G6RUN_ACTION=STOP_MINIO_NOW", self.lines)

    def test_the_fixed_silo_path_is_the_one_observed_on_glmdch(self):
        self.assertEqual(run.SILO_SOURCE, "/var/lib/docker/volumes/"
                         "centenario-qa-lab2_pmgm_srv01_minio_data/_data")
        self.assertEqual(run.MINIO_DATA_DESTINATION, "/data")


class G6RunWindowTests(G6RunBase):
    def backup(self, minio, **over):
        env = self.env(RunFakeDocker(minio=minio), **over)
        self.go("init", env, **{"restic-sha256": PIN})
        pins = {"restic-sha256": PIN, "repo-id": ptest.REPO_ID,
                "key-epoch": prod.key_epoch(self.key)}
        self.clock = NOW + dt.timedelta(minutes=5)
        code, out = self.go("backup", env, **pins)
        return code, out, list(self.lines)

    def test_operator_is_told_to_stop_then_to_start_with_the_measured_window(self):
        code, out, lines = self.backup(["true", "true", "false"])
        self.assertEqual(code, 0, lines)
        stop = lines.index("G6RUN_ACTION=STOP_MINIO_NOW")
        start = lines.index("G6RUN_ACTION=START_MINIO_NOW")
        self.assertLess(stop, start)
        self.assertLess(start, [i for i, l in enumerate(lines)
                                if l.startswith("ARCHIVE_NAME=")][0])
        self.assertIn("MINIO_WINDOW_SECONDS=1", lines)
        self.assertEqual(self.slept, [run.POLL_SECONDS])

    def test_minio_already_stopped_before_the_gate_is_refused(self):
        code, out, _ = self.backup("false")
        self.assertEqual((code, out), (1, {"G6RUN_STOP": "G6RUN_MINIO_STOPPED_EARLY"}))

    def test_a_window_over_the_cap_invalidates_the_backup_and_asks_to_start(self):
        ticks = iter([0.0, 0.0, 0.0] + [run.WINDOW_MAX_SECONDS + 5.0] * 4)
        code, out, lines = self.backup(["true", "false"], monotonic=lambda: next(ticks))
        self.assertEqual((code, out["G6RUN_STOP"]), (1, "G6RUN_WINDOW_EXCEEDED"))
        self.assertIn("G6RUN_ACTION=START_MINIO_NOW", lines)
        self.assertEqual(os.listdir(os.path.join(self.state, "exports-v2")), [])

    def test_a_failure_after_the_stop_still_prints_the_start_instruction_once(self):
        boom = mock.Mock(side_effect=OSError("disk"))
        with mock.patch.object(cap, "copy_tree_nofollow", boom):
            code, out, lines = self.backup(["true", "false"])
        self.assertEqual(code, 1)
        self.assertEqual(lines.count("G6RUN_ACTION=START_MINIO_NOW"), 1)
        self.assertIn("REASON=ABORTED", lines)

    def test_operator_that_never_stops_minio_times_out_without_a_start_notice(self):
        code, out, lines = self.backup("true")
        self.assertEqual((code, out["G6RUN_STOP"]), (1, "G6CAP_SILO_NOT_QUIESCENT"))
        self.assertNotIn("G6RUN_ACTION=START_MINIO_NOW", lines)
        self.assertEqual(sum(self.slept), run.STOP_WAIT_SECONDS)

    def test_an_interrupt_during_the_window_still_prints_the_start_instruction(self):
        boom = mock.Mock(side_effect=KeyboardInterrupt)
        with mock.patch.object(cap, "copy_tree_nofollow", boom):
            with self.assertRaises(KeyboardInterrupt):
                self.backup(["true", "false"])
        self.assertIn("G6RUN_ACTION=START_MINIO_NOW", self.lines)


class G6RunInitAndFailureTests(G6RunBase):
    def test_key_is_only_read_by_init_and_must_be_long_printable_ascii(self):
        def boom():
            raise AssertionError("key must not be read")
        env = self.env(RunFakeDocker(minio="false"), read_key=boom)
        code, out = self.go("preflight", env)
        self.assertIn(code, (0, 1))
        self.assertIn("PREFLIGHT_READY", out)
        for bad in (b"short", b"x" * 40 + b" ", b"x" * 39 + b"\xc3", b"x" * 39 + b"\n"):
            self.key = bad
            code, out = self.go("init", self.env(RunFakeDocker()), **{"restic-sha256": PIN})
            self.assertEqual((code, out), (1, {"G6RUN_STOP": "G6RUN_KEY_REFUSED"}), bad)
        self.assertEqual(os.listdir(self.state), [])

    def test_unexpected_errors_print_only_a_fixed_code_never_text(self):
        def explode():
            raise RuntimeError("secret detail /etc/pmgm/key")
        code, out = self.go("init", self.env(RunFakeDocker(), read_key=explode),
                            **{"restic-sha256": PIN})
        self.assertEqual((code, out), (1, {"G6RUN_STOP": "G6RUN_UNEXPECTED"}))
        self.assertNotIn("secret", "\n".join(self.lines))

    def test_a_non_code_stop_message_is_not_echoed(self):
        class Stop(Exception):
            pass

        def leaky():
            raise Stop("path /root/x leaked")
        code, out = self.go("init", self.env(RunFakeDocker(), read_key=leaky),
                            **{"restic-sha256": PIN})
        self.assertEqual(out, {"G6RUN_STOP": "G6RUN_UNEXPECTED"})

    def test_output_values_with_spaces_or_newlines_are_refused(self):
        env = self.env(RunFakeDocker())
        for bad in ("a b", "a\nG6RUN_STOP=OK", "x" * 201, "ñ"):
            with self.assertRaises(run.Stop) as ctx:
                run._emit(env, key=bad)
            self.assertEqual(str(ctx.exception), "G6RUN_OUTPUT_REFUSED")
        self.assertEqual(self.lines, [])

    def test_second_init_is_stop_and_changes_nothing(self):
        env = self.env(RunFakeDocker())
        self.assertEqual(self.go("init", env, **{"restic-sha256": PIN})[0], 0)
        snapshot = sorted(Path(self.state).rglob("*"))
        code, out = self.go("init", env, **{"restic-sha256": PIN})
        self.assertEqual((code, out), (1, {"G6RUN_STOP": "G6P_INIT_TARGET_EXISTS"}))
        self.assertEqual(sorted(Path(self.state).rglob("*")), snapshot)


class G6RunPreflightTests(G6RunBase):
    def tree(self):
        return sorted((str(p), p.lstat().st_mtime_ns) for p in Path(self.base).rglob("*"))

    def test_preflight_reports_facts_and_changes_nothing(self):
        image = vtest.IMAGE
        runner = RunFakeDocker(minio="false", image=(
            'sha256:%s ["%s"]\n' % ("a" * 64, image)).encode())
        before = self.tree()
        code, out = self.go("preflight", self.env(runner), **{
            "restic-sha256": hashlib.sha256(b"fake-restic").hexdigest(), "image": image})
        self.assertEqual(code, 0 if out["PYTHON_FCNTL_SEALS"] == "yes" else 1, self.lines)
        self.assertEqual(out["PREFLIGHT_READY"], "yes" if code == 0 else "no")
        self.assertEqual(out["SILO_BOUND_TO_MOUNT"], "yes")
        self.assertEqual(self.tree(), before)
        self.assertEqual(out["RESTIC_MATCHES_PIN"], "yes")
        self.assertEqual(out["MINIO_QUIESCENT"], "yes")
        self.assertEqual(out["POSTGRES_IDENTITY"], "centenario-qa-lab2|postgres|true")
        self.assertEqual((out["SILO_SOURCE_FILES"], out["SILO_SOURCE_OK"]), ("2", "yes"))
        self.assertEqual((out["IMAGE_PRESENT"], out["IMAGE_MATCHES_PIN"]), ("yes", "yes"))
        self.assertEqual(out["STATE_ROOT"], "present")
        self.assertIn(out["PYTHON_FCNTL_SEALS"], ("yes", "no"))
        self.assertEqual(out["SHA256_PRODUCER"], "1" * 64)
        self.assertTrue(all(c[3] in ("inspect", "image") for c in runner.calls))

    def test_preflight_flags_a_wrong_pin_a_live_minio_and_a_missing_image(self):
        runner = RunFakeDocker(minio="true", image=None)
        code, out = self.go("preflight", self.env(runner), **{
            "restic-sha256": "0" * 64, "image": vtest.IMAGE})
        self.assertEqual((code, out["PREFLIGHT_READY"]), (1, "no"))
        self.assertEqual((out["RESTIC_MATCHES_PIN"], out["MINIO_QUIESCENT"],
                          out["IMAGE_PRESENT"], out["IMAGE_MATCHES_PIN"]),
                         ("no", "no", "no", "no"))

    def test_preflight_flags_an_unsafe_silo_source(self):
        os.symlink("/etc", os.path.join(self.silo, "link"))
        code, out = self.go("preflight", self.env(RunFakeDocker(minio="false")))
        self.assertEqual((code, out["SILO_SOURCE_OK"], out["PREFLIGHT_READY"]), (1, "no", "no"))

    def test_missing_state_root_is_reported_not_created(self):
        shutil.rmtree(self.state)
        code, out = self.go("preflight", self.env(RunFakeDocker(minio="false"),
                                                  state_root=self.state))
        self.assertEqual(out["STATE_ROOT"], "missing")
        self.assertFalse(os.path.exists(self.state))


class G6RunImageParseTests(unittest.TestCase):
    def test_only_an_exact_repo_digest_entry_matches(self):
        image = vtest.IMAGE
        ident = "sha256:" + "a" * 64
        good = ('%s ["%s"]' % (ident, image)).encode()
        self.assertTrue(run.image_matches_pin(good, image))
        for bad in (
                ('%s ["x/%s"]' % (ident, image)).encode(),       # substring only
                ('%s []' % ident).encode(),
                ('%s "%s"' % (ident, image)).encode(),           # not a list
                ('nothex ["%s"]' % image).encode(),
                ('%s ["%s", ' % (ident, image)).encode(),        # broken json
                b"", b"\xff\xfe"):
            self.assertFalse(run.image_matches_pin(bad, image), bad)


class G6RunModuleTrustTests(G6RunBase):
    def test_siblings_must_be_regular_owner_trusted_and_not_group_writable(self):
        good = Path(self.base, "m.py")
        good.write_text("X = 1\n")
        os.chmod(good, 0o644)
        run._trusted_sibling(good, UID)
        os.chmod(good, 0o664)
        with self.assertRaises(run.Stop) as ctx:
            run._trusted_sibling(good, UID)
        self.assertEqual(str(ctx.exception), "G6RUN_MODULE_UNTRUSTED")
        os.chmod(good, 0o644)
        link = Path(self.base, "l.py")
        os.symlink(good, link)
        with self.assertRaises(run.Stop):
            run._trusted_sibling(link, UID)
        if UID != 0:  # root-owned files are always trusted by design
            with self.assertRaises(run.Stop):
                run._trusted_sibling(good, UID + 12345)
        with self.assertRaises(run.Stop) as ctx:
            run._trusted_sibling(Path(self.base, "absent.py"), UID)
        self.assertEqual(str(ctx.exception), "G6RUN_MODULE_UNAVAILABLE")


class G6RunStaticTests(unittest.TestCase):
    def test_no_shell_environment_override_or_service_control_in_the_source(self):
        for forbidden in ("shell=True", "os.system", "os.popen", "import subprocess",
                          "os.environ", "getenv", "eval(", "exec(", "input(",
                          '"stop"', '"kill"', '"restart"', '"rm"', '"pause"',
                          '"run"', '"create"', '"compose"', "--privileged"):
            self.assertNotIn(forbidden, SOURCE, forbidden)

    def test_start_appears_only_as_the_expected_watchdog_target(self):
        self.assertEqual(SOURCE.count('"start"'), 1)
        self.assertIn('[env.docker, "start", env.minio]', SOURCE)

    def test_every_docker_command_comes_from_the_audited_builders(self):
        self.assertIn("argv_inspect", SOURCE)
        self.assertEqual(SOURCE.count('"image", "inspect"'), 1)

    def test_the_file_is_an_entrypoint_but_never_runs_on_import(self):
        self.assertIn('if __name__ == "__main__":', SOURCE)
        self.assertEqual(SOURCE.count("raise SystemExit(main("), 1)
        self.assertEqual(run.STATE_ROOT, "/var/lib/pmgm-qa525-g6-v2")
        self.assertTrue(run.SILO_SOURCE.endswith("/centenario-qa-lab2_pmgm_srv01_minio_data/_data"))


if __name__ == "__main__":
    unittest.main()
