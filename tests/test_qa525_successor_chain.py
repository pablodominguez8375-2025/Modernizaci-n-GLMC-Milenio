"""Successor chain over the REAL dual transaction (hermetic Docker): A applied -> B applied/rolled back."""
import copy
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[1]


def load(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


base = load("t_chain_txtests", ROOT / "tests/test_qa525_supervised_dual_transaction.py")
succ = load("t_chain_succ", ROOT / "scripts/qa/operators/qa525-successor-baseline.py")
tx = base.tx
SHA1, SHA2 = "a" * 40, "b" * 40
NEW1 = {"api": "sha256:" + "1" * 64, "web": "sha256:" + "2" * 64}
NEW2 = {"api": "sha256:" + "3" * 64, "web": "sha256:" + "4" * 64}


class ChainTests(unittest.TestCase):
    def setUp(self):
        self.sandbox = tempfile.TemporaryDirectory()
        self.addCleanup(self.sandbox.cleanup)
        self.state = Path(self.sandbox.name)
        self.state.chmod(0o700)
        patcher = patch.object(tx, "root_path", side_effect=lambda p, directory=False: Path(p))
        patcher.start()
        self.addCleanup(patcher.stop)
        self.fake = base.Fake()
        self.fake.fail_image = None
        original = self.fake.run

        def run(args):
            files = [args[i + 1] for i, x in enumerate(args) if x == "--file"]
            image = json.loads(Path(files[-1]).read_text())["services"][args[-1]]["image"]
            if getattr(self.fake, "fail_images", None) and image in self.fake.fail_images:
                raise RuntimeError("simulated failure")
            if self.fake.fail_image and image == self.fake.fail_image:
                raise RuntimeError("simulated failure")
            return original(args)
        self.run = run

    def go(self, sha, new, before):
        return tx.transact(before=before, new=new, source_sha=sha, layers=base.LAYERS,
                           workdir=str(base.WORK), state_dir=self.state, run=self.run,
                           live_snapshot=self.fake.snapshot, evidence=lambda *a: True,
                           health=lambda: True, audit=lambda *a: None)

    def journals(self):
        return {p.name[5:-5]: json.loads(p.read_text()) for p in self.state.glob("dual-*.json")}

    def live(self):
        snap = self.fake.snapshot()
        return {n: [snap[n]["id"], snap[n]["image"], snap[n]["status"]] for n in ("api", "web")}

    def prepared(self, sha, images):
        return {"source_sha": sha, "status": "PREPARED_API_WEB_SIGNATURE_VERIFIED_NO_DEPLOYMENT",
                "api_image_id": images["api"], "web_image_id": images["web"]}

    def test_applied_chain_resolves_next_baseline_from_real_journals(self):
        self.go(SHA1, NEW1, self.fake.snapshot())
        self.go(SHA2, NEW2, self.fake.snapshot())
        journals = self.journals()
        self.assertEqual({"APPLIED"}, {j["status"] for j in journals.values()})
        self.assertEqual(SHA2, succ.resolve_baseline(self.prepared(SHA2, NEW2), journals, self.live(),
                                                     lambda s: s == SHA2))

    def test_mid_failure_rolls_back_both_keeps_previous_journal_and_stops_next_successor(self):
        self.go(SHA1, NEW1, self.fake.snapshot())
        first = (self.state / ("dual-" + SHA1 + ".json")).read_bytes()
        before = self.fake.snapshot()
        self.fake.fail_image = NEW2["web"]            # API recreated, WEB fails mid-way
        with self.assertRaises(RuntimeError):
            self.go(SHA2, NEW2, before)
        self.assertEqual(before["api"]["image"], self.fake.now["api"]["image"])
        self.assertEqual(before["web"]["image"], self.fake.now["web"]["image"])
        self.assertEqual(first, (self.state / ("dual-" + SHA1 + ".json")).read_bytes())
        journals = self.journals()
        self.assertEqual("ROLLED_BACK", journals[SHA2]["status"])
        self.assertEqual("APPLIED", journals[SHA1]["status"])
        # A failed attempt is never retried implicitly and needs a human before the next successor.
        with self.assertRaises(succ.Blocked) as ctx:
            succ.resolve_baseline(self.prepared(SHA1, NEW1), journals, self.live(), lambda s: True)
        self.assertEqual("BASELINE_JOURNAL_NOT_APPLIED", str(ctx.exception))
        with self.assertRaises(tx.Blocked):
            self.go(SHA2, NEW2, self.fake.snapshot())

    def test_failed_rollback_requires_manual_recovery_and_blocks_baseline(self):
        self.go(SHA1, NEW1, self.fake.snapshot())
        before = self.fake.snapshot()
        self.fake.fail_image = NEW2["web"]
        self.fake.fail_images = {before["api"]["image"], before["web"]["image"]}  # rollback targets
        with self.assertRaises(tx.Blocked):
            self.go(SHA2, NEW2, before)
        journals = self.journals()
        self.assertEqual("RECOVERY_MANUALLY_REQUIRED", journals[SHA2]["status"])
        with self.assertRaises(succ.Blocked):
            succ.resolve_baseline(self.prepared(SHA1, NEW1), journals, self.live(), lambda s: True)


if __name__ == "__main__":
    unittest.main()
