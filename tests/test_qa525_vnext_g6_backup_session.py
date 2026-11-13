"""Exercise the session itself; no sudo, Docker, systemd or host data.

Signals are real OS signals delivered inside a disposable child Python process.
The host command boundary is replaced by a stateful double, not a physical drill.
"""
import importlib.util
import json
import os
from pathlib import Path
import signal
import subprocess
import sys
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[1]


def load(filename):
    spec = importlib.util.spec_from_file_location(filename.replace("-", "_"), ROOT / "scripts/qa" / filename)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


session = load("qa525-g6-backup-session.py")
guard = load("qa525-g6-backup-finalize-guard.py")
DEAD = {"LoadState": "not-found", "ActiveState": "inactive", "SubState": "dead"}
ACTIVE = {"LoadState": "loaded", "ActiveState": "active", "SubState": "waiting"}


class Host:
    def __init__(self, fault=None):
        self.fault = fault
        self.running, self.health = True, "healthy"
        self.timer, self.service = dict(DEAD), dict(DEAD)
        self.calls = []
        self.now = 0

    def minio(self):
        self.calls.append("read-minio")
        if self.fault == "read-denied" and "stop-minio" in self.calls:
            raise RuntimeError("private host output must never leak")
        return {"running": self.running, "health": self.health}

    def unit(self, suffix):
        self.calls.append("read-" + suffix)
        if self.fault == "unit-denied" and "disarm" in self.calls:
            raise RuntimeError("private sudo error")
        return dict(getattr(self, suffix))

    def arm(self):
        self.calls.append("arm")
        self.timer = dict(ACTIVE)
        if self.fault == "arm-lost-reply":
            raise TimeoutError()

    def stop_minio(self):
        self.calls.append("stop-minio")
        if self.fault == "stop-denied":
            raise RuntimeError()
        if self.fault != "stop-lies":
            self.running = False

    def start_minio(self):
        self.calls.append("start-minio")
        if self.fault == "start-denied":
            raise RuntimeError()
        self.running = True
        if self.fault == "unhealthy":
            self.health = "unhealthy"

    def disarm(self):
        self.calls.append("disarm")
        if self.fault == "disarm-denied":
            raise RuntimeError()
        if self.fault != "disarm-lies":
            self.timer = dict(DEAD)
        if self.fault == "rescue-running":
            self.service = dict(ACTIVE)
        if self.fault == "post-disarm-unhealthy":
            self.health = "unhealthy"

    def watchdog(self):
        self.calls.append("watchdog")
        if self.fault == "timer-early" and self.calls.count("watchdog") == 2:
            self.timer = dict(DEAD)
        if self.timer != ACTIVE or self.fault == "timer-format":
            raise RuntimeError()
        return 900

    def sleep(self, seconds):
        self.now += seconds


def markers(on_line):
    on_line("ARCHIVE_NAME=centenario-qa-g6-v2-CIFRADO-20261112T235959Z.tar.gz")
    on_line("ARCHIVE_SHA256=" + "a" * 64)
    on_line("SNAPSHOT_ID=" + "c" * 64)
    on_line("MANIFEST_SHA256=" + "b" * 64)
    on_line("LAUNCH_DONE")
    on_line("BOOT_RUN_DONE")


def exercise(fault=None, signal_number=None):
    host = Host(fault)
    obj = session.Session(host, host.watchdog, guard.final_status,
                          clock=lambda: host.now, sleep=host.sleep, healthy_timeout=3)

    def install():
        host.calls.append("install-go")
        if fault == "install-partial":
            raise RuntimeError()

    def launch(on_line):
        host.calls.append("launch")
        if fault == "runner-before":
            return 1
        on_line("G6RUN_ACTION=STOP_MINIO_NOW")
        if signal_number:
            os.kill(os.getpid(), signal_number)
        if fault == "tty-eof":
            raise EOFError()
        if fault == "timeout":
            raise TimeoutError()
        if fault == "worker-unsettled":
            raise session.WorkerUnsettled()
        if fault == "runner-inside":
            on_line("G6RUN_ACTION=START_MINIO_NOW")
            on_line("REASON=ABORTED")
            return 1
        on_line("G6RUN_ACTION=START_MINIO_NOW")
        if fault == "duplicate-stop":
            on_line("G6RUN_ACTION=STOP_MINIO_NOW")
        if fault != "markers-absent":
            markers(on_line)
        if fault == "duplicate-marker":
            on_line("BOOT_RUN_DONE")
        if fault == "late-failure":
            on_line("G6RUN_STOP=G6RUN_UNEXPECTED")
        return 0

    def archive():
        host.calls.append("archive-go")
        if fault == "custody-raises":
            raise RuntimeError()
        return fault != "custody-false"

    status, evidence = obj.run(consent=session.CONSENT, go_verified=True,
                               install_go=install, launch=launch, archive_go=archive)
    return status, evidence, host, obj


