"""Hermetic tests for the QA525 child-containment gate and the denied-network
sandbox. No Docker, no host paths, no outbound traffic."""
import importlib.util
import json
import os
from pathlib import Path
import shutil
import signal
import subprocess
import sys
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[1]
SCRIPT = ROOT / "scripts" / "qa" / "qa525-vnext-containment.py"
spec = importlib.util.spec_from_file_location("qa525_vnext_containment", SCRIPT)
mod = importlib.util.module_from_spec(spec)
spec.loader.exec_module(mod)
NONCE = "qa525-vnext-journal-nonce-20261105"


class Tree:
    """A fake cgroup v2 tree: <root>/system.slice/<unit>/cgroup.{events,procs}."""

    def __init__(self, path):
        self.root = Path(path)
        (self.root / "system.slice").mkdir(parents=True)
        self.unit = mod.unit_name(NONCE, "api")

    def scope(self, populated=None, procs="", events=None):
        directory = self.root / "system.slice" / self.unit
        directory.mkdir()
        text = events if events is not None else "populated %s\nfrozen 0\n" % populated
        (directory / "cgroup.events").write_text(text)
        (directory / "cgroup.procs").write_text(procs)
        return directory

    def handle(self):
        return mod.CgroupScope(self.unit, self.root)


class ContainmentTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.tree = Tree(self.tmp.name)

    def tearDown(self):
        self.tmp.cleanup()

    def test_populated_scope_blocks_recovery_and_empty_or_gone_passes(self):
        self.tree.scope(populated=1, procs="4242\n")
        self.assertEqual(self.tree.handle().state(), "ACTIVE")
        with self.assertRaisesRegex(mod.Stop, "CHILDREN_STILL_ALIVE"):
            mod.require_quiescent([self.tree.handle()])
        shutil.rmtree(self.tree.root / "system.slice" / self.tree.unit)
        self.assertEqual(self.tree.handle().state(), "ABSENT")
        mod.require_quiescent([self.tree.handle()])
        self.tree.scope(populated=0)
        self.assertEqual(self.tree.handle().state(), "EMPTY")
        mod.require_quiescent([self.tree.handle()])

    def test_any_live_scope_among_several_blocks(self):
        other = Tree(self.tmp.name + "/other")
        other.unit = mod.unit_name(NONCE, "web")
        other.scope(populated=1, procs="9\n")
        self.tree.scope(populated=0)
        with self.assertRaisesRegex(mod.Stop, "CHILDREN_STILL_ALIVE"):
            mod.require_quiescent([self.tree.handle(), other.handle()])

    def test_ambiguity_is_indeterminate_never_empty(self):
        cases = {
            "malformed events": dict(events="garbage\n"),
            "missing key": dict(events="frozen 0\n"),
            "bad value": dict(events="populated 2\n"),
            "duplicate key": dict(events="populated 0\npopulated 1\n"),
            "procs disagree": dict(populated=0, procs="77\n"),
        }
        for name, kwargs in cases.items():
            with self.subTest(name), tempfile.TemporaryDirectory() as directory:
                tree = Tree(directory)
                tree.scope(**kwargs)
                with self.assertRaises(mod.Indeterminate):
                    mod.require_quiescent([tree.handle()])

    def test_missing_parent_symlinks_and_non_directories_are_indeterminate(self):
        with tempfile.TemporaryDirectory() as directory:
            with self.assertRaises(mod.Indeterminate):  # no system.slice at all
                mod.require_quiescent([mod.CgroupScope(self.tree.unit, Path(directory))])
        with tempfile.TemporaryDirectory() as directory:
            real = Path(directory) / "real"
            real.mkdir()
            (Path(directory) / "tree").mkdir()
            os.symlink(real, Path(directory) / "tree" / "system.slice")
            with self.assertRaises(mod.Indeterminate):
                mod.CgroupScope(self.tree.unit, Path(directory) / "tree").state()
        directory = self.tree.scope(populated=0)
        (directory / "cgroup.events").unlink()
        os.symlink("/proc/self/status", directory / "cgroup.events")
        with self.assertRaises(mod.Indeterminate):
            self.tree.handle().state()

    def test_adapter_exceptions_and_unknown_states_are_indeterminate(self):
        class Boom:
            def state(self):
                raise OSError("io")
        class Odd:
            def state(self):
                return "MAYBE"
        for scope in (Boom(), Odd()):
            with self.subTest(scope=type(scope).__name__), self.assertRaises(mod.Indeterminate):
                mod.require_quiescent([scope])
        for bad in ([], [object()], [Odd()] * 9):
            with self.subTest(bad=len(bad)), self.assertRaises(mod.Stop):
                mod.require_quiescent(bad)

    def test_unit_names_and_argv_are_strict_and_shell_free(self):
        unit = mod.unit_name(NONCE, "web")
        self.assertRegex(unit, r"^qa525-[a-f0-9]{16}-web\.scope$")
        self.assertNotEqual(unit, mod.unit_name(NONCE, "api"))
        self.assertNotEqual(unit, mod.unit_name(NONCE[:-1] + "x", "web"))
        for nonce, service in (("short", "api"), (NONCE, "db"), (NONCE + "/..", "api"), (1, "api")):
            with self.subTest(nonce=nonce), self.assertRaises(mod.Stop):
                mod.unit_name(nonce, service)
        argv = mod.systemd_run_argv(unit, ["/usr/bin/docker", "compose", "up", "--detach"])
        self.assertEqual(argv[:3], ["/usr/bin/systemd-run", "--scope", "--collect"])
        self.assertIn("--unit=" + unit, argv)
        self.assertEqual(argv[argv.index("--") + 1:][0], "/usr/bin/docker")
        for command in (["docker"], ["/bin/sh", "-c", "x"], ["/usr/bin/docker", "a;b"],
                        ["/usr/bin/docker", "$(id)"], ["/usr/bin/docker", ""], [],
                        ["/usr/bin/docker"] * 65, "docker up"):
            with self.subTest(command=str(command)[:30]), self.assertRaises(mod.Stop):
                mod.systemd_run_argv(unit, command)
        for bad_unit in ("x.scope", "qa525-zz-api.scope", unit + "\n", "../" + unit):
            with self.subTest(unit=bad_unit), self.assertRaises(mod.Stop):
                mod.systemd_run_argv(bad_unit, ["/usr/bin/docker", "ps"])
        with self.assertRaises(mod.Stop):
            mod.CgroupScope(unit, Path("relative"))

    def test_real_orphan_child_survives_parent_and_is_detected(self):
        """A surviving child holds a (process-group) stand-in for the cgroup."""
        class GroupScope:
            def __init__(self, pgid):
                self.pgid = pgid

            def state(self):
                try:
                    os.killpg(self.pgid, 0)
                except ProcessLookupError:
                    return "ABSENT"
                return "ACTIVE"

        child = subprocess.Popen([sys.executable, "-c", "import time; time.sleep(60)"],
                                 start_new_session=True)
        try:
            scope = GroupScope(child.pid)  # the 'orchestrator' dies; the child keeps going
            with self.assertRaisesRegex(mod.Stop, "CHILDREN_STILL_ALIVE"):
                mod.require_quiescent([scope])
            os.killpg(child.pid, signal.SIGKILL)
            child.wait(timeout=10)
            mod.require_quiescent([scope])
        finally:
            if child.poll() is None:
                child.kill()
                child.wait()

    def test_module_never_runs_or_kills_anything(self):
        source = SCRIPT.read_text(encoding="utf-8")
        for forbidden in ("import subprocess", "import socket", "import os\n", "os.kill", "os.system",
                          "write_text", "write_bytes", "cgroup.kill", "Popen"):
            self.assertNotIn(forbidden, source, forbidden)


