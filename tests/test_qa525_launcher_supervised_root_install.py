"""QA525 root installer adversarial offline guards. NEVER touches physical host."""
import ast
from contextlib import nullcontext
import importlib.util
import io
import json
import os
from pathlib import Path
import socket
import stat
import sys
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import Mock, patch

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "scripts/qa/operators/qa525-launcher-supervised-root-install.py"
spec = importlib.util.spec_from_file_location("qa525_signed_root_installer", SOURCE)
m = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = m
spec.loader.exec_module(m)

SHA = "a" * 40
NONCE = "b" * 64


def fake_modules(*, gate_error=None, approval_error=None, contract_error=None):
    inspector = SimpleNamespace(inspect=Mock())
    preflight = SimpleNamespace(
        signed_candidate_preflight=Mock(side_effect=gate_error))
    signed = SimpleNamespace(verify_current=Mock())
    approval = SimpleNamespace(
        read_root_approval=Mock(side_effect=approval_error))
    contract = SimpleNamespace(
        assess_review=Mock(side_effect=contract_error))
    template = SimpleNamespace(render=Mock())
    retained = SimpleNamespace(inspect_retained=Mock())
    return inspector, preflight, signed, approval, contract, template, retained


class RootLauncherInstallerTests(unittest.TestCase):
    def test_exact_five_shared_locks_in_operational_order(self):
        self.assertEqual(
            [str(p) for p in m.LOCKS],
            ["/opt/centenario/qa-api-refresh/operation.lock",
             "/opt/centenario/qa-auto-lite/web-update.lock",
             "/run/lock/pmgm-master-qa-604.lock",
             "/run/lock/pmgm-qa754-web-hotfix.lock",
             "/run/lock/pmgm-qa754-api-activation.lock"])

    def test_wrong_host_blocks_before_import_or_filesystem(self):
        with patch.object(m.os, "geteuid", return_value=0), \
             patch.object(m.socket, "gethostname", return_value="srv01"), \
             patch.object(m, "verify_inputs") as modules:
            with self.assertRaisesRegex(m.Blocked,
                                        "SIGNED_LAUNCHER_ROOT_GLMDCH_ONLY"):
                m.replace_approved()
            modules.assert_not_called()
        with patch.object(m.os, "geteuid", return_value=1000), \
             patch.object(m.socket, "gethostname", return_value="glmdch"), \
             patch.object(m, "verify_inputs") as modules:
            with self.assertRaises(m.Blocked):
                m.replace_approved()
            modules.assert_not_called()

    def test_signature_failure_blocks_before_approval_and_any_journal(self):
        modules = fake_modules(gate_error=m.Blocked("SIGNATURE_UNAVAILABLE"))
        with patch.object(m, "require_host"), \
             patch.object(m, "verify_inputs", return_value=modules), \
             patch.object(m, "root_chain"), \
             patch.object(m, "all_five_locks", return_value=nullcontext()), \
             patch.object(m, "scan_history", return_value=set()), \
             patch.object(m, "write_once") as writer, \
             patch.object(m.os, "replace") as swap:
            with self.assertRaisesRegex(m.Blocked, "SIGNATURE_UNAVAILABLE"):
                m.replace_approved()
            modules[3].read_root_approval.assert_not_called()
            writer.assert_not_called()
            swap.assert_not_called()

    def test_missing_approval_blocks_without_transaction(self):
        modules = fake_modules(
            approval_error=m.Blocked("LAUNCHER_APPROVAL_PATH_MISSING"))
        modules[1].signed_candidate_preflight.return_value = {
            "status": "SIGNED_LAUNCHER_CANDIDATE_VERIFIED_NO_INSTALL",
            "source_sha": SHA}
        with patch.object(m, "require_host"), \
             patch.object(m, "verify_inputs", return_value=modules), \
             patch.object(m, "root_chain"), \
             patch.object(m, "all_five_locks", return_value=nullcontext()), \
             patch.object(m, "scan_history", return_value=set()), \
             patch.object(m, "write_once") as writer, \
             patch.object(m.os, "replace") as swap:
            with self.assertRaisesRegex(m.Blocked,
                                        "LAUNCHER_APPROVAL_PATH_MISSING"):
                m.replace_approved()
            writer.assert_not_called()
            swap.assert_not_called()

    def test_invalid_signed_cut_policy_rejects_before_persistence(self):
        modules = fake_modules(contract_error=m.Blocked("EXPIRED_NONCE"))
        modules[1].signed_candidate_preflight.return_value = {
            "status": "SIGNED_LAUNCHER_CANDIDATE_VERIFIED_NO_INSTALL",
            "source_sha": SHA}
        modules[3].read_root_approval.return_value = {"nonce": NONCE}
        with patch.object(m, "require_host"), \
             patch.object(m, "verify_inputs", return_value=modules), \
             patch.object(m, "root_chain"), \
             patch.object(m, "all_five_locks", return_value=nullcontext()), \
             patch.object(m, "scan_history", return_value=set()), \
             patch.object(m, "write_once") as writer:
            with self.assertRaisesRegex(m.Blocked, "EXPIRED_NONCE"):
                m.replace_approved()
            writer.assert_not_called()

    def test_durable_exclusive_write_and_non_overwrite(self):
        with tempfile.TemporaryDirectory() as td:
            dest = Path(td) / "intent.json"
            content = m.json_bytes({"nonce": NONCE, "status": "SWAP_INTENT_NO_DEPLOYMENT"})
            m.write_once(dest, content)
            self.assertEqual(dest.read_bytes(), content)
            self.assertEqual(stat.S_IMODE(dest.stat().st_mode), 0o600)
            with self.assertRaises(FileExistsError):
                m.write_once(dest, b"alteration")
            self.assertEqual(dest.read_bytes(), content)

    def test_pending_and_unknown_journals_block_replay(self):
        with tempfile.TemporaryDirectory() as td:
            old_state = m.STATE
            try:
                m.STATE = Path(td)
                with patch.object(m, "root_chain"):
                    self.assertEqual(set(), m.scan_history())
                    (m.STATE / ("intent-" + NONCE + ".json")).write_text("{}")
                    with self.assertRaisesRegex(
                            m.Blocked, "ROOT_LAUNCHER_PENDING_MANUAL_RECONCILIATION"):
                        m.scan_history()
                    (m.STATE / ("intent-" + NONCE + ".json")).unlink()
                    (m.STATE / "unknown.tmp").write_text("tamper")
                    with self.assertRaisesRegex(m.Blocked,
                                                "ROOT_LAUNCHER_UNKNOWN_JOURNAL_ENTRY"):
                        m.scan_history()
            finally:
                m.STATE = old_state

    def test_cli_does_not_offer_paths_force_or_disable_authentication(self):
        script = SOURCE.read_text()
        self.assertIn('parser.add_argument("--replace-approved"', script)
        self.assertNotIn("--force", script)
        self.assertNotIn("--unsigned", script)
        self.assertNotIn("--skip-verification", script)
        self.assertNotIn("--target", script)
        self.assertNotIn("subprocess", script)
        self.assertNotIn("docker", script.lower().replace("no docker", ""))
        tree = ast.parse(script)
        funcs = {n.name:n for n in tree.body if isinstance(n, ast.FunctionDef)}
        self.assertIn("replace_approved", funcs)
        calls = [n for n in ast.walk(funcs["replace_approved"])
                 if isinstance(n, ast.Call) and isinstance(n.func, ast.Attribute)]
        needed = {"signed_candidate_preflight", "assess_review",
                  "verify_current", "verify_manifest", "inspect_retained",
                  "write_once", "replace", "fsync"}
        actual = {n.func.attr for n in calls}
        self.assertTrue(needed.difference(actual).issubset({"fsync"}))
        self.assertIn("ROOT_APPROVAL_CHANGED_DURING_RELEASE_DOWNLOAD", script)
        self.assertIn("ROOT_REVIEW_NONCE_CHANGED_DURING_DOWNLOAD", script)
        self.assertIn("ROOT_LAUNCHER_PENDING_MANUAL_RECONCILIATION", script)
        self.assertIn("requires_manual_reconciliation_if_intent_exists", script)

    def test_reject_any_other_cli_arguments_without_host_access(self):
        with patch.object(m, "replace_approved") as run:
            with self.assertRaises(SystemExit):
                m.main(["--replace-approved", "--target", "/tmp/unsafe"])
            run.assert_not_called()


if __name__ == "__main__":
    unittest.main()
