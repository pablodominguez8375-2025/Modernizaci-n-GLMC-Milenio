"""No-network unit tests for the read-only QA automation planner."""
import importlib.util
from pathlib import Path
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[1]
SPEC = importlib.util.spec_from_file_location("qa_agent_plan", ROOT / "scripts/qa/qa-agent-plan.py")
agent = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(agent)

OLD, NEW = "a" * 40, "b" * 40
ART = {"status": "READY_FOR_SIGNED_STAGING_ONLY",
       "scope": "NO_HOST_ACCESS_NO_DEPLOYMENT_NO_UAT",
       "source_sha": NEW, "artifact_id": 90210,
       "artifact_sha256": "sha256:" + "c" * 64}


class QaAgentPlannerTests(unittest.TestCase):
    def test_first_boot_cannot_fetch_candidate(self):
        called = []
        def missing():
            raise FileNotFoundError("no supervised baseline")
        with self.assertRaises(FileNotFoundError):
            agent.plan(missing, lambda: called.append("selector"), lambda *_: None)
        self.assertEqual([], called)

    def test_ready_fast_forward_may_stage_not_deploy(self):
        result = agent.plan(lambda: OLD, lambda: dict(ART),
            lambda before, after: {
                "status": "FAST_FORWARD_CANDIDATE_ONLY",
                "previous_deployed_sha": before, "candidate_sha": after,
            })
        self.assertEqual("QA_SIGNED_CANDIDATE_ELIGIBLE_FOR_STAGING_ONLY", result["status"])
        self.assertEqual("READ_ONLY_NO_HOST_DEPLOYMENT", result["scope"])
        self.assertEqual(90210, result["artifact_id"])

    def test_same_version_does_nothing(self):
        result = agent.plan(lambda: NEW, lambda: dict(ART),
                            lambda *_: {"status": "ALREADY_DEPLOYED_NO_ACTION"})
        self.assertEqual("QA_UP_TO_DATE_NO_ACTION", result["status"])
        self.assertNotIn("artifact_id", result)

    def test_failed_workflow_blocks_without_ancestry_lookup(self):
        def should_not_compare(*_):
            self.fail("must not check ancestry for failed CI")
        with self.assertRaisesRegex(agent.Blocked, "DEV_NOT_READY"):
            agent.plan(lambda: OLD, lambda: {"status": "BLOCKED_QA_AGENT_READINESS"},
                       should_not_compare)

    def test_invalid_metadata_blocks(self):
        for variant in ({"artifact_id": True}, {"artifact_id": -2}, {"artifact_id": None},
                        {"artifact_sha256": None}, {"source_sha": None}):
            with self.subTest(variant=variant):
                with self.assertRaisesRegex(agent.Blocked, "INVALID_CANDIDATE"):
                    agent.plan(lambda: OLD, lambda: {**ART, **variant},
                               lambda *_: self.fail("unreachable"))

    def test_rollback_or_divergence_blocks(self):
        for value in ("NON_FAST_FORWARD", "BLOCKED_QA_ANTIROLLBACK", None):
            with self.subTest(value=value), self.assertRaisesRegex(
                    agent.Blocked, "NON_FAST_FORWARD_UPDATE_FORBIDDEN|INVALID_ANCESTRY_DECISION"):
                agent.plan(lambda: OLD, lambda: dict(ART),
                           lambda *_: None if value is None else {"status": value})

    def test_mismatched_sha_from_antirollback_blocks(self):
        with self.assertRaisesRegex(agent.Blocked, "RESPONSE_MISMATCH"):
            agent.plan(lambda: OLD, lambda: dict(ART),
                       lambda *_: {"status": "FAST_FORWARD_CANDIDATE_ONLY",
                                  "previous_deployed_sha": "d" * 40,
                                  "candidate_sha": NEW})

    def test_mismatched_candidate_sha_from_antirollback_blocks(self):
        with self.assertRaisesRegex(agent.Blocked, "RESPONSE_MISMATCH"):
            agent.plan(lambda: OLD, lambda: dict(ART),
                       lambda *_: {"status": "FAST_FORWARD_CANDIDATE_ONLY",
                                  "previous_deployed_sha": OLD,
                                  "candidate_sha": "d" * 40})

    def test_string_artifact_id_not_accepted(self):
        with self.assertRaisesRegex(agent.Blocked, "INVALID_CANDIDATE"):
            agent.plan(lambda: OLD, lambda: {**ART, "artifact_id": "90210"}, lambda *_: {})

    def test_baseline_invalid_is_blocked(self):
        with self.assertRaisesRegex(agent.Blocked, "BASELINE"):
            agent.plan(lambda: None, lambda: self.fail("no fetch"), lambda *_: {})


if __name__ == "__main__":
    unittest.main()
