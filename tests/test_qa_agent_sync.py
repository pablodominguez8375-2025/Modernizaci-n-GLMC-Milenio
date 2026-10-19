"""QA #525 offline regression: no automatic first boot or silent promotion."""
import importlib.util
from pathlib import Path
import unittest

FILE = Path(__file__).resolve().parents[1] / "scripts/qa/qa-agent-sync.py"
spec = importlib.util.spec_from_file_location("qa_agent_sync", FILE)
qa = importlib.util.module_from_spec(spec)
spec.loader.exec_module(qa)

OLD = "a" * 40
NEW = "b" * 40
DIGEST = "sha256:" + "c" * 64
SELECTED = {"status": "READY_FOR_SIGNED_STAGING_ONLY",
            "scope": "NO_HOST_ACCESS_NO_DEPLOYMENT_NO_UAT",
            "source_sha": NEW, "artifact_id": 9876, "artifact_sha256": DIGEST}
STAGED = {"status": "VERIFIED_QA_CANDIDATE_PREPARED_NOT_DEPLOYED",
          "source_sha": NEW, "artifact_id": 9876,
          "scope": "NO_DOCKER_NO_INSTALLATION_NO_QA_ACCEPTANCE"}


def good_plan(reader, selector, compare):
    prior = reader()
    item = selector()
    compare(prior, item["source_sha"])
    return {"status": "QA_SIGNED_CANDIDATE_ELIGIBLE_FOR_STAGING_ONLY",
            "source_sha": item["source_sha"], "artifact_id": item["artifact_id"],
            "artifact_sha256": item["artifact_sha256"],
            "scope": "READ_ONLY_NO_HOST_DEPLOYMENT"}


class AgentSyncTests(unittest.TestCase):
    def setUp(self):
        self.acquired = []
        self.selected = 0
        self.baselines = 0

    def baseline(self):
        self.baselines += 1
        return OLD

    def select(self):
        self.selected += 1
        return dict(SELECTED)

    def acquire(self, callback):
        self.acquired.append(callback())
        self.acquired.append(callback())
        return dict(STAGED)

    def sync(self, planner=good_plan, baseline=None, select=None, acquire=None):
        return qa.sync(planner, baseline or self.baseline, select or self.select,
                       lambda *_: {"status": "FAST_FORWARD_CANDIDATE_ONLY"},
                       acquire or self.acquire)

    def test_success_stages_only_after_double_pinning(self):
        result = self.sync()
        self.assertEqual("SIGNED_QA_CANDIDATE_STAGED_NO_DEPLOYMENT", result["status"])
        self.assertIn("NO_DOCKER", result["scope"])
        self.assertEqual(3, self.selected)
        self.assertEqual(2, self.baselines)
        self.assertEqual(2, len(self.acquired))

    def test_missing_baseline_blocks_before_network_and_token(self):
        def missing():
            raise RuntimeError("FIRST_DEPLOYMENT_REQUIRES_SUPERVISED_APPROVAL")
        with self.assertRaisesRegex(RuntimeError, "FIRST_DEPLOYMENT"):
            self.sync(baseline=missing, select=lambda: self.fail("network"),
                      acquire=lambda _: self.fail("token"))
        self.assertEqual([], self.acquired)

    def test_up_to_date_no_download(self):
        def same(reader, selector, compare):
            return {"status": "QA_UP_TO_DATE_NO_ACTION", "source_sha": reader()}
        result = self.sync(planner=same, select=lambda: self.fail("selector"),
                           acquire=lambda _: self.fail("acquisition"))
        self.assertEqual("QA_UP_TO_DATE_NO_ACTION", result["status"])

    def test_up_to_date_mismatch_is_rejected(self):
        with self.assertRaisesRegex(qa.Blocked, "PLAN_SHA_MISMATCH"):
            self.sync(planner=lambda *_: {"status": "QA_UP_TO_DATE_NO_ACTION",
                                          "source_sha": NEW})

    def test_plan_cannot_request_deployment(self):
        with self.assertRaisesRegex(qa.Blocked, "PLAN_NOT_ELIGIBLE"):
            self.sync(planner=lambda *_: {"status": "DEPLOY_TO_PRODUCTION"})
        self.assertFalse(self.acquired)

    def test_plan_scope_and_artifact_id_are_restricted(self):
        for patch in ({"scope": "DEPLOYMENT_ALLOWED"}, {"artifact_id": True},
                      {"artifact_sha256": "sha256:wrong"}, {"source_sha": "dev"}):
            with self.subTest(patch=patch), self.assertRaisesRegex(qa.Blocked, "PLAN_METADATA_INVALID"):
                self.sync(planner=lambda _a, _b, _c: {**good_plan(lambda: OLD,
                    lambda: SELECTED, lambda *_: None), **patch})

    def test_candidate_change_between_plan_and_stage_blocks(self):
        def select():
            self.selected += 1
            return dict(SELECTED) if self.selected == 1 else {**SELECTED, "source_sha": "d" * 40}
        with self.assertRaisesRegex(qa.Blocked, "DEV_ARTIFACT_CHANGED_AFTER_PLAN"):
            self.sync(select=select)
        self.assertEqual(0, len(self.acquired))

    def test_artifact_digest_change_blocks_without_downloading(self):
        def select():
            self.selected += 1
            return dict(SELECTED) if self.selected == 1 else {**SELECTED, "artifact_sha256": "sha256:" + "d" * 64}
        with self.assertRaisesRegex(qa.Blocked, "DEV_ARTIFACT_CHANGED_AFTER_PLAN"):
            self.sync(select=select)

    def test_staging_must_return_matching_sha_and_scope(self):
        for update in ({"source_sha": OLD}, {"artifact_id": 25},
                       {"scope": "DEPLOYED"}, {"status": "UNVERIFIED"}):
            with self.subTest(update=update), self.assertRaisesRegex(qa.Blocked, "STAGING_RESULT_MISMATCH"):
                self.sync(acquire=lambda _: {**STAGED, **update})

    def test_baseline_change_during_staging_blocks_result(self):
        def baseline():
            self.baselines += 1
            return OLD if self.baselines == 1 else "e" * 40
        with self.assertRaisesRegex(qa.Blocked, "QA_BASELINE_CHANGED_DURING_STAGING"):
            self.sync(baseline=baseline)

    def test_missing_or_false_selection_scope_blocks(self):
        for scope in ("DEPLOYMENT_ALLOWED", None, ""):
            with self.subTest(scope=scope), self.assertRaisesRegex(qa.Blocked, "SIGNED_SELECTION_INVALID"):
                self.sync(select=lambda: {**SELECTED, "scope": scope})


if __name__ == "__main__":
    unittest.main()
