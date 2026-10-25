import hashlib
import importlib.util
import io
from pathlib import Path
import sys
import tempfile
import unittest
from types import SimpleNamespace
from unittest.mock import Mock, patch
import json
from urllib.error import HTTPError

ROOT = Path(__file__).resolve().parents[1]


def module(name, filename):
    spec = importlib.util.spec_from_file_location(name, ROOT / "scripts/qa" / filename)
    value = importlib.util.module_from_spec(spec)
    sys.modules[name] = value
    spec.loader.exec_module(value)
    return value


mod = module("public_release_test", "qa-public-release-download.py")
publisher = module("qa_publisher_test", "qa-signed-publish.py")


class Response(io.BytesIO):
    status = 200


class Opener:
    def __init__(self, data=b"signed-envelope", location="https://release-assets.githubusercontent.com/file?sig=public"):
        self.requests = []
        self.data, self.location = data, location

    def open(self, request, **kwargs):
        self.requests.append(request)
        if len(self.requests) == 1:
            raise HTTPError(request.full_url, 302, "Found", {"Location": self.location}, None)
        return Response(self.data)


class ReleaseTests(unittest.TestCase):
    def selection(self):
        return {"status": "READY_FOR_SIGNED_STAGING_ONLY", "source_sha": "a" * 40,
                "artifact_sha256": "sha256:" + hashlib.sha256(b"signed-envelope").hexdigest()}

    def test_exact_envelope_digest_no_authorization_header(self):
        opener = Opener()
        with tempfile.TemporaryDirectory() as tmp:
            out = Path(tmp) / "release.zip"
            mod.download(self.selection(), out, opener)
            self.assertEqual(b"signed-envelope", out.read_bytes())
            self.assertEqual(0o600, out.stat().st_mode & 0o777)
        for request in opener.requests:
            self.assertFalse(request.has_header("Authorization"))
        self.assertIn("/qa-signed-" + "a" * 40 + "/", opener.requests[0].full_url)

    def test_corrupt_asset_is_deleted(self):
        with tempfile.TemporaryDirectory() as tmp:
            out = Path(tmp) / "release.zip"
            with self.assertRaisesRegex(mod.Blocked, "RELEASE_DIGEST_MISMATCH"):
                mod.download(self.selection(), out, Opener(data=b"tampered"))
            self.assertFalse(out.exists())

    def test_redirect_cannot_leak_to_another_host_or_insecure_scheme(self):
        for target in ("http://release-assets.githubusercontent.com/file", "https://evil.test/file",
                       "https://user:password@release-assets.githubusercontent.com/file"):
            with tempfile.TemporaryDirectory() as tmp:
                with self.assertRaisesRegex(mod.Blocked, "RELEASE_REDIRECT_NOT_ALLOWED"):
                    mod.download(self.selection(), Path(tmp) / "asset.zip", Opener(location=target))

    def test_invalid_selection_never_constructs_download(self):
        for values in ({"source_sha": "main"}, {"source_sha": "../main"},
                       {"artifact_sha256": "anything"}, {"status": "UNSIGNED"}):
            with self.assertRaises(mod.Blocked):
                mod.identity({**self.selection(), **values})

    def test_publication_requires_own_push_dev_not_pr_fork_or_main(self):
        event = {"ref": "refs/heads/dev", "repository": {"full_name": publisher.REPO}, "deleted": False}
        self.assertTrue(publisher.allowed_event("push", event))
        self.assertFalse(publisher.allowed_event("pull_request", event))
        for values in ({"ref": "refs/heads/main"}, {"repository": {"full_name": "evil/fork"}},
                       {"deleted": True}):
            self.assertFalse(publisher.allowed_event("push", {**event, **values}))

    def test_quiet_final_push_is_published_after_older_green_cut(self):
        old = {"source_sha": "a" * 40}
        new = {"source_sha": "b" * 40}
        readiness = SimpleNamespace(GateFailure=RuntimeError,
                                    evaluate_latest_validated=Mock(side_effect=[old, old, new]))
        event = {"ref": "refs/heads/dev", "repository": {"full_name": publisher.REPO},
                 "after": new["source_sha"]}
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / "event.json"
            path.write_text(json.dumps(event))
            with patch.dict(publisher.os.environ, {"GITHUB_EVENT_PATH": str(path), "GITHUB_EVENT_NAME": "push"}), \
                    patch.object(publisher, "module", return_value=readiness), \
                    patch.object(publisher, "api", return_value={"commit": {"sha": new["source_sha"]}}), \
                    patch.object(publisher, "publish_selected") as publish, \
                    patch.object(publisher.time, "sleep"):
                self.assertEqual(0, publisher.main())
                self.assertEqual([old, new], [call.args[0] for call in publish.call_args_list])


if __name__ == "__main__":
    unittest.main()
