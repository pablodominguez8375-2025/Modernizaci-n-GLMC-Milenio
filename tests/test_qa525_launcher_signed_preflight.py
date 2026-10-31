"""No-host tests for exact-dev Sigstore launcher replacement preflight."""
import ast
import importlib.util
from pathlib import Path
import os
import socket
import tempfile
import types
import unittest
from unittest.mock import Mock, patch

ROOT = Path(__file__).resolve().parents[1]
FILE = ROOT / "scripts/qa/operators/qa525-launcher-signed-preflight.py"
spec = importlib.util.spec_from_file_location("qa525_signed_launcher_preflight_test", FILE)
preflight = importlib.util.module_from_spec(spec)
spec.loader.exec_module(preflight)

SHA = "a" * 40
DIGEST = "sha256:" + "b" * 64
OLD = {
    "status": "LAUNCHER_OBSERVED_UPGRADE_NOT_AUTHORIZED",
    "old_launcher_template_verified": True,
    "may_apply": False,
    "may_install": False,
    "current_launcher_sha256": "c" * 64,
    "current_operator_sha256": "d" * 64,
    "current_launcher": "/usr/local/sbin/pmgm-qa-update",
    "current_operator": "/opt/centenario/qa525-signed-stage/old/qa525-update.py",
}
OPER = b"".join(x.encode() for x in preflight.REQUIRED_OPERATOR_CONTRACT)


class SignedLauncherPreflightTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.parent = Path(self.temp.name)
        self.inspector = types.SimpleNamespace(inspect=Mock(return_value=dict(OLD)))
        self.signer = types.SimpleNamespace(
            SCOPE="LOCAL_STAGING_ONLY_NO_DOCKER_NO_HOST_DEPLOYMENT_NO_UAT",
            staging=types.SimpleNamespace(verify_manifest=Mock()),
            verify_current=Mock(side_effect=self.valid_signed_stage),
        )
        self.host = patch.object(preflight.os, "geteuid", return_value=0)
        self.hostname = patch.object(preflight.socket, "gethostname",
                                     return_value="glmdch")
        self.source_guard = patch.object(preflight, "trusted_file",
                                         return_value=True)
        self.private_guard = patch.object(preflight, "private_stage_parent",
                                          return_value=True)
        self.hash_guard = patch.object(preflight, "stable_hash", return_value=OPER)
        for change in (self.host, self.hostname, self.source_guard,
                       self.private_guard, self.hash_guard):
            change.start()
            self.addCleanup(change.stop)

    def valid_signed_stage(self, path):
        path.mkdir()
        file = path / preflight.CANDIDATE
        file.parent.mkdir(parents=True)
        file.write_bytes(OPER)
        return {
            "status": "SIGNATURE_VERIFIED_STAGED_NOT_DEPLOYED",
            "scope": self.signer.SCOPE,
            "source_sha": SHA,
            "release_asset_sha256": DIGEST,
        }

    def check(self):
        return preflight.signed_candidate_preflight(
            self.inspector, self.signer, stage_parent=self.parent)

    def test_checked_twice_signed_verified_no_install_no_persisted_stage(self):
        result = self.check()
        self.assertEqual("SIGNED_LAUNCHER_CANDIDATE_VERIFIED_NO_INSTALL",
                         result["status"])
        self.assertEqual(SHA, result["source_sha"])
        self.assertEqual(DIGEST, result["release_asset_sha256"])
        self.assertTrue(result["sigstore_verified_in_this_operation"])
        self.assertTrue(result["signed_manifest_verified"])
        self.assertIs(result["may_apply"], False)
        self.assertIs(result["may_install"], False)
        self.assertIs(result["replacement_applied"], False)
        self.assertEqual(2, self.inspector.inspect.call_count)
        self.signer.verify_current.assert_called_once()
        self.signer.staging.verify_manifest.assert_called_once()
        self.assertEqual([], list(self.parent.iterdir()))

    def test_old_launcher_rejected_before_signature_download(self):
        for key, value in (("status", "LAUNCHER_UNKNOWN"),
                           ("old_launcher_template_verified", False),
                           ("may_install", True),
                           ("current_launcher_sha256", "not-sha")):
            with self.subTest(key=key):
                invalid = dict(OLD, **{key: value})
                self.inspector.inspect.return_value = invalid
                self.signer.verify_current.reset_mock()
                with self.assertRaises(preflight.Blocked):
                    self.check()
                self.signer.verify_current.assert_not_called()
        self.inspector.inspect.return_value = dict(OLD)

    def test_tampered_signed_stage_claim_is_rejected(self):
        self.signer.verify_current.side_effect = None
        for result in (
            {"status": "READY_FOR_SIGNED_STAGING_ONLY", "scope": self.signer.SCOPE,
             "source_sha": SHA, "release_asset_sha256": DIGEST},
            {"status": "SIGNATURE_VERIFIED_STAGED_NOT_DEPLOYED",
             "scope": "NO_SIGNATURE_CHECK", "source_sha": SHA,
             "release_asset_sha256": DIGEST},
            {"status": "SIGNATURE_VERIFIED_STAGED_NOT_DEPLOYED",
             "scope": self.signer.SCOPE, "source_sha": "x" * 40,
             "release_asset_sha256": DIGEST},
            {"status": "SIGNATURE_VERIFIED_STAGED_NOT_DEPLOYED",
             "scope": self.signer.SCOPE, "source_sha": SHA,
             "release_asset_sha256": "sha256:bad"}):
            self.signer.verify_current.return_value = result
            with self.subTest(result=result), self.assertRaises(preflight.Blocked):
                self.check()
        self.signer.verify_current.side_effect = self.valid_signed_stage

    def test_signature_manifest_must_really_pass(self):
        self.signer.staging.verify_manifest.side_effect = RuntimeError(
            "fake tampered signed manifest")
        with self.assertRaisesRegex(RuntimeError,
                                    "fake tampered signed manifest"):
            self.check()
        self.assertEqual([], list(self.parent.iterdir()))

    def test_candidate_missing_recovery_command_blocks(self):
        with patch.object(preflight, "stable_hash",
                          return_value=b"--status --apply-prepared"):
            with self.assertRaisesRegex(preflight.Blocked,
                                        "PREFLIGHT_REQUIRED_RECOVERY_CONTRACT_MISSING"):
                self.check()

    def test_launcher_changed_during_download_blocks(self):
        new = dict(OLD, current_launcher_sha256="e" * 64)
        self.inspector.inspect.side_effect = [OLD, new]
        with self.assertRaisesRegex(preflight.Blocked,
                                    "PREFLIGHT_OLD_LAUNCHER_CHANGED"):
            self.check()

    def test_wrong_host_does_not_ever_download_release(self):
        with patch.object(preflight.socket, "gethostname", return_value="srv01"):
            with self.assertRaisesRegex(preflight.Blocked,
                                        "PREFLIGHT_ONLY_GLMDCH_ROOT"):
                self.check()
        self.signer.verify_current.assert_not_called()

    def test_candidate_symlink_and_hardlink_rejected_by_open_guard(self):
        target = self.parent / "candidate.py"
        target.write_text("test")
        symlink = self.parent / "candidate-link"
        symlink.symlink_to(target)
        with self.assertRaises(preflight.Blocked):
            preflight.stable_hash(symlink)
        hardlink = self.parent / "candidate-hardlink"
        os.link(target, hardlink)
        with self.assertRaises(preflight.Blocked):
            preflight.stable_hash(target)

    def test_untrusted_stage_parent_cannot_be_used(self):
        # A writable or non-root fake parent must never be a staging root.
        with self.assertRaises(preflight.Blocked):
            preflight.private_stage_parent(self.parent)
        with self.assertRaises(preflight.Blocked):
            preflight.trusted_file(self.parent)

    def test_preflight_has_no_direct_launcher_write_primitive(self):
        contents = FILE.read_text()
        calls = [node.func for node in ast.walk(ast.parse(contents))
                 if isinstance(node, ast.Call)]
        os_writes = {"rename", "replace", "unlink", "remove", "chmod",
                     "chown", "execv", "execve", "system", "popen"}
        self.assertFalse([node.attr for node in calls
                          if isinstance(node, ast.Attribute)
                          and isinstance(node.value, ast.Name)
                          and node.value.id == "os" and node.attr in os_writes])
        self.assertNotIn("docker compose", contents)
        self.assertNotIn("subprocess.run(", contents)


if __name__ == "__main__":
    unittest.main()
