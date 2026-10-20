from datetime import timedelta
import hashlib
import importlib.util
import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location("installer_test", ROOT / "scripts/qa/qa-lite-install.py")
mod = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = mod
spec.loader.exec_module(mod)


class InstallerTests(unittest.TestCase):
    def test_initial_publication_race_waits_without_build_or_container_mutation(self):
        selection = {"source_sha": "a" * 40, "artifact_sha256": "sha256:" + hashlib.sha256(b"envelope").hexdigest(),
                     "status": "READY_FOR_SIGNED_STAGING_ONLY"}
        attempts = [0]
        def download(current, archive):
            attempts[0] += 1
            if attempts[0] == 1:
                raise mod.public_download.Blocked("SIGNED_PUBLIC_RELEASE_NOT_READY")
            archive.write_bytes(b"envelope")
        def stage(archive, digest, sha, package, **kwargs):
            package.mkdir()
        with tempfile.TemporaryDirectory() as tmp, patch.object(mod, "STATE", Path(tmp)), \
                patch.object(mod.adapter, "trusted_path", side_effect=lambda p, **kw: Path(p)), \
                patch.object(mod.public_download, "download", side_effect=download), \
                patch.object(mod.staging, "stage", side_effect=stage), \
                patch.object(mod.time, "sleep") as sleep, \
                patch.object(mod.builder, "prepare_web") as build:
            package, verifier = mod.verified_candidate(selection)
            self.assertTrue(package.is_dir())
            self.assertEqual(2, attempts[0])
            sleep.assert_called_once_with(30)
            build.assert_not_called()

    def test_health_uses_actual_api_ready_contract_and_checks_public_version(self):
        sha = "a" * 40
        responses = {"/health/web": (200, {}, b"ok\n"),
                     "/health/ready": (200, {}, b'{"status":"ready","database":"postgresql"}'),
                     "/qa-version.json": (200, {}, json.dumps({"environment": "QA_SYNTHETIC", "source_sha": sha}).encode())}
        with patch.object(mod.health, "inspect", return_value={"decision": "PUBLIC_ENTRY_CHECKS_PASS_ONLY"}), \
                patch.object(mod.health, "request", side_effect=responses.__getitem__):
            self.assertTrue(mod.public_health())
            self.assertTrue(mod.public_health(sha))
            self.assertFalse(mod.public_health("b" * 40))
            responses["/health/ready"] = (200, {}, b"<html>SPA fallback</html>")
            self.assertFalse(mod.public_health())

    def test_canonical_identity_matches_existing_public_qa(self):
        self.assertEqual(mod.health.ISSUER, mod.builder.AUTHORITY)
        self.assertEqual("https://sistema.granlogiamixta.cl/identity/realms/pmgm", mod.builder.AUTHORITY)

    def test_backup_expiry_blocks_before_reading_or_any_docker(self):
        with self.assertRaisesRegex(mod.adapter.Blocked, "OFFHOST_BACKUP_EXPIRED"):
            mod.backup_verified(mod.BACKUP_TIME + timedelta(hours=49))

    def test_backup_actual_bytes_required_not_boolean_receipt(self):
        with tempfile.TemporaryDirectory() as tmp:
            backup = Path(tmp) / "ciphertext"
            backup.write_bytes(b"encrypted-synthetic-fixture")
            with patch.object(mod, "BACKUP", backup), \
                    patch.object(mod, "BACKUP_SHA", hashlib.sha256(backup.read_bytes()).hexdigest()):
                self.assertTrue(mod.backup_verified(mod.BACKUP_TIME + timedelta(hours=1)))
                backup.write_bytes(b"corrupt")
                with self.assertRaisesRegex(mod.adapter.Blocked, "BACKUP_CIPHERTEXT_CHANGED"):
                    mod.backup_verified(mod.BACKUP_TIME + timedelta(hours=1))

    def test_automatic_first_install_or_unfinished_journal_cannot_bypass_bootstrap(self):
        with tempfile.TemporaryDirectory() as tmp, patch.object(mod, "STATE", Path(tmp)), \
                patch.object(mod.os, "geteuid", return_value=0), \
                patch.object(mod.socket, "gethostname", return_value="glmdch"), \
                patch.object(mod.adapter, "trusted_path", side_effect=lambda p, **kw: Path(p)):
            with self.assertRaisesRegex(mod.adapter.Blocked, "FIRST_INSTALL_REQUIRES_SUPERVISED_BOOTSTRAP"):
                mod.install(False)
            (Path(tmp) / "pending.json").write_text("{}")
            with self.assertRaisesRegex(mod.adapter.Blocked, "INTERRUPTED_UPDATE_USE_RECOVER"):
                mod.install(True)

    def test_backup_or_public_health_failure_prevents_build(self):
        with tempfile.TemporaryDirectory() as tmp, patch.object(mod, "STATE", Path(tmp)), \
                patch.object(mod.os, "geteuid", return_value=0), \
                patch.object(mod.socket, "gethostname", return_value="glmdch"), \
                patch.object(mod.adapter, "trusted_path", side_effect=lambda p, **kw: Path(p)), \
                patch.object(mod.ready, "evaluate_latest_validated", return_value={"source_sha": "a" * 40}), \
                patch.object(mod, "backup_verified", return_value=True), \
                patch.object(mod, "public_health", return_value=False), \
                patch.object(mod.builder, "prepare_web") as build:
            with self.assertRaisesRegex(mod.adapter.Blocked, "PUBLIC_HEALTH_NOT_READY"):
                mod.install(True)
            build.assert_not_called()

    def test_rollback_or_divergent_dev_is_not_fast_forward(self):
        for value in ({"status": "behind"}, {"status": "ahead", "merge_base_commit": {"sha": "c" * 40}}):
            with patch.object(mod.ready, "fetch_public", return_value=value):
                with self.assertRaisesRegex(mod.adapter.Blocked, "DEV_NOT_FAST_FORWARD"):
                    mod.check_ancestry("a" * 40, "b" * 40)
        with patch.object(mod.ready, "fetch_public", return_value={"status": "ahead", "merge_base_commit": {"sha": "a" * 40}}):
            self.assertTrue(mod.check_ancestry("a" * 40, "b" * 40))

    def test_private_atomic_journal_preserves_unknown_source_as_null(self):
        with tempfile.TemporaryDirectory() as tmp:
            out = Path(tmp) / "pending.json"
            mod.write_json(out, {"previous_source_sha": None, "image": mod.FIRST_IMAGE})
            self.assertIsNone(json.loads(out.read_bytes())["previous_source_sha"])
            self.assertEqual(0o600, out.stat().st_mode & 0o777)


if __name__ == "__main__":
    unittest.main()
