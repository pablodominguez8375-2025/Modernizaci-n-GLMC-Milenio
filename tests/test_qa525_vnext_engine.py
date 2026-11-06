"""Fully assembled vNext engine over a fake world and private lock fixture."""
import copy
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

import test_qa525_vnext_baseline_plan as bp
import test_qa525_vnext_journal as jt
from test_qa525_vnext_integration_contract import observation

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / "scripts/qa"
PATH = BASE / "qa525-vnext-engine.py"
spec = importlib.util.spec_from_file_location("qa525_engine_test", PATH)
engine = importlib.util.module_from_spec(spec)
spec.loader.exec_module(engine)


class Reader:
    def __init__(self, baseline, target, world):
        self.baseline, self.target, self.world = baseline, target, world

    def capture_baseline(self):
        return copy.deepcopy(self.baseline)

    def observe(self):
        obs = observation(self.baseline, self.target, use_target=True)
        for service in ("api", "web"):
            source = self.target if self.world[service] == "target" else self.baseline
            obs["images"][service] = copy.deepcopy(source["images"][service])
            obs["layers"][service] = copy.deepcopy(source["layers"][service])
        obs["health"]["web_sha"] = (
            self.target if self.world["web"] == "target" else self.baseline)["web_sha"]
        return obs


class Scope:
    def __init__(self, unit):
        self.unit = unit

    def state(self):
        return "ABSENT"


class EngineTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.fx = jt.JournalFixture(self.tmp.name)
        self.lockdir = Path(self.tmp.name) / "locks"
        self.lockdir.mkdir(mode=0o700)
        self.lockdir.chmod(0o700)
        for name in engine.locks.SYNTHETIC_NAMES:
            path = self.lockdir / name
            path.touch(mode=0o600)
            path.chmod(0o600)
        self.base, self.target = bp.baseline(), bp.target()
        self.world = {"api": "baseline", "web": "baseline"}
        self.reader = Reader(self.base, self.target, self.world)
        self.runs = []
        self.fail = set()

    def tearDown(self):
        self.tmp.cleanup()

    def runner(self, argv):
        service = argv[-1]
        files = [argv[i + 1] for i, value in enumerate(argv) if value == "--file"]
        side = "target" if files == self.target["layers"][service] else "baseline"
        self.runs.append((service, side))
        if (service, side) in self.fail:
            return 1
        self.world[service] = side
        return 0

    def fly(self, gate=lambda _stage: True):
        return engine.run_synthetic(
            root=self.fx.root, admission=self.fx.admission,
            admission_sha256=self.fx.digest, now=jt.NOW,
            synthetic_lock_dir=self.lockdir, reader=self.reader,
            target=self.target, project="centenario", workdir="/opt/centenario",
            env_file="/etc/pmgm/qa.env", runner=self.runner,
            scope_factory=Scope, settle=lambda: None, external_gate=gate)

    def terminal(self):
        path = self.fx.root / ("terminal-" + jt.NONCE + ".json")
        return json.loads(path.read_text())["status"] if path.exists() else None

    def test_happy_path_records_scopes_then_applies_under_all_locks(self):
        result = self.fly()
        self.assertEqual(result["status"], "APPLIED")
        self.assertEqual(self.runs, [("api", "target"), ("web", "target")])
        self.assertEqual(self.terminal(), "APPLIED")
        manifest = self.fx.root / ("scopes-" + jt.NONCE + ".json")
        self.assertTrue(manifest.exists())
        self.assertEqual(len(json.loads(manifest.read_text())["units"]), 2)

    def test_web_failure_uses_baseline_rollback_and_records_terminal(self):
        self.fail.add(("web", "target"))
        result = self.fly()
        self.assertEqual(result["status"], "ROLLED_BACK")
        self.assertEqual(self.runs, [("api", "target"), ("web", "target"),
                                     ("web", "baseline"), ("api", "baseline")])
        self.assertEqual(self.world, {"api": "baseline", "web": "baseline"})
        self.assertEqual(self.terminal(), "ROLLED_BACK")

    def test_denied_external_gate_cannot_reach_a_runner(self):
        closed = {"done": False}
        def gate(_stage):
            if not closed["done"]:
                # The engine checks the plane again after the external gate.
                # Locate and close one held lock through the active file table
                # is intentionally impossible from this callback; denial is
                # still proven by returning false before any runner call.
                closed["done"] = True
                return False
            return True
        with self.assertRaisesRegex(engine.flight.Stop, "^FLIGHT_GATE_DENIED$"):
            self.fly(gate)
        self.assertEqual(self.runs, [])
        self.assertIsNone(self.terminal())

    def test_no_physical_or_shell_entrypoint_exists(self):
        raw = PATH.read_text(encoding="utf-8")
        for token in ("import subprocess", "import socket", "urllib", "requests",
                      "Popen(", "os.system(", "LEGACY_LOCKS", "--review-and-run"):
            self.assertNotIn(token, raw)
        with self.assertRaises(SystemExit):
            exec(compile(raw, str(PATH), "exec"), {"__name__": "__main__",
                                                    "__file__": str(PATH)})


if __name__ == "__main__":
    unittest.main()
