#!/usr/bin/env python3
"""G6 one-session lifecycle, INACTIVE candidate for the GO-H adapter.

No CLI, no imports of sibling code, no authority from a path or environment.
The reviewed outer GO-H must supply authenticated launch/custody callbacks,
the pinned G6 runner's watchdog_deadline and the final_status policy. This
module does not install a GO, authenticate a bundle or obtain authorization.
FixedHostPort calls only system binaries through sudo -n; never sudo Python.
Do not ship to glmdch until the complete pinned kit/adapter is rehearsed.
"""
from __future__ import annotations

import json
import os
import re
import selectors
import signal
import subprocess
import time
from types import SimpleNamespace

MINIO = "centenario-qa-lab2-minio-1"
WD = "pmgm-g6-minio-restart"
SOCKET = "unix:///var/run/docker.sock"
SILO = "/var/lib/docker/volumes/centenario-qa-lab2_pmgm_srv01_minio_data/_data"
MARKERS = {"ARCHIVE_NAME": "archive_name", "ARCHIVE_SHA256": "archive_sha256",
           "SNAPSHOT_ID": "snapshot_id", "MANIFEST_SHA256": "manifest_sha256"}
CONSENT = "AUTORIZO VENTANA MINIO"


class Stop(Exception):
    pass


class WorkerUnsettled(Stop):
    """The outer adapter could not prove the privileged worker terminated."""
    def __init__(self, code="STOP_WORKER_UNSETTLED", process=None):
        super().__init__(code)
        self.process = process  # retain handle for supervised incident recovery


class BootstrapProcess:
    """Stream the fixed PRE-root command, retaining the caller's terminal.

    This is a non-root GO-H adapter, not a bypass of PRE-root. The outer kit
    MUST authenticate its own bytes and binary pins before constructing this
    object. No PIN/key in argv, pipe or log. The existing bootstrap reads stdin
    and Restic getpass retains /dev/tty. SIGTERM is forwarded through sudo;
    failed termination is reported as UNSETTLED, never followed by early start.
    """
    def __init__(self, source_sha, python, *, timeout=1200, terminate_timeout=15):
        if (not re.fullmatch(r"[a-f0-9]{40}", source_sha)
                or not re.fullmatch(r"/usr/bin/python3\.\d{1,2}", python)):
            raise Stop("STOP_BOOTSTRAP_ARGUMENTS")
        root = "/opt/centenario/qa525-root-source/" + source_sha
        self.argv = ["/usr/bin/sudo", "-n", "/bin/sh", "-c", 'umask 022; exec "$@"',
                     "sh", python, "-I", "-S", "-u", root + "/qa525-vnext-root-bootstrap.py",
                     "run", root, root + "/manifest-g6-backup.json"]
        self.timeout, self.terminate_timeout = timeout, terminate_timeout

    def __call__(self, on_line):
        if os.geteuid() == 0 or not os.isatty(0):
            raise Stop("STOP_BOOTSTRAP_TERMINAL_OR_UID")
        return self.monitor(self.argv, on_line, timeout=self.timeout,
                            terminate_timeout=self.terminate_timeout)

    @staticmethod
    def monitor(argv, on_line, *, timeout, terminate_timeout=15):
        """Internal transport; tests use a harmless Python child, never sudo."""
        proc = subprocess.Popen(argv, stdout=subprocess.PIPE, bufsize=0)
        deadline = time.monotonic() + timeout
        pending = b""
        selector = selectors.DefaultSelector()
        selector.register(proc.stdout, selectors.EVENT_READ)
        try:
            while selector.get_map():
                remaining = deadline - time.monotonic()
                if remaining <= 0:
                    raise Stop("STOP_BOOTSTRAP_TIMEOUT")
                for key, _ in selector.select(min(remaining, 0.25)):
                    block = os.read(key.fd, 4096)
                    if not block:
                        selector.unregister(key.fileobj)
                        if pending:
                            raise Stop("STOP_OUTPUT_TRUNCATED")
                        break
                    pending += block
                    while b"\n" in pending:
                        line, pending = pending.split(b"\n", 1)
                        if len(line) > 4096:
                            raise Stop("STOP_OUTPUT_INVALID")
                        on_line(line.decode("ascii", "strict"))
                    if len(pending) > 4096:
                        raise Stop("STOP_OUTPUT_INVALID")
            return proc.wait(timeout=max(0.01, deadline - time.monotonic()))
        except BaseException:
            # Reap sudo and its child before recovery. Never force-kill sudo:
            # doing so could orphan its root child and race the Silo copier.
            try:
                if proc.poll() is None:
                    proc.terminate()
                proc.wait(timeout=terminate_timeout)
            except (OSError, subprocess.TimeoutExpired):
                raise WorkerUnsettled(process=proc) from None
            raise
        finally:
            selector.close()
            proc.stdout.close()


