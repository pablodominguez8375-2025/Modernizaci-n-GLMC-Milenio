"""Offline acquisition chain tests. No network, token, Docker or host actions."""
import importlib.util
from pathlib import Path
import tempfile
import unittest

SPEC = importlib.util.spec_from_file_location(
    "qa_acquire_candidate",
    Path(__file__).resolve().parents[1] / "scripts/qa/qa-acquire-candidate.py")
agent = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(agent)
SHA = "a" * 40
DIGEST = "sha256:" + "b" * 64
SELECTION = {
    "status": "READY_FOR_SIGNED_STAGING_ONLY",
    "scope": "NO_HOST_ACCESS_NO_DEPLOYMENT_NO_UAT",
    "source_sha": SHA, "artifact_id": 123, "artifact_sha256": DIGEST,
}


class AcquireCandidateTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name) / "qa-staging"
        self.root.mkdir(mode=0o700)
        self.calls = []
        self.select_count = 0

    def select(self):
        self.select_count += 1
        return dict(SELECTION)

    def token(self, path):
        self.calls.append("token")
        return "synthetic-read-only-token"

    def download(self, ident, digest, token, output):
        self.calls.append("download")
        self.assertEqual(123, ident)
        self.assertEqual(DIGEST, digest)
        self.assertEqual("synthetic-read-only-token", token)
        output.write_bytes(b"synthetic")
        return {"status": "DOWNLOADED_UNVERIFIED_NOT_INSTALLED",
                "artifact_id": ident, "sha256": digest}

    def stage(self, path, digest, source, output):
        self.calls.append("stage")
        self.assertTrue(path.is_file())
        self.assertEqual(DIGEST, digest)
        self.assertEqual(SHA, source)
        output.mkdir()
        (output / "BUILD-INFO.txt").write_text("verified-in-fixture")
        return {"status": "SIGNED_CODE_STAGED_NOT_INSTALLED", "source_sha": source}

    def go(self, selector=None, downloader=None, stager=None, token=None):
        return agent.acquire(
            self.root, self.root / "private-token",
            selector or self.select,
            downloader or self.download,
            stager or self.stage,
            token or self.token)

    def test_valid_signed_candidate_is_prepared_not_installed(self):
        result = self.go()
        self.assertEqual("VERIFIED_QA_CANDIDATE_PREPARED_NOT_DEPLOYED", result["status"])
        self.assertEqual("NO_DOCKER_NO_INSTALLATION_NO_QA_ACCEPTANCE", result["scope"])
        self.assertEqual(["token", "download", "stage"], self.calls)
        self.assertEqual(2, self.select_count)
        self.assertEqual("verified-in-fixture", (self.root / SHA / "BUILD-INFO.txt").read_text())
        self.assertFalse(list(self.root.glob(".pmgm-acquire-*")))

    def test_insecure_root_directory_is_rejected_before_token(self):
        self.root.chmod(0o755)
        with self.assertRaisesRegex(agent.Blocked, "STAGING_ROOT_OWNER_OR_MODE"):
            self.go()
        self.assertEqual([], self.calls)

    def test_no_overwrite_of_existing_candidate(self):
        (self.root / SHA).mkdir()
        with self.assertRaisesRegex(agent.Blocked, "ALREADY_STAGED"):
            self.go()
        self.assertEqual([], self.calls)

    def test_invalid_selection_blocks_before_token(self):
        def invalid():
            return {"status": "BLOCKED_QA_AGENT_READINESS"}
        with self.assertRaisesRegex(agent.Blocked, "INVALID_DEV_SELECTION"):
            self.go(selector=invalid)
        self.assertEqual([], self.calls)

    def test_failed_download_does_not_stage_or_leave_files(self):
        def fail_download(*args):
            raise OSError("synthetic network failure: must not leak")
        with self.assertRaises(OSError):
            self.go(downloader=fail_download)
        self.assertFalse((self.root / SHA).exists())
        self.assertFalse(list(self.root.glob(".pmgm-acquire-*")))

    def test_incorrect_download_status_is_rejected(self):
        def failed(*args):
            return {"status": "UNVERIFIED"}
        with self.assertRaisesRegex(agent.Blocked, "CANDIDATE_DOWNLOAD_NOT_VERIFIED"):
            self.go(downloader=failed)
        self.assertFalse((self.root / SHA).exists())

    def test_signature_failure_does_not_publish(self):
        def unsigned(*args):
            raise ValueError("signed evidence invalid")
        with self.assertRaises(ValueError):
            self.go(stager=unsigned)
        self.assertFalse((self.root / SHA).exists())
        self.assertFalse(list(self.root.glob(".pmgm-acquire-*")))

    def test_head_move_rejects_toctou_and_cleans_temp(self):
        def moved():
            self.select_count += 1
            if self.select_count == 1:
                return dict(SELECTION)
            return {**SELECTION, "source_sha": "c" * 40}
        with self.assertRaisesRegex(agent.Blocked, "CHANGED_DURING_STAGING"):
            self.go(selector=moved)
        self.assertFalse((self.root / SHA).exists())
        self.assertFalse(list(self.root.glob(".pmgm-acquire-*")))

    def test_changed_artifact_digest_blocks_even_if_sha_same(self):
        def changed():
            self.select_count += 1
            return dict(SELECTION) if self.select_count == 1 else {
                **SELECTION, "artifact_sha256": "sha256:" + "c" * 64}
        with self.assertRaisesRegex(agent.Blocked, "CHANGED_DURING_STAGING"):
            self.go(selector=changed)
        self.assertFalse((self.root / SHA).exists())

    def test_unsigned_staging_result_is_rejected(self):
        def invalid_stage(_, __, ___, output):
            output.mkdir()
            return {"status": "ZIP_ONLY"}
        with self.assertRaisesRegex(agent.Blocked, "SIGNATURE_OR_MANIFEST_FAILED"):
            self.go(stager=invalid_stage)
        self.assertFalse((self.root / SHA).exists())


if __name__ == "__main__":
    unittest.main()
