"""Offline tests for the read-only API+WEB baseline capture CLI: NO Docker, NO host."""
import contextlib
import importlib.util
import io
import json
from pathlib import Path
import unittest
from unittest.mock import patch

SRC = Path(__file__).resolve().parents[1] / "scripts/qa/operators/qa525-apiweb-baseline-capture.py"
spec = importlib.util.spec_from_file_location("qa525_baseline_capture_test", SRC)
mod = importlib.util.module_from_spec(spec)
spec.loader.exec_module(mod)

SERVICES = {"api": "a", "web": "b", "keycloak": "c", "postgres": "d", "minio": "e", "gateway": "f", "clamav": "1",
            "db-grants": "2", "migrator": "3", "minio-init": "4"}
ONESHOT = {"db-grants", "migrator", "minio-init"}
WEB_SHA = "9" * 40


def fake_docker(calls, drift=False, state_override=None):
    ids = {n: c * 64 for n, c in SERVICES.items()}
    by_id = {v: k for k, v in ids.items()}

    def run(args):
        calls.append(list(args))
        if args[0] == "ps":
            return "\\n".join(ids.values()).replace("\\n", "\n")
        if args[:2] == ["image", "inspect"]:
            return WEB_SHA
        name = by_id[args[-1]]
        image = "sha256:" + SERVICES[name] * 64
        if drift and name == "api" and sum(1 for c in calls if c[0] == "inspect" and c[-1] == args[-1]) > 1:
            image = "sha256:" + "0" * 64
        layers = "/l/base.yml,/l/" + name + ".yml"
        state = "exited" if name in ONESHOT else "running"
        if state_override and name == state_override[0]:
            state = state_override[1]
        return json.dumps([args[-1], image, state, "t", 0, name, "/w", layers, [], {}, {}, 0])
    return run


class BaselineCaptureTests(unittest.TestCase):
    def test_captures_canonical_baseline_twice_with_only_read_verbs(self):
        calls = []
        result = mod.capture(fake_docker(calls))
        self.assertEqual("BASELINE_CAPTURED_READ_ONLY_NO_APPLY", result["status"])
        self.assertRegex(result["baseline_sha256"], r"^sha256:[0-9a-f]{64}$")  # same format as the apiweb.baseline_sha256 pin
        self.assertEqual(WEB_SHA, result["baseline"]["web_sha"])
        self.assertFalse(result["may_apply"] or result["wrote_files"] or result["go_spent"])
        self.assertEqual({"ps", "inspect", "image"}, {c[0] for c in calls})
        for verb in ("run", "up", "start", "stop", "restart", "rm", "build", "pull", "exec", "compose", "kill"):
            self.assertNotIn(verb, [c[0] for c in calls])

    def test_same_digest_on_repeat(self):
        self.assertEqual(mod.capture(fake_docker([]))["baseline_sha256"], mod.capture(fake_docker([]))["baseline_sha256"])

    def test_drift_between_reads_is_blocked(self):
        with self.assertRaisesRegex(mod.Blocked, "BASELINE_UNSTABLE_BETWEEN_READS"):
            mod.capture(fake_docker([], drift=True))

    def test_stopped_target_or_dirty_oneshot_is_not_a_baseline(self):
        with self.assertRaises(Exception):
            mod.capture(fake_docker([], state_override=("api", "exited")))
        with self.assertRaises(Exception):
            mod.capture(fake_docker([], state_override=("migrator", "running")))

    def test_only_explicit_capture_and_root_and_no_leak(self):
        out = io.StringIO()
        with contextlib.redirect_stdout(out):
            self.assertEqual(2, mod.main([]))
            with patch.object(mod.os, "geteuid", return_value=1000):
                self.assertEqual(2, mod.main(["--capture"]))
            with patch.object(mod.os, "geteuid", return_value=0), \
                    patch.object(mod, "capture", side_effect=RuntimeError("TOKEN=secret")):
                self.assertEqual(2, mod.main(["--capture"]))
        self.assertIn("ROOT_REQUIRED", out.getvalue())
        self.assertNotIn("secret", out.getvalue())

    def test_source_is_read_only(self):
        text = SRC.read_text()
        for op in ("--force-recreate", "docker compose", "'up'", '"up"', "open(", "write_text", "systemctl", "pg_dump"):
            self.assertNotIn(op, text)


if __name__ == "__main__":
    unittest.main()