def properties(raw):
    out = {}
    for line in raw.decode("ascii", "strict").splitlines():
        key, sep, value = line.partition("=")
        if not sep or key in out:
            raise Stop("STOP_UNIT_READBACK")
        out[key] = value
    return out


class FixedHostPort:
    """System-binary boundary. Caller verifies these binaries before use.

    Neither shell=True nor caller-selected service/container names. Process
    output is bounded and never included in an exception or public report.
    """
    def __init__(self, run=subprocess.run):
        self._run = run

    def call(self, argv, timeout=20):
        try:
            result = self._run(argv, stdin=subprocess.DEVNULL, stdout=subprocess.PIPE,
                               stderr=subprocess.DEVNULL, timeout=timeout, check=False,
                               env={"PATH": "/usr/bin:/bin", "LC_ALL": "C"})
        except (OSError, subprocess.TimeoutExpired):
            raise Stop("STOP_HOST_COMMAND") from None
        if result.returncode != 0 or len(result.stdout) > 65536:
            raise Stop("STOP_HOST_COMMAND")
        return result.stdout

    def minio(self):
        raw = self.call(["/usr/bin/sudo", "-n", "/usr/bin/docker", "--host", SOCKET,
                         "inspect", MINIO])
        try:
            records = json.loads(raw)
            if type(records) is not list or len(records) != 1:
                raise ValueError()
            d = records[0]
            labels = d["Config"]["Labels"]
            mounts = [m for m in d["Mounts"] if m["Destination"] == "/data"]
            if (d["Name"] != "/" + MINIO
                    or labels["com.docker.compose.project"] != "centenario-qa-lab2"
                    or labels["com.docker.compose.service"] != "minio"
                    or len(mounts) != 1 or mounts[0]["Type"] != "volume"
                    or mounts[0]["Source"] != SILO
                    or type(d["State"]["Running"]) is not bool):
                raise ValueError()
            return {"running": d["State"]["Running"],
                    "health": d["State"].get("Health", {}).get("Status", "unknown")}
        except (ValueError, KeyError, TypeError):
            raise Stop("STOP_MINIO_READBACK") from None

    def unit(self, suffix):
        if suffix not in ("timer", "service"):
            raise Stop("STOP_UNIT_NAME")
        return properties(self.call([
            "/usr/bin/sudo", "-n", "/usr/bin/systemctl", "show", "--no-pager",
            WD + "." + suffix, "--property=LoadState,ActiveState,SubState"]))

    def arm(self):
        # Retain transient state where possible for readback. A collected unit
        # is accepted only with explicit not-found/inactive/dead properties.
        self.call(["/usr/bin/sudo", "-n", "/usr/bin/systemd-run", "--on-active=15min",
                   "--timer-property=AccuracySec=1s", "--unit=" + WD,
                   "/usr/bin/docker", "start", MINIO])

    def stop_minio(self):
        self.call(["/usr/bin/sudo", "-n", "/usr/bin/docker", "--host", SOCKET,
                   "stop", "-t", "30", MINIO], timeout=45)

    def start_minio(self):
        self.call(["/usr/bin/sudo", "-n", "/usr/bin/docker", "--host", SOCKET,
                   "start", MINIO], timeout=45)

    def disarm(self):
        # Only the timer is stopped: never cancel a running rescue service.
        self.call(["/usr/bin/sudo", "-n", "/usr/bin/systemctl", "stop", WD + ".timer"])


