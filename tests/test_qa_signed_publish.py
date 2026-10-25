"""Regression: signed QA publisher must never report success for an unpublished push.

Offline only: no GitHub token, network, download, signature bypass, Docker or QA access.
"""
from contextlib import contextmanager
import importlib.util
import json
import os
from pathlib import Path
from tempfile import TemporaryDirectory
from types import SimpleNamespace
import unittest
from unittest.mock import Mock, patch

SCRIPT = Path(__file__).resolve().parents[1] / "scripts/qa/qa-signed-publish.py"
spec = importlib.util.spec_from_file_location("qa_signed_publisher", SCRIPT)
publisher = importlib.util.module_from_spec(spec)
spec.loader.exec_module(publisher)

TARGET = "a" * 40
PREVIOUS = "b" * 40
NEWER = "c" * 40


class SyntheticGateFailure(Exception):
    pass


@contextmanager
def fake_push(selection, *, tip=TARGET, event_name="push"):
    """Simulate the signed publishing decision without any side effects."""
    with TemporaryDirectory() as root:
        path = Path(root) / "event.json"
        path.write_text(json.dumps({
            "repository": {"full_name": publisher.REPO},
            "ref": "refs/heads/dev",
            "after": TARGET,
            "deleted": False,
        }), encoding="utf-8")
        evaluator = Mock()
        if isinstance(selection, list):
            evaluator.side_effect = selection
        elif isinstance(selection, Exception):
            evaluator.side_effect = selection
        else:
            evaluator.return_value = selection
        readiness = SimpleNamespace(
            GateFailure=SyntheticGateFailure,
            evaluate_latest_validated=evaluator,
        )

        def api(path):
            if path != "/branches/dev":
                raise AssertionError("Unexpected network path: " + path)
            return {"commit": {"sha": tip}}

        with (
            patch.dict(os.environ, {
                "GITHUB_EVENT_PATH": str(path),
                "GITHUB_EVENT_NAME": event_name,
            }),
            patch.object(publisher, "module", return_value=readiness),
            patch.object(publisher, "api", side_effect=api),
            patch.object(publisher, "publish_selected") as publish,
            patch.object(publisher.time, "sleep") as sleeper,
        ):
            yield readiness, publish, sleeper


class QaSignedPublishTests(unittest.TestCase):
    def test_latest_green_push_publishes_its_exact_sha(self):
        with fake_push({"source_sha": TARGET}) as (_, publish, sleep):
            self.assertEqual(0, publisher.main())
            self.assertEqual(TARGET, publish.call_args.args[0]["source_sha"])
            publish.assert_called_once()
            sleep.assert_not_called()

    def test_previous_release_only_is_not_a_green_new_publish(self):
        with fake_push({"source_sha": PREVIOUS}) as (_, publish, sleep):
            with self.assertRaisesRegex(SystemExit, "QA_TARGET_SHA_NOT_PUBLISHED"):
                publisher.main()
            publish.assert_called_once()
            self.assertEqual(50, sleep.call_count)

    def test_previous_then_target_publishes_each_once(self):
        with fake_push([{"source_sha": PREVIOUS}, {"source_sha": TARGET}]) as (_, publish, sleep):
            self.assertEqual(0, publisher.main())
            self.assertEqual([PREVIOUS, TARGET],
                             [call.args[0]["source_sha"] for call in publish.call_args_list])
            self.assertEqual(1, sleep.call_count)

    def test_no_ready_release_is_a_failure(self):
        with fake_push(SyntheticGateFailure("not ready")) as (_, publish, sleep):
            with self.assertRaisesRegex(SystemExit, "QA_PUBLICATION_GATES_NOT_READY"):
                publisher.main()
            publish.assert_not_called()
            self.assertEqual(50, sleep.call_count)

    def test_superseded_push_exits_without_publishing(self):
        with fake_push({"source_sha": PREVIOUS}, tip=NEWER) as (_, publish, sleep):
            self.assertEqual(0, publisher.main())
            publish.assert_not_called()
            sleep.assert_not_called()

    def test_manual_dispatch_cannot_become_a_signed_push(self):
        with fake_push({"source_sha": TARGET}, event_name="workflow_dispatch") as (_, publish, _):
            with self.assertRaisesRegex(SystemExit, "NOT_A_TRUSTED_DEV_PUSH"):
                publisher.main()
            publish.assert_not_called()


if __name__ == "__main__":
    unittest.main()
