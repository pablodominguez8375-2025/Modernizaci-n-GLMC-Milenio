"""Hermetic tests for the B0 read-only readback (temporary directories only)."""
import hashlib
import importlib.util
import json
import os
import pathlib
import tempfile
import unittest

SRC = pathlib.Path(__file__).resolve().parents[1] / "scripts/qa/qa525-b0-readback-readonly.py"
spec = importlib.util.spec_from_file_location("b0", SRC)
b0 = importlib.util.module_from_spec(spec)
spec.loader.exec_module(b0)

LAUNCHERS = [b"launcher-1\n", b"launcher-2\n", b"launcher-3\n", b"launcher-4\n"]
H = [hashlib.sha256(x).hexdigest() for x in LAUNCHERS]
N = [("%d" % i) * 64 for i in (1, 2, 3)]


def canon(r):
    return (json.dumps(r, sort_keys=True, separators=(",", ":")) + "\n").encode("ascii")


class B0Tests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        root = pathlib.Path(self.tmp.name)
        self.state, self.store = root / "state", root / "store"
        self.state.mkdir(mode=0o700)
        self.store.mkdir()
        self.shim = root / "shim"
        self.shim.write_bytes(LAUNCHERS[3])
        self.seed()

    def put(self, name, data, mode=0o600):
        p = self.state / name
        p.write_bytes(data)
        os.chmod(p, mode)

    def seed(self):
        legacy = {"status": "LAUNCHER_ATOMIC_SWAP_COMMITTED", "source_sha": "d4" * 20, "release_asset_sha256": "sha256:" + "0" * 64,
                  "old_launcher_sha256": H[0], "new_launcher_sha256": H[1], "candidate_operator_sha256": "e5" * 32, "nonce": N[0]}
        self.put("receipt-%s.json" % N[0], canon(legacy))
        self.put("old-%s.bin" % N[0], LAUNCHERS[0], 0o700)
        for i in (1, 2):
            rec = {"status": "LAUNCHER_ATOMIC_SWAP_COMMITTED", "nonce": N[i], "old_launcher_sha256": "sha256:" + H[i],
                   "new_launcher_sha256": "sha256:" + H[i + 1],
                   "evidence": {"app_archive_sha256": "x", "app_source_sha": "y", "candidate_operator_sha256": "z", "shim_sha256": "w"}}
            self.put("receipt-%s.json" % N[i], canon(rec))
            self.put("old-%s.bin" % N[i], LAUNCHERS[i], 0o700)
            self.put("intent-%s.superseded" % N[i], canon(dict(rec, status="SWAP_INTENT_NO_DEPLOYMENT")))
            self.put("inflight-swap-%s.done" % N[i], b"{}\n")

    def run_b0(self, **kw):
        return b0.readback(shim=str(self.shim), state=str(self.state), store=str(self.store), **kw)

    def snap(self):
        return {n: (self.state / n).read_bytes() for n in os.listdir(self.state)}

    def test_mixed_chain_passes_as_evidence_and_writes_nothing(self):
        before = self.snap()
        rep = self.run_b0(expect_shim=H[3], expect_original=H[0])
        self.assertEqual(rep["status"], "B0_READBACK_PASS_EVIDENCE_ONLY", rep["problems"])
        self.assertEqual(rep["chain"]["original"], H[0])
        self.assertEqual(rep["chain"]["live"], H[3])
        self.assertEqual(rep["chain"]["length"], 3)
        self.assertEqual(sorted(r["format"] for r in rep["state"]["receipts"]), ["legacy", "modern", "modern"])
        self.assertEqual(self.snap(), before)

    def test_pending_marker_stops(self):
        self.put("inflight.json", b"{}\n")
        rep = self.run_b0()
        self.assertEqual(rep["status"], "B0_READBACK_STOP")
        self.assertIn("PENDING:inflight.json", rep["problems"])

    def test_pending_intent_and_rollback_intent_stop(self):
        self.put("intent-%s.json" % ("9" * 64), b"{}\n")
        self.put("rollback-intent-%s.json" % ("8" * 64), b"{}\n")
        rep = self.run_b0()
        self.assertEqual(rep["status"], "B0_READBACK_STOP")
        self.assertEqual(len([p for p in rep["problems"] if p.startswith("PENDING:")]), 2)

    def test_unknown_entry_stops(self):
        self.put("surprise.txt", b"x")
        self.assertIn("UNKNOWN_ENTRY:surprise.txt", self.run_b0()["problems"])

    def test_fork_is_ambiguous(self):
        rec = {"status": "LAUNCHER_ATOMIC_SWAP_COMMITTED", "nonce": "7" * 64, "old_launcher_sha256": "sha256:" + H[1],
               "new_launcher_sha256": "sha256:" + hashlib.sha256(b"other").hexdigest(),
               "evidence": {"app_archive_sha256": "x", "app_source_sha": "y", "candidate_operator_sha256": "z", "shim_sha256": "w"}}
        self.put("receipt-%s.json" % ("7" * 64), canon(rec))
        self.put("old-%s.bin" % ("7" * 64), LAUNCHERS[1], 0o700)
        rep = self.run_b0()
        self.assertEqual(rep["status"], "B0_READBACK_STOP")
        self.assertTrue(any(p.startswith("RECEIPT_INVALID_OR_AMBIGUOUS") or p.startswith("CHAIN_") for p in rep["problems"]))

    def test_gap_in_chain_stops(self):
        for pre in ("receipt-", "old-"):
            os.unlink(self.state / (pre + N[1] + (".json" if pre == "receipt-" else ".bin")))
        rep = self.run_b0()
        self.assertEqual(rep["status"], "B0_READBACK_STOP")
        self.assertTrue(any(p.startswith("CHAIN_") for p in rep["problems"]))

    def test_backup_hash_mismatch_and_missing_stop(self):
        self.put("old-%s.bin" % N[1], b"tampered", 0o700)
        os.unlink(self.state / ("old-%s.bin" % N[2]))
        probs = self.run_b0()["problems"]
        self.assertIn("BACKUP_HASH_MISMATCH:" + N[1], probs)
        self.assertIn("BACKUP_MISSING:" + N[2], probs)

    def test_live_must_equal_shim_and_pins_must_match(self):
        self.shim.write_bytes(b"drifted\n")
        self.assertIn("LIVE_NEQ_SHIM", self.run_b0()["problems"])
        self.shim.write_bytes(LAUNCHERS[3])
        self.assertIn("SHIM_PIN_MISMATCH", self.run_b0(expect_shim="0" * 64)["problems"])
        self.assertIn("ORIGINAL_PIN_MISMATCH", self.run_b0(expect_original="0" * 64)["problems"])

    def test_module_hashes_and_pins(self):
        sha = "ab" * 20
        d = self.store / sha / "scripts/qa"
        d.mkdir(parents=True)
        for m in b0.MODULES:
            (d / m).write_bytes(m.encode())
        want = {b0.MODULES[1]: hashlib.sha256(b0.MODULES[1].encode()).hexdigest()}
        rep = self.run_b0(source_sha=sha, expect_modules=want)
        self.assertEqual(rep["status"], "B0_READBACK_PASS_EVIDENCE_ONLY", rep["problems"])
        bad = self.run_b0(source_sha=sha, expect_modules={b0.MODULES[1]: "0" * 64})
        self.assertIn("MODULE_PIN_MISMATCH:" + b0.MODULES[1], bad["problems"])

    def test_symlink_entry_stops(self):
        os.symlink("/etc/passwd", self.state / ("receipt-%s.json" % ("6" * 64)))
        self.assertTrue(any(p.startswith("SYMLINK:") for p in self.run_b0()["problems"]))

    def test_cli_requires_root_and_out_is_exclusive(self):
        if os.geteuid() != 0:
            self.assertEqual(b0.main([]), 2)
        out = pathlib.Path(self.tmp.name) / "out.json"
        out.write_text("x")
        with self.assertRaises(FileExistsError):
            b0.main(["--allow-non-root", "--out", str(out)])


if __name__ == "__main__":
    unittest.main()
