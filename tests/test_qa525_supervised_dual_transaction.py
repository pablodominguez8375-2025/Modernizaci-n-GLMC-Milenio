"""Hermetic QA525 dual API+WEB transaction regressions; no real Docker or QA."""
import copy
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

SCRIPT = Path(__file__).resolve().parents[1] / "scripts/qa/operators/qa525-supervised-dual-transaction.py"
spec = importlib.util.spec_from_file_location("qa525_dual_test", SCRIPT)
tx = importlib.util.module_from_spec(spec)
spec.loader.exec_module(tx)
WORK = Path("/opt/centenario/qa-lab-firstboot/recovery-v2")
TARGET = "a" * 40
NEW = {"api": "sha256:" + "1" * 64, "web": "sha256:" + "2" * 64}
LAYERS = {
    "api": (str(WORK / "compose.recovery.json"),
            "/opt/centenario/qa-api-refresh/api-" + "b" * 40 + ".json",
            "/etc/pmgm/qa754-identity-overlay.stage.json"),
    "web": (str(WORK / "compose.recovery.json"),
            str(WORK / ("web-" + "c" * 64 + ".json"))),
}


def baseline():
    result = {}
    for i, service in enumerate(tx.TARGETS + tx.DEPENDENCIES):
        result[service] = {"id": format(i + 3, "064x"),
                           "image": "sha256:" + format(i + 10, "064x"),
                           "status": "running"}
    for i, service in enumerate(tx.ONESHOTS):
        result[service] = {"id": format(i + 16, "064x"),
                           "status": "exited", "exit_code": 0}
    return result


class Fake:
    def __init__(self):
        self.initial = baseline()
        self.now = copy.deepcopy(self.initial)
        self.commands = []
        self.fail_web = False
        self.fail_rollback = False
        self.health_fail_first = False
        self.health_calls = 0
        self.audit_events = []

    def run(self, args):
        assert args[:5] == ["compose", "--project-name", tx.PROJECT,
                             "--project-directory", str(WORK)]
        assert "--no-deps" in args
        assert "--no-build" in args
        assert args[-1] in tx.TARGETS
        assert "down" not in args and "rm" not in args
        service = args[-1]
        candidate = args[args.index("--file", 6) + 1] if False else None
        files = [args[i + 1] for i, x in enumerate(args) if x == "--file"]
        image = json.loads(Path(files[-1]).read_text())["services"][service]["image"]
        if self.fail_web and service == "web" and image == NEW["web"]:
            raise RuntimeError("simulated WEB failure")
        if self.fail_rollback and image == self.initial[service]["image"]:
            raise RuntimeError("simulated rollback failure")
        self.commands.append((service, image, files))
        self.now[service]["image"] = image
        self.now[service]["id"] = format(len(self.commands) + 100, "064x")

    def snapshot(self):
        return copy.deepcopy(self.now)

    def evidence(self, phase, sha, previous, future):
        assert sha == TARGET
        assert previous == self.initial
        assert future == NEW
        return True

    def health(self):
        self.health_calls += 1
        return not (self.health_fail_first and self.health_calls == 1)

    def audit(self, event, sha):
        self.audit_events.append(event)


