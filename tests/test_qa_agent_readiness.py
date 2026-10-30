"""Offline, no-secrets safety tests for QA auto-agent readiness."""
import importlib.util
from pathlib import Path
import unittest
from unittest.mock import patch
from urllib.error import HTTPError, URLError
from email.message import Message

FILE = Path(__file__).resolve().parents[1] / "scripts/qa/qa-agent-readiness.py"
spec = importlib.util.spec_from_file_location("qa_agent_readiness", FILE)
agent = importlib.util.module_from_spec(spec)
spec.loader.exec_module(agent)

SOURCE_SHA = "a" * 40


class HttpDiagnosticTests(unittest.TestCase):
    def test_rate_limit_and_retry_headers_have_safe_distinct_codes(self):
        for status, headers, expected in (
                (403, {"X-RateLimit-Remaining": "0"}, "GITHUB_API_RATE_LIMITED"),
                (429, {}, "GITHUB_API_RETRY_LATER"),
                (403, {"Retry-After": "60"}, "GITHUB_API_RETRY_LATER"),
                (500, {}, "GITHUB_API_HTTP_ERROR")):
            with self.subTest(status=status):
                msg = Message()
                for key, value in headers.items():
                    msg[key] = value
                error = HTTPError("https://api.github.com/", status, "sensitive-detail", msg, None)
                with patch.object(agent, "urlopen", side_effect=error):
                    with self.assertRaisesRegex(agent.GateFailure, "^" + expected + "$"):
                        agent.fetch_public("/branches/dev")

    def test_network_failure_and_absent_headers_never_expose_details(self):
        for error, expected in (
                (URLError("sensitive-detail"), "GITHUB_API_UNAVAILABLE"),
                (HTTPError("https://api.github.com/", 403, "sensitive-detail", None, None),
                 "GITHUB_API_HTTP_ERROR")):
            with patch.object(agent, "urlopen", side_effect=error):
                with self.assertRaisesRegex(agent.GateFailure, "^" + expected + "$"):
                    agent.fetch_public("/branches/dev")


def fixture():
    runs = [{
        "name": name, "id": 8000 + i, "run_attempt": 1,
        "head_sha": SOURCE_SHA, "head_branch": "dev",
        "event": "push", "status": "completed", "conclusion": "success",
    } for i, name in enumerate(agent.REQUIRED)]
    silo = next(r for r in runs if r["name"] == "QA Silo supply and recovery")
    artifact = {
        "name": agent.ARTIFACT_PREFIX + SOURCE_SHA,
        "id": 10101, "expired": False,
        "size_in_bytes": 123456, "digest": "sha256:" + "b" * 64,
    }
    data = {
        "/branches/dev": {"commit": {"sha": SOURCE_SHA}},
        "/actions/runs?head_sha=" + SOURCE_SHA + "&per_page=100": {"workflow_runs": runs},
        "/actions/runs/" + str(silo["id"]) + "/artifacts?per_page=100": {
            "artifacts": [artifact]},
    }
    return data, runs, artifact


class QaAgentReadinessTests(unittest.TestCase):
    def test_fully_successful_exact_dev_sha_is_staging_only(self):
        data, _, art = fixture()
        result = agent.evaluate(data.__getitem__)
        self.assertEqual("READY_FOR_SIGNED_STAGING_ONLY", result["status"])
        self.assertEqual(SOURCE_SHA, result["source_sha"])
        self.assertEqual(art["digest"], result["artifact_sha256"])
        self.assertIn("NO_DEPLOYMENT", result["scope"])

    def test_missing_workflow_is_blocked(self):
        data, runs, _ = fixture()
        runs.pop()
        with self.assertRaisesRegex(agent.GateFailure, "MISSING"):
            agent.evaluate(data.__getitem__)

    def test_failed_and_incomplete_workflows_block(self):
        for status, result in [("completed", "failure"), ("in_progress", None)]:
            with self.subTest(status=status, conclusion=result):
                data, runs, _ = fixture()
                runs[0]["status"] = status
                runs[0]["conclusion"] = result
                with self.assertRaisesRegex(agent.GateFailure, "NOT_SUCCESS"):
                    agent.evaluate(data.__getitem__)

    def test_pull_request_run_cannot_satisfy_protected_dev_gate(self):
        data, runs, _ = fixture()
        runs[0]["event"] = "pull_request"
        with self.assertRaisesRegex(agent.GateFailure, "MISSING"):
            agent.evaluate(data.__getitem__)

    def test_wrong_source_sha_never_selected(self):
        data, runs, _ = fixture()
        runs[0]["head_sha"] = "c" * 40
        with self.assertRaisesRegex(agent.GateFailure, "MISSING"):
            agent.evaluate(data.__getitem__)

    def test_expired_or_missing_artifact_is_blocked(self):
        for mutation in ("expired", "missing"):
            data, _, art = fixture()
            art["expired"] = mutation == "expired"
            if mutation == "missing":
                art["name"] = "unrelated-artifact"
            with self.subTest(mutation=mutation), self.assertRaisesRegex(agent.GateFailure, "NOT_UNIQUE"):
                agent.evaluate(data.__getitem__)

    def test_tampered_digest_and_size_are_blocked(self):
        for k, val in [("digest", "sha256:bad"), ("size_in_bytes", 0)]:
            data, _, art = fixture()
            art[k] = val
            with self.subTest(k=k), self.assertRaisesRegex(agent.GateFailure, "INVALID"):
                agent.evaluate(data.__getitem__)

    def test_duplicate_artifacts_are_blocked(self):
        data, _, art = fixture()
        path = "/actions/runs/8001/artifacts?per_page=100"
        data[path]["artifacts"].append(dict(art, id=10102))
        with self.assertRaisesRegex(agent.GateFailure, "NOT_UNIQUE"):
            agent.evaluate(data.__getitem__)

    def test_invalid_dev_sha_is_blocked(self):
        data, _, _ = fixture()
        data["/branches/dev"]["commit"]["sha"] = "dev"
        with self.assertRaisesRegex(agent.GateFailure, "SHA_MISSING"):
            agent.evaluate(data.__getitem__)

    def test_newer_failed_run_never_uses_old_success(self):
        data, runs, _ = fixture()
        newer = dict(runs[0], id=9000, conclusion="failure")
        runs.append(newer)
        with self.assertRaisesRegex(agent.GateFailure, "NOT_SUCCESS"):
            agent.evaluate(data.__getitem__)


if __name__ == "__main__":
    unittest.main()
