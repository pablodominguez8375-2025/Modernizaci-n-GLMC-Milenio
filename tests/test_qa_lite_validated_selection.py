import importlib.util
from pathlib import Path
import unittest

FILE = Path(__file__).with_name("test_qa_agent_readiness.py")
spec = importlib.util.spec_from_file_location("qa_readiness_fixture", FILE)
fixture = importlib.util.module_from_spec(spec)
spec.loader.exec_module(fixture)
agent = fixture.agent
TIP = "c" * 40


def data_fixture():
    data, runs, artifact = fixture.fixture()
    data["/branches/dev"] = {"commit": {"sha": TIP}}
    data["/actions/runs?head_sha=" + TIP + "&per_page=100"] = {"workflow_runs": []}
    data["/actions/runs?branch=dev&event=push&status=success&per_page=100"] = {"workflow_runs": list(runs)}
    data["/compare/" + fixture.SOURCE_SHA + "..." + TIP] = {
        "status": "ahead", "merge_base_commit": {"sha": fixture.SOURCE_SHA}}
    return data, runs, artifact


class ValidatedSelectionTests(unittest.TestCase):
    def test_newest_green_ancestor_is_selected_while_tip_builds(self):
        data, _, _ = data_fixture()
        self.assertEqual(fixture.SOURCE_SHA, agent.evaluate_latest_validated(data.__getitem__)["source_sha"])

    def test_green_tip_is_preferred(self):
        data, _, _ = fixture.fixture()
        self.assertEqual(fixture.SOURCE_SHA, agent.evaluate_latest_validated(data.__getitem__)["source_sha"])

    def test_fork_or_rewritten_history_candidate_is_not_allowed(self):
        data, _, _ = data_fixture()
        data["/compare/" + fixture.SOURCE_SHA + "..." + TIP]["status"] = "diverged"
        with self.assertRaisesRegex(agent.GateFailure, "NO_RECENT_VALIDATED"):
            agent.evaluate_latest_validated(data.__getitem__)

    def test_latest_failed_attempt_invalidates_historical_success(self):
        data, runs, _ = data_fixture()
        runs.append(dict(runs[0], id=9000, conclusion="failure"))
        with self.assertRaisesRegex(agent.GateFailure, "NO_RECENT_VALIDATED"):
            agent.evaluate_latest_validated(data.__getitem__)

    def test_expired_artifact_never_selected(self):
        data, _, artifact = data_fixture()
        artifact["expired"] = True
        with self.assertRaisesRegex(agent.GateFailure, "NO_RECENT_VALIDATED"):
            agent.evaluate_latest_validated(data.__getitem__)


if __name__ == "__main__":
    unittest.main()
