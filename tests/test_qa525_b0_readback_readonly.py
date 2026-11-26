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
CAND_SHA = "cd" * 20
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
        args = dict(shim=str(self.shim), state=str(self.state), store=str(self.store), source_sha=SRC_SHA, candidate_sha=CAND_SHA,
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
        for kw in ({"source_sha": "zz"}, {"candidate_sha": None}, {"candidate_sha": SRC_SHA}, {"expect_shim": None}, {"expect_original": "1"}, {"expect_modules": {}},
                   {"expect_modules": {b0.MODULES[0]: self.mods[b0.MODULES[0]]}}):
            with self.assertRaises(ValueError):
                self.run_b0(**kw)

    def test_cli_requires_every_pin(self):
        with self.assertRaises(SystemExit):
            b0.main(["--allow-non-root"])
        with self.assertRaises(SystemExit):
            b0.main(["--allow-non-root", "--source-sha", SRC_SHA, "--candidate-sha", CAND_SHA])

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
        orig = b0.read_at
        calls = []

        def racing(dfd, name, want, limit=b0.MAX_FILE):
            if not calls and name.startswith("receipt-"):
                calls.append(1)
                self.put("intruder-late.txt", b"x")
            return orig(dfd, name, want, limit)

        b0.read_at = racing
        try:
            self.stops("STATE_CHANGED_DURING_READ")
        finally:
            b0.read_at = orig

    def hook_read_at(self, prefix, action):
        orig, fired = b0.read_at, []

        def hooked(dfd, name, want, limit=b0.MAX_FILE):
            if not fired and name.startswith(prefix):
                fired.append(1)
                action(name)
            return orig(dfd, name, want, limit)

        b0.read_at = hooked
        self.addCleanup(setattr, b0, "read_at", orig)

    def test_substituted_receipt_same_bytes_new_inode_stops_before_read(self):
        def swap(name):
            data = (self.state / name).read_bytes()
            os.unlink(self.state / name)
            self.put(name, data)
        self.hook_read_at("receipt-", swap)
        self.stops("IDENTITY_CHANGED:receipt-")

    def test_substituted_shim_identity_stops(self):
        def swap(name):
            self.shim.write_bytes(LAUNCHERS[3])  # same bytes, new mtime/ctime
            os.chmod(self.shim, 0o700)
        orig, fired = b0.read_at, []

        def hooked(dfd, n, want, limit=b0.MAX_FILE):
            if not fired and n == "shim":
                fired.append(1)
                swap(n)
            return orig(dfd, n, want, limit)
        b0.read_at = hooked
        self.addCleanup(setattr, b0, "read_at", orig)
        self.stops("SHIM_UNREADABLE")

    def test_module_changed_before_close_stops(self):
        target = self.store / SRC_SHA / "scripts/qa" / b0.MODULES[1]
        orig, seen = b0.read_at, []

        def hooked(dfd, n, want, limit=b0.MAX_FILE):
            res = orig(dfd, n, want, limit)
            if n == b0.MODULES[1]:
                seen.append(1)
                if len(seen) == 1:  # after first (open) read, mutate before the closing revalidation
                    target.write_bytes(b"tampered-after-read")
            return res
        b0.read_at = hooked
        self.addCleanup(setattr, b0, "read_at", orig)
        self.stops("MODULE:%s_CHANGED_DURING_READ" % b0.MODULES[1])

    def test_store_candidate_and_partial_stop(self):
        (self.store / CAND_SHA).mkdir()
        self.stops("STORE_CANDIDATE_PRESENT")
        (self.store / CAND_SHA).rmdir()
        (self.store / ("ef" * 20 + ".partial")).mkdir()
        self.stops("STORE_PARTIAL_PRESENT:")
        (self.store / (CAND_SHA + ".partial")).mkdir()
        rep = self.run_b0()
        self.assertEqual(len(rep["store"]["partials"]), 2)

    def test_store_missing_source_or_untrusted_dir_stops(self):
        os.chmod(self.store, 0o777)
        self.stops("STORE_ANCESTOR_UNTRUSTED")
        os.chmod(self.store, 0o755)
        (self.store / SRC_SHA / "scripts/qa" / b0.MODULES[0]).chmod(0o666)
        self.stops("MODULE:%s_METADATA_VIOLATION" % b0.MODULES[0])

    def test_untrusted_ancestor_stops(self):
        shared = pathlib.Path(self.tmp.name) / "shared"
        shared.mkdir()
        inner = shared / "state"
        os.rename(self.state, inner)
        os.chmod(shared, 0o777)  # group/other-writable, not sticky
        rep = self.run_b0(state=str(inner))
        self.assertIn("STATE_ANCESTOR_UNTRUSTED", rep["problems"])
        os.chmod(shared, 0o755)
        self.assertEqual(self.run_b0(state=str(inner))["status"], "B0_READBACK_PASS_EVIDENCE_ONLY")

    def test_foreign_owned_ancestor_stops(self):
        rep = self.run_b0(expect_uid=os.geteuid() + 12345)
        self.assertTrue(any(p.endswith("_ANCESTOR_UNTRUSTED") or "METADATA" in p for p in rep["problems"]))

    def test_state_and_store_revalidated_at_close(self):
        orig = b0.revalidate
        fired = []

        def hooked(rec, problems, label):
            if not fired:
                fired.append(1)
                self.put("late-arrival.txt", b"x")
                (self.store / "late-dir").mkdir()
            return orig(rec, problems, label)
        b0.revalidate = hooked
        self.addCleanup(setattr, b0, "revalidate", orig)
        rep = self.run_b0()
        self.assertIn("STATE_CHANGED_AT_CLOSE", rep["problems"])
        self.assertIn("STORE_CHANGED_AT_CLOSE", rep["problems"])

    def close_hook(self, action):
        orig, fired = b0.revalidate, []

        def hooked(rec, problems, label):
            if not fired:
                fired.append(1)
                action()
            return orig(rec, problems, label)
        b0.revalidate = hooked
        self.addCleanup(setattr, b0, "revalidate", orig)

    def test_replaced_state_dir_with_same_names_stops_at_close(self):
        def swap():
            clone = pathlib.Path(self.tmp.name) / "state-clone"
            clone.mkdir(mode=0o700)
            for n in os.listdir(self.state):
                (clone / n).write_bytes((self.state / n).read_bytes())
                os.chmod(clone / n, os.lstat(self.state / n).st_mode & 0o777)
            os.rename(self.state, pathlib.Path(self.tmp.name) / "state-old")
            os.rename(clone, self.state)
        self.close_hook(swap)
        self.assertIn("STATE_CHANGED_AT_CLOSE", self.run_b0()["problems"])

    def test_state_chmod_between_reads_stops(self):
        self.close_hook(lambda: os.chmod(self.state, 0o755))
        probs = self.run_b0()["problems"]
        self.assertIn("STATE_CHANGED_AT_CLOSE", probs)

    def test_store_dir_chmod_or_replacement_stops_at_close(self):
        self.close_hook(lambda: os.chmod(self.store, 0o775))
        probs = self.run_b0()["problems"]
        self.assertTrue("STORE_CHANGED_AT_CLOSE" in probs or "STORE_ANCESTOR_UNTRUSTED" in probs, probs)

    def test_removed_state_dir_stops_at_close(self):
        def gone():
            os.rename(self.state, pathlib.Path(self.tmp.name) / "state-moved")
        self.close_hook(gone)
        self.assertIn("STATE_CHANGED_AT_CLOSE", self.run_b0()["problems"])

    def test_stable_readback_is_positive(self):
        self.assertEqual(self.run_b0()["status"], "B0_READBACK_PASS_EVIDENCE_ONLY")

    def test_limits_are_reported(self):
        rep = self.run_b0()
        self.assertIn("NO_SHARED_LOCK_WITH_INSTALLER", rep["limits"])

    def test_state_dir_must_be_0700(self):
        os.chmod(self.state, 0o755)
        self.stops("STATE_DIR_METADATA_VIOLATION")

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
        hlink = pathlib.Path(self.tmp.name) / "linkshimdir"
        os.symlink(self.tmp.name, hlink)
        self.assertIn("SHIM_PATH_HAS_SYMLINK", self.run_b0(shim=str(hlink / "shim"))["problems"])

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
        self.assertIn("MODULE:%s_UNREADABLE" % b0.MODULES[0], self.run_b0()["problems"])

    def test_cli_out_is_exclusive_and_exit_codes(self):
        out = pathlib.Path(self.tmp.name) / "out.json"
        base = ["--allow-non-root", "--state", str(self.state), "--shim", str(self.shim), "--store", str(self.store),
                "--source-sha", SRC_SHA, "--candidate-sha", CAND_SHA, "--expect-shim-sha256", H[3], "--expect-original-sha256", H[0]]
        for m, h in self.mods.items():
            base += ["--expect-module", "%s=%s" % (m, h)]
        lk = self.mklocks()
        old, b0.LOCKS = b0.LOCKS, tuple(lk)  # production set is fixed in code; tests swap the module constant
        self.addCleanup(setattr, b0, "LOCKS", old)
        self.assertEqual(b0.main(base + ["--out", str(out)]), 0)
        self.assertEqual(os.stat(out).st_mode & 0o777, 0o600)
        with self.assertRaises(FileExistsError):
            b0.main(base + ["--out", str(out)])

    def mklocks(self, n=3):
        d = pathlib.Path(self.tmp.name) / "locks"
        d.mkdir(mode=0o700, exist_ok=True)
        paths = []
        for i in range(n):
            q = d / ("l%d.lock" % i)
            q.write_bytes(b"")
            os.chmod(q, 0o600)
            paths.append(str(q))
        return paths

    def test_default_locks_are_the_five_installer_locks(self):
        self.assertEqual(len(b0.LOCKS), 5)
        self.assertIn("/run/lock/pmgm-qa754-api-activation.lock", b0.LOCKS)

    def test_locks_held_during_and_released_after(self):
        import fcntl
        lk = self.mklocks()
        r = self.run_b0(locks=lk)
        self.assertEqual(r["status"], "B0_READBACK_PASS_EVIDENCE_ONLY")
        self.assertEqual([h["path"] for h in r["legacy_locks"]["held"]], lk)
        self.assertIn("EXCLUDES_ONLY_WRITERS_THAT_TAKE_THE_5_LEGACY_LOCKS", r["limits"])
        self.assertFalse(any(x.startswith("NO_SHARED_LOCK") for x in r["limits"]))
        for q in lk:  # released: another opener can take them
            fd = os.open(q, os.O_RDONLY)
            fcntl.flock(fd, fcntl.LOCK_EX | fcntl.LOCK_NB)
            os.close(fd)

    def test_lock_busy_is_stop(self):
        import fcntl
        lk = self.mklocks()
        fd = os.open(lk[1], os.O_RDONLY)
        fcntl.flock(fd, fcntl.LOCK_EX | fcntl.LOCK_NB)
        try:
            r = self.run_b0(locks=lk)
        finally:
            os.close(fd)
        self.assertEqual(r["status"], "B0_READBACK_STOP")
        self.assertIn("LOCK:%s:BUSY" % lk[1], r["problems"])
        self.assertEqual(r["legacy_locks"]["held"], [])

    def test_lock_missing_is_stop_and_never_created(self):
        lk = self.mklocks()
        os.unlink(lk[2])
        r = self.run_b0(locks=lk)
        self.assertIn("LOCK:%s:MISSING" % lk[2], r["problems"])
        self.assertFalse(os.path.exists(lk[2]))

    def test_lock_symlink_and_bad_mode_and_nlink_are_stop(self):
        lk = self.mklocks()
        os.unlink(lk[0])
        os.symlink(lk[1], lk[0])
        self.assertTrue(any(x.startswith("LOCK:%s:" % lk[0]) for x in self.run_b0(locks=lk)["problems"]))
        lk = self.mklocks()
        os.chmod(lk[1], 0o644)
        self.assertIn("LOCK:%s:UNTRUSTED" % lk[1], self.run_b0(locks=lk)["problems"])
        os.chmod(lk[1], 0o600)
        os.link(lk[1], lk[1] + ".hard")
        self.assertIn("LOCK:%s:UNTRUSTED" % lk[1], self.run_b0(locks=lk)["problems"])

    def test_lock_replaced_during_readback_is_stop(self):
        lk = self.mklocks()
        orig = b0._readback

        def swap(**kw):
            r = orig(**kw)
            os.unlink(lk[0])
            open(lk[0], "wb").close()
            os.chmod(lk[0], 0o600)
            return r
        b0._readback = swap
        try:
            r = self.run_b0(locks=lk)
        finally:
            b0._readback = orig
        self.assertIn("LOCK:%s:CHANGED_AT_CLOSE" % lk[0], r["problems"])
        self.assertEqual(r["status"], "B0_READBACK_STOP")

    def cli_base(self):
        base = ["--allow-non-root", "--state", str(self.state), "--shim", str(self.shim), "--store", str(self.store),
                "--source-sha", SRC_SHA, "--candidate-sha", CAND_SHA, "--expect-shim-sha256", H[3], "--expect-original-sha256", H[0]]
        for m, h in self.mods.items():
            base += ["--expect-module", "%s=%s" % (m, h)]
        return base

    def test_cli_lock_set_missing_is_exit_1_and_nothing_inspected(self):
        old, b0.LOCKS = b0.LOCKS, (str(pathlib.Path(self.tmp.name) / "absent.lock"),)
        self.addCleanup(setattr, b0, "LOCKS", old)
        out = pathlib.Path(self.tmp.name) / "o.json"
        self.assertEqual(b0.main(self.cli_base() + ["--out", str(out)]), 1)
        rep = json.loads(out.read_bytes())
        self.assertIs(rep["inspected"], False)
        self.assertNotIn("state", rep)
        self.assertEqual(rep["status"], "B0_READBACK_STOP")

    def test_cli_lock_override_forbidden_for_root_and_without_flag(self):
        base = self.cli_base() + ["--lock", self.mklocks()[0]]
        if os.geteuid() == 0:
            self.assertEqual(b0.main(base), 2)
        self.assertEqual(b0.main([x for x in base if x != "--allow-non-root"]), 2)

    def test_missing_lock_does_not_call_inspection(self):
        lk = self.mklocks()
        os.unlink(lk[0])
        called = []
        orig = b0._readback
        b0._readback = lambda **kw: called.append(1) or orig(**kw)
        try:
            r = self.run_b0(locks=lk)
        finally:
            b0._readback = orig
        self.assertEqual(called, [])
        self.assertEqual(r["status"], "B0_READBACK_STOP")

    def test_lock_under_symlinked_directory_is_stop(self):
        lk = self.mklocks()
        real = pathlib.Path(self.tmp.name) / "locks"
        link = pathlib.Path(self.tmp.name) / "locklink"
        os.symlink(real, link)
        r = self.run_b0(locks=[str(link / "l0.lock")])
        self.assertTrue(any("PATH_HAS_SYMLINK" in x for x in r["problems"]))
        self.assertIs(r["inspected"], False)

    def test_lock_mode_changed_at_close_is_stop(self):
        lk = self.mklocks()
        orig = b0._readback

        def chm(**kw):
            r = orig(**kw)
            os.chmod(lk[1], 0o666)
            return r
        b0._readback = chm
        try:
            r = self.run_b0(locks=lk)
        finally:
            b0._readback = orig
        self.assertIn("LOCK:%s:CHANGED_AT_CLOSE" % lk[1], r["problems"])

    def test_lock_directory_replaced_at_close_is_stop(self):
        lk = self.mklocks()
        d = pathlib.Path(lk[0]).parent
        orig = b0._readback

        def swapdir(**kw):
            r = orig(**kw)
            os.rename(d, str(d) + ".old")
            d.mkdir(mode=0o700)
            for q in lk:
                open(q, "wb").close()
                os.chmod(q, 0o600)
            return r
        b0._readback = swapdir
        try:
            r = self.run_b0(locks=lk)
        finally:
            b0._readback = orig
        self.assertIn("LOCK:%s:CHANGED_AT_CLOSE" % lk[0], r["problems"])


class ReadAtHardening(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.path = pathlib.Path(self.tmp.name) / "f"
        self.path.write_bytes(b"abc")
        os.chmod(self.path, 0o600)
        self.dfd = os.open(self.tmp.name, os.O_RDONLY | os.O_DIRECTORY)
        self.addCleanup(os.close, self.dfd)

    def want(self):
        return b0.identity(os.stat("f", dir_fd=self.dfd))

    def test_ok(self):
        self.assertEqual(b0.read_at(self.dfd, "f", self.want())[0], hashlib.sha256(b"abc").hexdigest())

    def test_group_writable_rejected_before_read(self):
        os.chmod(self.path, 0o660)
        orig = os.read
        os.read = lambda *a: self.fail("read before metadata check")
        try:
            with self.assertRaises(ValueError) as c:
                b0.read_at(self.dfd, "f", None)
        finally:
            os.read = orig
        self.assertEqual(str(c.exception), "BAD_METADATA")

    def test_hardlink_rejected(self):
        os.link(self.path, pathlib.Path(self.tmp.name) / "g")
        with self.assertRaises(ValueError) as c:
            b0.read_at(self.dfd, "f", None)
        self.assertEqual(str(c.exception), "BAD_METADATA")

    def test_mutation_during_read_rejected(self):
        orig = os.read

        def mutate(fd, n):
            data = orig(fd, n)
            with open(self.path, "r+b") as h:  # same size, new mtime/ctime
                h.write(b"xyz")
            os.utime(self.path, ns=(1, 2))
            return data
        os.read = mutate
        try:
            with self.assertRaises(ValueError) as c:
                b0.read_at(self.dfd, "f", None)
        finally:
            os.read = orig
        self.assertEqual(str(c.exception), "CHANGED_DURING_READ")


if __name__ == "__main__":
    unittest.main()