class TransactionTests(unittest.TestCase):
    def setUp(self):
        self.sandbox = tempfile.TemporaryDirectory()
        self.addCleanup(self.sandbox.cleanup)
        self.state = Path(self.sandbox.name)
        self.state.chmod(0o700)
        self.root = patch.object(tx, "root_path", side_effect=lambda path, directory=False: Path(path))
        self.root.start()
        self.addCleanup(self.root.stop)
        self.fake = Fake()

    def do(self, evidence=None):
        return tx.transact(
            before=copy.deepcopy(self.fake.initial), new=NEW, source_sha=TARGET,
            layers=LAYERS, workdir=str(WORK), state_dir=self.state,
            run=self.fake.run, live_snapshot=self.fake.snapshot,
            evidence=evidence or self.fake.evidence,
            health=self.fake.health, audit=self.fake.audit,
        )

    def test_success_never_changes_dependency_or_oneshot(self):
        receipt = self.do()
        self.assertEqual(receipt["status"], "DUAL_API_WEB_APPLIED")
        self.assertEqual(self.fake.now["api"]["image"], NEW["api"])
        self.assertEqual(self.fake.now["web"]["image"], NEW["web"])
        self.assertEqual([s for s, _, _ in self.fake.commands], ["api", "web"])
        self.assertEqual(self.fake.snapshot()["keycloak"], self.fake.initial["keycloak"])
        self.assertEqual(self.fake.snapshot()["migrator"], self.fake.initial["migrator"])
        self.assertEqual(json.loads(next(self.state.glob("dual-*.json")).read_text())["status"], "APPLIED")
        self.assertEqual(self.fake.audit_events, ["DUAL_API_WEB_APPLIED"])

    def test_failed_web_apply_restores_both(self):
        self.fake.fail_web = True
        with self.assertRaises(RuntimeError):
            self.do()
        self.assertEqual(self.fake.now["api"]["image"], self.fake.initial["api"]["image"])
        self.assertEqual(self.fake.now["web"]["image"], self.fake.initial["web"]["image"])
        self.assertEqual([s for s, _, _ in self.fake.commands], ["api", "web", "api"])
        self.assertEqual(json.loads(next(self.state.glob("dual-*.json")).read_text())["status"], "ROLLED_BACK")
        self.assertEqual(self.fake.audit_events, ["DUAL_API_WEB_ROLLED_BACK"])

    def test_health_fail_reverts_both(self):
        self.fake.health_fail_first = True
        with self.assertRaises(tx.Blocked):
            self.do()
        self.assertEqual(self.fake.now["api"]["image"], self.fake.initial["api"]["image"])
        self.assertEqual(self.fake.now["web"]["image"], self.fake.initial["web"]["image"])
        self.assertEqual(self.fake.audit_events, ["DUAL_API_WEB_ROLLED_BACK"])

    def test_rollback_failure_is_explicit_and_preserves_receipt(self):
        self.fake.fail_web = True
        self.fake.fail_rollback = True
        with self.assertRaisesRegex(tx.Blocked, "DUAL_ROLLBACK_FAILED_MANUAL_RECOVERY_REQUIRED"):
            self.do()
        self.assertEqual(json.loads(next(self.state.glob("dual-*.json")).read_text())["status"],
                         "RECOVERY_MANUALLY_REQUIRED")
        self.assertEqual(self.fake.audit_events, ["DUAL_API_WEB_RECOVERY_REQUIRED"])

    def test_unreviewed_evidence_fails_before_mutation(self):
        with self.assertRaisesRegex(tx.Blocked, "SIGNED_G6_SENSITIVE"):
            self.do(evidence=lambda *_: False)
        self.assertEqual(self.fake.commands, [])
        self.assertFalse(list(self.state.iterdir()))

    def test_incomplete_service_baseline_fails(self):
        previous = copy.deepcopy(self.fake.initial)
        previous.pop("keycloak")
        with self.assertRaisesRegex(tx.Blocked, "BASELINE_SERVICE_SET_INVALID"):
            tx.baseline_contract(previous, NEW, TARGET, LAYERS, str(WORK))

    def test_one_shot_must_be_exited_zero(self):
        previous = baseline()
        previous["migrator"]["exit_code"] = 1
        with self.assertRaisesRegex(tx.Blocked, "BASELINE_JOB_INVALID"):
            tx.baseline_contract(previous, NEW, TARGET, LAYERS, str(WORK))

    def test_identity_layer_required(self):
        api = list(LAYERS["api"])
        api[2] = "/tmp/fake-identity.json"
        with self.assertRaisesRegex(tx.Blocked, "COMPOSE_LAYER_BINDING_INVALID"):
            tx.normalized_layers({"api": api, "web": list(LAYERS["web"])}, WORK)

    def test_unknown_extra_compose_layer_blocks(self):
        with self.assertRaisesRegex(tx.Blocked, "COMPOSE_LAYERS_UNRECOGNIZED"):
            tx.normalized_layers({"api": list(LAYERS["api"]) + ["/tmp/new"],
                                  "web": list(LAYERS["web"])}, WORK)

    def test_existing_journal_prevents_replay(self):
        self.do()
        with self.assertRaisesRegex(tx.Blocked, "TRANSACTION_ALREADY_EXISTS_OR_INCOMPLETE"):
            self.do()

    def test_changed_baseline_prevents_activation(self):
        self.fake.now["postgres"]["id"] = "f" * 64
        with self.assertRaisesRegex(tx.Blocked, "BASELINE_CHANGED_BEFORE_APPLY"):
            self.do()
        self.assertFalse(list(self.state.iterdir()))


if __name__ == "__main__":
    unittest.main()