class G6BackupSessionTests(unittest.TestCase):
    def test_unsettled_worker_keeps_watchdog_and_go_in_place(self):
        status, _, host, _ = exercise("worker-unsettled")
        self.assertEqual(status, "STOP_WORKER_UNSETTLED")
        self.assertNotIn("start-minio", host.calls)
        self.assertNotIn("disarm", host.calls)
        self.assertNotIn("archive-go", host.calls)
        self.assertEqual(host.timer, ACTIVE)

    def test_invalid_marker_not_retained_as_public_evidence(self):
        host = Host()
        obj = session.Session(host, host.watchdog, guard.final_status)
        obj.phase = "after"
        with self.assertRaises(session.Stop):
            obj.on_line("ARCHIVE_SHA256=secret-password")
        self.assertNotIn("secret", str(obj.evidence))

    def test_positive_checks_readback_after_disarm_and_archives(self):
        status, evidence, host, obj = exercise()
        self.assertEqual(status, "PASS_BACKUP_FINAL_STATE")
        self.assertEqual(evidence["watchdog_active_state"], "inactive")
        self.assertTrue(evidence["go_archived"])
        self.assertLess(host.calls.index("arm"), host.calls.index("install-go"))
        self.assertLess(host.calls.index("disarm"), host.calls.index("archive-go"))
        self.assertEqual(host.calls[-4:], ["read-timer", "read-service", "read-minio", "archive-go"])

    def test_authorization_failure_has_zero_host_actions(self):
        for consent, verified in (("continue", True), (session.CONSENT, False), (session.CONSENT, "true")):
            host = Host()
            obj = session.Session(host, host.watchdog, guard.final_status)
            status, _ = obj.run(consent=consent, go_verified=verified,
                                install_go=None, launch=None, archive_go=None)
            self.assertEqual(status, "STOP_AUTHORIZATION_REQUIRED")
            self.assertEqual(host.calls, [])

    def test_preexisting_or_unknown_watchdog_never_mutated(self):
        for unit in (ACTIVE, {}, {"LoadState": "not-found"}):
            host = Host()
            host.timer = unit
            obj = session.Session(host, host.watchdog, guard.final_status)
            obj.run(consent=session.CONSENT, go_verified=True,
                    install_go=None, launch=None, archive_go=None)
            self.assertNotIn("arm", host.calls)
            self.assertNotIn("disarm", host.calls)

    def test_cannot_reuse(self):
        _, _, host, obj = exercise()
        before = list(host.calls)
        status, _ = obj.run(consent=session.CONSENT, go_verified=True,
                            install_go=None, launch=None, archive_go=None)
        self.assertEqual(status, "STOP_SESSION_REUSE")
        self.assertEqual(before, host.calls)

    def test_failures_never_return_pass(self):
        for fault in ("runner-before", "runner-inside", "markers-absent", "late-failure",
                      "install-partial", "stop-denied", "stop-lies", "start-denied",
                      "unhealthy", "disarm-denied", "disarm-lies", "rescue-running",
                      "read-denied", "unit-denied", "post-disarm-unhealthy", "tty-eof",
                      "timeout", "timer-early", "timer-format", "duplicate-stop",
                      "duplicate-marker", "custody-false", "custody-raises", "arm-lost-reply"):
            with self.subTest(fault=fault):
                status, evidence, host, _ = exercise(fault)
                self.assertNotEqual(status, "PASS_BACKUP_FINAL_STATE")
                self.assertNotIn("private", str(evidence))
                if "install-go" in host.calls:
                    self.assertIn("archive-go", host.calls)
                if fault in {"start-denied", "unhealthy", "read-denied"}:
                    self.assertNotIn("disarm", host.calls)
                    self.assertEqual(host.timer, ACTIVE)
                if fault == "timer-early":
                    self.assertNotIn("stop-minio", host.calls)
                if fault in {"timer-format", "arm-lost-reply"}:
                    self.assertNotIn("install-go", host.calls)
                    self.assertIn("disarm", host.calls)

    def test_real_signals_recover_and_retire_in_child_process(self):
        for sig in (signal.SIGHUP, signal.SIGINT, signal.SIGTERM):
            with self.subTest(signal=sig):
                code = ("import sys;sys.path.insert(0, 'tests');"
                        "from test_qa525_vnext_g6_backup_session import exercise;"
                        f"s,e,h,_=exercise(signal_number={int(sig)});"
                        "assert s!='PASS_BACKUP_FINAL_STATE';"
                        "assert h.running and h.timer['ActiveState']=='inactive';"
                        "assert 'archive-go' in h.calls;print('RECOVERY_TEST_PASS')")
                p = subprocess.run([sys.executable, "-c", code], cwd=ROOT, capture_output=True, timeout=10)
                self.assertEqual(p.returncode, 0, p.stderr.decode())
                self.assertEqual(p.stdout.strip(), b"RECOVERY_TEST_PASS")

    def test_direct_execution_is_inert(self):
        p = subprocess.run([sys.executable, str(ROOT / "scripts/qa/qa525-g6-backup-session.py")],
                           capture_output=True, timeout=10)
        self.assertNotEqual(p.returncode, 0)
        self.assertIn(b"LIBRARY_ONLY_NO_DIRECT_GO", p.stderr)


