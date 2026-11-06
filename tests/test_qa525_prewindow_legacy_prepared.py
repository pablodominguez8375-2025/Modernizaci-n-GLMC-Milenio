"""QA525: legacy prepared.json does not gate the launcher-only signed preflight.

Offline/hermetic: mocks root/hostname; never touches /opt, /etc or glmdch.
"""
from contextlib import nullcontext
import hashlib
import importlib.util
from pathlib import Path
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import Mock, patch

ROOT = Path(__file__).resolve().parents[1]
OPS = ROOT / "scripts/qa/operators"


def load(name, file):
    spec = importlib.util.spec_from_file_location(name, OPS / file)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


class LegacyPreparedDoesNotBlockLauncherOnlyTests(unittest.TestCase):
    def test_signed_candidate_preflight_uses_private_temp_beside_prepared(self):
        pre = load("qa525_prepared_regression_preflight",
                   "qa525-launcher-signed-preflight.py")
        with tempfile.TemporaryDirectory(prefix="qa525-expired-prepared-") as td:
            parent = Path(td)
            prepared = parent / "prepared.json"
            original = b'{"source_sha":"a184936d", "expired":true}\n'
            prepared.write_bytes(original)
            old = {
                "status": "LAUNCHER_OBSERVED_UPGRADE_NOT_AUTHORIZED",
                "old_launcher_template_verified": True,
                "may_apply": False, "may_install": False,
                "current_launcher_sha256": "1" * 64,
                "current_operator_sha256": "2" * 64,
                "current_operator": "/opt/centenario/old.py",
                "current_launcher": "/usr/local/sbin/pmgm-qa-update",
            }
            inspector = SimpleNamespace(inspect=Mock(return_value=old))
            source_sha = "cfe4e6573ece2ae81414cc252047d36cc56e5c73"
            payload = b"#!/usr/bin/python3\nprint('sandbox candidate only')\n"
            observed = hashlib.sha256(payload).hexdigest()

            def fake_verified_stage(package):
                candidate = package / pre.CANDIDATE
                candidate.parent.mkdir(parents=True)
                candidate.write_bytes(payload)
                (package / "MANIFEST.sha256").write_text(
                    observed + "  ./" + pre.CANDIDATE.as_posix() + "\n",
                    encoding="utf-8",
                )
                return {
                    "status": "SIGNATURE_VERIFIED_STAGED_NOT_DEPLOYED",
                    "scope": "SIGNED_SCOPE_TEST",
                    "source_sha": source_sha,
                    "release_asset_sha256": "sha256:" + "a" * 64,
                }

            signed = SimpleNamespace(
                SCOPE="SIGNED_SCOPE_TEST",
                verify_current=Mock(side_effect=fake_verified_stage),
                staging=SimpleNamespace(verify_manifest=Mock(return_value=True)),
            )
            with patch.object(pre.os, "geteuid", return_value=0), \
                 patch.object(pre.socket, "gethostname", return_value="glmdch"), \
                 patch.object(pre, "trusted_file"), \
                 patch.object(pre, "private_stage_parent"), \
                 patch.object(pre, "stable_hash", side_effect=lambda path, maximum=2_000_000: Path(path).read_bytes()), \
                 patch.object(pre, "validate_operator_ast"):
                result = pre.signed_candidate_preflight(
                    inspector, signed, stage_parent=parent)
            self.assertEqual(result["status"],
                             "SIGNED_LAUNCHER_CANDIDATE_VERIFIED_NO_INSTALL")
            self.assertEqual(result["candidate_operator_sha256"], observed)
            self.assertEqual(prepared.read_bytes(), original)
            self.assertEqual(sorted(p.name for p in parent.iterdir()),
                             ["prepared.json"])
            self.assertEqual(signed.verify_current.call_count, 1)
            self.assertEqual(signed.staging.verify_manifest.call_count, 2)

    def test_replace_approved_reaches_signed_preflight_with_old_prepared(self):
        inst = load("qa525_prepared_regression_installer",
                    "qa525-launcher-supervised-root-install.py")
        with tempfile.TemporaryDirectory(prefix="qa525-replace-old-prepared-") as td:
            parent = Path(td)
            prepared = parent / "prepared.json"
            original = b'{"source_sha":"a184936d", "expired":true}\n'
            prepared.write_bytes(original)
            stop = inst.Blocked("SANDBOX_STOP_BEFORE_INSTALL")
            gate = Mock(side_effect=stop)
            modules = (
                SimpleNamespace(inspect=Mock()),
                SimpleNamespace(signed_candidate_preflight=gate),
                SimpleNamespace(verify_current=Mock()),
                SimpleNamespace(read_root_approval=Mock()),
                SimpleNamespace(assess_review=Mock()),
                SimpleNamespace(render=Mock()),
                SimpleNamespace(inspect_retained=Mock()),
            )
            with patch.object(inst, "PREFLIGHT_STATE", parent), \
                 patch.object(inst, "require_host"), \
                 patch.object(inst, "verify_inputs", return_value=modules), \
                 patch.object(inst, "root_chain"), \
                 patch.object(inst, "all_five_locks", return_value=nullcontext()), \
                 patch.object(inst, "scan_history", return_value=set()), \
                 patch.object(inst, "write_once") as writer, \
                 patch.object(inst.os, "replace") as swap:
                with self.assertRaisesRegex(inst.Blocked,
                                            "SANDBOX_STOP_BEFORE_INSTALL"):
                    inst.replace_approved()
            gate.assert_called_once()
            self.assertEqual(gate.call_args.kwargs["stage_parent"], parent)
            self.assertEqual(prepared.read_bytes(), original)
            writer.assert_not_called()
            swap.assert_not_called()


