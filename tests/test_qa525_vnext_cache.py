import datetime as dt
from email.message import Message
import hashlib
from io import BytesIO
import importlib.util
import json
import os
from pathlib import Path
import socket
import stat
import tempfile
import unittest
from unittest import mock


ROOT = Path(__file__).resolve().parents[1]
SCRIPT = ROOT / "scripts" / "qa" / "qa525-vnext-cache.py"
SPEC = importlib.util.spec_from_file_location("qa525_vnext_cache", SCRIPT)
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)

APP = "6eed4f00b7917da3cca36bbda70e16d16328648d"
OPS = "2e9d40a0be8a39e65e786673b23ab721c63e325b"
NONCE = "qa525-vnext-admission-0001"
NOW = dt.datetime(2026, 11, 5, 12, 0, tzinfo=dt.timezone.utc)


def write_private(path, value):
    path.parent.mkdir(mode=0o700, parents=True, exist_ok=True)
    path.write_text(json.dumps(value, sort_keys=True) + "\n" if not isinstance(value, bytes) else "")
    if isinstance(value, bytes):
        path.write_bytes(value)
    path.chmod(0o600)


class AdmissionFixture:
    def __init__(self, root):
        self.root = Path(root)
        self.cache_root = self.root / "cache"
        self.cache = self.cache_root / APP
        self.state = self.root / "state"
        self.output = self.state / "admission.json"
        self.g6 = self.root / "g6.json"
        self.approval = self.root / "approval.json"
        artifact = b"signed artifact bytes\n"
        digest = "sha256:" + hashlib.sha256(artifact).hexdigest()
        write_private(self.cache / "artifact.zip", artifact)
        package = self.cache / "package"
        files = {
            "BUILD-INFO.txt": ("SOURCE_SHA=" + APP + "\n" +
                               "STORAGE=SIGNED_REBUILT_SILO_ARCHIVE_SYNTHETIC_QA\n").encode(),
            "storage-bundle/project-provenance.sigstore.json": b"{}\n",
            "storage-bundle/rebuild-report.json": b"{}\n",
            "storage-bundle/server-fixed.openvex.json": b"{}\n",
            "storage-bundle/server-cve-triage.json": b"{}\n",
            "storage-bundle/silo-qa-review-only.tar": b"synthetic\n",
        }
        for name, payload in files.items():
            write_private(package / name, payload)
        manifest = "".join(hashlib.sha256(payload).hexdigest() + "  ./" + name + "\n"
                           for name, payload in sorted(files.items()))
        write_private(package / "MANIFEST.sha256", manifest.encode())
        for directory in (self.cache_root, self.cache, package):
            directory.chmod(0o700)
        write_private(self.cache / "cache.json", {
            "schema": "qa525-cache-v2", "status": "SYNCED_ATTESTED",
            "ops_source_sha": OPS, "app_source_sha": APP,
            "artifact_sha256": digest, "network_allowed_after_sync": False,
            "synced_at": "2026-11-05T11:55:00Z",
            "cache_expires_at": "2026-11-05T18:00:00Z",
        })
        write_private(self.g6, {
            "g6_offhost_sha256_verified": True,
            "expires_at": "2026-11-05T14:00:00Z",
        })
        write_private(self.approval, {
            "status": "APPROVED", "nonce": NONCE,
            "ops_source_sha": OPS, "app_source_sha": APP,
            "expires_at": "2026-11-05T12:30:00Z",
        })

    def args(self):
        return type("Args", (), {
            "ops_sha": OPS, "app_sha": APP, "nonce": NONCE,
            "cache_root": self.cache_root, "g6_receipt": self.g6,
            "approval": self.approval, "state_dir": self.state,
            "output": self.output, "g6_margin_seconds": 1800,
            "staging": ROOT / "scripts" / "qa" / "qa-signed-staging.py",
        })()


