"""Build preparation: no runtime changes, source/config TOCTOU and VITE limits."""
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location("qa_web_build", ROOT / "scripts/qa/qa-lite-web-build.py")
mod = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = mod
spec.loader.exec_module(mod)
SHA = "f" * 40
IMAGE = "sha256:" + "a" * 64


class BuildTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.root = Path(self.tmp.name)
        self.package = self.root / SHA
        (self.package / "frontend").mkdir(parents=True)
        (self.package / "frontend/Dockerfile").write_text("FROM nginx:stable-alpine\n")
        self.config = self.root / "compose.json"
        self.args = {"VITE_USE_MOCKS": "false", "VITE_API_BASE_URL": "",
                     "VITE_OIDC_AUTHORITY": mod.AUTHORITY, "VITE_OIDC_CLIENT_ID": "pmgm-web"}
        self.web = {"build": {"context": mod.OLD_CONTEXT, "args": self.args}}
        self.calls = []
        self.source_calls = 0
        self.patches = [patch.object(mod, "COMPOSE", self.config),
                        patch.object(mod.transport, "trusted_path", side_effect=lambda p, **kw: Path(p)),
                        patch.object(mod.os, "geteuid", return_value=0),
                        patch.object(mod.socket, "gethostname", return_value="glmdch")]
        for value in self.patches:
            value.start()
            self.addCleanup(value.stop)
        self.write_config()

    def write_config(self):
        self.config.write_text(json.dumps({"services": {"web": self.web}}))
        os.chmod(self.config, 0o600)
        mod.COMPOSE_SHA256 = hashlib.sha256(self.config.read_bytes()).hexdigest()

    def verify(self, package, sha):
        self.assertEqual((self.package, SHA), (package, sha))
        self.source_calls += 1
        return True

    def engine(self, args, **kwargs):
        self.calls.append(args)
        if args[0] == "build":
            self.assertIn("qa-version.json", Path(args[args.index("--file") + 1]).read_text())
            Path(args[args.index("--iidfile") + 1]).write_text(IMAGE)
            return ""
        return SHA if "revision" in args[-2] else IMAGE

    def test_build_has_no_compose_runtime_or_secret_inputs(self):
        result = mod.prepare_web(self.package, SHA, self.verify, run=self.engine)
        self.assertEqual("LOCAL_WEB_IMAGE_PREPARED_NOT_DEPLOYED", result["status"])
        self.assertEqual(2, self.source_calls)
        self.assertEqual(IMAGE, result["web_image_id"])
        self.assertEqual("build", self.calls[0][0])
        self.assertTrue(all(x[0] in {"build", "image"} for x in self.calls))
        self.assertNotIn("VITE_OIDC_AUTHORITY", json.dumps(result))
        self.assertIn("org.opencontainers.image.revision=" + SHA, self.calls[0])

    def test_unverified_source_never_builds(self):
        with self.assertRaisesRegex(mod.transport.Blocked, "SIGNED_SOURCE_NOT_VERIFIED"):
            mod.prepare_web(self.package, SHA, lambda *a: False, run=self.engine)
        self.assertEqual([], self.calls)

    def test_config_hash_drift_blocks(self):
        self.config.write_text("changed")
        with self.assertRaisesRegex(mod.transport.Blocked, "PHYSICAL_COMPOSE_CHANGED"):
            mod.prepare_web(self.package, SHA, self.verify, run=self.engine)
        self.assertEqual([], self.calls)

    def test_only_known_public_identity_build_args_are_allowed(self):
        for key, value in (("SECRET", "private"), ("VITE_USE_MOCKS", "true"),
                           ("VITE_OIDC_AUTHORITY", "http://evil"),
                           ("VITE_API_BASE_URL", "https://evil")):
            previous = self.args.copy()
            self.args[key] = value
            self.write_config()
            with self.assertRaises(mod.transport.Blocked):
                mod.prepare_web(self.package, SHA, self.verify, run=self.engine)
            self.args.clear()
            self.args.update(previous)
        self.assertEqual([], self.calls)

    def test_changed_source_or_config_after_build_never_returns_ready(self):
        for change in ("source", "config"):
            self.write_config()
            counter = [0]
            def verify(*args):
                counter[0] += 1
                return counter[0] == 1 or change != "source"
            def run(args, **kwargs):
                result = self.engine(args, **kwargs)
                if args[0] == "build" and change == "config":
                    self.config.write_text("changed")
                return result
            with self.assertRaises(mod.transport.Blocked):
                mod.prepare_web(self.package, SHA, verify, run=run)

    def test_changed_layout_or_extra_build_options_blocks(self):
        self.web["build"]["ssh"] = ["default"]
        self.write_config()
        with self.assertRaisesRegex(mod.transport.Blocked, "WEB_BUILD_LAYOUT_CHANGED"):
            mod.prepare_web(self.package, SHA, self.verify, run=self.engine)


if __name__ == "__main__":
    unittest.main()
