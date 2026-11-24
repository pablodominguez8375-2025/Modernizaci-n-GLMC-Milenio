"""Hermetic proof: the vnext LAUNCHER_ONLY child admits a mixed legacy+modern custody state.

Context (#1089): the legacy supervised installer's ``scan_history`` rejects modern receipts and the
``inflight-*.done`` markers.  The signed-GO path (``qa525-vnext-root-launcher.py`` -> child ``install``) never
calls it: it checks the live launcher against the signed pin, refuses any pending marker/intent, and writes
only its own nonce.  These tests pin that behaviour so the claim is reproducible.  Temporary paths only.
"""
import json
import os
import pathlib
import stat
import unittest

import test_qa525_vnext_root_launcher_child as base

child, sha, hx = base.child, base.sha, base.hx
OLD, NONCE, SRC40 = base.OLD, base.NONCE, base.SRC40
N_LEGACY, N_MODERN = "b2" * 32, "c3" * 32


def canon(record):
    return (json.dumps(record, sort_keys=True, separators=(",", ":")) + "\n").encode("ascii")


class MixedHistoryTests(unittest.TestCase):
    setUp = base.InstallTests.setUp
    memfd = base.InstallTests.memfd
    claims = base.InstallTests.claims
    pins = base.InstallTests.pins
    run_install = base.InstallTests.run_install
    untouched = base.InstallTests.untouched

    def put(self, name, data, mode):
        p = pathlib.Path(self.state, name)
        p.write_bytes(data)
        os.chmod(p, mode)
        return p

    def seed_history(self):
        """Legacy 7-key receipt + modern receipt with evidence, archived intents and .done markers."""
        legacy_old, modern_old = b"legacy-launcher-bytes\n", b"modern-launcher-bytes\n"
        legacy = {"status": "LAUNCHER_ATOMIC_SWAP_COMMITTED", "source_sha": "d4" * 20,
                  "release_asset_sha256": sha(b"legacy-asset"), "old_launcher_sha256": hx(legacy_old),
                  "new_launcher_sha256": hx(modern_old), "candidate_operator_sha256": "e5" * 32, "nonce": N_LEGACY}
        modern = {"status": "LAUNCHER_ATOMIC_SWAP_COMMITTED", "nonce": N_MODERN,
                  "old_launcher_sha256": sha(modern_old), "new_launcher_sha256": sha(OLD),
                  "evidence": {"app_archive_sha256": sha(b"a"), "app_source_sha": "f6" * 20,
                               "candidate_operator_sha256": sha(b"o"), "shim_sha256": sha(OLD)}}
        files = {
            "receipt-%s.json" % N_LEGACY: (canon(legacy), 0o600),
            "old-%s.bin" % N_LEGACY: (legacy_old, 0o700),
            "intent-%s.superseded" % N_LEGACY: (canon(dict(legacy, status="SWAP_INTENT_NO_DEPLOYMENT")), 0o600),
            "receipt-%s.json" % N_MODERN: (canon(modern), 0o600),
            "old-%s.bin" % N_MODERN: (modern_old, 0o700),
            "intent-%s.superseded" % N_MODERN: (canon(dict(modern, status="SWAP_INTENT_NO_DEPLOYMENT")), 0o600),
            "inflight-swap-%s.done" % N_MODERN: (canon({"op": "swap", "nonce": N_MODERN, "status": "IN_FLIGHT"}), 0o600),
        }
        for name, (data, mode) in files.items():
            self.put(name, data, mode)
        return {name: data for name, (data, _) in files.items()}

    def snapshot(self):
        return {n: pathlib.Path(self.state, n).read_bytes() for n in os.listdir(self.state)}

    def modes(self):
        return {n: stat.S_IMODE(os.lstat(os.path.join(self.state, n)).st_mode) for n in os.listdir(self.state)}

    def assert_rejected_without_changes(self, code, **kw):
        """Fail closed: exact error, store empty, state bytes+modes identical, launcher untouched."""
        before, modes = self.snapshot(), self.modes()
        with self.assertRaises(child.Stop) as cm:
            self.run_install(**kw)
        self.assertEqual(str(cm.exception), code)
        self.assertEqual(os.listdir(self.store), [])
        self.assertEqual(self.snapshot(), before)
        self.assertEqual(self.modes(), modes)
        self.untouched()

    def test_mixed_history_does_not_block_a_new_authenticated_swap_and_is_preserved(self):
        before = self.seed_history()
        receipt = self.run_install()
        self.assertEqual(receipt["status"], "LAUNCHER_ATOMIC_SWAP_COMMITTED")
        self.assertEqual(pathlib.Path(self.launcher).read_bytes(), child.render(SRC40, self.store))
        after = self.snapshot()
        for name, data in before.items():  # no custody record is modified, renamed or deleted
            self.assertEqual(after[name], data, name)
        new = set(after) - set(before)
        self.assertIn("receipt-%s.json" % NONCE, new)
        self.assertIn("old-%s.bin" % NONCE, new)
        self.assertIn("inflight-swap-%s.done" % NONCE, new)
        self.assertNotIn("inflight.json", after)

    def test_pending_marker_in_mixed_history_blocks_before_any_write(self):
        self.seed_history()
        self.put("inflight.json", canon({"op": "swap", "nonce": "d4" * 32, "status": "IN_FLIGHT"}), 0o600)
        self.assert_rejected_without_changes("CHILD_RECOVERY_REQUIRED")

    def test_unarchived_intent_in_mixed_history_blocks(self):
        self.seed_history()
        self.put("intent-%s.json" % ("d4" * 32), canon({"status": "SWAP_INTENT_NO_DEPLOYMENT"}), 0o600)
        self.assert_rejected_without_changes("CHILD_RECOVERY_REQUIRED")

    def test_pending_rollback_intent_in_mixed_history_blocks(self):
        self.seed_history()
        self.put("rollback-intent-%s.json" % ("d4" * 32), canon({"status": "ROLLBACK_INTENT"}), 0o600)
        self.assert_rejected_without_changes("CHILD_RECOVERY_REQUIRED")

    def test_live_launcher_drift_against_signed_pin_blocks_regardless_of_history(self):
        self.seed_history()
        self.assert_rejected_without_changes("CHILD_OLD_LAUNCHER_DRIFT",
                                             pins=self.pins(old_launcher_sha256=hx(b"not-the-live-launcher")))

    def test_replayed_nonce_blocks(self):
        self.seed_history()
        self.put("receipt-%s.json" % NONCE, b"{}\n", 0o600)
        self.assert_rejected_without_changes("CHILD_REPLAY_OR_PARTIAL_STATE")

    def test_successful_swap_preserves_the_modes_of_all_prior_records(self):
        self.seed_history()
        modes = self.modes()
        self.run_install()
        after = self.modes()
        for name, mode in modes.items():
            self.assertEqual(after[name], mode, name)


if __name__ == "__main__":
    unittest.main()
