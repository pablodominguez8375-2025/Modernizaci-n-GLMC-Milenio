"""Hermetic QA525 orchestration tests; no GitHub, gh binary, Docker or host."""
import importlib.util
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

FILE = Path(__file__).resolve().parents[1] / "scripts/qa/qa525-stage-current-release.py"
spec = importlib.util.spec_from_file_location("qa525_current_staging_test", FILE)
stage = importlib.util.module_from_spec(spec)
spec.loader.exec_module(stage)

SHA = "a" * 40
DIGEST = "sha256:" + "b" * 64
RELEASE = {
    "status": "READY_FOR_SIGNATURE_VERIFICATION_ONLY",
    "scope": stage.gate.RESULT_SCOPE,
    "source_sha": SHA,
    "release_tag": "qa-signed-" + SHA,
    "release_asset_sha256": DIGEST,
    "release_asset_size": 256,
    "release_asset_id": 100,
    "publish_workflow_run": 101,
    "silo_workflow_run": 102,
}


class CurrentSignedStagingTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory(prefix="qa525-staging-contract-")
        self.addCleanup(self.tmp.cleanup)
        self.private = Path(self.tmp.name) / "private"
        self.private.mkdir(mode=0o700)
        self.output = self.private / "approved-code"
        self.calls = []

    def download(self, selection, archive):
        self.calls.append(("download", selection.copy()))
        self.assertEqual("READY_FOR_SIGNED_STAGING_ONLY", selection["status"])
        self.assertEqual(SHA, selection["source_sha"])
        self.assertEqual(DIGEST, selection["artifact_sha256"])
        archive.write_bytes(b"synthetic-release")
        return {"source_sha": SHA, "sha256": DIGEST}

    def signed_stage(self, archive, digest, sha, output, *, tokenless=False):
        self.calls.append(("stage", tokenless))
        self.assertTrue(tokenless)
        self.assertEqual((sha, digest), (SHA, DIGEST))
        self.assertEqual(b"synthetic-release", archive.read_bytes())
        output.mkdir()
        (output / "BUILD-INFO.txt").write_text("synthetic already verified by mocked signer")
        return {"status": "SIGNED_CODE_STAGED_NOT_INSTALLED", "source_sha": SHA}

    def run(self, gate_values=None, *, downloader=None, signer=None):
        replies = list(gate_values or [RELEASE, RELEASE])
        with (patch.object(stage.gate, "evaluate", side_effect=replies) as gates,
              patch.object(stage.download, "download",
                           side_effect=downloader or self.download) as down,
              patch.object(stage.staging, "stage",
                           side_effect=signer or self.signed_stage) as sig):
            result = stage.verify_current(self.output)
        self.assertEqual(2, gates.call_count)
        self.assertEqual(1, down.call_count)
        self.assertEqual(1, sig.call_count)
        return result

    def test_exact_head_bundle_is_verified_then_staged(self):
        result = self.run()
        self.assertEqual("SIGNATURE_VERIFIED_STAGED_NOT_DEPLOYED", result["status"])
        self.assertEqual(stage.SCOPE, result["scope"])
        self.assertEqual(SHA, result["source_sha"])
        self.assertEqual(["download", "stage"], [item[0] for item in self.calls])
        self.assertTrue((self.output / "BUILD-INFO.txt").is_file())
        self.assertEqual(["approved-code"], [p.name for p in self.private.iterdir()])

    def test_new_dev_during_signature_verification_blocks_atomically(self):
        changed = {**RELEASE, "source_sha": "c" * 40}
        with patch.object(stage.gate, "evaluate", side_effect=[RELEASE, changed]), \
             patch.object(stage.download, "download", side_effect=self.download), \
             patch.object(stage.staging, "stage", side_effect=self.signed_stage):
            with self.assertRaisesRegex(stage.Blocked, "CURRENT_RELEASE_CHANGED"):
                stage.verify_current(self.output)
        self.assertFalse(self.output.exists())
        self.assertEqual([], list(self.private.iterdir()))

    def test_changed_digest_or_release_tag_after_download_blocks(self):
        for field, value in (("release_tag", "qa-signed-" + "c" * 40),
                             ("release_asset_sha256", "sha256:" + "c" * 64),
                             ("release_asset_id", 999)):
            with self.subTest(field=field):
                changed = {**RELEASE, field: value}
                with patch.object(stage.gate, "evaluate", side_effect=[RELEASE, changed]), \
                     patch.object(stage.download, "download", side_effect=self.download), \
                     patch.object(stage.staging, "stage", side_effect=self.signed_stage):
                    with self.assertRaises(stage.Blocked):
                        stage.verify_current(self.output)
                self.assertEqual([], list(self.private.iterdir()))

    def test_signature_failure_leaves_no_candidate(self):
        with patch.object(stage.gate, "evaluate", return_value=RELEASE), \
             patch.object(stage.download, "download", side_effect=self.download), \
             patch.object(stage.staging, "stage", side_effect=stage.staging.Blocked("ATTESTATION_FAILED")):
            with self.assertRaisesRegex(stage.staging.Blocked, "ATTESTATION_FAILED"):
                stage.verify_current(self.output)
        self.assertEqual([], list(self.private.iterdir()))

    def test_download_or_receipt_failure_leaves_no_candidate(self):
        def bad_receipt(selection, archive):
            archive.write_bytes(b"bad")
            return {"source_sha": SHA, "sha256": "sha256:" + "c" * 64}
        with patch.object(stage.gate, "evaluate", return_value=RELEASE), \
             patch.object(stage.download, "download", side_effect=bad_receipt), \
             patch.object(stage.staging, "stage") as sig:
            with self.assertRaisesRegex(stage.Blocked, "PUBLIC_DOWNLOAD_RECEIPT"):
                stage.verify_current(self.output)
            sig.assert_not_called()
        self.assertEqual([], list(self.private.iterdir()))

    def test_invalid_signer_receipt_never_exposes_staging(self):
        def bad_signature_receipt(archive, digest, sha, output, *, tokenless):
            output.mkdir()
            return {"status": "UNKNOWN", "source_sha": sha}
        with patch.object(stage.gate, "evaluate", return_value=RELEASE), \
             patch.object(stage.download, "download", side_effect=self.download), \
             patch.object(stage.staging, "stage", side_effect=bad_signature_receipt):
            with self.assertRaisesRegex(stage.Blocked, "SIGNED_STAGING_RECEIPT"):
                stage.verify_current(self.output)
        self.assertFalse(self.output.exists())

    def test_existing_output_is_not_overwritten_or_touched(self):
        self.output.mkdir()
        (self.output / "sentinel").write_text("preserve")
        with patch.object(stage.gate, "evaluate") as read:
            with self.assertRaisesRegex(stage.Blocked, "DESTINATION_ALREADY_EXISTS"):
                stage.verify_current(self.output)
            read.assert_not_called()
        self.assertEqual("preserve", (self.output / "sentinel").read_text())

    def test_private_parent_and_absolute_output_required(self):
        with self.assertRaisesRegex(stage.Blocked, "OUTPUT_MUST_BE_ABSOLUTE"):
            stage.trusted_destination(Path("relative/output"))
        self.private.chmod(0o755)
        with self.assertRaisesRegex(stage.Blocked, "PRIVATE_STAGING_PARENT_UNTRUSTED"):
            stage.verify_current(self.output)
        self.private.chmod(0o700)
        missing = self.tmp.name + "/missing/output"
        with self.assertRaisesRegex(stage.Blocked, "PRIVATE_STAGING_PARENT_MISSING"):
            stage.trusted_destination(Path(missing))

    def test_output_created_after_signing_is_not_overwritten(self):
        def racing_signer(archive, digest, sha, output, *, tokenless):
            self.signed_stage(archive, digest, sha, output, tokenless=tokenless)
            self.output.mkdir()
            (self.output / "sentinel").write_text("preserve")
            return {"status": "SIGNED_CODE_STAGED_NOT_INSTALLED", "source_sha": sha}
        with patch.object(stage.gate, "evaluate", return_value=RELEASE), \
             patch.object(stage.download, "download", side_effect=self.download), \
             patch.object(stage.staging, "stage", side_effect=racing_signer):
            with self.assertRaisesRegex(stage.Blocked, "DESTINATION_CHANGED"):
                stage.verify_current(self.output)
        self.assertEqual("preserve", (self.output / "sentinel").read_text())

    def test_not_approved_gate_fails_before_network_or_disk(self):
        bad = {**RELEASE, "status": "APPLIED"}
        with patch.object(stage.gate, "evaluate", return_value=bad), \
             patch.object(stage.download, "download") as down:
            with self.assertRaisesRegex(stage.Blocked, "CURRENT_RELEASE_NOT_APPROVED"):
                stage.verify_current(self.output)
            down.assert_not_called()
        self.assertEqual([], list(self.private.iterdir()))

    def test_no_docker_or_install_entrypoints_in_orchestrator(self):
        src = FILE.read_text()
        for forbidden in ("docker", "subprocess", "systemctl", "compose up",
                          "INSTALAR.sh", "os.system", "chmod", "GH_TOKEN"):
            # The module docstring may mention Docker as a prohibition.
            self.assertNotIn(forbidden, src.split('"""', 2)[-1])


if __name__ == "__main__":
    unittest.main()
