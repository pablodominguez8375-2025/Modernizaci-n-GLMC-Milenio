"""Regression for supervised QA initial baseline and GitHub commit ancestry."""
import importlib.util
import json
import os
from pathlib import Path
import tempfile
import unittest

SPEC = importlib.util.spec_from_file_location(
    "qa_antirollback", Path(__file__).resolve().parents[1] / "scripts/qa/qa-antirollback.py")
policy = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(policy)
OLD, NEW = "a" * 40, "b" * 40


def ancestry(status="ahead", *, old=OLD, new=NEW, ahead_by=1):
    return {
        "status": status,
        "ahead_by": ahead_by,
        "base_commit": {"sha": old},
        "merge_base_commit": {"sha": old},
        "commits": [{"sha": new}],
    }


class AntirollbackTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="qa-antirollback-")
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.state = self.root / "last-deployed.json"

    def save(self, data=None):
        data = data or {"version": 1, "last_deployed_sha": OLD, "qa_only": True}
        self.state.write_text(json.dumps(data))
        self.state.chmod(0o600)

    def test_missing_baseline_disallows_auto_first_boot(self):
        with self.assertRaisesRegex(policy.Blocked, "FIRST_DEPLOYMENT"):
            policy.baseline(self.state)

    def test_baseline_owner_and_mode_and_sha(self):
        self.save()
        self.assertEqual(OLD, policy.baseline(self.state))
        self.state.chmod(0o644)
        with self.assertRaisesRegex(policy.Blocked, "OWNER_MODE"):
            policy.baseline(self.state)

    def test_systemd_exception_never_accepts_local_0440_file(self):
        self.save()
        self.state.chmod(0o440)
        from unittest.mock import patch
        with patch.dict(os.environ, {"CREDENTIALS_DIRECTORY": str(self.root)}):
            self.assertFalse(policy.is_systemd_credential_copy(
                self.state, self.state.stat()))
            with self.assertRaisesRegex(policy.Blocked, "OWNER_MODE"):
                policy.baseline(self.state)

    def test_systemd_exception_requires_exact_credential_name(self):
        self.save()
        self.state.chmod(0o440)
        from unittest.mock import patch
        with patch.dict(os.environ, {"CREDENTIALS_DIRECTORY": "/run/credentials/mock.service"}):
            self.assertFalse(policy.is_systemd_credential_copy(
                self.state, self.state.stat()))

    def test_symlink_refused(self):
        self.save()
        link = self.root / "link.json"
        link.symlink_to(self.state)
        with self.assertRaisesRegex(policy.Blocked, "UNAVAILABLE"):
            policy.baseline(link)

    def test_broken_or_duplicate_json_refused(self):
        self.save()
        self.state.write_text('{"version":1,"version":1,"last_deployed_sha":"' + OLD + '","qa_only":true}')
        with self.assertRaisesRegex(policy.Blocked, "DUPLICATE"):
            policy.baseline(self.state)
        self.state.write_text("{")
        with self.assertRaisesRegex(policy.Blocked, "JSON_INVALID"):
            policy.baseline(self.state)

    def test_baseline_must_be_qa_only(self):
        for invalid in [{"version": 1, "last_deployed_sha": OLD, "qa_only": False},
                        {"version": 1, "last_deployed_sha": "dev", "qa_only": True},
                        {"version": 2, "last_deployed_sha": OLD, "qa_only": True}]:
            with self.subTest(data=invalid):
                self.save(invalid)
                with self.assertRaisesRegex(policy.Blocked, "SCHEMA_INVALID"):
                    policy.baseline(self.state)

    def test_equal_sha_skips_without_http_call(self):
        def forbidden(_b, _h):
            self.fail("must not call API on already deployed SHA")
        self.assertEqual("ALREADY_DEPLOYED_NO_ACTION", policy.evaluate(OLD, OLD, forbidden)["status"])

    def test_only_provable_fast_forward_allowed(self):
        result = policy.evaluate(OLD, NEW, lambda _a, _b: ancestry())
        self.assertEqual("FAST_FORWARD_CANDIDATE_ONLY", result["status"])
        self.assertIn("NO_INSTALLATION", result["scope"])

    def test_blocks_behind_diverged_or_missing_merge_base(self):
        for data in [ancestry("behind"), ancestry("diverged"),
                     ancestry(old="c" * 40), {**ancestry(), "merge_base_commit": None},
                     {**ancestry(), "ahead_by": 0}, {**ancestry(), "commits": []},
                     {**ancestry(), "commits": [{}]}, {}]:
            with self.subTest(data=data), self.assertRaisesRegex(policy.Blocked, "NON_FAST_FORWARD"):
                policy.evaluate(OLD, NEW, lambda _a, _b: data)

    def test_bad_candidate_sha_refused_before_api(self):
        for bad in ["dev", "A" * 40, "a" * 39]:
            with self.subTest(candidate=bad), self.assertRaisesRegex(policy.Blocked, "INVALID_COMMIT"):
                policy.evaluate(OLD, bad, lambda _a, _b: self.fail("must not call API"))

    def test_oversized_baseline_is_refused(self):
        self.state.write_text(" " * 4000)
        self.state.chmod(0o600)
        with self.assertRaisesRegex(policy.Blocked, "OWNER_MODE_OR_SIZE"):
            policy.baseline(self.state)

    def test_unsupported_extra_key_disallowed(self):
        self.save({"version": 1, "qa_only": True,
                   "last_deployed_sha": OLD, "production": True})
        with self.assertRaisesRegex(policy.Blocked, "SCHEMA_INVALID"):
            policy.baseline(self.state)


if __name__ == "__main__":
    unittest.main()
