"""QA827: required push Showcase cannot be replaced by manual dispatch."""
from pathlib import Path
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

    def test_validation_executes_before_long_frontend_matrix(self):
        text = WORKFLOW.read_text()
        self.assertIn("test_qa827_showcase_push_gate.py",text)
        self.assertLess(text.index("test_qa827_showcase_push_gate.py"),
                        text.index("Capture responsive visual evidence"))


if __name__ == "__main__":
    unittest.main()
