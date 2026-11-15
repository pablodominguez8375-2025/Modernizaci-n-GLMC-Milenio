"""QA525 successor baseline (draft, inactive): offline adversarial tests, no host side effects."""
import copy
import importlib.util
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]


def load(name, rel):
    spec = importlib.util.spec_from_file_location(name, ROOT / rel)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


succ = load("qa525_successor_baseline_test", "scripts/qa/operators/qa525-successor-baseline.py")
policy = load("qa525_successor_policy_test", "scripts/qa/operators/qa525-current-change-policy.py")

A, B, C = "a" * 40, "b" * 40, "c" * 40            # cut chain: A (applied) -> B -> C
DIGEST_B, DIGEST_C = "sha256:" + "b" * 64, "sha256:" + "c" * 64
IMG = {"api": "sha256:" + "1" * 64, "web": "sha256:" + "2" * 64}


def signed(sha, digest):
    return {"status": "READY_FOR_SIGNATURE_VERIFICATION_ONLY", "scope": policy.gate.RESULT_SCOPE,
            "source_sha": sha, "release_tag": "qa-signed-" + sha, "release_asset_sha256": digest}


def applied(sha, images):
    prepared = {"source_sha": sha, "status": "PREPARED_API_WEB_SIGNATURE_VERIFIED_NO_DEPLOYMENT",
                "api_image_id": images["api"], "web_image_id": images["web"]}
    journal = {"source_sha": sha, "status": "APPLIED", "new_images": dict(images)}
    live = {n: ["cid-" + n, images[n], "running"] for n in ("api", "web")}
    return prepared, {sha: journal}, live


def diff(base, n=4, ahead=7, names=None):
    names = names or ["frontend/src/App%d.tsx" % i for i in range(n)]
    return {"status": "ahead", "merge_base_commit": {"sha": base}, "behind_by": 0,
            "ahead_by": ahead, "total_commits": ahead,
            "files": [{"filename": x, "status": "modified"} for x in names]}


def run(selected, sha, digest, state, d, healthy=True):
    prepared, journals, live = state
    return succ.decide(selected, sha, digest, prepared, journals, live,
                       lambda s: healthy and s == prepared["source_sha"], d, policy.classify)