def bind_watchdog(host, authenticated_runner, *, clock=time.monotonic):
    """Use the existing runner's validator; never a new timespan/argv parser.

    authenticated_runner comes from the verified outer kit, not this file's
    directory. Exact argv verification remains the runner's existing rule.
    """
    def show(argv, capture_limit):
        if (argv[:3] != ["/usr/bin/systemctl", "show", "--no-pager"]
                or capture_limit > 65536):
            raise Stop("STOP_WATCHDOG_READ_ARGUMENTS")
        try:
            raw = host.call(["/usr/bin/sudo", "-n"] + argv)
            if len(raw) > capture_limit:
                return 1, b""
            return 0, raw
        except Stop:
            return 1, b""
    env = SimpleNamespace(systemctl="/usr/bin/systemctl", systemctl_runner=show,
                          docker="/usr/bin/docker", minio=MINIO, monotonic=clock)
    return lambda: authenticated_runner.watchdog_deadline(env)


def inactive(unit):
    return (unit.get("LoadState") in {"loaded", "not-found"}
            and unit.get("ActiveState") == "inactive" and unit.get("SubState") == "dead")


class Session:
    """One-shot lifecycle, wired to real readbacks and the existing final guard.

    launch(on_line) must wait for its bootstrap/child to terminate, including
    on a signal/timeout, BEFORE returning or raising. It preserves the terminal
    for PIN/getpass and enforces its own deadline (BootstrapProcess provides
    that transport). archive_go() also retires partial GO files on failure.
    watchdog() is the *authenticated* runner's watchdog_deadline port: checks
    actual ActiveState/SubState/ExecStart and monotonic 600..900 s deadline.
    """
    def __init__(self, host, watchdog, final_status, *, clock=time.monotonic,
                 sleep=time.sleep, healthy_timeout=60):
        self.host, self.watchdog, self.final_status = host, watchdog, final_status
        self.clock, self.sleep = clock, sleep
        self.healthy_timeout = healthy_timeout
        self.used = False
        self.evidence = {}
        self.failure = None
        self.phase = "before"
        self.owned = False
        self.unsettled_worker = None

    def on_line(self, line):
        # No unfiltered bootstrap output is logged. No credential/token lines
        # are returned to the caller. Duplicate or out-of-order controls fail.
        if not isinstance(line, str) or len(line) > 4096:
            raise Stop("STOP_OUTPUT_INVALID")
        line = line.rstrip("\r\n")
        if line == "G6RUN_ACTION=STOP_MINIO_NOW":
            if self.phase != "before":
                raise Stop("STOP_WINDOW_SEQUENCE")
            # Recheck *immediately* before the only stop action, after PIN/DB
            # dumps. An early watchdog fire must invalidate the operation.
            self.watchdog()
            current = self.host.minio()
            if current != {"running": True, "health": "healthy"}:
                raise Stop("STOP_MINIO_PREWINDOW")
            self.phase = "stopping"
            self.host.stop_minio()
            if self.host.minio()["running"] is not False:
                raise Stop("STOP_MINIO_STOP_FAILED")
            self.phase = "copy"
        elif line == "G6RUN_ACTION=START_MINIO_NOW":
            if self.phase not in {"copy", "stopping"}:
                raise Stop("STOP_WINDOW_SEQUENCE")
            self.host.start_minio()
            self.phase = "after"
        elif line in {"BOOT_RUN_DONE", "LAUNCH_DONE"}:
            if line in self.evidence:
                raise Stop("STOP_DUPLICATE_MARKER")
            self.evidence[line] = True
        else:
            key, _, value = line.partition("=")
            if key in MARKERS:
                if self.phase != "after" or MARKERS[key] in self.evidence:
                    raise Stop("STOP_BACKUP_MARKER_SEQUENCE")
                pattern = (r"centenario-qa-g6-v2-CIFRADO-\d{8}T\d{6}Z\.tar\.gz"
                           if key == "ARCHIVE_NAME" else
                           r"[a-f0-9]{8,64}" if key == "SNAPSHOT_ID" else r"[a-f0-9]{64}")
                if not re.fullmatch(pattern, value):
                    raise Stop("STOP_MARKER_INVALID")
                self.evidence[MARKERS[key]] = value
            elif key in {"G6RUN_STOP", "REASON"}:
                self.failure = "STOP_RUNNER_FAILED"

    def settle(self):
        """Start/read healthy FIRST, then disarm/read both units. No fake PASS."""
        if not self.owned:
            return
        try:
            state = self.host.minio()
            if state["running"] is not True:
                self.host.start_minio()
            deadline = self.clock() + self.healthy_timeout
            while True:
                state = self.host.minio()
                self.evidence.update(minio_running=state["running"], minio_health=state["health"])
                if state == {"running": True, "health": "healthy"}:
                    break
                if self.clock() >= deadline:
                    return  # watchdog remains armed; recovery state not proven
                self.sleep(1)
            self.host.disarm()  # exceptions do NOT create an inactive assertion
            timer, service = self.host.unit("timer"), self.host.unit("service")
            if not inactive(timer) or not inactive(service):
                raise Stop("STOP_WATCHDOG_NOT_SETTLED")
            # Fresh MinIO read AFTER disarm/readback avoids stale success.
            state = self.host.minio()
            self.evidence.update(minio_running=state["running"], minio_health=state["health"],
                                 watchdog_active_state="inactive", watchdog_sub_state="dead")
        except Exception:
            self.failure = "STOP_RECOVERY_UNPROVEN"

    def run(self, *, consent, go_verified, install_go, launch, archive_go):
        if self.used:
            return "STOP_SESSION_REUSE", {}
        self.used = True
        if consent != CONSENT or go_verified is not True:
            return "STOP_AUTHORIZATION_REQUIRED", {}
        touched = False
        unsettled = False
        handlers = {}
        def interrupted(signum, frame):
            raise Stop("STOP_SESSION_INTERRUPTED")
        try:
            if self.host.minio() != {"running": True, "health": "healthy"}:
                raise Stop("STOP_MINIO_PREFLIGHT")
            if not all(inactive(self.host.unit(s)) for s in ("timer", "service")):
                raise Stop("STOP_WATCHDOG_PREEXISTING")
            for sig in (signal.SIGHUP, signal.SIGTERM, signal.SIGINT):
                handlers[sig] = signal.signal(sig, interrupted)
            # Mark ownership BEFORE arm: command may succeed remotely but lose
            # its reply (sudo/systemd timeout). Cleanup must still reconcile it.
            self.owned = True
            self.host.arm()
            self.watchdog()  # before installing/spending any GO
            touched = True
            install_go()
            rc = launch(self.on_line)
            self.evidence["bootstrap_exit"] = rc
        except WorkerUnsettled as exc:
            unsettled = True
            self.unsettled_worker = exc.process
            self.failure = "STOP_WORKER_UNSETTLED"
        except Exception:
            self.failure = "STOP_SESSION_FAILED"
        finally:
            # A second HUP/TERM/INT cannot interrupt recovery/custody. SIGKILL
            # cannot be trapped: the independent systemd timer remains essential.
            for sig in handlers:
                signal.signal(sig, signal.SIG_IGN)
            if not unsettled:
                self.settle()
            # A live worker may still be reading its GO; don't move it or
            # restart MinIO while its Silo copy may be alive. Leave rescue on.
            if touched and not unsettled:
                try:
                    self.evidence["go_archived"] = archive_go() is True
                except Exception:
                    self.evidence["go_archived"] = False
            for sig, handler in handlers.items():
                signal.signal(sig, handler)
        self.evidence["backup_markers_confirmed"] = (
            self.phase == "after" and self.evidence.get("BOOT_RUN_DONE") is True
            and self.evidence.get("LAUNCH_DONE") is True
            and all(name in self.evidence for name in MARKERS.values()))
        status = self.final_status(self.evidence)
        if unsettled:
            return "STOP_WORKER_UNSETTLED", dict(self.evidence)
        if status == "PASS_BACKUP_FINAL_STATE" and self.failure:
            status = self.failure
        return status, dict(self.evidence)


if __name__ == "__main__":
    raise SystemExit("QA525_SESSION_LIBRARY_ONLY_NO_DIRECT_GO")
