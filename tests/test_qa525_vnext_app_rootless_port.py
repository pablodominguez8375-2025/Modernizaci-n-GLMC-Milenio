"""QA525 APP rootless port tests: FAKE GH runner, not Sigstore proof.

These are contract and failure-path tests; they do NOT establish provenance,
and no code path here ever authorizes installer/API/WEB/production.
"""
import hashlib
import importlib.util
import os
from pathlib import Path
import subprocess
import tempfile
from types import SimpleNamespace
import unittest
from unittest import mock

SRC = Path(__file__).resolve().parents[1] / "scripts/qa/qa525-vnext-app-rootless-port.py"
spec = importlib.util.spec_from_file_location("qa525_app_rootless_port_test", SRC)
app = importlib.util.module_from_spec(spec)
spec.loader.exec_module(app)

INNER = b"TEST INNER ZIP BYTES - NOT A GENUINE SIGNED ARTIFACT"
BUNDLE = b'{"synthetic":"not-a-Sigstore-attestation"}'
APP_SHA = "a" * 40
FAKE_BINARY_PIN = "sha256:" + "f" * 64


def digest(data):
    return "sha256:" + hashlib.sha256(data).hexdigest()


@unittest.skipIf(os.geteuid() == 0, "root is prohibited: run as ordinary CI user")
class RootlessAppPortTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.private = Path(self.tmp.name, "private")
        self.private.mkdir(mode=0o700)
        self.path = self.private / "inner.zip"
        self.path.write_bytes(INNER)
        self.path.chmod(0o600)
        self.calls = []
        # Only the test harness replaces the subprocess boundary. Production
        # verifier_for exposes no runner injection or Sigstore bypass.
        self.runner_patcher = mock.patch.object(app.subprocess, "run", side_effect=self.fake_runner)
        self.runner_patcher.start()
        self.addCleanup(self.runner_patcher.stop)
        self.good_challenge = {
            "repository": app.REPO,
            "signer_workflow": app.WORKFLOW,
            "oidc_issuer": app.ISSUER,
            "source_sha": APP_SHA,
            "archive_sha256": digest(INNER),
            "bundle_sha256": digest(BUNDLE),
        }

    def fake_runner(self, argv, **kwargs):
        self.calls.append((tuple(argv), kwargs))
        return SimpleNamespace(returncode=0)

    def verifier(self, **overrides):
        request = dict(
            signed_inner_zip=self.path, trusted_gh_sha256=FAKE_BINARY_PIN,
            expected_source_sha=APP_SHA, expected_inner_sha256=digest(INNER),
            expected_bundle_sha256=digest(BUNDLE),
        )
        request.update(overrides)
        return app.verifier_for(**request)

    def denied(self, code, fn, *args, **kwargs):
        with self.assertRaises(app.Stop) as e:
            fn(*args, **kwargs)
        self.assertEqual(str(e.exception), code)

    def test_pinned_cli_arguments_and_private_sealed_material(self):
        ver = self.verifier()
        with mock.patch.object(app, "_check_binary", return_value=None) as check:
            self.assertIs(ver(BUNDLE, dict(self.good_challenge)), True)
        self.assertEqual(check.call_count, 2)
        self.assertEqual(check.call_args.args, (FAKE_BINARY_PIN,))
        self.assertEqual(len(self.calls), 1)
        args, kw = self.calls[0]
        self.assertEqual(args[:3], (str(app.GH), "attestation", "verify"))
        self.assertEqual(args[args.index("--repo") + 1], app.REPO)
        self.assertEqual(args[args.index("--signer-workflow") + 1], app.WORKFLOW)
        self.assertEqual(args[args.index("--source-digest") + 1], APP_SHA)
        self.assertIn("--deny-self-hosted-runners", args)
        self.assertEqual(args[args.index("--format") + 1], "json")
        self.assertEqual(kw["env"]["GH_PROMPT_DISABLED"], "1")
        self.assertNotIn("GH_TOKEN", kw["env"])
        self.assertNotIn("GITHUB_TOKEN", kw["env"])
        self.assertEqual(kw["stdout"], subprocess.DEVNULL)
        self.assertEqual(kw["stderr"], subprocess.DEVNULL)
        self.assertEqual(kw["timeout"], 120)
        self.assertEqual(kw["check"], False)
        self.assertFalse(Path(args[3]).exists())
        self.assertFalse(Path(args[args.index("--bundle") + 1]).exists())

    def test_mixed_signer_repo_source_issuer_digest_rejected_before_cli(self):
        ver = self.verifier()
        altered = [
            dict(self.good_challenge, repository="attacker/project"),
            dict(self.good_challenge, signer_workflow="attacker/repo/action.yml"),
            dict(self.good_challenge, oidc_issuer="https://evil.example"),
            dict(self.good_challenge, source_sha="b" * 40),
            dict(self.good_challenge, archive_sha256="sha256:" + "0" * 64),
            dict(self.good_challenge, bundle_sha256="sha256:" + "1" * 64),
        ]
        for case in altered:
            with self.subTest(challenge=case):
                self.denied("APP_PORT_CHALLENGE_MISMATCH", ver, BUNDLE, case)
        self.denied("APP_PORT_CHALLENGE_MISMATCH", ver, BUNDLE + b"x", self.good_challenge)
        self.denied("APP_PORT_INPUT_INVALID", ver, "not-bytes", self.good_challenge)
        self.denied("APP_PORT_INPUT_INVALID", ver, BUNDLE, {"repository":app.REPO})
        self.denied("APP_PORT_INPUT_INVALID", ver, BUNDLE,
                    dict(self.good_challenge, extra="unsupported"))
        self.assertFalse(self.calls)

    def test_private_source_file_ownership_and_symlinks_enforced(self):
        ver = self.verifier()
        with mock.patch.object(app, "_check_binary", return_value=None):
            self.path.chmod(0o644)
            self.denied("APP_PORT_SOURCE_UNTRUSTED", ver, BUNDLE, self.good_challenge)
            self.path.chmod(0o600)
            self.private.chmod(0o755)
            self.denied("APP_PORT_SOURCE_UNTRUSTED", ver, BUNDLE, self.good_challenge)
            self.private.chmod(0o700)
            symlink = self.private / "symlink.zip"
            symlink.symlink_to(self.path)
            other = self.verifier(signed_inner_zip=symlink)
            self.denied("APP_PORT_SOURCE_UNTRUSTED", other, BUNDLE, self.good_challenge)
        self.assertFalse(self.calls)

    def test_wrong_zip_sha_rejected_before_fake_runner(self):
        self.path.write_bytes(b"DIFFERENT CONTENT")
        self.path.chmod(0o600)
        ver = self.verifier()
        with mock.patch.object(app, "_check_binary", return_value=None):
            self.denied("APP_PORT_SOURCE_HASH_MISMATCH", ver, BUNDLE, self.good_challenge)
        self.assertFalse(self.calls)

    def test_gh_failure_and_timeout_do_not_return_success(self):
        bad = lambda argv, **kwargs: SimpleNamespace(returncode=1)
        ver = self.verifier()
        with mock.patch.object(app, "_check_binary", return_value=None), \
                mock.patch.object(app.subprocess, "run", side_effect=bad):
            self.denied("APP_PORT_SIGSTORE_DENIED", ver, BUNDLE, self.good_challenge)
        def timeout(argv, **kwargs):
            raise subprocess.TimeoutExpired(argv, 120)
        with mock.patch.object(app, "_check_binary", return_value=None), \
                mock.patch.object(app.subprocess, "run", side_effect=timeout):
            self.denied("APP_PORT_SIGSTORE_DENIED", ver, BUNDLE, self.good_challenge)

    def test_runner_can_not_swap_sealed_files_after_fake_verification(self):
        def tamper(argv, **kwargs):
            Path(argv[3]).write_bytes(b"evil")
            return SimpleNamespace(returncode=0)
        ver = self.verifier()
        with mock.patch.object(app, "_check_binary", return_value=None), \
                mock.patch.object(app.subprocess, "run", side_effect=tamper):
            self.denied("APP_PORT_SIGSTORE_DENIED", ver, BUNDLE, self.good_challenge)

    def test_runtime_binary_pin_mismatch_is_not_bypassed(self):
        ver = self.verifier()
        with mock.patch.object(app, "_check_binary", side_effect=app.Stop("APP_PORT_BINARY_MISMATCH")):
            self.denied("APP_PORT_BINARY_MISMATCH", ver, BUNDLE, self.good_challenge)
        self.assertFalse(self.calls)

    def test_rejects_bad_constructor_pins_and_no_root_execution(self):
        for change in (
            {"expected_source_sha": "notsha"},
            {"expected_inner_sha256": "sha256:0"},
            {"expected_bundle_sha256": True},
            {"trusted_gh_sha256": "abc"},
            {"signed_inner_zip": str(self.path)},
        ):
            with self.subTest(change=change):
                self.denied("APP_PORT_PINS_INVALID", self.verifier, **change)
        # A caller-supplied fake runner must not be accepted by the public API.
        with self.assertRaises(TypeError):
            self.verifier(runner=self.fake_runner)
        with mock.patch.object(app.os, "geteuid", return_value=0):
            self.denied("APP_PORT_ROOT_FORBIDDEN", self.verifier)

    def test_large_input_rejected_without_runner(self):
        ver = self.verifier()
        with mock.patch.object(app, "MAX_BUNDLE", len(BUNDLE) - 1):
            self.denied("APP_PORT_INPUT_INVALID", ver, BUNDLE, self.good_challenge)
        with mock.patch.object(app, "MAX_ZIP", 6):
            with mock.patch.object(app, "_check_binary", return_value=None):
                self.denied("APP_PORT_SOURCE_UNTRUSTED", ver, BUNDLE, self.good_challenge)
        self.assertFalse(self.calls)

    def test_python_library_is_not_an_execution_entrypoint(self):
        data = SRC.read_text()
        self.assertIn("APP_PORT_ROOT_FORBIDDEN", data)
        self.assertIn("APP_PORT_SIGSTORE_DENIED", data)
        self.assertNotIn("sudo ", data)
        self.assertNotIn("docker compose", data)
        with self.assertRaises(SystemExit):
            exec(compile(data, str(SRC), "exec"), {"__name__": "__main__"})


if __name__ == "__main__":
    unittest.main()
