"""Hermetic tests for the QA525 vNext admission->journal bridge. No host or keys."""
import ast
import datetime as dt
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

import test_qa525_vnext_journal as jt
import test_qa525_vnext_trust_binding as trust

BASE = Path(__file__).resolve().parents[1] / "scripts" / "qa"


def load(name, filename):
    spec = importlib.util.spec_from_file_location(name, BASE / filename)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


bridge = load("qa525_vnext_admission_bridge_t", "qa525-vnext-admission-bridge.py")
locks = load("qa525_vnext_locks_for_bridge", "qa525-vnext-locks.py")


class _Journal:
    """Real journal module; records whether begin was reached."""
    def __init__(self):
        self.calls = 0

    def begin(self, *args):
        self.calls += 1
        return jt.mod.begin(*args)

    @staticmethod
    def lease_is_held(lease):
        return jt.mod.lease_is_held(lease)


class BridgeTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.fx = jt.JournalFixture(self.tmp.name)
        self.record = trust.admission_record()
        self.fx.put(self.record)
        self.admission_bytes = trust.canonical(self.record)
        self.approval_bytes = trust.canonical(trust.approval_record(self.record))
        self.journal = _Journal()
        self.bound = bridge.BoundJournal(
            self.journal, self.admission_bytes, self.approval_bytes,
            trusted_policy_hash=trust.POLICY)

    def tearDown(self):
        self.tmp.cleanup()

    def begin(self, bound=None, now=trust.NOW, digest=None):
        return (bound or self.bound).begin(
            self.fx.root, self.fx.admission, digest or self.fx.digest, now)

    def nonce_files(self):
        return sorted(p.name for p in self.fx.root.iterdir()
                      if p.name != ".qa525-transaction.lock")

    def test_binding_then_journal_begin_yields_a_held_lease(self):
        self.assertEqual(self.fx.digest, "sha256:" + __import__("hashlib").sha256(
            self.admission_bytes).hexdigest())
        with self.begin() as lease:
            self.assertTrue(self.bound.lease_is_held(lease))
            self.assertEqual(self.bound.last_binding["admission_sha256"], self.fx.digest)
            self.assertIs(self.bound.last_binding["signature_verified_here"], False)
        self.assertEqual(jt.mod.inspect(self.fx.root, self.record["nonce"]), "APPLYING")

    def test_wrong_policy_or_anchor_is_denied_before_any_nonce_is_consumed(self):
        bad_policy = bridge.BoundJournal(
            self.journal, self.admission_bytes, self.approval_bytes,
            trusted_policy_hash="sha256:" + "9" * 64)
        with self.assertRaisesRegex(bridge.Stop, "^BRIDGE_BINDING_REJECTED$"):
            self.begin(bad_policy)
        self.assertEqual(bad_policy.last_cause, "BINDING_POLICY_MISMATCH")
        anchor = trust.approval_record(self.record)
        anchor["sponsor_approval_sha256"] = "not-a-digest"
        bad_anchor = bridge.BoundJournal(
            self.journal, self.admission_bytes, trust.canonical(anchor),
            trusted_policy_hash=trust.POLICY)
        with self.assertRaisesRegex(bridge.Stop, "^BRIDGE_BINDING_REJECTED$"):
            self.begin(bad_anchor)
        self.assertEqual(self.journal.calls, 0)
        self.assertEqual(self.nonce_files(), [])

    def test_window_is_rechecked_with_the_time_of_begin(self):
        late = trust.NOW + dt.timedelta(minutes=40)
        with self.assertRaisesRegex(bridge.Stop, "^BRIDGE_BINDING_REJECTED$"):
            self.begin(now=late)
        self.assertEqual(self.bound.last_cause, "BINDING_WINDOW_INVALID")
        self.assertEqual(self.journal.calls, 0)

    def test_file_swapped_after_binding_is_refused_by_the_journal_hash(self):
        swapped = dict(self.record, artifact_id=57)
        self.fx.admission.write_text(json.dumps(swapped, sort_keys=True) + "\n")
        self.fx.admission.chmod(0o600)
        with self.assertRaisesRegex(bridge.Stop, "^BRIDGE_JOURNAL_BEGIN_DENIED$"):
            self.begin()
        self.assertEqual(self.nonce_files(), [])

    def test_caller_cannot_substitute_another_digest(self):
        with self.assertRaisesRegex(bridge.Stop, "^BRIDGE_DIGEST_MISMATCH$"):
            self.begin(digest="sha256:" + "0" * 64)
        self.assertEqual(self.journal.calls, 0)

    def test_one_shot_even_after_a_failed_attempt(self):
        with self.assertRaises(bridge.Stop):
            self.begin(digest="sha256:" + "0" * 64)
        with self.assertRaisesRegex(bridge.Stop, "^BRIDGE_ALREADY_USED$"):
            self.begin()
        self.assertEqual(self.journal.calls, 0)

    def test_invalid_construction_is_rejected(self):
        with self.assertRaisesRegex(bridge.Stop, "^BRIDGE_JOURNAL_INVALID$"):
            bridge.BoundJournal(object(), b"{}", b"{}", trusted_policy_hash=trust.POLICY)
        with self.assertRaisesRegex(bridge.Stop, "^BRIDGE_BYTES_INVALID$"):
            bridge.BoundJournal(self.journal, "{}", b"{}", trusted_policy_hash=trust.POLICY)

    def test_errors_never_echo_external_text(self):
        secret = b'{"token":"ghp_SECRET","path":"/root/x"}'
        junk = bridge.BoundJournal(self.journal, secret, secret,
                                   trusted_policy_hash=trust.POLICY)
        with self.assertRaises(bridge.Stop) as ctx:
            self.begin(junk)
        self.assertNotIn("SECRET", str(ctx.exception) + repr(junk.last_cause))
        self.assertTrue(junk.last_cause is None or junk.last_cause.startswith("BINDING_"))

    def test_plugs_into_the_lock_plane_as_its_journal(self):
        fixture = Path(self.tmp.name)
        fixture.chmod(0o700)
        lock_dir = fixture / "locks"
        lock_dir.mkdir(mode=0o700)
        lock_dir.chmod(0o700)
        for name in locks.SYNTHETIC_NAMES:
            item = lock_dir / name
            with item.open("xb"):
                pass
            item.chmod(0o600)
        with locks.synthetic_flight(self.bound, self.fx.root, self.fx.admission,
                                    self.fx.digest, trust.NOW,
                                    synthetic_lock_dir=lock_dir) as plane:
            self.assertEqual(plane.assert_held()["legacy_locks_held"], 5)
        self.assertIsNotNone(self.bound.last_binding)

    def test_library_has_no_host_effects(self):
        source = (BASE / "qa525-vnext-admission-bridge.py").read_text()
        imported = set()
        for node in ast.walk(ast.parse(source)):
            if isinstance(node, ast.Import):
                imported |= {a.name.split(".")[0] for a in node.names}
            elif isinstance(node, ast.ImportFrom):
                imported.add((node.module or "").split(".")[0])
        self.assertEqual(imported, {"importlib", "pathlib"})
        with self.assertRaises(SystemExit):
            exec(compile(source, "b", "exec"),
                 {"__name__": "__main__", "__file__": str(BASE / "x.py")})


if __name__ == "__main__":
    unittest.main()
