"""QA525 spend store: PASS/FAIL matrix for the four gaps listed on #1041 (off-host; nothing touches glmdch/main/prod).

G1 trusted root | G2 authenticated GO + binding BEFORE spend | G3 crash windows | G4 ambiguous marker = spent.
Not physical evidence: see LIMITS in scripts/qa/qa525_spend_store.py.
"""
import importlib.util
import json
import os
import shutil
import subprocess
import sys
import tempfile
import time
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
_spec = importlib.util.spec_from_file_location("qa525_spend_store", ROOT / "scripts/qa/qa525_spend_store.py")
S = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(S)

NONCE, REL, PLAN = "a" * 64, "b" * 64, "c" * 64
HAVE_SSH = shutil.which("ssh-keygen") is not None
WORKER = r'''
import importlib.util, sys
spec = importlib.util.spec_from_file_location("s", sys.argv[1]); s = importlib.util.module_from_spec(spec); spec.loader.exec_module(s)
fault = sys.argv[4] or None
with s.SpendStore(sys.argv[2]) as st:
    print(st.spend(sys.argv[3], "b"*64, "c"*64, _fault=fault))
'''


def run_worker(store_dir, nonce=NONCE, fault=""):
    return subprocess.run([sys.executable, "-I", "-c", WORKER, str(ROOT / "scripts/qa/qa525_spend_store.py"), str(store_dir), nonce, fault],
                          capture_output=True, text=True, timeout=30)


class SpendStoreBase(unittest.TestCase):
    def setUp(self):
        self.base = Path(tempfile.mkdtemp(prefix=".qa525-spend-", dir=os.environ.get("QA525_TEST_BASE", str(ROOT))))   # 0700, owned by us; never sticky /tmp (QA525_TEST_BASE=/root for the root run)
        self.addCleanup(shutil.rmtree, self.base, True)
        self.store_dir = self.base / "store"
        self.store_dir.mkdir(mode=0o700)


class SpendStoreG1TrustedRoot(SpendStoreBase):
    def test_ok(self):
        with S.SpendStore(str(self.store_dir)) as st:
            self.assertEqual(st.spend(NONCE, REL, PLAN), "SPENT")

    def test_relative_and_dotdot_rejected(self):
        for bad in ("relative/dir", str(self.store_dir) + "/../store", ""):
            with self.subTest(bad=bad), self.assertRaises(S.StoreError):
                S.open_trusted_dir(bad)

    def test_symlink_component_rejected(self):
        link = self.base / "link"
        link.symlink_to(self.store_dir)
        with self.assertRaises(S.StoreError):
            S.open_trusted_dir(str(link))

    def test_symlink_in_middle_rejected(self):
        (self.base / "real").mkdir(mode=0o700)
        (self.base / "real" / "store").mkdir(mode=0o700)
        (self.base / "mid").symlink_to(self.base / "real")
        with self.assertRaises(S.StoreError):
            S.open_trusted_dir(str(self.base / "mid" / "store"))

    def test_group_or_other_writable_parent_rejected(self):
        for mode in (0o770, 0o707, 0o777):
            parent = self.base / f"p{mode:o}"
            (parent / "store").mkdir(parents=True, mode=0o700)
            os.chmod(parent, mode)
            with self.subTest(mode=oct(mode)), self.assertRaises(S.StoreError):
                S.open_trusted_dir(str(parent / "store"))
            os.chmod(parent, 0o700)

    def test_final_dir_must_be_0700(self):
        for mode in (0o755, 0o750, 0o705):
            d = self.base / f"f{mode:o}"
            d.mkdir()
            os.chmod(d, mode)
            with self.subTest(mode=oct(mode)), self.assertRaises(S.StoreError):
                S.open_trusted_dir(str(d))

    @unittest.skipUnless(os.geteuid() == 0, "needs root to chown")
    def test_foreign_owner_rejected(self):
        d = self.base / "foreign"
        (d / "store").mkdir(parents=True, mode=0o700)
        os.chown(d, 54321, 54321)
        with self.assertRaises(S.StoreError):
            S.open_trusted_dir(str(d / "store"))
        os.chown(d / "store", 54321, 54321)
        with self.assertRaises(S.StoreError):
            S.open_trusted_dir(str(d / "store"))

    def test_missing_dir_rejected_not_created(self):
        with self.assertRaises(S.StoreError):
            S.open_trusted_dir(str(self.base / "nope"))
        self.assertFalse((self.base / "nope").exists())


