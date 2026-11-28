import hashlib
import importlib.util
import json
import os
import pathlib
import tempfile
import unittest

SRC = pathlib.Path(__file__).resolve().parents[1] / "scripts/qa/operators/qa525-g6-backup-gate-ro.py"
spec = importlib.util.spec_from_file_location("g6bg", SRC)
g6bg = importlib.util.module_from_spec(spec)
spec.loader.exec_module(g6bg)

REPO = "9" * 64
EPOCH = "a" * 32


def canon(doc):
    return (json.dumps(doc, sort_keys=True, separators=(",", ":"), ensure_ascii=True, allow_nan=False) + "\n").encode("ascii")


def absent_runner(argv):
    return 0, "LoadState=not-found\nActiveState=inactive\nSubState=dead\n"


class Case(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        t = pathlib.Path(self.tmp.name)
        self.state = t / "g6"
        self.state.mkdir(mode=0o700)
        os.chmod(self.state, 0o700)
        (self.state / "key-v2").write_bytes(b"k" * 40)
        os.chmod(self.state / "key-v2", 0o600)
        for name in ("repo-v2", "state-v2"):
            (self.state / name).mkdir()
            os.chmod(self.state / name, 0o700)
        self.ident = self.state / "state-v2" / "identity.json"
        self.ident.write_bytes(canon({"schema": "qa525-g6-v2-identity", "restic_repo_id": REPO, "key_epoch": EPOCH}))
        os.chmod(self.ident, 0o600)
        self.restic = t / "restic"
        self.restic.write_bytes(b"restic-bin")
        os.chmod(self.restic, 0o755)
        self.silo = t / "silo"
        self.silo.mkdir()
        (self.silo / "a").write_bytes(b"x" * 10)
        self.silo = pathlib.Path(os.path.realpath(self.silo))
        self.pin = hashlib.sha256(b"restic-bin").hexdigest()
        self.lines = []

    def tearDown(self):
        self.tmp.cleanup()

    def run_gate(self, runner=absent_runner, **over):
        argv = ["--repo-id", REPO, "--key-epoch", EPOCH, "--restic-sha256", self.pin]
        deps = {"out": self.lines.append, "euid": 0, "state_root": str(self.state), "restic": str(self.restic),
                "silo": str(self.silo), "runner": runner}
        deps.update(over)
        return g6bg.main(argv, deps)

    def stop(self):
        return [l for l in self.lines if l.startswith("G6BG_STOP")]


@unittest.skipUnless(os.geteuid() == 0, "root-owned metadata checks need root")
class GateTests(Case):
    def test_pass(self):
        self.assertEqual(self.run_gate(), 0)
        self.assertIn("G6BG_GATE=PASS", self.lines)
        self.assertIn("G6BG_SILO_BYTES=10", self.lines)
        self.assertIn("G6BG_WATCHDOG=ABSENT", self.lines)

    def test_identity_mismatch(self):
        self.ident.write_bytes(canon({"schema": "qa525-g6-v2-identity", "restic_repo_id": "8" * 64, "key_epoch": EPOCH}))
        self.assertEqual(self.run_gate(), 1)
        self.assertEqual(self.stop(), ["G6BG_STOP:G6BG_IDENTITY_MISMATCH"])

    def test_identity_not_canonical(self):
        self.ident.write_bytes(json.dumps({"schema": "qa525-g6-v2-identity", "restic_repo_id": REPO, "key_epoch": EPOCH}).encode())
        self.assertEqual(self.run_gate(), 1)
        self.assertEqual(self.stop(), ["G6BG_STOP:G6BG_IDENTITY_INVALID"])

    def test_identity_extra_field(self):
        self.ident.write_bytes(canon({"schema": "qa525-g6-v2-identity", "restic_repo_id": REPO, "key_epoch": EPOCH, "x": 1}))
        self.assertEqual(self.run_gate(), 1)

    def test_key_wrong_size_or_mode(self):
        (self.state / "key-v2").write_bytes(b"k" * 41)
        self.assertEqual(self.run_gate(), 1)
        self.assertEqual(self.stop(), ["G6BG_STOP:G6BG_KEY_SIZE"])
        (self.state / "key-v2").write_bytes(b"k" * 40)
        os.chmod(self.state / "key-v2", 0o644)
        self.lines.clear()
        self.assertEqual(self.run_gate(), 1)
        self.assertEqual(self.stop(), ["G6BG_STOP:G6BG_META_MISMATCH"])

    def test_repo_dir_mode(self):
        os.chmod(self.state / "repo-v2", 0o755)
        self.assertEqual(self.run_gate(), 1)

    def test_restic_hash_and_writable(self):
        self.restic.write_bytes(b"other")
        self.assertEqual(self.run_gate(), 1)
        self.assertEqual(self.stop(), ["G6BG_STOP:G6BG_RESTIC_HASH"])
        self.restic.write_bytes(b"restic-bin")
        os.chmod(self.restic, 0o775)
        self.lines.clear()
        self.assertEqual(self.run_gate(), 1)
        self.assertEqual(self.stop(), ["G6BG_STOP:G6BG_META_MISMATCH"])

    def test_silo_symlink_inside(self):
        os.symlink("a", self.silo / "link")
        self.assertEqual(self.run_gate(), 1)
        self.assertEqual(self.stop(), ["G6BG_STOP:G6BG_SILO_INVALID"])

    def test_silo_path_is_symlink(self):
        link = pathlib.Path(self.tmp.name) / "silolink"
        os.symlink(self.silo, link)
        self.assertEqual(self.run_gate(silo=str(link)), 1)
        self.assertEqual(self.stop(), ["G6BG_STOP:G6BG_SILO_INVALID"])

    def test_space(self):
        orig = g6bg.shutil.disk_usage
        g6bg.shutil.disk_usage = lambda p: type("U", (), {"free": 1})()
        try:
            self.assertEqual(self.run_gate(), 1)
        finally:
            g6bg.shutil.disk_usage = orig
        self.assertEqual(self.stop(), ["G6BG_STOP:G6BG_SPACE"])

    def test_watchdog_present(self):
        present = lambda argv: (0, "LoadState=loaded\nActiveState=active\nSubState=waiting\n")
        self.assertEqual(self.run_gate(runner=present), 1)
        self.assertEqual(self.stop(), ["G6BG_STOP:G6BG_WATCHDOG_PRESENT"])

    def test_watchdog_query_failure_is_stop(self):
        self.assertEqual(self.run_gate(runner=lambda argv: (1, "")), 1)

    def test_not_root(self):
        self.assertEqual(self.run_gate(euid=1000), 1)
        self.assertEqual(self.stop(), ["G6BG_STOP:G6BG_NOT_ROOT"])

    def test_usage(self):
        out = []
        for argv in ([], ["--repo-id", "x", "--key-epoch", EPOCH, "--restic-sha256", self.pin],
                     ["--repo-id", REPO, "--repo-id", REPO, "--restic-sha256", self.pin]):
            self.assertEqual(g6bg.main(argv, {"out": out.append, "euid": 0}), 1)
        self.assertTrue(all(l == "G6BG_STOP:G6BG_USAGE" for l in out))

    def test_never_prints_key(self):
        self.run_gate()
        self.assertFalse(any("kkkk" in l for l in self.lines))


if __name__ == "__main__":
    unittest.main()
