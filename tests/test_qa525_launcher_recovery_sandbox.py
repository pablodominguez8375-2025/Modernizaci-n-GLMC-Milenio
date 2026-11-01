"""Adversarial launcher rollback tests: disposable temp paths ONLY."""
import importlib.util
import hashlib
import json
import os
from pathlib import Path
import signal
import stat
import subprocess
import sys
import tempfile
import time
import unittest

ROOT = Path(__file__).resolve().parents[1]
MODULE = ROOT / "scripts/qa/operators/qa525-launcher-recovery-sandbox.py"
spec = importlib.util.spec_from_file_location("qa525_recovery_sandbox", MODULE)
m = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = m
spec.loader.exec_module(m)
CUT = "a" * 64
REVERSAL = "b" * 64
SHA = "c" * 40
DIGEST = "sha256:" + "d" * 64
OLD = b"original synthetically reviewed launcher\n"
NEW = b"synthetic signed replacement launcher\n"


class RecoverySandboxTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory(prefix=m.PREFIX)
        self.addCleanup(self.tmp.cleanup)
        self.root = Path(self.tmp.name)
        self.state = self.root / "state"
        self.live_dir = self.root / "live"
        self.state.mkdir(mode=0o700)
        self.live_dir.mkdir(mode=0o700)
        self.live = self.live_dir / "pmgm-qa-update"
        self.live.write_bytes(NEW)
        self.live.chmod(0o700)
        self.backup = self.state / ("old-" + CUT + ".bin")
        self.backup.write_bytes(OLD)
        self.backup.chmod(0o700)
        self.state.joinpath("recovery.lock").write_bytes(b"lock\n")
        self.state.joinpath("recovery.lock").chmod(0o600)
        self.receipt = self.state / ("receipt-" + CUT + ".json")
        self.source = {
            "status": "LAUNCHER_ATOMIC_SWAP_COMMITTED",
            "source_sha": SHA,
            "release_asset_sha256": DIGEST,
            "old_launcher_sha256": m.sha(OLD),
            "new_launcher_sha256": m.sha(NEW),
            "candidate_operator_sha256": "e" * 64,
            "nonce": CUT,
        }
        self.receipt.write_bytes(m.encode(self.source))
        self.receipt.chmod(0o600)
        self.now = int(time.time())
        self.approval = {
            "action": "rollback-root-launcher-supervised",
            "scope": m.SCOPE, "operator": "soporte@glmdch",
            "approved": True, "cut_nonce": CUT, "nonce": REVERSAL,
            "source_sha": SHA, "release_asset_sha256": DIGEST,
            "old_launcher_sha256": m.sha(OLD),
            "new_launcher_sha256": m.sha(NEW),
            "issued_at": self.now - 5, "expires_at": self.now + 120,
        }

    def call(self, approval=None, **kwargs):
        return m.rollback(self.root, self.approval if approval is None
                          else approval, now=self.now, **kwargs)

    def test_authorized_reversal_preserves_original_receipt_and_backup(self):
        result = self.call()
        self.assertEqual("SIMULATED_LAUNCHER_ROLLBACK_PASS_NO_HOST_CHANGE",
                         result["status"])
        self.assertEqual(OLD, self.live.read_bytes())
        self.assertEqual(OLD, self.backup.read_bytes())
        self.assertEqual(self.source, json.loads(self.receipt.read_text()))
        self.assertFalse((self.state / ("rollback-intent-" + REVERSAL + ".json")).exists())
        self.assertEqual(NEW, (self.state /
                               ("rollback-new-" + REVERSAL + ".bin")).read_bytes())
        self.assertEqual("ROLLBACK_ATOMIC_SWAP_COMMITTED",
            json.loads((self.state / ("rollback-receipt-" + REVERSAL + ".json"))
                       .read_text())["status"])
        self.assertFalse(result["may_install"])
        self.assertFalse(result["may_apply"])
        self.assertFalse(result["production_touched"])
        with self.assertRaises(m.Blocked):
            self.call()  # A completed recovery cannot replay the same act.

    def test_wrong_approval_expired_or_reused_nonce_blocks_without_writes(self):
        for key, value in (
            ("approved", False), ("operator", "somebody"), ("scope", "API_WEB"),
            ("cut_nonce", REVERSAL), ("nonce", CUT),
            ("old_launcher_sha256", "f" * 64), ("expires_at", self.now),
        ):
            with self.subTest(field=key):
                altered = {**self.approval, key: value}
                with self.assertRaises(m.Blocked):
                    self.call(altered)
                self.assertEqual(NEW, self.live.read_bytes())
                self.assertEqual(3, len(list(self.state.iterdir())))

    def test_forged_receipt_backup_drift_and_unknown_journal_block(self):
        original = self.receipt.read_bytes()
        for issue in ("receipt", "backup", "pending"):
            with self.subTest(issue=issue):
                if issue == "receipt":
                    altered = dict(self.source)
                    altered["new_launcher_sha256"] = "0" * 64
                    self.receipt.write_bytes(m.encode(altered))
                elif issue == "backup":
                    self.backup.write_bytes(b"corrupted backup\n")
                else:
                    (self.state / ("intent-" + CUT + ".json")).write_text("pending")
                with self.assertRaises(m.Blocked):
                    self.call()
                self.assertEqual(NEW, self.live.read_bytes())
                self.receipt.write_bytes(original)
                self.backup.write_bytes(OLD)
                pending = self.state / ("intent-" + CUT + ".json")
                if pending.exists():
                    pending.unlink()

    def test_failure_before_or_after_swap_preserves_intent_and_blocks_retry(self):
        for phase, expected in (("after_backup", NEW), ("after_swap", OLD)):
            with self.subTest(phase=phase):
                with tempfile.TemporaryDirectory(prefix=m.PREFIX) as td:
                    import shutil
                    other = Path(td)
                    shutil.copytree(self.root / "state", other / "state")
                    shutil.copytree(self.root / "live", other / "live")
                    def crash(stage):
                        if stage == phase:
                            raise RuntimeError("synthetic interruption")
                    with self.assertRaisesRegex(RuntimeError, "interruption"):
                        m.rollback(other, self.approval, now=self.now,
                                   checkpoint=crash)
                    self.assertEqual(expected,
                                     (other / "live/pmgm-qa-update").read_bytes())
                    self.assertTrue((other / "state" /
                              ("rollback-intent-" + REVERSAL + ".json")).is_file())
                    with self.assertRaises(m.Blocked):
                        m.rollback(other, self.approval, now=self.now)
                    self.assertEqual(expected,
                                     (other / "live/pmgm-qa-update").read_bytes())

    def test_never_accepts_host_paths_or_symlink_backups(self):
        with self.assertRaises(m.Blocked):
            m.rollback(Path("/opt/centenario"), self.approval, now=self.now)
        self.backup.unlink()
        self.backup.symlink_to(self.live)
        with self.assertRaises(m.Blocked):
            self.call()
        self.assertEqual(NEW, self.live.read_bytes())

    def test_actual_sigkill_after_atomic_swap_does_not_auto_retry(self):
        code = """
import importlib.util, os, signal, sys, time
from pathlib import Path
spec = importlib.util.spec_from_file_location("qa525_child", sys.argv[1])
mod = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = mod
spec.loader.exec_module(mod)
import json
approval = json.loads(Path(sys.argv[3]).read_text())
def checkpoint(stage):
    if stage == "after_swap":
        os.kill(os.getpid(), signal.SIGKILL)
mod.rollback(Path(sys.argv[2]), approval, now=int(sys.argv[4]),
             checkpoint=checkpoint)
"""
        approval_path = self.root / "approval.synthetic.json"
        approval_path.write_text(json.dumps(self.approval))
        process = subprocess.run(
            [sys.executable, "-B", "-c", code, str(MODULE),
             str(self.root), str(approval_path), str(self.now)],
            capture_output=True, text=True, timeout=20)
        self.assertEqual(-signal.SIGKILL, process.returncode,
                         process.stderr)
        self.assertEqual(OLD, self.live.read_bytes())
        self.assertTrue((self.state /
                         ("rollback-intent-" + REVERSAL + ".json")).is_file())
        with self.assertRaises(m.Blocked):
            self.call()
        self.assertEqual(OLD, self.live.read_bytes())


if __name__ == "__main__":
    unittest.main()
