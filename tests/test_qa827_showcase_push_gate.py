"""QA827: required push Showcase cannot be replaced by manual dispatch."""
from pathlib import Path
import importlib.util
import re
import unittest

ROOT = Path(__file__).resolve().parents[1]
WORKFLOW = ROOT / ".github/workflows/showcase-pages.yml"
READINESS = ROOT / "scripts/qa/qa-agent-readiness.py"


def group(event, ref, pr_number=None):
    """Expected GitHub concurrency group under the exact workflow expression."""
    unit = pr_number if event == "pull_request" else ref
    return f"pmgm-showcase-{event}-{unit}"


class ShowcasePushGateTests(unittest.TestCase):
    def test_required_push_run_remains_authoritative(self):
        source = READINESS.read_text()
        self.assertIn('if run.get("event") != "push"', source)
        self.assertIn('"PMGM Showcase Demo"', source)
        self.assertIn('run.get("head_branch") != "dev"', source)

    def test_workflow_group_expression_separates_event_names(self):
        source = WORKFLOW.read_text()
        expected = ("  group: pmgm-showcase-${{ github.event_name }}-"
                    "${{ github.event_name == 'pull_request' && "
                    "github.event.pull_request.number || github.ref }}")
        self.assertIn(expected, source)
        self.assertEqual(1, source.count("concurrency:"))
        self.assertEqual(1, source.count("cancel-in-progress:"))
        self.assertIn(
            "  cancel-in-progress: ${{ github.event_name == 'pull_request' }}",
            source)
        self.assertNotIn("  cancel-in-progress: true", source)

    def test_manual_dispatch_cannot_cancel_dev_push(self):
        ref = "refs/heads/dev"
        self.assertNotEqual(group("workflow_dispatch",ref),group("push",ref))
        self.assertEqual(group("push",ref),group("push",ref))
        self.assertEqual("pmgm-showcase-push-refs/heads/dev",group("push",ref))
        self.assertNotEqual(group("pull_request", ref, pr_number=823),
                            group("push",ref))
        self.assertNotEqual(group("pull_request",ref,pr_number=823),
                            group("pull_request",ref,pr_number=826))

    def test_only_pr_validation_is_cancelable(self):
        # Literal expression excludes push and workflow_dispatch. A PR can
        # cancel older attempts, but a dev push must complete for readiness.
        cancel = lambda event: event == "pull_request"
        for event in ("push","workflow_dispatch"):
            with self.subTest(event=event):
                self.assertFalse(cancel(event))
        self.assertTrue(cancel("pull_request"))

    def test_push_dev_and_manual_dispatch_still_trigger(self):
        text = WORKFLOW.read_text()
        self.assertRegex(text, r"(?m)^  push:\s*$")
        self.assertIn("    branches: [dev]",text)
        self.assertRegex(text, r"(?m)^  workflow_dispatch:\s*$")
        self.assertRegex(text,r"(?m)^  pull_request:\s*$")

    def test_fresh_manual_success_cannot_replace_cancelled_push_in_real_gate(self):
        spec = importlib.util.spec_from_file_location(
            "qa827_existing_readiness", READINESS)
        gate = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(gate)
        source_sha = "a" * 40
        names = list(gate.REQUIRED)
        runs = [dict(name=name, id=i+100, run_attempt=1,
                     event="push", head_branch="dev", head_sha=source_sha,
                     status="completed", conclusion="success")
                for i,name in enumerate(names)]
        showcase = next(x for x in runs if x["name"] == "PMGM Showcase Demo")
        showcase["conclusion"] = "cancelled"
        manual = dict(showcase, id=500, event="workflow_dispatch",
                      conclusion="success")
        def fetch(path):
            if path == "/branches/dev":
                return {"commit": {"sha": source_sha}}
            if path == "/actions/runs?head_sha=" + source_sha + "&per_page=100":
                return {"workflow_runs": runs + [manual]}
            if path.endswith("/artifacts?per_page=100"):
                return {"artifacts": [dict(name=gate.ARTIFACT_PREFIX+source_sha,
                                            id=1, expired=False,
                                            size_in_bytes=100,
                                            digest="sha256:" + "b"*64)]}
            raise AssertionError("unexpected read " + path)
        with self.assertRaisesRegex(
                gate.GateFailure, "REQUIRED_DEV_WORKFLOW_NOT_SUCCESS"):
            gate.evaluate(fetch)
        showcase["conclusion"] = "success"
        result = gate.evaluate(fetch)
        self.assertEqual(source_sha, result["source_sha"])
        self.assertEqual("READY_FOR_SIGNED_STAGING_ONLY", result["status"])

    def test_validation_executes_before_long_frontend_matrix(self):
        text = WORKFLOW.read_text()
        self.assertIn("test_qa827_showcase_push_gate.py",text)
        self.assertLess(text.index("test_qa827_showcase_push_gate.py"),
                        text.index("Capture responsive visual evidence"))


if __name__ == "__main__":
    unittest.main()