class G6BackupFixedHostPortTests(unittest.TestCase):
    def test_exact_commands_only_system_binaries(self):
        calls = []
        def run(argv, **kwargs):
            calls.append((argv, kwargs))
            return subprocess.CompletedProcess(argv, 0, b"", b"")
        host = session.FixedHostPort(run)
        host.arm(); host.stop_minio(); host.start_minio(); host.disarm()
        self.assertEqual(len(calls), 4)
        for argv, kwargs in calls:
            self.assertEqual(argv[:2], ["/usr/bin/sudo", "-n"])
            self.assertIn(argv[2], {"/usr/bin/docker", "/usr/bin/systemctl", "/usr/bin/systemd-run"})
            self.assertNotIn("shell", kwargs)
        self.assertIn("--on-active=15min", calls[0][0])
        self.assertEqual(calls[-1][0][-1], session.WD + ".timer")

    def test_docker_identity_and_mount_are_bound(self):
        record = {"Name": "/" + session.MINIO, "Config": {"Labels": {
            "com.docker.compose.project": "centenario-qa-lab2", "com.docker.compose.service": "minio"}},
            "Mounts": [{"Destination": "/data", "Type": "volume", "Source": session.SILO}],
            "State": {"Running": True, "Health": {"Status": "healthy"}}}
        def run(argv, **kwargs):
            return subprocess.CompletedProcess(argv, 0, json.dumps([record]).encode(), b"")
        host = session.FixedHostPort(run)
        self.assertEqual(host.minio(), {"running": True, "health": "healthy"})
        record["Mounts"][0]["Source"] = "/unrelated"
        with self.assertRaises(session.Stop):
            host.minio()

    def test_read_denied_or_timeout_is_never_inactive(self):
        for mode in ("denied", "timeout"):
            def run(argv, **kwargs):
                if mode == "timeout":
                    raise subprocess.TimeoutExpired(argv, 20, output=b"sensitive")
                return subprocess.CompletedProcess(argv, 1, b"sensitive", b"")
            with self.assertRaisesRegex(session.Stop, "^STOP_HOST_COMMAND$"):
                session.FixedHostPort(run).unit("timer")

    def test_duplicate_properties_refused(self):
        with self.assertRaises(session.Stop):
            session.properties(b"ActiveState=active\nActiveState=inactive\n")

    def test_real_runner_watchdog_validator_with_systemd_readback(self):
        runner = load("qa525-vnext-g6-v2-run.py")
        class ReadHost:
            def call(self, argv):
                if argv[-1] == "--property=ExecStart":
                    return ("ExecStart={ path=/usr/bin/docker ; argv[]=/usr/bin/docker start "
                            + session.MINIO + " ; ignore_errors=no ; }\n").encode()
                return ("ActiveState=active\nSubState=waiting\nUnit=" + session.WD
                        + ".service\nNextElapseUSecMonotonic=3w 5d 1h 20min 7s\n").encode()
        deadline = runner.usec_property_seconds("3w 5d 1h 20min 7s")
        check = session.bind_watchdog(ReadHost(), runner, clock=lambda: deadline - 850)
        self.assertEqual(check(), deadline)
        with self.assertRaises(runner.Stop):
            session.bind_watchdog(ReadHost(), runner, clock=lambda: deadline - 599)()