NETNS_CHILD = r"""
import importlib.util, json, socket, sys, tempfile
from pathlib import Path
root, tests = Path(sys.argv[1]), Path(sys.argv[2])
sys.path.insert(0, str(tests))
def load(name, rel):
    s = importlib.util.spec_from_file_location(name, root / rel)
    m = importlib.util.module_from_spec(s); s.loader.exec_module(m); return m
contain = load("c", "scripts/qa/qa525-vnext-containment.py")
import test_qa525_vnext_journal as jt
out = {}
# 1) the sandbox really denies the network
try:
    socket.create_connection(("192.0.2.1", 80), timeout=1); out["connect"] = "ALLOWED"
except OSError as e:
    out["connect"] = "DENIED"
try:
    socket.getaddrinfo("example.com", 443); out["dns"] = "ALLOWED"
except OSError:
    out["dns"] = "DENIED"
# 2) the offline pieces still work with zero network
with tempfile.TemporaryDirectory() as d:
    fx = jt.JournalFixture(d)
    with fx.begin():
        out["journal"] = jt.mod.inspect(fx.root, jt.NONCE)
out["argv"] = contain.systemd_run_argv(contain.unit_name("n" * 30, "api"), ["/usr/bin/docker", "ps"])[0]
print(json.dumps(out, sort_keys=True))
"""


def _netns_prefix():
    if shutil.which("unshare") is None:
        return None
    for prefix in (["unshare", "-n"], ["sudo", "-n", "unshare", "-n"]):
        try:
            done = subprocess.run(prefix + ["true"], capture_output=True, timeout=30)
        except (OSError, subprocess.SubprocessError):
            continue
        if done.returncode == 0:
            return prefix
    return None


class DeniedNetworkTests(unittest.TestCase):
    def test_offline_modules_run_inside_an_empty_network_namespace(self):
        prefix = _netns_prefix()
        if prefix is None:
            self.fail("NETNS_UNAVAILABLE: cannot create a network namespace; this gate must not be skipped")
        done = subprocess.run(
            prefix + [sys.executable, "-I", "-c", NETNS_CHILD, str(ROOT), str(ROOT / "tests")],
            capture_output=True, text=True, timeout=120)
        self.assertEqual(done.returncode, 0, done.stderr[-800:])
        data = json.loads(done.stdout.strip().splitlines()[-1])
        self.assertEqual(data["connect"], "DENIED")
        self.assertEqual(data["dns"], "DENIED")
        self.assertEqual(data["journal"], "APPLYING")
        self.assertEqual(data["argv"], "/usr/bin/systemd-run")


if __name__ == "__main__":
    unittest.main()