class SpendStoreG3G4Spend(SpendStoreBase):
    def test_eight_processes_one_spend(self):
        procs = [subprocess.Popen([sys.executable, "-I", "-c", WORKER, str(ROOT / "scripts/qa/qa525_spend_store.py"), str(self.store_dir), NONCE, ""],
                                  stdout=subprocess.PIPE, text=True) for _ in range(8)]
        outs = [p.communicate(timeout=30)[0].strip() for p in procs]
        self.assertTrue(all(p.returncode == 0 for p in procs), outs)
        self.assertEqual((outs.count("SPENT"), outs.count("REPLAY")), (1, 7), outs)
        with S.SpendStore(str(self.store_dir)) as st:
            self.assertEqual(st.state(NONCE, REL, PLAN), "SPENT_COMPLETE")

    def test_replay_after_restart(self):
        self.assertEqual(run_worker(self.store_dir).stdout.strip(), "SPENT")
        self.assertEqual(run_worker(self.store_dir).stdout.strip(), "REPLAY")

    def test_crash_windows_never_execute_twice(self):
        expected = {"after_create": "SPENT_AMBIGUOUS", "partial_write": "SPENT_AMBIGUOUS", "after_write": "SPENT_COMPLETE",
                    "after_fsync": "SPENT_COMPLETE", "after_dirsync": "SPENT_COMPLETE"}
        for fault, state in expected.items():
            d = self.base / f"crash-{fault}"
            d.mkdir(mode=0o700)
            with self.subTest(fault=fault):
                crashed = run_worker(d, fault=fault)
                self.assertEqual(crashed.returncode, 75, crashed.stderr)
                again = run_worker(d)
                self.assertEqual(again.stdout.strip(), "REPLAY", "a nonce whose spend may have been interrupted was executed again")
                with S.SpendStore(str(d)) as st:
                    got = st.state(NONCE, REL, PLAN)
                    # 'after_write' flushes but may not be durable: both outcomes are acceptable, never UNSPENT
                    self.assertIn(got, (state, "SPENT_AMBIGUOUS"))
                    self.assertNotEqual(got, "UNSPENT")

    def test_garbage_markers_count_as_spent(self):
        with S.SpendStore(str(self.store_dir)) as st:
            marker = self.store_dir / ("spent-" + NONCE)
            for make in (lambda: marker.write_bytes(b""), lambda: marker.write_bytes(b"{not json"),
                         lambda: marker.write_bytes(json.dumps({"nonce": NONCE}).encode() + b"\n"),
                         lambda: marker.mkdir(), lambda: marker.symlink_to(self.base)):
                if marker.is_symlink() or marker.is_file():
                    marker.unlink()
                elif marker.is_dir():
                    marker.rmdir()
                make()
                self.assertEqual(st.spend(NONCE, REL, PLAN), "REPLAY")
                self.assertEqual(st.state(NONCE), "SPENT_AMBIGUOUS")

    def test_symlink_marker_victim_untouched(self):
        victim = self.base / "victim"
        victim.write_text("DO_NOT_TOUCH")
        (self.store_dir / ("spent-" + NONCE)).symlink_to(victim)
        with S.SpendStore(str(self.store_dir)) as st:
            self.assertEqual(st.spend(NONCE, REL, PLAN), "REPLAY")
        self.assertEqual(victim.read_text(), "DO_NOT_TOUCH")

    def test_symlink_lock_not_followed(self):
        victim = self.base / "victim"
        victim.write_text("DO_NOT_TOUCH")
        (self.store_dir / ".lock").symlink_to(victim)
        with S.SpendStore(str(self.store_dir)) as st, self.assertRaises(OSError):
            st.spend(NONCE, REL, PLAN)
        self.assertEqual(victim.read_text(), "DO_NOT_TOUCH")
        self.assertFalse((self.store_dir / ("spent-" + NONCE)).exists())

    def test_bad_identifiers(self):
        with S.SpendStore(str(self.store_dir)) as st:
            for bad in ("A" * 64, "a" * 63, "../" + "a" * 61, ""):
                with self.subTest(bad=bad), self.assertRaises(ValueError):
                    st.spend(bad, REL, PLAN)
        self.assertEqual(sorted(p.name for p in self.store_dir.iterdir()), [".lock"] if (self.store_dir / ".lock").exists() else [])

    def test_state_checks_binding(self):
        with S.SpendStore(str(self.store_dir)) as st:
            st.spend(NONCE, REL, PLAN)
            self.assertEqual(st.state(NONCE, REL, PLAN), "SPENT_COMPLETE")
            self.assertEqual(st.state(NONCE, "d" * 64, PLAN), "SPENT_AMBIGUOUS")
            self.assertEqual(st.state(NONCE, REL, "e" * 64), "SPENT_AMBIGUOUS")
            self.assertEqual(st.state("f" * 64), "UNSPENT")


