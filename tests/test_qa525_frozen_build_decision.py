"""QA525 frozen BUILD_ONLY review gate: negative-first, off-host unit tests."""
import importlib.util
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]
PATH = ROOT / "scripts/qa/operators/qa525-frozen-build-decision.py"
spec = importlib.util.spec_from_file_location("qa525_frozen_gate", PATH)
gate = importlib.util.module_from_spec(spec)
spec.loader.exec_module(gate)


def reason_for(path):
    if path == "infrastructure/silo/Dockerfile.qa-rebuild":
        return "INFRASTRUCTURE_CHANGE"
    if "auth" in path:
        return "ACCOUNT_OR_IDENTITY_CHANGE"
    if "/Migrations/" in path:
        return "SCHEMA_OR_MODEL_CHANGE"
    return None


def diff(files=None):
    paths = sorted(gate.QA_ONLY_REVIEW_PATHS)
    if files is not None:
        paths = files
    return {"status": "ahead",
            "merge_base_commit": {"sha": gate.PREDECESSOR},
            "ahead_by": 106, "behind_by": 0,
            "files": [{"filename": p, "status": "modified"} for p in paths]}


def review(**changes):
    params = {"source": gate.SOURCE, "asset_digest": gate.DIGEST,
              "baseline": gate.PREDECESSOR, "diff": diff(),
              "reason_for": reason_for,
              "release_authenticated": True, "review_authenticated": True}
    params.update(changes)
    return gate.review_contract(**params)


class FrozenReviewGateTest(unittest.TestCase):
    def blocked(self, code, **changes):
        with self.assertRaises(gate.Blocked) as caught:
            review(**changes)
        self.assertEqual(str(caught.exception), code)

    def test_review_does_not_grant_build_or_apply(self):
        result = review()
        self.assertEqual(result["status"], "FROZEN_DIFF_REVIEW_ELIGIBLE_NO_BUILD_AUTHORITY")
        self.assertIs(result["may_build"], False)
        self.assertIs(result["may_apply"], False)

    def test_source_changed_stop(self):
        self.blocked("FROZEN_SOURCE_MISMATCH", source="b" * 40)

    def test_asset_digest_changed_stop(self):
        self.blocked("FROZEN_DIGEST_MISMATCH", asset_digest="sha256:" + "a" * 64)

    def test_baseline_changed_stop(self):
        self.blocked("APPLIED_BASELINE_MISMATCH", baseline="c" * 40)

    def test_signature_not_verified_stop(self):
        self.blocked("SIGNED_RELEASE_NOT_AUTHENTICATED", release_authenticated=False)

    def test_review_not_authenticated_stop(self):
        self.blocked("QA_ONLY_TECHNICAL_REVIEW_NOT_AUTHENTICATED",
                     review_authenticated=False)

    def test_diff_truncated_stop(self):
        d = diff(["qa/" + str(n) for n in range(300)])
        self.blocked("DIFF_MISSING_OR_TRUNCATED", diff=d)

    def test_non_ancestor_diff_stop(self):
        d = diff()
        d["merge_base_commit"]["sha"] = "a" * 40
        self.blocked("DIFF_NOT_ANCESTRAL", diff=d)

    def test_schema_change_stop(self):
        d = diff(sorted(gate.QA_ONLY_REVIEW_PATHS) +
                 ["backend/src/PMGM.Api/Migrations/AddTable.cs"])
        self.blocked("RUNTIME_OR_UNREVIEWED_SENSITIVE_CHANGE", diff=d)

    def test_unexpected_qa_review_scope_stop(self):
        d = diff(sorted(gate.QA_ONLY_REVIEW_PATHS -
                        {"tests/test_qa525_pat_auth.py"}))
        self.blocked("QA_ONLY_REVIEW_SCOPE_CHANGED", diff=d)

    def test_path_traversal_stop(self):
        d = diff(["../bad"])
        self.blocked("DIFF_PATH_UNTRUSTED", diff=d)


if __name__ == "__main__":
    unittest.main()
