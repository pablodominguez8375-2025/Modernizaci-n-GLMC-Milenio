"""Offline QA525 signed API+WEB image-preparation tests, NO Docker or host."""
import contextlib
import importlib.util
import io
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import Mock, patch

SRC = Path(__file__).resolve().parents[1] / "scripts/qa/operators/qa525-current-image-preparation.py"
spec = importlib.util.spec_from_file_location("qa525_dynamic_image_prepare_test", SRC)
mod = importlib.util.module_from_spec(spec)
spec.loader.exec_module(mod)

SHA = "a" * 40
API = "sha256:" + "b" * 64
WEB = "sha256:" + "c" * 64
OLD = "sha256:" + "d" * 64
CID = "e" * 64


class DynamicImagePreparationTests(unittest.TestCase):
    def setUp(self):
        self.t = tempfile.TemporaryDirectory(prefix="qa525-dynamic-test-")
        self.addCleanup(self.t.cleanup)
        self.package = Path(self.t.name) / "signed-source"
        (self.package / "backend").mkdir(parents=True)
        (self.package / "backend" / "Dockerfile").write_text("FROM scratch\n")
        self.events = []

    def run_docker(self, args, **kwargs):
        if args[0] == "build":
            self.events.append("build-api")
            p = Path(args[args.index("--iidfile") + 1])
            p.write_text(API)
            self.assertIn("org.opencontainers.image.revision=" + SHA,
                          args[args.index("--label") + 1])
            self.assertNotIn("up", args)
            return ""
        if args[:3] == ["image", "inspect", "--format"]:
            return API if args[3] == "{{.Id}}" else SHA
        raise AssertionError("unexpected Docker command")

    def verify(self, package, sha):
        self.assertEqual(self.package, package)
        self.assertEqual(SHA, sha)
        self.events.append("verified")
        return True

    def web(self, package, sha, verify):
        self.events.append("build-web")
        self.assertTrue(verify(package, sha))
        return {"status": "LOCAL_WEB_IMAGE_PREPARED_NOT_DEPLOYED",
                "source_sha": sha, "web_image_id": WEB}

    def test_build_signed_api_and_web_without_container_changes(self):
        result = mod.build_images(self.package, SHA, self.verify,
                                  self.run_docker, self.web)
        self.assertEqual({"api_image_id": API, "web_image_id": WEB}, result)
        self.assertEqual(1, self.events.count("build-api"))
        self.assertEqual(1, self.events.count("build-web"))
        self.assertGreaterEqual(self.events.count("verified"), 4)

    def test_signature_missing_prevents_api_build(self):
        runner = Mock()
        with self.assertRaisesRegex(mod.Blocked, "SOURCE_NOT_REVERIFIED"):
            mod.build_images(self.package, SHA, lambda *_: False,
                             runner, self.web)
        runner.assert_not_called()

    def test_unsigned_mutation_after_api_build_blocks(self):
        index = [0]
        def verify(package, sha):
            index[0] += 1
            return index[0] == 1
        with self.assertRaisesRegex(mod.Blocked, "SOURCE_CHANGED_DURING_API_BUILD"):
            mod.build_images(self.package, SHA, verify, self.run_docker, self.web)
        self.assertNotIn("build-web", self.events)

    def test_source_mutation_before_web_build_blocks(self):
        index = [0]
        def verify(package, sha):
            index[0] += 1
            return index[0] <= 2
        with self.assertRaisesRegex(mod.Blocked, "SOURCE_CHANGED_BEFORE_WEB_BUILD"):
            mod.build_images(self.package, SHA, verify, self.run_docker, self.web)

    def test_different_release_sha_in_web_receipt_blocks(self):
        def wrong(pkg, sha, verify):
            return {"status": "LOCAL_WEB_IMAGE_PREPARED_NOT_DEPLOYED",
                    "source_sha": "f" * 40, "web_image_id": WEB}
        with self.assertRaisesRegex(mod.Blocked, "WEB_PREPARATION_INVALID"):
            mod.build_images(self.package, SHA, self.verify, self.run_docker, wrong)

    def test_no_duplicate_image_ids(self):
        def duplicate(pkg, sha, verify):
            return {"status": "LOCAL_WEB_IMAGE_PREPARED_NOT_DEPLOYED",
                    "source_sha": sha, "web_image_id": API}
        with self.assertRaisesRegex(mod.Blocked, "WEB_PREPARATION_INVALID"):
            mod.build_images(self.package, SHA, self.verify,
                             self.run_docker, duplicate)

    def test_invalid_source_sha_stops_before_docker(self):
        runner = Mock()
        with self.assertRaisesRegex(mod.Blocked, "TARGET_NOT_CURRENT_SHA"):
            mod.build_images(self.package, "old", self.verify,
                             runner, self.web)
        runner.assert_not_called()

    def test_invalid_api_image_id_rejected(self):
        def bad(args, **kwargs):
            if args[0] == "build":
                Path(args[args.index("--iidfile") + 1]).write_text("fake-tag")
                return ""
            raise AssertionError("should not inspect")
        with self.assertRaisesRegex(mod.Blocked, "BUILT_API_IMAGE_INVALID"):
            mod.build_images(self.package, SHA, self.verify, bad, self.web)

    def test_api_build_context_must_contain_dockerfile(self):
        (self.package / "backend" / "Dockerfile").unlink()
        runner = Mock()
        with self.assertRaisesRegex(mod.Blocked, "SIGNED_API_BUILD_CONTEXT_INVALID"):
            mod.build_images(self.package, SHA, self.verify, runner, self.web)
        runner.assert_not_called()

    def test_snapshot_only_has_seven_services_and_no_secrets(self):
        def docker(args, **kw):
            name = args[-1].split("-")[-2]
            return json.dumps([{
                "Id": CID, "Image": OLD, "State": {"Status": "running"},
                "Config": {
                    "Env": ["SystemMail__EncryptionKey=very-secret"],
                    "Labels": {
                        "com.docker.compose.project": mod.PROJECT,
                        "com.docker.compose.service": name,
                    },
                },
            }])
        snap = mod.snapshot(docker)
        self.assertEqual(set(mod.SERVICES), set(snap))
        self.assertNotIn("very-secret", json.dumps(snap))
        self.assertTrue(all(v["running"] for v in snap.values()))

    def test_snapshot_stopped_or_wrong_project_is_rejected(self):
        for failed in ("stopped", "project"):
            def docker(args, **kw):
                name = args[-1].split("-")[-2]
                return json.dumps([{
                    "Id": CID, "Image": OLD,
                    "State": {"Status": "stopped" if failed == "stopped" else "running"},
                    "Config": {"Labels": {
                        "com.docker.compose.project": "wrong" if failed == "project" else mod.PROJECT,
                        "com.docker.compose.service": name,
                    }},
                }])
            with self.subTest(failed=failed):
                with self.assertRaisesRegex(mod.Blocked, "DOCKER_BASELINE_INVALID"):
                    mod.snapshot(docker)

    def test_live_gate_blocks_when_identity_risk_or_sha_drift(self):
        with patch.object(mod.stage.signed.staging, "verify_manifest"), \
             patch.object(mod.stage, "validated_host_check", return_value={
                 "status": "BLOCKED_REVIEW_REQUIRED",
                 "source_sha": SHA, "may_apply": False,
             }):
            self.assertFalse(mod.signed_source_guard(self.package, SHA))

    def test_manifest_verification_fails_closed(self):
        with patch.object(mod.stage.signed.staging, "verify_manifest", side_effect=RuntimeError("invalid")):
            self.assertFalse(mod.signed_source_guard(self.package, SHA))

    def test_not_glmdch_blocks_preparation_before_any_file_or_docker_access(self):
        with patch.object(mod.os, "geteuid", return_value=1000), \
             patch.object(mod.socket, "gethostname", return_value="somewhere"), \
             patch.object(mod, "trusted_state") as state, \
             patch.object(mod.stage, "stage_current") as stage:
            with self.assertRaisesRegex(mod.Blocked, "HOST_NOT_AUTHORIZED"):
                mod.prepare()
            state.assert_not_called()
            stage.assert_not_called()

    def test_only_explicit_prepare_cli_allowed_and_no_secret_errors(self):
        out = io.StringIO()
        with patch.object(mod, "prepare", side_effect=RuntimeError("SMTP_SECRET=private")), \
             contextlib.redirect_stdout(out):
            result = mod.main(["--prepare"])
        self.assertEqual(2, result)
        self.assertIn("PREPARATION_NOT_VERIFIED", out.getvalue())
        self.assertNotIn("SMTP_SECRET", out.getvalue())
        with patch.object(mod, "prepare") as prep:
            self.assertEqual(2, mod.main([]))
            prep.assert_not_called()

    def test_code_does_not_contain_apply_operations(self):
        source = SRC.read_text()
        for op in ("--force-recreate", "'up'", '"up"', "systemctl", "pg_dump",
                   "dropdb", "docker compose up", "migration apply"):
            self.assertNotIn(op, source)
        self.assertIn('"may_apply": False', source)
        self.assertLess(source.index("bootstrap_import_trust()"),
                        source.index("stage = load("))


if __name__ == "__main__":
    unittest.main()