class SuccessorBaselineTests(unittest.TestCase):
    def setUp(self):
        self.state = applied(A, {"api": IMG["api"], "web": IMG["web"]})

    def test_positive_baseline_is_last_applied_not_a_constant(self):
        result = run(signed(B, DIGEST_B), B, DIGEST_B, self.state, diff(A))
        self.assertEqual(A, result["analytical_baseline_sha"])
        self.assertEqual("ELIGIBLE_FOR_HOST_PREFLIGHT_ONLY", result["status"])
        self.assertIs(False, result["may_apply"])

    def test_repeat_chain_A_to_B_to_C_without_changing_code(self):
        run(signed(B, DIGEST_B), B, DIGEST_B, self.state, diff(A))
        img_b = {"api": "sha256:" + "3" * 64, "web": "sha256:" + "4" * 64}
        after_b = applied(B, img_b)
        result = run(signed(C, DIGEST_C), C, DIGEST_C, after_b, diff(B, ahead=3))
        self.assertEqual(B, result["analytical_baseline_sha"])

    def test_sensitive_change_still_requires_review(self):
        d = diff(A, names=["backend/src/PMGM.Api/Program.cs"])
        result = run(signed(B, DIGEST_B), B, DIGEST_B, self.state, d)
        self.assertEqual("BLOCKED_REVIEW_REQUIRED", result["status"])
        migration = diff(A, names=["backend/src/X/Migrations/20261101_x.cs"])
        self.assertIn("SCHEMA_OR_MODEL_CHANGE",
                      run(signed(B, DIGEST_B), B, DIGEST_B, self.state, migration)["reasons"])

    def blocked(self, code, *args, **kw):
        with self.assertRaises(succ.Blocked) as ctx:
            run(*args, **kw)
        self.assertEqual(code, str(ctx.exception))

    def test_false_applied_state(self):
        prepared, journals, live = self.state
        journals[A]["status"] = "PENDING"
        self.blocked("BASELINE_JOURNAL_NOT_APPLIED", signed(B, DIGEST_B), B, DIGEST_B,
                     (prepared, journals, live), diff(A))
        for status in ("ROLLED_BACK", "RECOVERY_MANUALLY_REQUIRED"):
            journals[A]["status"] = status
            self.blocked("BASELINE_JOURNAL_NOT_APPLIED", signed(B, DIGEST_B), B, DIGEST_B,
                         (prepared, journals, live), diff(A))

    def test_other_incomplete_journal_blocks(self):
        prepared, journals, live = self.state
        journals["d" * 40] = {"source_sha": "d" * 40, "status": "PENDING"}
        self.blocked("BASELINE_JOURNAL_NOT_APPLIED", signed(B, DIGEST_B), B, DIGEST_B,
                     (prepared, journals, live), diff(A))

    def test_live_does_not_match_journal_or_prepared(self):
        prepared, journals, live = self.state
        live["web"] = ["cid", "sha256:" + "9" * 64, "running"]
        self.blocked("BASELINE_NOT_LIVE", signed(B, DIGEST_B), B, DIGEST_B, (prepared, journals, live), diff(A))
        prepared, journals, live = applied(A, IMG)
        live["api"][2] = "exited"
        self.blocked("BASELINE_NOT_LIVE", signed(B, DIGEST_B), B, DIGEST_B, (prepared, journals, live), diff(A))
        prepared, journals, live = applied(A, IMG)
        journals[A]["new_images"]["api"] = "sha256:" + "8" * 64
        self.blocked("BASELINE_PREPARED_JOURNAL_MISMATCH", signed(B, DIGEST_B), B, DIGEST_B,
                     (prepared, journals, live), diff(A))

    def test_health_failure_blocks(self):
        self.blocked("BASELINE_HEALTH_UNVERIFIED", signed(B, DIGEST_B), B, DIGEST_B, self.state, diff(A),
                     healthy=False)

    def test_wrong_merge_base_or_not_ancestor(self):
        self.blocked("DIFF_NOT_ANCESTRAL", signed(B, DIGEST_B), B, DIGEST_B, self.state, diff("e" * 40))
        behind = diff(A)
        behind["behind_by"] = 1
        self.blocked("DIFF_NOT_ANCESTRAL", signed(B, DIGEST_B), B, DIGEST_B, self.state, behind)
        diverged = diff(A)
        diverged["status"] = "diverged"
        self.blocked("DIFF_NOT_ANCESTRAL", signed(B, DIGEST_B), B, DIGEST_B, self.state, diverged)

    def test_truncated_or_oversize_diff_is_never_approved_partially(self):
        self.blocked("DIFF_TRUNCATED_OR_OVERSIZE", signed(B, DIGEST_B), B, DIGEST_B, self.state,
                     diff(A, n=300))
        self.blocked("DIFF_TRUNCATED_OR_OVERSIZE", signed(B, DIGEST_B), B, DIGEST_B, self.state,
                     diff(A, n=420))
        # 299 files is the largest diff that is provably complete.
        self.assertEqual("ELIGIBLE_FOR_HOST_PREFLIGHT_ONLY",
                         run(signed(B, DIGEST_B), B, DIGEST_B, self.state, diff(A, n=299))["status"])

    def test_commit_count_must_be_complete(self):
        self.blocked("DIFF_COMMITS_INCOMPLETE", signed(B, DIGEST_B), B, DIGEST_B, self.state, diff(A, ahead=250))
        mismatch = diff(A)
        mismatch["total_commits"] = 9
        self.blocked("DIFF_COMMITS_INCOMPLETE", signed(B, DIGEST_B), B, DIGEST_B, self.state, mismatch)

    def test_moving_target_and_wrong_digest(self):
        self.blocked("TARGET_MOVED_OR_NOT_FROZEN", signed(C, DIGEST_C), B, DIGEST_B, self.state, diff(A))
        self.blocked("TARGET_DIGEST_CHANGED", signed(B, DIGEST_C), B, DIGEST_B, self.state, diff(A))
        self.blocked("TARGET_DIGEST_INVALID", signed(B, DIGEST_B), B, "sha256:xyz", self.state, diff(A))
        self.blocked("TARGET_SHA_INVALID", signed(B, DIGEST_B), "B", DIGEST_B, self.state, diff(A))
        not_ready = signed(B, DIGEST_B)
        not_ready["status"] = "BLOCKED"
        self.blocked("TARGET_RELEASE_NOT_READY", not_ready, B, DIGEST_B, self.state, diff(A))

    def test_target_already_applied(self):
        self.blocked("TARGET_ALREADY_APPLIED", signed(A, DIGEST_B), A, DIGEST_B, self.state, diff(A))

    def test_classifier_cannot_return_a_different_baseline_or_apply_authority(self):
        prepared, journals, live = self.state
        for tweak in ({"analytical_baseline_sha": "f" * 40}, {"may_apply": True}, {"source_sha": "f" * 40}):
            def classify(baseline, selected, d, tweak=tweak):
                out = copy.deepcopy(policy.classify(baseline, selected, d))
                out.update(tweak)
                return out
            with self.assertRaises(succ.Blocked) as ctx:
                succ.decide(signed(B, DIGEST_B), B, DIGEST_B, prepared, journals, live,
                            lambda s: True, diff(A), classify)
            self.assertEqual("CLASSIFICATION_EVIDENCE_INVALID", str(ctx.exception))

    def test_errors_never_echo_input(self):
        secret = "SECRET-%s" % ("x" * 30)
        prepared, journals, live = self.state
        prepared["source_sha"] = secret
        with self.assertRaises(succ.Blocked) as ctx:
            succ.resolve_baseline(prepared, journals, live, lambda s: True)
        self.assertNotIn("SECRET", str(ctx.exception))


if __name__ == "__main__":
    unittest.main()
