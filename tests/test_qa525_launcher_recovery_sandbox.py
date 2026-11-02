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

    def test_approval_mutation_after_backup_blocks_before_swap(self):
        def mutate(stage):
            if stage == "after_backup":
                self.approval["nonce"] = "f" * 64
        with self.assertRaisesRegex(
                m.Blocked, "RECOVERY_APPROVAL_CHANGED_DURING_TRANSACTION"):
            self.call(checkpoint=mutate)
        self.assertEqual(NEW, self.live.read_bytes())
        self.assertTrue((self.state /
                         ("rollback-intent-" + REVERSAL + ".json")).exists())
        with self.assertRaises(m.Blocked):
            self.call()

    def delayed_packets(self):
        path = ROOT / "scripts/qa/operators/qa525-launcher-replacement-contract.py"
        sp = importlib.util.spec_from_file_location("qa525_delayed_test_policy", path)
        policy = importlib.util.module_from_spec(sp)
        sp.loader.exec_module(policy)
        claim = {
            "purpose": "QA525_DELAYED_RECOVERY_REVIEW_UNTRUSTED",
            "source_sha": "f" * 40,
            "release_asset_sha256": "sha256:" + "7" * 64,
            "manifest_sha256": "8" * 64,
            "executor_sha256": "9" * 64,
            "issued_at": self.now - 5,
            "expires_at": self.now + 120,
        }
        approval = {
            "action": policy.DELAYED_RECOVERY_ACTION,
            "scope": policy.DELAYED_RECOVERY_SCOPE,
            "environment": policy.ENVIRONMENT,
            "operator": "soporte@glmdch",
            "approved": True,
            "cut_source_sha": SHA,
            "cut_release_asset_sha256": DIGEST,
            "cut_nonce": CUT,
            "cut_receipt_sha256": policy.frozen_sha(self.source),
            "old_launcher_sha256": m.sha(OLD),
            "new_launcher_sha256": m.sha(NEW),
            "recovery_source_sha": claim["source_sha"],
            "recovery_release_asset_sha256":
                claim["release_asset_sha256"],
            "recovery_manifest_sha256": claim["manifest_sha256"],
            "recovery_executor_sha256": claim["executor_sha256"],
            "nonce": REVERSAL,
            "issued_at": self.now - 5,
            "expires_at": self.now + 120,
        }
        return claim, approval

    def test_delayed_dual_provenance_recovery_only_in_disposable_sandbox(self):
        # A is a historical committed receipt; the original approval is not
        # consulted. B is a distinct fresh *untrusted* synthetic identity.
        claim, approval = self.delayed_packets()
        self.assertNotEqual(self.source["source_sha"], claim["source_sha"])
        report = m.simulate_delayed_dual_recovery(
            self.root, claim, approval, now=self.now)
        self.assertEqual("SIMULATED_DELAYED_DUAL_PROVENANCE_NO_HOST_CHANGE",
                         report["status"])
        self.assertFalse(report["approved_for_host_recovery"])
        self.assertFalse(report["recovery_claim_authenticated"])
        self.assertEqual(OLD, self.live.read_bytes())
        self.assertEqual(OLD, self.backup.read_bytes())
        self.assertEqual(self.source, json.loads(self.receipt.read_text()))
        self.assertEqual(NEW, (self.state /
            ("rollback-new-" + REVERSAL + ".bin")).read_bytes())
        with self.assertRaises(m.Blocked):
            m.simulate_delayed_dual_recovery(
                self.root, claim, approval, now=self.now)

    def test_delayed_dual_provenance_rejects_unbound_sources_without_writes(self):
        claim, approval = self.delayed_packets()
        for change in (
            {"source_sha": "0" * 40},
            {"release_asset_sha256": "sha256:" + "0" * 64},
            {"manifest_sha256": "0" * 64},
            {"executor_sha256": "0" * 64},
            {"expires_at": self.now},
        ):
            with self.subTest(change=change):
                with self.assertRaises(m.Blocked):
                    m.simulate_delayed_dual_recovery(
                        self.root, {**claim, **change}, approval, now=self.now)
                self.assertEqual(NEW, self.live.read_bytes())
                self.assertEqual(3, len(list(self.state.iterdir())))
        for change in (
            {"cut_receipt_sha256": "0" * 64},
            {"recovery_source_sha": "0" * 40},
            {"nonce": CUT},
            {"scope": "API_AND_WEB"},
            {"expires_at": self.now},
        ):
            with self.subTest(change=change):
                with self.assertRaises(m.Blocked):
                    m.simulate_delayed_dual_recovery(
                        self.root, claim, {**approval, **change}, now=self.now)
                self.assertEqual(NEW, self.live.read_bytes())
                self.assertEqual(3, len(list(self.state.iterdir())))

    def test_delayed_dual_provenance_mutation_aborts_before_swap(self):
        claim, approval = self.delayed_packets()
        def mutate(stage):
            if stage == "after_backup":
                claim["manifest_sha256"] = "0" * 64
        with self.assertRaisesRegex(
                m.Blocked, "DELAYED_SANDBOX_CONSENT_OR_CLAIM_CHANGED"):
            m.simulate_delayed_dual_recovery(
                self.root, claim, approval, now=self.now, checkpoint=mutate)
        self.assertEqual(NEW, self.live.read_bytes())
        self.assertTrue((self.state /
            ("rollback-intent-" + REVERSAL + ".json")).is_file())
        with self.assertRaises(m.Blocked):
            m.simulate_delayed_dual_recovery(
                self.root, claim, approval, now=self.now)

    def test_delayed_dual_provenance_post_swap_crash_blocks_replay(self):
        claim, approval = self.delayed_packets()
        def crash(stage):
            if stage == "after_swap":
                raise RuntimeError("synthetic post-swap crash")
        with self.assertRaisesRegex(RuntimeError, "post-swap crash"):
            m.simulate_delayed_dual_recovery(
                self.root, claim, approval, now=self.now, checkpoint=crash)
        self.assertEqual(OLD, self.live.read_bytes())
        self.assertTrue((self.state /
            ("rollback-intent-" + REVERSAL + ".json")).is_file())
        with self.assertRaises(m.Blocked):
            m.simulate_delayed_dual_recovery(
                self.root, claim, approval, now=self.now)

    def test_delayed_dual_provenance_refuses_real_host_path(self):
        claim, approval = self.delayed_packets()
        with self.assertRaises(m.Blocked):
            m.simulate_delayed_dual_recovery(
                Path("/opt/centenario"), claim, approval, now=self.now)

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