def _fd_count():
    return len(os.listdir("/proc/self/fd"))


class SpendStoreSpecialFilesAndRaces(SpendStoreBase):
    def test_special_file_types_for_allowed_signers_rejected(self):
        d = self.base / "special"
        d.mkdir(mode=0o700)
        os.mkfifo(d / "fifo", 0o600)
        (d / "adir").mkdir(mode=0o700)
        import socket
        sock = socket.socket(socket.AF_UNIX)
        sock.bind(str(d / "sock"))
        self.addCleanup(sock.close)
        for name in ("fifo", "adir", "sock", "missing"):
            with self.subTest(name), self.assertRaises(S.StoreError):
                S.read_trusted_file(str(d / name))          # the FIFO case must NOT hang (O_NONBLOCK)
        with self.assertRaises(S.StoreError):
            S.read_trusted_file("/dev/null")                  # character device

    def test_fd_not_leaked_on_failures(self):
        d = self.base / "leak"
        d.mkdir(mode=0o700)
        os.mkfifo(d / "fifo", 0o600)
        (d / "wr").write_text("x")
        os.chmod(d / "wr", 0o666)
        (d / "ok").write_text("x")
        os.chmod(d / "ok", 0o600)
        (d / "link").symlink_to(d / "ok")
        before = _fd_count()
        for _ in range(50):
            for target in ("fifo", "wr", "link", "missing"):
                with self.assertRaises(S.StoreError):
                    S.read_trusted_file(str(d / target))
            for bad in (str(d / "ok"), str(d / "nope"), "relative"):
                with self.assertRaises(S.StoreError):
                    S.open_trusted_dir(bad)                  # a file / missing / relative path
        self.assertEqual(S.read_trusted_file(str(d / "ok")), b"x")
        self.assertEqual(_fd_count(), before, "file descriptors leaked on failure paths")

    def test_lock_that_is_not_a_regular_file_fails_closed_and_closes_fd(self):
        os.mkfifo(self.store_dir / ".lock", 0o600)
        before = _fd_count()
        with S.SpendStore(str(self.store_dir)) as st:
            for _ in range(20):
                with self.assertRaises(S.StoreError):
                    st.spend(NONCE, REL, PLAN)
        self.assertEqual(_fd_count(), before)
        self.assertFalse((self.store_dir / ("spent-" + NONCE)).exists())

    def test_concurrent_path_substitution_never_returns_the_victim(self):
        import threading
        d = self.base / "race"
        d.mkdir(mode=0o700)
        good, victim, path = d / "good", d / "victim", d / "signers"
        good.write_text("GOOD")
        victim.write_text("VICTIM")
        os.chmod(good, 0o600)
        os.chmod(victim, 0o600)
        os.symlink(good, path)
        stop = threading.Event()

        def swap():
            toggle = 0
            while not stop.is_set():
                tmp = d / "tmp-link"
                if tmp.is_symlink():
                    tmp.unlink()
                os.symlink(victim if toggle else good, tmp)
                os.replace(tmp, path)
                toggle ^= 1

        worker = threading.Thread(target=swap)
        worker.start()
        seen = set()
        try:
            for _ in range(400):
                try:
                    seen.add(S.read_trusted_file(str(path)))
                except S.StoreError:
                    seen.add(b"REJECTED")
        finally:
            stop.set()
            worker.join()
        # the final component is a symlink in every state => O_NOFOLLOW rejects it every time; the victim content is never returned
        self.assertEqual(seen, {b"REJECTED"})