class G6BackupBootstrapProcessTests(unittest.TestCase):
    def test_fixed_bootstrap_argv_rejects_injection(self):
        obj = session.BootstrapProcess("a" * 40, "/usr/bin/python3.14")
        self.assertEqual(obj.argv[:4], ["/usr/bin/sudo", "-n", "/bin/sh", "-c"])
        self.assertEqual(obj.argv[-1], "/opt/centenario/qa525-root-source/" + "a" * 40 + "/manifest-g6-backup.json")
        for sha, py in ((";evil", "/usr/bin/python3.14"), ("a" * 40, "/tmp/python")):
            with self.assertRaises(session.Stop):
                session.BootstrapProcess(sha, py)

    def test_real_stdout_transport_drives_recovery_session(self):
        host = Host()
        obj = session.Session(host, host.watchdog, guard.final_status)
        code = ("import time;print('G6RUN_ACTION=STOP_MINIO_NOW',flush=True);time.sleep(.1);"
                "print('G6RUN_ACTION=START_MINIO_NOW',flush=True);time.sleep(.1);"
                "print('ARCHIVE_NAME=centenario-qa-g6-v2-CIFRADO-20261112T235959Z.tar.gz');"
                "print('ARCHIVE_SHA256='+'a'*64);print('MANIFEST_SHA256='+'b'*64);"
                "print('SNAPSHOT_ID='+'c'*64);print('LAUNCH_DONE');print('BOOT_RUN_DONE')")
        def launch(line):
            return session.BootstrapProcess.monitor([sys.executable, "-u", "-c", code], line, timeout=5)
        status, _ = obj.run(consent=session.CONSENT, go_verified=True,
                            install_go=lambda: None, launch=launch, archive_go=lambda: True)
        self.assertEqual(status, "PASS_BACKUP_FINAL_STATE")
        self.assertIn("stop-minio", host.calls)
        self.assertIn("start-minio", host.calls)

    def test_transport_timeout_and_truncated_output_deny(self):
        for code in ("import time;time.sleep(20)", "print('partial',end='',flush=True)"):
            with self.subTest(code=code), self.assertRaises(session.Stop):
                session.BootstrapProcess.monitor([sys.executable, "-u", "-c", code],
                                                lambda line: None, timeout=.15)

    def test_worker_ignoring_termination_is_not_force_killed_or_claimed_settled(self):
        with tempfile.TemporaryDirectory() as td:
            pidfile = Path(td) / "pid"
            code = ("import signal,time,os,pathlib;signal.signal(signal.SIGTERM,signal.SIG_IGN);"
                    f"pathlib.Path({str(pidfile)!r}).write_text(str(os.getpid()));time.sleep(20)")
            try:
                with self.assertRaises(session.WorkerUnsettled) as caught:
                    session.BootstrapProcess.monitor([sys.executable, "-u", "-c", code],
                                                    lambda line: None, timeout=.3, terminate_timeout=.1)
                self.assertTrue(pidfile.exists())
            finally:
                if pidfile.exists():
                    pid = int(pidfile.read_text())
                    os.kill(pid, signal.SIGKILL)  # only our synthetic fixture child
                    caught.exception.process.wait(timeout=5)


if __name__ == "__main__":
    unittest.main()