class OfflineAdmission(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.fx = AdmissionFixture(self.tmp.name)
        self.time = mock.patch.object(MODULE, "now_utc", return_value=NOW)
        self.root_gate = mock.patch.object(MODULE, "require_root", return_value=None)
        self.time.start()
        self.root_gate.start()

    def tearDown(self):
        self.time.stop()
        self.root_gate.stop()
        self.tmp.cleanup()

    def test_happy_path_is_bound_and_consumes_nonce(self):
        result = MODULE.admit(self.fx.args())
        self.assertEqual(result["status"], "ADMITTED")
        self.assertEqual(result["app_source_sha"], APP)
        self.assertEqual(result["ops_source_sha"], OPS)
        self.assertFalse(result["network_allowed"])
        self.assertEqual(json.loads(self.fx.output.read_text()), result)
        marker = self.fx.state / ("nonce-consumed-" + NONCE + ".json")
        self.assertTrue(marker.exists())
        for path in (self.fx.output, marker):
            self.assertEqual(stat.S_IMODE(path.stat().st_mode), 0o600)

    def test_admission_performs_no_network_calls(self):
        with mock.patch.object(socket, "create_connection", side_effect=AssertionError("network")), \
             mock.patch.object(socket.socket, "connect", side_effect=AssertionError("network")):
            self.assertEqual(MODULE.admit(self.fx.args())["status"], "ADMITTED")

    def test_nonce_replay_fails_closed(self):
        MODULE.admit(self.fx.args())
        with self.assertRaisesRegex(MODULE.Stop, "NONCE_REPLAY"):
            MODULE.admit(self.fx.args())

    def test_artifact_change_fails_closed(self):
        self.fx.cache.joinpath("artifact.zip").write_bytes(b"changed")
        self.fx.cache.joinpath("artifact.zip").chmod(0o600)
        with self.assertRaisesRegex(MODULE.Stop, "CACHE_ARTIFACT_CHANGED"):
            MODULE.admit(self.fx.args())

    def test_staged_package_manifest_is_reverified_offline(self):
        target = self.fx.cache / "package" / "storage-bundle" / "rebuild-report.json"
        target.write_text('{"tampered":true}\n')
        target.chmod(0o600)
        with self.assertRaisesRegex(MODULE.Stop, "CACHE_PACKAGE_INVALID"):
            MODULE.admit(self.fx.args())

    def test_expired_cache_fails_closed(self):
        receipt = json.loads(self.fx.cache.joinpath("cache.json").read_text())
        receipt["cache_expires_at"] = "2026-11-05T11:59:59Z"
        write_private(self.fx.cache / "cache.json", receipt)
        with self.assertRaisesRegex(MODULE.Stop, "CACHE_EXPIRED"):
            MODULE.admit(self.fx.args())

    def test_g6_margin_is_checked_before_state_mutation(self):
        write_private(self.fx.g6, {
            "g6_offhost_sha256_verified": True,
            "expires_at": "2026-11-05T12:29:59Z",
        })
        with self.assertRaisesRegex(MODULE.Stop, "G6_MARGIN_LOW"):
            MODULE.admit(self.fx.args())
        self.assertFalse(self.fx.state.exists())

    def test_g6_must_be_offhost_verified(self):
        write_private(self.fx.g6, {
            "g6_offhost_sha256_verified": False,
            "expires_at": "2026-11-05T14:00:00Z",
        })
        with self.assertRaisesRegex(MODULE.Stop, "G6_NOT_VERIFIED"):
            MODULE.admit(self.fx.args())

    def test_approval_must_bind_both_planes_and_nonce(self):
        for field, value in (("ops_source_sha", "0" * 40),
                             ("app_source_sha", "1" * 40),
                             ("nonce", "other-nonce-000000000000000")):
            with self.subTest(field=field):
                fx = AdmissionFixture(Path(self.tmp.name) / field)
                approval = json.loads(fx.approval.read_text())
                approval[field] = value
                write_private(fx.approval, approval)
                with self.assertRaisesRegex(MODULE.Stop, "APPROVAL_BINDING_INVALID"):
                    MODULE.admit(fx.args())

    def test_expired_approval_fails_before_nonce_consumption(self):
        approval = json.loads(self.fx.approval.read_text())
        approval["expires_at"] = "2026-11-05T11:59:59Z"
        write_private(self.fx.approval, approval)
        with self.assertRaisesRegex(MODULE.Stop, "APPROVAL_EXPIRED"):
            MODULE.admit(self.fx.args())
        self.assertFalse(self.fx.state.exists())

    def test_symlink_hardlink_and_loose_mode_rejected(self):
        cases = ("symlink", "hardlink", "mode")
        for case in cases:
            with self.subTest(case=case), tempfile.TemporaryDirectory() as directory:
                fx = AdmissionFixture(directory)
                target = fx.g6
                if case == "symlink":
                    real = target.with_suffix(".real")
                    target.rename(real)
                    target.symlink_to(real)
                elif case == "hardlink":
                    os.link(target, target.with_suffix(".link"))
                else:
                    target.chmod(0o644)
                with self.assertRaisesRegex(MODULE.Stop, "FILE_(UNTRUSTED|UNAVAILABLE)"):
                    MODULE.admit(fx.args())

    def test_cache_root_symlink_or_loose_directory_is_rejected(self):
        for case in ("symlink", "mode"):
            with self.subTest(case=case), tempfile.TemporaryDirectory() as directory:
                fx = AdmissionFixture(directory)
                if case == "symlink":
                    real = fx.cache_root.with_name("cache-real")
                    fx.cache_root.rename(real)
                    fx.cache_root.symlink_to(real, target_is_directory=True)
                else:
                    fx.cache_root.chmod(0o755)
                with self.assertRaisesRegex(MODULE.Stop, "CACHE_DIRECTORY_UNTRUSTED"):
                    MODULE.admit(fx.args())


class StaticContract(unittest.TestCase):
    def test_script_compiles_and_has_three_separate_commands(self):
        source = SCRIPT.read_text()
        compile(source, str(SCRIPT), "exec")
        self.assertIn('sub.add_parser("sync")', source)
        self.assertIn('sub.add_parser("admit")', source)
        self.assertIn('sub.add_parser("postcheck")', source)
        self.assertNotIn('sub.add_parser("apply")', source)

    def test_no_anonymous_fallback_and_no_apply_verb(self):
        source = SCRIPT.read_text()
        self.assertIn("PAT_REQUIRED_NO_ANONYMOUS_FALLBACK", source)
        self.assertIn('"network_allowed": False', source)
        self.assertNotRegex(source, r"--review-and-run|docker\s+(compose|run|stop|restart)|pmgm-qa-update")

    def test_root_gate_remains_fail_closed(self):
        with mock.patch.object(MODULE.os, "geteuid", return_value=1000), \
             self.assertRaisesRegex(MODULE.Stop, "ROOT_REQUIRED"):
            MODULE.require_root("ROOT_REQUIRED")


class SyncDownload(unittest.TestCase):
    class Response:
        def __init__(self, url, payload):
            self.url = url
            self.stream = BytesIO(payload)
            self.headers = {"Content-Length": str(len(payload))}

        def geturl(self):
            return self.url

        def read(self, size=-1):
            return self.stream.read(size)

    class RedirectOpener:
        def __init__(self, location):
            self.location = location
            self.request = None

        def open(self, request, timeout=None):
            self.request = request
            headers = Message()
            headers["Location"] = self.location
            raise MODULE.HTTPError(request.full_url, 302, "Found", headers, None)

    class BlobOpener:
        def __init__(self, location, payload):
            self.location = location
            self.payload = payload
            self.request = None

        def open(self, request, timeout=None):
            self.request = request
            return SyncDownload.Response(self.location, self.payload)

    def test_off_origin_api_url_is_rejected_before_network(self):
        with tempfile.TemporaryDirectory() as directory, \
             mock.patch.object(MODULE, "build_opener", side_effect=AssertionError("network")):
            with self.assertRaisesRegex(MODULE.Stop, "ARTIFACT_URL_INVALID"):
                MODULE.download_artifact("https://example.invalid/a.zip", "secret", Path(directory) / "a")

    def test_untrusted_redirect_is_rejected(self):
        url = MODULE.load_readiness(ROOT / "scripts/qa/qa-agent-readiness.py").API + "/actions/artifacts/1/zip"
        api = self.RedirectOpener("https://evil.invalid/a.zip")
        with tempfile.TemporaryDirectory() as directory, \
             mock.patch.object(MODULE, "build_opener", return_value=api):
            with self.assertRaisesRegex(MODULE.Stop, "ARTIFACT_REDIRECT_UNTRUSTED"):
                MODULE.download_artifact(url, "secret", Path(directory) / "a")

    def test_authorization_is_not_forwarded_to_blob(self):
        url = MODULE.load_readiness(ROOT / "scripts/qa/qa-agent-readiness.py").API + "/actions/artifacts/1/zip"
        location = "https://safe.blob.core.windows.net/container/object?sig=abc"
        api = self.RedirectOpener(location)
        blob = self.BlobOpener(location, b"artifact")
        with tempfile.TemporaryDirectory() as directory, \
             mock.patch.object(MODULE, "build_opener", side_effect=[api, blob]):
            destination = Path(directory) / "artifact.zip"
            MODULE.download_artifact(url, "secret", destination)
            self.assertEqual(destination.read_bytes(), b"artifact")
        self.assertEqual(api.request.get_header("Authorization"), "Bearer secret")
        self.assertIsNone(blob.request.get_header("Authorization"))


class AppliedPostcheck(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.root = Path(self.tmp.name)
        self.prepared = self.root / "prepared.json"
        self.journal = self.root / ("dual-" + APP + ".json")
        self.launcher = self.root / "launcher.json"
        self.runtime = self.root / "runtime.json"
        self.api = "sha256:" + "a" * 64
        self.web = "sha256:" + "b" * 64
        self.root_gate = mock.patch.object(MODULE, "require_root", return_value=None)
        self.root_gate.start()
        write_private(self.prepared, {"source_sha": APP, "status": "PREPARED"})
        write_private(self.journal, {
            "source_sha": APP, "status": "APPLIED",
            "new_images": {"api": self.api, "web": self.web},
        })
        write_private(self.launcher, {
            "status": "QA525_UPDATE_STATUS_READ_ONLY", "services_running": True,
            "prepared_exists": True, "archive_reconciliation_required": False,
            "may_apply": False,
        })
        write_private(self.runtime, {"services": {
            "api": {"status": "running", "revision": APP, "image": self.api},
            "web": {"status": "running", "revision": APP, "image": self.web},
        }})

    def tearDown(self):
        self.root_gate.stop()
        self.tmp.cleanup()

    def args(self):
        return type("Args", (), {
            "app_sha": APP, "prepared": self.prepared, "journal": self.journal,
            "launcher_status": self.launcher, "runtime": self.runtime,
        })()

    def test_retained_prepared_is_valid_when_bound_and_closed(self):
        result = MODULE.postcheck(self.args())
        self.assertEqual(result["status"], "PASS_APPLIED")
        self.assertTrue(result["prepared_retained"])
        self.assertFalse(result["may_apply"])

    def test_retained_prepared_for_other_sha_is_rejected(self):
        write_private(self.prepared, {"source_sha": "0" * 40, "status": "PREPARED"})
        with self.assertRaisesRegex(MODULE.Stop, "PREPARED_NOT_BOUND_TO_APPLIED"):
            MODULE.postcheck(self.args())

    def test_runtime_and_next_drift_are_rejected(self):
        runtime = json.loads(self.runtime.read_text())
        runtime["services"]["api"]["image"] = "sha256:" + "c" * 64
        write_private(self.runtime, runtime)
        with self.assertRaisesRegex(MODULE.Stop, "RUNTIME_MISMATCH_API"):
            MODULE.postcheck(self.args())
        write_private(self.runtime, {"services": {
            "api": {"status": "running", "revision": APP, "image": self.api},
            "web": {"status": "running", "revision": APP, "image": self.web},
        }})
        write_private(Path(str(self.journal) + ".next"), {})
        with self.assertRaisesRegex(MODULE.Stop, "JOURNAL_NEXT_PRESENT"):
            MODULE.postcheck(self.args())


if __name__ == "__main__":
    unittest.main()
