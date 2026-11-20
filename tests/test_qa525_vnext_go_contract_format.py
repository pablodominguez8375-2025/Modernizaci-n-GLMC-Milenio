"""QA525: regression of canonical document BYTES only. Never approves GO; no host, sudo, secrets."""
import hashlib
import importlib.util
import json
from pathlib import Path
import unittest

SCRIPTS = Path(__file__).resolve().parents[1] / "scripts" / "qa"


def load(name, filename):
    spec = importlib.util.spec_from_file_location(name, SCRIPTS / filename)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


class TestQa525GoFormats(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.boot = load("qa525_test_bootstrap", "qa525-vnext-root-bootstrap.py")
        cls.sign = load("qa525_test_signing", "qa525-vnext-sponsor-signing.py")
        cls.cut = load("qa525_test_cut", "qa525-vnext-go-cut-contract.py")

    @staticmethod
    def manifest():
        # Values below are SYNTHETIC; not source/host pins.
        return {
            "config": {"name": "sponsor-config.json", "sha256": "1" * 64},
            "files": {"qa525-vnext-root-launcher.py": "2" * 64},
            "helpers": {
                "gh": {"path": "/usr/bin/gh", "sha256": "3" * 64},
                "ssh-keygen": {"path": "/usr/bin/ssh-keygen", "sha256": "4" * 64},
            },
            "interpreter": {"path": "/usr/bin/python3.14", "sha256": "5" * 64},
        }

    @staticmethod
    def canon_manifest(doc):
        return json.dumps(doc, sort_keys=True, separators=(",", ":")).encode()

    def check(self, raw, pin=None):
        return self.boot.check_manifest_pin(raw, pin or hashlib.sha256(raw).hexdigest())

    def go(self, expires="2026-11-11T12:14:00Z"):
        return self.sign.build_go_cut(
            purpose="LAUNCHER_ONLY", nonce="a" * 64,
            ops_source_sha="b" * 40, app_source_sha="b" * 40,
            app_archive_sha256="sha256:" + "c" * 64,
            plan_sha256="sha256:" + "d" * 64,
            admission_sha256="sha256:" + "e" * 64,
            policy_hash="sha256:" + "f" * 64,
            release_asset_sha256="sha256:" + "1" * 64,
            old_launcher_sha256="2" * 64, candidate_operator_sha256="3" * 64,
            issued_at="2026-11-11T12:00:00Z", expires_at=expires)

    def test_manifest_v3_canonical_success(self):
        files, interpreter, helpers, config = self.check(self.canon_manifest(self.manifest()))
        self.assertEqual(set(files), {"qa525-vnext-root-launcher.py"})
        self.assertEqual(interpreter["path"], "/usr/bin/python3.14")
        self.assertEqual(set(helpers), {"gh", "ssh-keygen"})
        self.assertEqual(config["name"], "sponsor-config.json")

    def test_manifest_trailing_newline_rejected_even_when_pinned(self):
        with self.assertRaisesRegex(self.boot.Stop, "BOOT_MANIFEST_INVALID"):
            self.check(self.canon_manifest(self.manifest()) + b"\n")

    def test_manifest_wrong_independent_pin_rejected(self):
        with self.assertRaisesRegex(self.boot.Stop, "BOOT_MANIFEST_PIN_MISMATCH"):
            self.check(self.canon_manifest(self.manifest()), "0" * 64)

    def test_manifest_extra_field_rejected(self):
        doc = self.manifest()
        doc["approved"] = True
        with self.assertRaisesRegex(self.boot.Stop, "BOOT_MANIFEST_INVALID"):
            self.check(self.canon_manifest(doc))

    def test_manifest_duplicate_field_rejected(self):
        raw = self.canon_manifest(self.manifest())
        duplicate = raw[:-1] + b',"config":{"name":"other.json","sha256":"' + b"1" * 64 + b'"}}'
        with self.assertRaisesRegex(self.boot.Stop, "BOOT_MANIFEST_INVALID"):
            self.check(duplicate)

    def test_launcher_go_canonical_success_without_authority(self):
        raw = self.go()
        self.assertTrue(raw.endswith(b"\n"))
        parsed = self.cut._parse(raw, launcher=True)
        self.assertEqual(parsed["scope"], "QA525_LAUNCHER_ONLY_NO_DOCKER_NO_IDENTITY_NO_MIGRATION")
        card = self.sign.review_card(raw)
        self.assertFalse(card["may_apply"])
        self.assertFalse(card["signed_here"])

    def test_launcher_go_without_newline_rejected(self):
        with self.assertRaisesRegex(self.cut.Stop, "GO_CUT_NOT_CANONICAL"):
            self.cut._parse(self.go()[:-1], launcher=True)

    def test_launcher_go_extra_field_rejected(self):
        obj = json.loads(self.go())
        obj["extra_authority"] = True
        raw = (json.dumps(obj, sort_keys=True, separators=(",", ":")) + "\n").encode()
        with self.assertRaisesRegex(self.cut.Stop, "GO_CUT_FIELDS_INVALID"):
            self.cut._parse(raw, launcher=True)

    def test_launcher_go_over_900_seconds_rejected(self):
        with self.assertRaisesRegex(self.sign.Stop, "SIGNSHEET_WINDOW_INVALID"):
            self.go(expires="2026-11-11T12:15:01Z")


if __name__ == "__main__":
    unittest.main()