@unittest.skipUnless(HAVE_SSH, "ssh-keygen not available")
class SpendStoreG2AuthenticatedGo(SpendStoreBase):
    def keygen(self, name):
        key = self.base / name
        subprocess.run(["ssh-keygen", "-q", "-t", "ed25519", "-N", "", "-C", name, "-f", str(key)], check=True)
        return key

    def sign(self, key, message, namespace=S.NAMESPACE):
        msg = self.base / "msg.bin"
        msg.write_bytes(message)
        subprocess.run(["ssh-keygen", "-Y", "sign", "-q", "-f", str(key), "-n", namespace, str(msg)], check=True)
        sig = Path(str(msg) + ".sig")
        data = sig.read_bytes()
        sig.unlink()
        return data

    def signers(self, key, principal="go-operator", mode=0o600):
        pub = Path(str(key) + ".pub").read_text().split()
        path = self.base / "allowed_signers"
        path.write_text(f"{principal} {pub[0]} {pub[1]}\n")
        os.chmod(path, mode)
        return str(path)

    def go(self, **over):
        body = {"v": 1, "nonce": NONCE, "release_sha": REL, "plan_sha256": PLAN, "purpose": S.PURPOSE, "not_after": int(time.time()) + 600}
        body.update(over)
        return S.canonical_go(body)

    def setUp(self):
        super().setUp()
        self.key = self.keygen("operator")
        self.signers_path = self.signers(self.key)

    def attempt(self, message=None, sig=None, release=REL, plan=PLAN, signers=None, principal="go-operator", now=None):
        message = self.go() if message is None else message
        sig = self.sign(self.key, message) if sig is None else sig
        with S.SpendStore(str(self.store_dir)) as st:
            return S.spend_authorized(st, message, sig, signers or self.signers_path, principal, release, plan, now=now)

    def assertRejected(self, **kw):
        with self.assertRaises((S.GoRejected, S.StoreError)):
            self.attempt(**kw)
        self.assertFalse((self.store_dir / ("spent-" + NONCE)).exists(), "a rejected GO must not spend the nonce")

    def test_good_go_spends_once(self):
        self.assertEqual(self.attempt(), "SPENT")
        self.assertEqual(self.attempt(), "REPLAY")

    def test_tampered_message_after_signing(self):
        message = self.go()
        sig = self.sign(self.key, message)
        self.assertRejected(message=message.replace(b'"v":1', b'"v":1 '), sig=sig)

    def test_signature_by_other_key(self):
        other = self.keygen("intruder")
        self.assertRejected(sig=self.sign(other, self.go()))

    def test_wrong_namespace(self):
        self.assertRejected(sig=self.sign(self.key, self.go(), namespace="file"))

    def test_wrong_principal(self):
        self.assertRejected(principal="someone-else")

    def test_release_mismatch(self):
        self.assertRejected(release="d" * 64)

    def test_plan_mismatch(self):
        self.assertRejected(plan="e" * 64)

    def test_signed_with_other_release_and_plan_not_reusable_for_this_one(self):
        self.assertRejected(message=self.go(release_sha="d" * 64))
        self.assertRejected(message=self.go(plan_sha256="e" * 64))

    def test_expired_and_too_long(self):
        self.assertRejected(message=self.go(not_after=int(time.time()) - 1))
        self.assertRejected(message=self.go(not_after=int(time.time()) + S.MAX_TTL_SECONDS + 3600))

    def test_wrong_purpose_version_extra_keys_and_types(self):
        self.assertRejected(message=self.go(purpose="other"))
        self.assertRejected(message=self.go(v=2))
        self.assertRejected(message=self.go(extra="x"))
        self.assertRejected(message=self.go(not_after=True))
        self.assertRejected(message=self.go(not_after="999999999999"))
        self.assertRejected(message=self.go(nonce="A" * 64))
        self.assertRejected(message=b"not json at all")

    def signed_raw(self, raw):
        """A VALIDLY SIGNED message that is not canonical must still be refused (signature covers bytes, canonical form is a separate rule)."""
        return self.attempt(message=raw, sig=self.sign(self.key, raw))

    def test_duplicate_keys_rejected_even_if_signed(self):
        good = self.go().decode()
        raw = good[:-1] + ',"v":1}'
        for dup in (raw, good[:-1] + ',"nonce":"' + "d" * 64 + '"}'):
            with self.subTest(dup=dup[-40:]), self.assertRaisesRegex(S.GoRejected, "duplicate key"):
                self.signed_raw(dup.encode())
        self.assertFalse((self.store_dir / ("spent-" + NONCE)).exists())

    def test_non_canonical_representations_rejected_even_if_signed(self):
        body = json.loads(self.go())
        variants = {
            "spaces": json.dumps(body, sort_keys=True).encode(),
            "unsorted": json.dumps(dict(reversed(list(body.items()))), separators=(",", ":")).encode(),
            "trailing_newline": self.go() + b"\n",
            "leading_space": b" " + self.go(),
            "unicode_escape": self.go().replace(b'"v"', b'"\\u0076"'),
            "bom": b"\xef\xbb\xbf" + self.go(),
            "nan": self.go().replace(b'"v":1', b'"v":NaN'),
            "invalid_utf8": self.go() + b"\xff",
            "float_epoch": self.go().replace(str(body["not_after"]).encode(), (str(body["not_after"]) + ".0").encode()),
        }
        for name, raw in variants.items():
            with self.subTest(name), self.assertRaises(S.GoRejected):
                self.signed_raw(raw)
        self.assertFalse((self.store_dir / ("spent-" + NONCE)).exists())

    def test_canonical_roundtrip_is_exact(self):
        body = json.loads(self.go())
        self.assertEqual(S.canonical_go(body), self.go())
        self.assertEqual(S.parse_go(self.go()), body)

    def test_allowed_signers_must_be_trusted(self):
        self.assertRejected(signers=self.signers(self.key, mode=0o666))
        self.assertRejected(signers=self.signers(self.key, mode=0o620))
        link = self.base / "signers-link"
        link.symlink_to(self.signers(self.key))
        self.assertRejected(signers=str(link))
        self.assertRejected(signers=str(self.base / "missing"))

    def test_no_marker_for_any_rejection_and_good_after_rejections(self):
        self.assertRejected(release="d" * 64)
        self.assertRejected(plan="e" * 64)
        self.assertEqual(self.attempt(), "SPENT")

    def test_ambiguous_marker_blocks_authenticated_replay(self):
        (self.store_dir / ("spent-" + NONCE)).write_bytes(b"")
        self.assertEqual(self.attempt(), "REPLAY")
        with S.SpendStore(str(self.store_dir)) as st:
            self.assertEqual(st.state(NONCE, REL, PLAN), "SPENT_AMBIGUOUS")

    def test_crash_after_authenticated_spend_then_replay(self):
        message = self.go()
        sig = self.sign(self.key, message)
        code = ("import importlib.util,sys\nspec=importlib.util.spec_from_file_location('s',sys.argv[1]);s=importlib.util.module_from_spec(spec);spec.loader.exec_module(s)\n"
                "m=open(sys.argv[3],'rb').read();g=open(sys.argv[4],'rb').read()\n"
                "with s.SpendStore(sys.argv[2]) as st: print(s.spend_authorized(st,m,g,sys.argv[5],'go-operator','b'*64,'c'*64,_fault=sys.argv[6] or None))")
        (self.base / "m.json").write_bytes(message)
        (self.base / "m.sig").write_bytes(sig)
        args = [sys.executable, "-I", "-c", code, str(ROOT / "scripts/qa/qa525_spend_store.py"), str(self.store_dir), str(self.base / "m.json"), str(self.base / "m.sig"), self.signers_path]
        crashed = subprocess.run(args + ["after_create"], capture_output=True, text=True, timeout=30)
        self.assertEqual(crashed.returncode, 75, crashed.stderr)
        again = subprocess.run(args + [""], capture_output=True, text=True, timeout=30)
        self.assertEqual(again.stdout.strip(), "REPLAY", again.stderr)


if __name__ == "__main__":
    unittest.main()