class PrewindowEmptyLockMetadataTests(unittest.TestCase):
    """Regression: GNU stat reports regular empty file for zero-byte locks."""

    def test_empty_regular_lock_valid_but_symlink_mode_and_link_count_rejected(self):
        import subprocess

        script = (OPS / "qa525-prewindow-readonly.sh").read_text(encoding="utf-8")
        matches = [line for line in script.splitlines()
                   if line.startswith("file_mode() {")]
        self.assertEqual(len(matches), 1)
        fake_sudo = r'''
sudo() {
  local op="$1"
  shift
  case "$op" in
    stat) command stat "$@" | sed -E 's/^[0-9]+:[0-9]+/0:0/' ;;
    test) command test "$@" ;;
    *) return 95 ;;
  esac
}
'''
        shell_script = fake_sudo + "\n" + matches[0] + '\nfile_mode "$1" 600\n'

        def passes(path):
            return subprocess.run(
                ["bash", "-e", "-u", "-o", "pipefail", "-c",
                 shell_script, "test", str(path)],
                capture_output=True, text=True, check=False,
            ).returncode == 0

        with tempfile.TemporaryDirectory(prefix="qa525-lock-metadata-") as tmp:
            root = Path(tmp)
            lock = root / "operation.lock"
            lock.write_bytes(b"")
            lock.chmod(0o600)
            self.assertTrue(passes(lock), "empty regular lock is valid")
            lock.write_bytes(b"payload")
            self.assertTrue(passes(lock), "nonempty regular lock is valid")
            lock.chmod(0o644)
            self.assertFalse(passes(lock), "incorrect mode must be denied")
            lock.chmod(0o600)
            alias = root / "alias.lock"
            alias.symlink_to(lock)
            self.assertFalse(passes(alias), "symlink must be denied")
            hard = root / "hard.lock"
            hard.hardlink_to(lock)
            self.assertFalse(passes(lock), "multiple links must be denied")
            self.assertFalse(passes(root), "directory must be denied")


if __name__ == "__main__":
    unittest.main()
