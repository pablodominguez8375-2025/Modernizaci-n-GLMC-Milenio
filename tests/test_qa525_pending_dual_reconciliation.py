"""Pure pending reconciliation contract regressions; no host access."""
import importlib.util
from pathlib import Path
import copy
import json
import tempfile
import time
import types
from unittest.mock import patch
import unittest

ROOT = Path(__file__).resolve().parents[1]
P = ROOT / "scripts/qa/operators/qa525-pending-dual-reconciliation.py"
if not P.exists():
    P = ROOT / "qa525-pending-dual-reconciliation.py"
spec = importlib.util.spec_from_file_location("reconciliation_test", P)
m = importlib.util.module_from_spec(spec)
spec.loader.exec_module(m)


class Contract(unittest.TestCase):
    def setUp(self):
        sha = "a" * 40
        digest = "sha256:" + "b" * 64
        self.baseline = {n: {"image_id": "old-" + n} for n in ("api", "web")}
        self.p = dict(source_sha=sha, source_asset_sha256=digest,
            status="PREPARED_API_WEB_SIGNATURE_VERIFIED_NO_DEPLOYMENT",
            baseline=self.baseline, api_image_id="new-api", web_image_id="new-web")
        self.j = dict(source_sha=sha, status="PENDING",
            old_images={n: "old-" + n for n in ("api", "web")},
            new_images={n: "new-" + n for n in ("api", "web")})
        self.cut = dict(target_sha=sha, release_asset_sha256=digest)
        self.r = dict(schema="qa525-pending-reconciliation-v1", action=m.ACTION,
            source_sha=sha, release_asset_sha256=digest,
            operator_name="soporte@glmdch", operator_reviewed=True,
            approved_at=1000, expires_at=1800, prepared_sha256="p", journal_sha256="j",
            sponsor_approval_reference="https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/issues/525#issuecomment-1",
            technical_review_reference="https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/issues/525#issuecomment-2")

    def check(self, now=1100, consumed=True, hashes=None):
        return m.validate(self.r, self.p, self.j, self.cut, now=now,
            hashes=hashes or {"prepared.json": "p", "journal": "j"},
            baseline=self.baseline, consumed=consumed)

    def fixture(self, root):
        state, private = root / "state", root / "etc"
        state.mkdir(mode=0o700)
        private.mkdir(mode=0o700)
        sha = "a" * 40
        self.cut["nonce"] = "d" * 64
        paths = [state / "prepared.json", private / "review.json",
                 private / "post.json", private / "cut.json",
                 state / ("dual-" + sha + ".json")]
        record = dict(source_sha=sha, release_asset_sha256=self.r["release_asset_sha256"])
        for path, data in zip(paths, (self.p, record, record, self.cut, self.j)):
            m.write_private(path, data)
        (state / ("approval-consumed-" + "d" * 64)).write_text(sha + "\n")
        self.r.update(approved_at=int(time.time()), expires_at=int(time.time()) + 800,
            prepared_sha256=m.digest(paths[0]), journal_sha256=m.digest(paths[-1]))
        review_path = private / "reconcile.json"
        m.write_private(review_path, self.r)
        core = types.SimpleNamespace(TARGETS=("api", "web"), DEPENDENCIES=("db",),
                                     ONESHOTS=("migrator",))
        live = {}
        for name in ("api", "web", "db", "migrator"):
            row = [None] * 12
            row[2] = "exited" if name == "migrator" else "running"
            row[7], row[11] = "base,image", 0
            live[name] = row
        adapter = types.SimpleNamespace(metadata=lambda: live, docker=lambda *a: "e" * 40)
        build = types.SimpleNamespace(snapshot=lambda run: self.baseline)
        approval = types.SimpleNamespace(APPROVAL=paths[3])
        entry = types.SimpleNamespace(STATE=state, REVIEW=paths[1],
            SINGLE_OPERATOR_REVIEW=paths[2], WORKDIR=root,
            private_json=lambda p: json.loads(p.read_text()),
            trusted=lambda p: p, single_operator_runtime=lambda run: None,
            reviewed_layers=lambda *a: None, health=lambda sha: True)
        return entry, (core, build, approval, None, None, adapter, None, None), review_path, paths

    def test_archive_preserves_five_records_and_consumed_nonce(self):
        with tempfile.TemporaryDirectory() as d:
            entry, modules, approval, paths = self.fixture(Path(d))
            result = m.reconcile(entry, modules, approval)
            self.assertFalse(result["may_apply"])
            self.assertTrue(all(not p.exists() for p in paths))
            archives = list(entry.STATE.glob("pending-dual-archive-*"))
            self.assertEqual(len(archives), 1)
            self.assertTrue(all((archives[0] / p.name).is_file() for p in paths))
            self.assertTrue((archives[0] / "receipt.json").is_file())
            self.assertFalse((entry.STATE / m.MARKER).exists())
            self.assertEqual(len(list(entry.STATE.glob("approval-consumed-*"))), 1)

    def test_interrupted_move_keeps_durable_blocker_and_refuses_reentry(self):
        with tempfile.TemporaryDirectory() as d:
            entry, modules, approval, paths = self.fixture(Path(d))
            original = Path.rename
            count = [0]
            def interrupted(path, dest):
                count[0] += 1
                if count[0] == 2:
                    raise OSError("synthetic interruption")
                return original(path, dest)
            with patch.object(Path, "rename", interrupted), self.assertRaises(OSError):
                m.reconcile(entry, modules, approval)
            self.assertTrue((entry.STATE / m.MARKER).is_file())
            self.assertFalse(paths[0].exists())
            self.assertTrue(paths[1].exists())
            with self.assertRaises(m.Blocked):
                m.reconcile(entry, modules, approval)

    def test_duplicate_basename_rejected_before_archival(self):
        with tempfile.TemporaryDirectory() as d:
            entry, modules, approval, paths = self.fixture(Path(d))
            duplicate = entry.REVIEW.with_name("prepared.json")
            entry.REVIEW.rename(duplicate)
            entry.REVIEW = duplicate
            paths[1] = duplicate
            before = {str(p): p.read_bytes() for p in paths}
            with self.assertRaisesRegex(m.Blocked,
                                        "^RECONCILE_ARCHIVE_BASENAME_COLLISION$"):
                m.reconcile(entry, modules, approval)
            self.assertEqual({str(p): p.read_bytes() for p in paths}, before)
            self.assertFalse((entry.STATE / m.MARKER).exists())
            self.assertEqual(list(entry.STATE.glob("pending-dual-archive-*")), [])
            self.assertEqual(list(entry.STATE.glob("reconciliation-consumed-*")), [])

    def test_archival_approval_replay_rejected_without_moving(self):
        with tempfile.TemporaryDirectory() as d:
            entry, modules, approval, paths = self.fixture(Path(d))
            spent = entry.STATE / ("reconciliation-consumed-" + m.digest(approval))
            m.write_private(spent, {"status": "RECONCILIATION_APPROVAL_CONSUMED"})
            with self.assertRaises(m.Blocked):
                m.reconcile(entry, modules, approval)
            self.assertTrue(all(p.exists() for p in paths))

    def test_changed_hash_does_not_move_records(self):
        with tempfile.TemporaryDirectory() as d:
            entry, modules, approval, paths = self.fixture(Path(d))
            paths[0].write_text(paths[0].read_text() + " ")
            with self.assertRaises(m.Blocked):
                m.reconcile(entry, modules, approval)
            self.assertTrue(all(p.exists() for p in paths))
            self.assertFalse((entry.STATE / m.MARKER).exists())

    def test_valid_contract_only_returns_source(self):
        self.assertEqual(self.check(), "a" * 40)

    def test_expired_future_or_long_lived_approval_rejected(self):
        for now in (999, 1800, 2000):
            with self.subTest(now=now), self.assertRaises(m.Blocked):
                self.check(now=now)
        self.r["expires_at"] = 2000
        with self.assertRaises(m.Blocked):
            self.check()

    def test_non_pending_journal_rejected(self):
        for status in ("APPLIED", "ROLLED_BACK", "RECOVERY_MANUALLY_REQUIRED"):
            self.j["status"] = status
            with self.subTest(status=status), self.assertRaises(m.Blocked):
                self.check()

    def test_unconsumed_nonce_rejected(self):
        with self.assertRaises(m.Blocked):
            self.check(consumed=False)

    def test_changed_evidence_rejected(self):
        with self.assertRaises(m.Blocked):
            self.check(hashes={"prepared.json": "altered", "journal": "j"})

    def test_changed_runtime_rejected(self):
        self.p["baseline"] = copy.deepcopy(self.baseline)
        self.baseline["api"]["image_id"] = "different"
        with self.assertRaises(m.Blocked):
            self.check()

    def test_cut_or_image_binding_rejected(self):
        self.cut["target_sha"] = "c" * 40
        with self.assertRaises(m.Blocked):
            self.check()
        self.cut["target_sha"] = "a" * 40
        self.j["new_images"]["web"] = "wrong"
        with self.assertRaises(m.Blocked):
            self.check()

    def test_deploy_action_rejected(self):
        self.r["action"] = "APPLY"
        with self.assertRaises(m.Blocked):
            self.check()


if __name__ == "__main__":
    unittest.main()
