"""Hermetic tests for the B0 read-only readback (temporary directories only)."""
import hashlib
import importlib.util
import json
import os
import pathlib
import tempfile
import threading
import unittest

SRC = pathlib.Path(__file__).resolve().parents[1] / "scripts/qa/qa525-b0-readback-readonly.py"
spec = importlib.util.spec_from_file_location("b0", SRC)
b0 = importlib.util.module_from_spec(spec)
spec.loader.exec_module(b0)

LAUNCHERS = [b"launcher-1\n", b"launcher-2\n", b"launcher-3\n", b"launcher-4\n"]
H = [hashlib.sha256(x).hexdigest() for x in LAUNCHERS]
N = [("%d" % i) * 64 for i in (1, 2, 3)]
SRC_SHA = "ab" * 20
def ev(new):
    return {"app_archive_sha256": "sha256:" + "a" * 64, "app_source_sha": "c" * 40,
            "candidate_operator_sha256": "sha256:" + "e" * 64, "shim_sha256": "sha256:" + new}



def canon(r):
    return (json.dumps(r, sort_keys=True, separators=(",", ":")) + "\n").encode("ascii")


class B0Tests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        root = pathlib.Path(self.tmp.name)
        self.state, self.store = root / "state", root / "store"
        self.state.mkdir(mode=0o700)
        d = self.store / SRC_SHA / "scripts/qa"
        d.mkdir(parents=True)
        self.mods = {}
        for m in b0.MODULES:
            (d / m).write_bytes(m.encode())
            self.mods[m] = hashlib.sha256(m.encode()).hexdigest()
        self.shim = root / "shim"
        self.shim.write_bytes(LAUNCHERS[3])
        os.chmod(self.shim, 0o700)
        self.seed()

    def put(self, name, data, mode=0o600):
        p = self.state / name
        p.write_bytes(data)
        os.chmod(p, mode)

    def modern(self, i):
        return {"status": "LAUNCHER_ATOMIC_SWAP_COMMITTED", "nonce": N[i], "old_launcher_sha256": "sha256:" + H[i],
                "new_launcher_sha256": "sha256:" + H[i + 1], "evidence": ev(H[i + 1])}

    def seed(self):
        legacy = {"status": "LAUNCHER_ATOMIC_SWAP_COMMITTED", "source_sha": "d4" * 20,
                  "release_asset_sha256": "sha256:" + "0" * 64, "old_launcher_sha256": H[0],
                  "new_launcher_sha256": H[1], "candidate_operator_sha256": "e5" * 32, "nonce": N[0]}
        self.put("receipt-%s.json" % N[0], canon(legacy))
        self.put("old-%s.bin" % N[0], LAUNCHERS[0], 0o700)
        for i in (1, 2):
            rec = self.modern(i)
            self.put("receipt-%s.json" % N[i], canon(rec))
            self.put("old-%s.bin" % N[i], LAUNCHERS[i], 0o700)
            self.put("intent-%s.superseded" % N[i], canon(dict(rec, status="SWAP_INTENT_NO_DEPLOYMENT")))
            self.put("inflight-swap-%s.done" % N[i], b"{}\n")

    def run_b0(self, **kw):
        args = dict(shim=str(self.shim), state=str(self.state), store=str(self.store), source_sha=SRC_SHA,
                    expect_shim=H[3], expect_original=H[0], expect_modules=dict(self.mods), expect_uid=os.geteuid())
        args.update(kw)
        return b0.readback(**args)

    def snap(self):
        return {n: ((self.state / n).read_bytes(), os.lstat(self.state / n).st_mode) for n in os.listdir(self.state)}

    def stops(self, prefix):
        rep = self.run_b0()
        self.assertEqual(rep["status"], "B0_READBACK_STOP")
        self.assertTrue(any(p.startswith(prefix) for p in rep["problems"]), rep["problems"])

    def test_mixed_chain_passes_as_evidence_and_writes_nothing(self):
        before = self.snap()
        rep = self.run_b0()
        self.assertEqual(rep["status"], "B0_READBACK_PASS_EVIDENCE_ONLY", rep["problems"])
        self.assertEqual((rep["chain"]["original"], rep["chain"]["live"], rep["chain"]["length"]), (H[0], H[3], 3))
        self.assertEqual(sorted(r["format"] for r in rep["state"]["receipts"]), ["legacy", "modern", "modern"])
        self.assertEqual(self.snap(), before)

    def test_incomplete_pins_raise(self):
        for kw in ({"source_sha": "zz"}, {"expect_shim": None}, {"expect_original": "1"}, {"expect_modules": {}},
                   {"expect_modules": {b0.MODULES[0]: self.mods[b0.MODULES[0]]}}):
            with self.assertRaises(ValueError):
                self.run_b0(**kw)

    def test_cli_requires_every_pin(self):
        with self.assertRaises(SystemExit):
            b0.main(["--allow-non-root"])
        with self.assertRaises(SystemExit):
            b0.main(["--allow-non-root", "--source-sha", SRC_SHA])

    def test_pending_markers_stop(self):
        self.put("inflight.json", b"{}\n")
        self.put("intent-%s.json" % ("9" * 64), b"{}\n")
        self.put("rollback-intent-%s.json" % ("8" * 64), b"{}\n")
        rep = self.run_b0()
        self.assertEqual(len([p for p in rep["problems"] if p.startswith("PENDING:")]), 3)

    def test_unknown_and_invalid_type_extension_combinations_stop(self):
        for name in ("surprise.txt", "receipt-%s.bin" % N[1], "old-%s.json" % N[1], "inflight-swap-%s.json" % N[1],
                     "intent-%s.done" % N[1]):
            with self.subTest(name=name):
                self.put(name, b"x")
                self.assertIn("UNKNOWN_ENTRY:" + name, self.run_b0()["problems"])
                os.unlink(self.state / name)

    def test_rollback_record_stops(self):
        self.put("rollback-%s.json" % N[2], b"{}\n")
        self.stops("ROLLBACK_RECORD_PRESENT:")

    def test_fifo_entry_does_not_block_and_stops(self):
        os.mkfifo(self.state / ("receipt-%s.json" % ("6" * 64)), 0o600)
        done = []
        t = threading.Thread(target=lambda: done.append(self.run_b0()), daemon=True)
        t.start()
        t.join(5)
        self.assertFalse(t.is_alive(), "readback blocked on FIFO")
        self.assertTrue(any(p.startswith("NOT_REGULAR:") for p in done[0]["problems"]))

    def test_fifo_non_receipt_entry_stops(self):
        os.mkfifo(self.state / ("inflight-swap-%s.done" % ("6" * 64)), 0o600)
        self.stops("NOT_REGULAR:")

    def test_fifo_shim_does_not_block(self):
        os.unlink(self.shim)
        os.mkfifo(self.shim, 0o700)
        done = []
        t = threading.Thread(target=lambda: done.append(self.run_b0()), daemon=True)
        t.start()
        t.join(5)
        self.assertFalse(t.is_alive())
        self.assertIn("SHIM_UNREADABLE", done[0]["problems"])

    def test_internal_nonce_must_equal_file_nonce(self):
        rec = self.modern(1)
        rec["nonce"] = "5" * 64
        self.put("receipt-%s.json" % N[1], canon(rec))
        self.stops("RECEIPT_INVALID_OR_AMBIGUOUS:")

    def test_metadata_violations_stop(self):
        self.put("old-%s.bin" % N[1], LAUNCHERS[1], 0o600)
        self.stops("METADATA_VIOLATION:old-")
        self.put("old-%s.bin" % N[1], LAUNCHERS[1], 0o700)
        self.put("inflight-swap-%s.done" % N[1], b"{}\n", 0o666)
        self.stops("METADATA_VIOLATION:inflight-swap-")
        self.put("inflight-swap-%s.done" % N[1], b"{}\n", 0o600)
        os.link(self.state / ("receipt-%s.json" % N[1]), self.state / ("receipt-%s.json" % ("4" * 64)))
        self.stops("METADATA_VIOLATION:")

    def test_wrong_owner_stops(self):
        rep = self.run_b0(expect_uid=os.geteuid() + 1)
        self.assertTrue(any(p.startswith("METADATA_VIOLATION:") for p in rep["problems"]))

    def test_shim_must_be_0700(self):
        os.chmod(self.shim, 0o755)
        self.assertIn("SHIM_METADATA_VIOLATION", self.run_b0()["problems"])

    def test_duplicate_json_keys_rejected(self):
        raw = canon(self.modern(1)).decode().replace('{"evidence"', '{"nonce":"%s","evidence"' % N[1], 1)
        self.assertIn('"nonce"', raw)
        self.put("receipt-%s.json" % N[1], raw.encode())
        self.stops("RECEIPT_INVALID_OR_AMBIGUOUS:")

    def test_snapshot_change_during_read_stops(self):
        orig = b0.read_regular
        calls = []

        def racing(path, limit=b0.MAX_FILE):
            if not calls and os.path.basename(path).startswith("receipt-"):
                calls.append(1)
                self.put("intruder-late.txt", b"x")
            return orig(path, limit)

        b0.read_regular = racing
        try:
            self.stops("STATE_CHANGED_DURING_READ")
        finally:
            b0.read_regular = orig

    def test_generation_schema_negatives(self):
        legacy = {"status": "LAUNCHER_ATOMIC_SWAP_COMMITTED", "source_sha": "d4" * 20,
                  "release_asset_sha256": "sha256:" + "0" * 64, "old_launcher_sha256": H[0],
                  "new_launcher_sha256": H[1], "candidate_operator_sha256": "e5" * 32, "nonce": N[0]}
        m = self.modern(1)
        bare = dict(m, old_launcher_sha256=H[1], new_launcher_sha256=H[2])
        cases = {
            "modern_without_prefixes": bare,
            "modern_empty_evidence": dict(m, evidence={}),
            "modern_evidence_extra_key": dict(m, evidence=dict(m["evidence"], extra="x")),
            "modern_evidence_bad_source": dict(m, evidence=dict(m["evidence"], app_source_sha="y")),
            "modern_evidence_bad_archive": dict(m, evidence=dict(m["evidence"], app_archive_sha256="x")),
            "modern_shim_not_new": dict(m, evidence=dict(m["evidence"], shim_sha256="sha256:" + "9" * 64)),
            "legacy_with_prefixes": dict(legacy, old_launcher_sha256="sha256:" + H[0], new_launcher_sha256="sha256:" + H[1]),
            "legacy_bad_source": dict(legacy, source_sha="zz"),
            "legacy_bad_asset": dict(legacy, release_asset_sha256="0" * 64),
            "mixed_schema": dict(legacy, evidence=m["evidence"]),
        }
        for name, rec in cases.items():
            with self.subTest(name=name):
                self.assertEqual(b0.classify(canon(rec), rec["nonce"])[0], "bad")
        self.assertEqual(b0.classify(canon(legacy), N[0])[0], "legacy")
        self.assertEqual(b0.classify(canon(m), N[1])[0], "modern")

    def test_symlinked_ancestor_stops(self):
        link = pathlib.Path(self.tmp.name) / "linkstate"
        os.symlink(self.state, link)
        rep = self.run_b0(state=str(link))
        self.assertIn("STATE_PATH_HAS_SYMLINK", rep["problems"])
        slink = pathlib.Path(self.tmp.name) / "linkstore"
        os.symlink(self.store, slink)
        self.assertIn("STORE_PATH_HAS_SYMLINK", self.run_b0(store=str(slink))["problems"])

    def test_symlink_entry_stops(self):
        os.symlink("/etc/passwd", self.state / ("receipt-%s.json" % ("6" * 64)))
        self.stops("NOT_REGULAR:")

    def test_fork_gap_backup_and_pin_mismatches(self):
        rec = self.modern(1)
        o = hashlib.sha256(b"other").hexdigest()
        rec.update(nonce="7" * 64, new_launcher_sha256="sha256:" + o, evidence=ev(o))
        self.put("receipt-%s.json" % ("7" * 64), canon(rec))
        self.put("old-%s.bin" % ("7" * 64), LAUNCHERS[1], 0o700)
        self.stops("RECEIPT_INVALID_OR_AMBIGUOUS:")
        os.unlink(self.state / ("receipt-%s.json" % ("7" * 64)))
        os.unlink(self.state / ("old-%s.bin" % ("7" * 64)))
        self.put("old-%s.bin" % N[1], b"tampered", 0o700)
        os.unlink(self.state / ("old-%s.bin" % N[2]))
        probs = self.run_b0()["problems"]
        self.assertIn("BACKUP_HASH_MISMATCH:" + N[1], probs)
        self.assertIn("BACKUP_MISSING:" + N[2], probs)

    def test_live_must_equal_shim_and_pins_must_match(self):
        self.assertIn("SHIM_PIN_MISMATCH", self.run_b0(expect_shim="0" * 64)["problems"])
        self.assertIn("ORIGINAL_PIN_MISMATCH", self.run_b0(expect_original="0" * 64)["problems"])
        self.shim.write_bytes(b"drifted\n")
        self.assertIn("LIVE_NEQ_SHIM", self.run_b0()["problems"])

    def test_module_pin_mismatch_and_missing(self):
        bad = dict(self.mods, **{b0.MODULES[1]: "0" * 64})
        self.assertIn("MODULE_PIN_MISMATCH:" + b0.MODULES[1], self.run_b0(expect_modules=bad)["problems"])
        os.unlink(self.store / SRC_SHA / "scripts/qa" / b0.MODULES[0])
        self.assertIn("MODULE_UNREADABLE:" + b0.MODULES[0], self.run_b0()["problems"])

    def test_cli_out_is_exclusive_and_exit_codes(self):
        out = pathlib.Path(self.tmp.name) / "out.json"
        base = ["--allow-non-root", "--state", str(self.state), "--shim", str(self.shim), "--store", str(self.store),
                "--source-sha", SRC_SHA, "--expect-shim-sha256", H[3], "--expect-original-sha256", H[0]]
        for m, h in self.mods.items():
            base += ["--expect-module", "%s=%s" % (m, h)]
        self.assertEqual(b0.main(base + ["--out", str(out)]), 0)
        self.assertEqual(os.stat(out).st_mode & 0o777, 0o600)
        with self.assertRaises(FileExistsError):
            b0.main(base + ["--out", str(out)])


if __name__ == "__main__":
    unittest.main()