class InterruptedAPendingSandboxTests(unittest.TestCase):
    """No-root recovery A->B in /tmp only; never authorizes real QA."""

    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory(prefix=m.PREFIX)
        self.addCleanup(self.tmp.cleanup)
        self.root = Path(self.tmp.name)
        self.state = self.root / "state"
        self.live_dir = self.root / "live"
        self.recovery = self.root / "recovery"
        for folder in (self.state, self.live_dir, self.recovery):
            folder.mkdir(mode=0o700)
        self.live = self.live_dir / "pmgm-qa-update"
        self.old, self.new = OLD, NEW
        self.nonce_a = CUT
        self.nonce_b = REVERSAL
        self.intent = {
            "status": "SWAP_INTENT_NO_DEPLOYMENT",
            "source_sha": SHA,
            "release_asset_sha256": DIGEST,
            "old_launcher_sha256": m.sha(self.old),
            "new_launcher_sha256": m.sha(self.new),
            "nonce": self.nonce_a,
        }
        self.intent_path = self.state / ("intent-" + CUT + ".json")
        self.intent_path.write_bytes(m.encode(self.intent))
        self.intent_path.chmod(0o600)
        self.backup = self.state / ("old-" + CUT + ".bin")
        self.backup.write_bytes(self.old)
        self.backup.chmod(0o700)
        self.lock = self.state / "recovery.lock"
        self.lock.write_bytes(b"lock")
        self.lock.chmod(0o600)
        self.now = int(time.time())
        self.claim = {
            "purpose": "QA525_DELAYED_RECOVERY_REVIEW_UNTRUSTED",
            "source_sha": "e" * 40,
            "release_asset_sha256": "sha256:" + "f" * 64,
            "manifest_sha256": "4" * 64,
            "executor_sha256": "5" * 64,
            "issued_at": self.now - 5,
            "expires_at": self.now + 120,
        }
        canonical_intent_sha = m.sha(m.encode(self.intent).strip())
        self.approval = {
            "action": "review-pending-root-launcher-reconciliation",
            "scope": "QA525_LAUNCHER_ONLY_PENDING_RECONCILIATION_NO_API_WEB",
            "environment": "QA_GLMDCH_ONLY",
            "operator": "soporte@glmdch",
            "approved": True,
            "cut_source_sha": self.intent["source_sha"],
            "cut_release_asset_sha256": self.intent["release_asset_sha256"],
            "cut_nonce": CUT, "cut_intent_sha256": canonical_intent_sha,
            "old_launcher_sha256": self.intent["old_launcher_sha256"],
            "new_launcher_sha256": self.intent["new_launcher_sha256"],
            "recovery_source_sha": self.claim["source_sha"],
            "recovery_release_asset_sha256": self.claim["release_asset_sha256"],
            "recovery_manifest_sha256": self.claim["manifest_sha256"],
            "recovery_executor_sha256": self.claim["executor_sha256"],
            "nonce": REVERSAL,
            "issued_at": self.now - 5,
            "expires_at": self.now + 120,
        }
        self.live.write_bytes(self.new)
        self.live.chmod(0o700)

    def call(self, *, claim=None, approval=None, checkpoint=None):
        return m.simulate_interrupted_A_pending_recovery(
            self.root, self.claim if claim is None else claim,
            self.approval if approval is None else approval,
            now=self.now,
            checkpoint=(checkpoint if checkpoint is not None
                        else lambda name: None))

    def test_post_swap_without_A_receipt_restores_in_tmp_with_B_receipt(self):
        a_before = self.intent_path.read_bytes()
        b_before = self.backup.read_bytes()
        result = self.call()
        self.assertEqual(
            "SIMULATED_INTERRUPTED_A_RESTORED_NO_HOST_CHANGE",
            result["status"])
        self.assertEqual("PENDING_AFTER_ATOMIC_SWAP_NO_RECEIPT",
                         result["phase"])
        self.assertEqual(self.old, self.live.read_bytes())
        self.assertEqual(a_before, self.intent_path.read_bytes())
        self.assertEqual(b_before, self.backup.read_bytes())
        receipt = self.recovery / (
            "pending-receipt-" + self.nonce_b + ".json")
        self.assertEqual(
            "PENDING_LAUNCHER_RECOVERY_RECONCILIATION_COMMITTED",
            json.loads(receipt.read_text())["status"])
        self.assertFalse(
            (self.recovery / ("pending-intent-" + self.nonce_b +
                              ".json")).exists())
        self.assertEqual(self.new, (
            self.recovery /
            ("pending-snapshot-" + self.nonce_b + ".bin")).read_bytes())
        for field in ("may_apply", "may_recover", "may_install",
                      "reconciliation_complete", "authenticated",
                      "production_touched"):
            self.assertIs(result[field], False)
        with self.assertRaises(m.Blocked):
            self.call()

    def test_pre_swap_without_receipt_preserves_original_bytes(self):
        self.live.write_bytes(self.old)
        a_before = self.intent_path.read_bytes()
        result = self.call()
        self.assertEqual("PENDING_BEFORE_ATOMIC_SWAP", result["phase"])
        self.assertEqual(self.old, self.live.read_bytes())
        self.assertEqual(a_before, self.intent_path.read_bytes())
        self.assertEqual(self.old, (
            self.recovery / ("pending-snapshot-" + REVERSAL +
                             ".bin")).read_bytes())
        self.assertFalse(result["may_install"])

    def test_forged_A_receipt_unknown_journal_and_bad_backup_stop(self):
        old_live = self.live.read_bytes()
        receipt_a = self.state / ("receipt-" + CUT + ".json")
        receipt_a.write_text('{"status":"LAUNCHER_ATOMIC_SWAP_COMMITTED"}')
        with self.assertRaises(m.Blocked):
            self.call()
        receipt_a.unlink()
        unknown = self.state / "unknown.txt"
        unknown.write_bytes(b"evil")
        with self.assertRaises(m.Blocked):
            self.call()
        unknown.unlink()
        self.backup.write_bytes(b"not original")
        with self.assertRaises(m.Blocked):
            self.call()
        self.assertEqual(old_live, self.live.read_bytes())
        self.assertEqual([], list(self.recovery.iterdir()))

    def test_B_claim_stale_or_nonce_replay_or_consent_forged_block(self):
        forged_cases = (
            ({"expires_at": self.now}, {}),
            ({"source_sha": SHA}, {}),
            ({"release_asset_sha256": DIGEST}, {}),
            ({}, {"approved": False}),
            ({}, {"nonce": CUT}),
            ({}, {"cut_intent_sha256": "0" * 64}),
            ({}, {"recovery_manifest_sha256": "0" * 64}),
            ({}, {"issued_at": self.now + 5}),
            ({}, {"expires_at": self.now + 901}),
        )
        for claim_changes, approval_changes in forged_cases:
            with self.subTest(claim=claim_changes, consent=approval_changes):
                with self.assertRaises(m.Blocked):
                    self.call(
                        claim={**self.claim, **claim_changes},
                        approval={**self.approval, **approval_changes})
                self.assertEqual([], list(self.recovery.iterdir()))
                self.assertEqual(self.new, self.live.read_bytes())

    def test_sigkill_after_intent_after_swap_and_receipt_block_replay(self):
        child_code = """
import importlib.util, json, os, signal, sys
from pathlib import Path
spec = importlib.util.spec_from_file_location("qa525_synthetic_child", sys.argv[1])
m = importlib.util.module_from_spec(spec)
spec.loader.exec_module(m)
claim = json.loads(Path(sys.argv[3]).read_text())
approval = json.loads(Path(sys.argv[4]).read_text())
def halt(stage):
    if stage == sys.argv[6]:
        os.kill(os.getpid(), signal.SIGKILL)
m.simulate_interrupted_A_pending_recovery(
    Path(sys.argv[2]), claim, approval, now=int(sys.argv[5]),
    checkpoint=halt)
"""
        for point, expected_live in (
            ("after_intent", self.new),
            ("after_swap", self.old),
            ("after_receipt", self.old),
        ):
            with self.subTest(point=point):
                # Each SIGKILL run gets an independent fresh disposable root.
                with tempfile.TemporaryDirectory(prefix=m.PREFIX) as td:
                    root = Path(td)
                    for dirname in ("state", "live", "recovery"):
                        (root / dirname).mkdir(mode=0o700)
                    live = root / "live" / "pmgm-qa-update"
                    live.write_bytes(self.new)
                    live.chmod(0o700)
                    intent = root / "state" / ("intent-" + CUT + ".json")
                    intent.write_bytes(m.encode(self.intent))
                    intent.chmod(0o600)
                    backup = root / "state" / ("old-" + CUT + ".bin")
                    backup.write_bytes(self.old)
                    backup.chmod(0o700)
                    lock = root / "state" / "recovery.lock"
                    lock.write_bytes(b"lock")
                    lock.chmod(0o600)
                    files = []
                    for name, packet in (
                            ("claim.json", self.claim),
                            ("approval.json", self.approval)):
                        p = root / name
                        p.write_text(json.dumps(packet))
                        files.append(p)
                    process = subprocess.run(
                        [sys.executable, "-B", "-c", child_code, str(MODULE),
                         str(root), str(files[0]), str(files[1]),
                         str(self.now), point],
                        capture_output=True, text=True, timeout=20)
                    self.assertEqual(-signal.SIGKILL,
                                     process.returncode, process.stderr)
                    self.assertEqual(expected_live, live.read_bytes())
                    self.assertTrue((root / "recovery" /
                        ("pending-intent-" + REVERSAL + ".json")).exists())
                    self.assertEqual(m.encode(self.intent), intent.read_bytes())
                    with self.assertRaises(m.Blocked):
                        m.simulate_interrupted_A_pending_recovery(
                            root, self.claim, self.approval, now=self.now)

    def test_no_real_host_path_and_symlink_rejected(self):
        with self.assertRaises(m.Blocked):
            m.simulate_interrupted_A_pending_recovery(
                Path("/opt/centenario"), self.claim, self.approval,
                now=self.now)
        self.backup.unlink()
        self.backup.symlink_to(self.live)
        with self.assertRaises(m.Blocked):
            self.call()
        self.assertEqual(self.new, self.live.read_bytes())



if __name__ == "__main__":
    unittest.main()
