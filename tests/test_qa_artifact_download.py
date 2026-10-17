"""No-network QA artifact downloader tests; token is never printed or forwarded."""
import hashlib
import importlib.util
import io
from email.message import Message
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch
from urllib.error import HTTPError

SPEC = importlib.util.spec_from_file_location(
    "qa_artifact_download",
    Path(__file__).resolve().parents[1] / "scripts/qa/qa-artifact-download.py")
qa = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(qa)


class FakeResponse:
    status = 200

    def __init__(self, content):
        self.inner = io.BytesIO(content)

    def read(self, n):
        return self.inner.read(n)

    def close(self):
        self.inner.close()


class FakeOpener:
    def __init__(self, data, redirect="https://pipelines.actions.githubusercontent.com/file.zip"):
        self.data = data
        self.redirect = redirect
        self.requests = []

    def open(self, req, timeout=None):
        self.requests.append(req)
        if len(self.requests) == 1:
            headers = Message()
            headers["Location"] = self.redirect
            raise HTTPError(req.full_url, 302, "Found", headers, None)
        return FakeResponse(self.data)


class QaArtifactDownloadTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.dest = self.root / "artifact.zip"
        self.bytes = b"synthetic GitHub artifact bytes"
        self.digest = "sha256:" + hashlib.sha256(self.bytes).hexdigest()

    def test_exact_digest_with_no_cross_host_authorization(self):
        opener = FakeOpener(self.bytes)
        result = qa.download(123, self.digest, "synthetic-read-only-token", self.dest, opener=opener)
        self.assertEqual("DOWNLOADED_UNVERIFIED_NOT_INSTALLED", result["status"])
        self.assertEqual(self.bytes, self.dest.read_bytes())
        self.assertIn("Authorization", opener.requests[0].headers)
        self.assertNotIn("Authorization", opener.requests[1].headers)
        self.assertEqual("https", opener.requests[1].full_url.split(":")[0])

    def test_mismatch_leaves_no_file(self):
        with self.assertRaisesRegex(qa.Blocked, "DIGEST_MISMATCH"):
            qa.download(123, "sha256:" + "a" * 64, "synthetic-token",
                        self.dest, opener=FakeOpener(self.bytes))
        self.assertFalse(self.dest.exists())
        self.assertFalse(list(self.root.glob(".pmgm-qa-download-*")))

    def test_malicious_redirect_is_rejected_without_second_request(self):
        opener = FakeOpener(self.bytes, redirect="https://evil.example.invalid/file.zip")
        with self.assertRaisesRegex(qa.Blocked, "UNTRUSTED"):
            qa.download(123, self.digest, "synthetic-token", self.dest, opener=opener)
        self.assertEqual(1, len(opener.requests))

    def test_redirect_requires_https_and_no_userinfo(self):
        for url in ("http://pipelines.actions.githubusercontent.com/x",
                    "https://person:secret@pipelines.actions.githubusercontent.com/x",
                    "https://actions.githubusercontent.com.evil.invalid/x",
                    "https://evil.example.invalid/x"):
            with self.subTest(url=url):
                self.assertFalse(qa.allowed_redirect(url))

    def test_existing_destination_is_never_overwritten(self):
        self.dest.write_text("protected")
        with self.assertRaisesRegex(qa.Blocked, "DESTINATION_NOT_NEW"):
            qa.download(123, self.digest, "synthetic-token",
                        self.dest, opener=FakeOpener(self.bytes))
        self.assertEqual("protected", self.dest.read_text())

    def test_size_limit_and_cleanup(self):
        with patch.object(qa, "MAX_ARTIFACT_BYTES", 4):
            with self.assertRaisesRegex(qa.Blocked, "SIZE_LIMIT"):
                qa.download(123, self.digest, "synthetic-token",
                            self.dest, opener=FakeOpener(self.bytes))
        self.assertFalse(self.dest.exists())
        self.assertFalse(list(self.root.glob(".pmgm-qa-download-*")))

    def test_read_token_requires_owner_and_0600(self):
        tok = self.root / "token"
        tok.write_text("synthetic-secret-token")
        tok.chmod(0o644)
        with self.assertRaisesRegex(qa.Blocked, "PERMISSIONS"):
            qa.read_token(tok)
        tok.chmod(0o600)
        self.assertEqual("synthetic-secret-token", qa.read_token(tok))

    def test_token_symlink_rejected(self):
        tok = self.root / "token"
        tok.write_text("synthetic-secret-token")
        tok.chmod(0o600)
        link = self.root / "link"
        link.symlink_to(tok)
        with self.assertRaisesRegex(qa.Blocked, "PERMISSIONS"):
            qa.read_token(link)

    def test_selection_requires_exact_marker_and_sha(self):
        item = {"status": "READY_FOR_SIGNED_STAGING_ONLY",
                "scope": "NO_HOST_ACCESS_NO_DEPLOYMENT_NO_UAT",
                "source_sha": "a" * 40,
                "artifact_id": 123,
                "artifact_sha256": self.digest}
        selection = self.root / "readiness.json"
        import json
        selection.write_text(json.dumps(item))
        self.assertEqual((123, self.digest, "a" * 40), qa.read_selection(selection))
        item["source_sha"] = "dev"
        selection.write_text(json.dumps(item))
        with self.assertRaisesRegex(qa.Blocked, "INVALID_SIGNED"):
            qa.read_selection(selection)

    def test_rejects_bad_artifact_id_before_http(self):
        for ident in (True, -1, "123"):
            with self.subTest(ident=ident), self.assertRaisesRegex(qa.Blocked, "ARTIFACT_ID"):
                qa.download(ident, self.digest, "synthetic-token",
                            self.dest, opener=FakeOpener(self.bytes))


if __name__ == "__main__":
    unittest.main()
