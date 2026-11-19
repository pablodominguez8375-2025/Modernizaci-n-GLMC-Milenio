"""QA525 GO-nonce journal: hostile filesystem inputs, fd hygiene, crash and cross-process replay.

TESTS ONLY against the real dev module; complements test_qa525_vnext_go_nonce_journal.py
without repeating its cases. No production change.
"""
import importlib.util
import os
from pathlib import Path
import shutil
import signal
import subprocess
import sys
import tempfile
import textwrap
import unittest

ROOT = Path(__file__).resolve().parents[1]
SRC = ROOT / "scripts/qa/qa525-vnext-go-nonce-journal.py"
spec = importlib.util.spec_from_file_location("qa525_nonce_journal_hostile", SRC)
mod = importlib.util.module_from_spec(spec)
spec.loader.exec_module(mod)
BIND = {k: "sha256:" + c * 64 for k, c in zip(mod.FIELDS, "abcde")}
NONCE = "H" * 40
UID = os.getuid()


def _fds():
    return sorted(os.listdir("/proc/self/fd"))


class HostileJournalTests(unittest.TestCase):
    def setUp(self):
        self.base = tempfile.mkdtemp(prefix="njh")
        os.chmod(self.base, 0o700)
        self.root = os.path.join(os.path.realpath(self.base), "j")
        os.mkdir(self.root, 0o700)
        self.addCleanup(shutil.rmtree, self.base, True)
        self.spent = os.path.join(self.root, mod.hashlib.sha256(NONCE.encode()).hexdigest() + ".spent")

    def go(self, nonce=NONCE, root=None):
        return mod.consume(root or self.root, nonce, BIND, uid=UID, now=10, expires_at=100)

    def stop(self, code, fn):
        with self.assertRaises(mod.Stop) as err:
            fn()
        self.assertEqual(str(err.exception), code)

    def test_lease_symlink_directory_and_hardlink_are_refused(self):
        lease = os.path.join(self.root, ".lease")
        victim = os.path.join(self.base, "victim")
        open(victim, "w").close()
        os.symlink(victim, lease)
        with self.assertRaises(mod.Stop):
            self.go()
        os.unlink(lease)
        os.mkdir(lease)
        with self.assertRaises(mod.Stop):
            self.go()
        os.rmdir(lease)
        os.chmod(victim, 0o600)
        os.link(victim, lease)  # nlink == 2
        self.stop("NONCE_LEASE_UNSAFE", self.go)
        self.assertEqual([f for f in os.listdir(self.root) if f.endswith(".spent")], [])

    def test_special_files_at_spent_name_are_replay_and_never_block(self):
        os.mkfifo(self.spent, 0o600)
        self.stop("NONCE_REPLAY", self.go)
        os.unlink(self.spent)
        os.mkdir(self.spent)
        self.stop("NONCE_REPLAY", self.go)

    def test_no_fd_leak_on_any_refusal_path(self):
        before = _fds()
        self.go()
        for _ in range(3):
            self.stop("NONCE_REPLAY", self.go)
        self.stop("NONCE_INVALID", lambda: self.go(nonce="short"))
        os.chmod(self.root, 0o755)
        self.stop("NONCE_ROOT_UNTRUSTED", self.go)
        os.chmod(self.root, 0o700)
        open(os.path.join(self.root, ".lease"), "w").close()
        os.chmod(os.path.join(self.root, ".lease"), 0o666)
        self.stop("NONCE_LEASE_UNSAFE", lambda: self.go("J" * 40))
        self.assertEqual(_fds(), before)

    def test_nonce_grammar_rejects_newline_unicode_and_non_str(self):
        for bad in (NONCE + "\n", NONCE[:-1] + "٣", NONCE.encode(), None, 7, "a/b" + "N" * 40):
            self.stop("NONCE_INVALID", lambda b=bad: self.go(nonce=b))
        self.assertEqual(os.listdir(self.root), [])

    def test_group_writable_ancestor_is_untrusted(self):
        os.chmod(self.base, 0o770)
        self.stop("NONCE_ROOT_UNTRUSTED", self.go)

    def test_sigkill_during_write_leaves_nonce_spent(self):
        code = textwrap.dedent("""
            import importlib.util, os, signal, sys
            spec = importlib.util.spec_from_file_location('j', sys.argv[1])
            m = importlib.util.module_from_spec(spec); spec.loader.exec_module(m)
            def die(*a, **k):
                os.kill(os.getpid(), signal.SIGKILL)
            m._write_all = die
            b = {k: 'sha256:' + c * 64 for k, c in zip(m.FIELDS, 'abcde')}
            m.consume(sys.argv[2], sys.argv[3], b, uid=os.getuid(), now=10, expires_at=100)
        """)
        p = subprocess.run([sys.executable, "-I", "-c", code, str(SRC), self.root, NONCE])
        self.assertEqual(p.returncode, -signal.SIGKILL)
        self.assertTrue(os.path.exists(self.spent))  # crash after creation: spent, never reusable
        self.stop("NONCE_REPLAY", self.go)

    def test_replay_across_independent_interpreters(self):
        code = textwrap.dedent("""
            import importlib.util, os, sys
            spec = importlib.util.spec_from_file_location('j', sys.argv[1])
            m = importlib.util.module_from_spec(spec); spec.loader.exec_module(m)
            b = {k: 'sha256:' + c * 64 for k, c in zip(m.FIELDS, 'abcde')}
            try:
                m.consume(sys.argv[2], sys.argv[3], b, uid=os.getuid(), now=10, expires_at=100)
                print('OK')
            except m.Stop as e:
                print(e)
        """)
        outs = [subprocess.run([sys.executable, "-I", "-c", code, str(SRC), self.root, NONCE],
                               capture_output=True, text=True).stdout.strip() for _ in range(3)]
        self.assertEqual(outs, ["OK", "NONCE_REPLAY", "NONCE_REPLAY"])
        self.stop("NONCE_REPLAY", self.go)

    def test_ancestor_swapped_for_symlink_after_lstat_cannot_redirect_journal(self):
        """Race: ancestors pass lstat, then one is replaced by a symlink before os.open(root)."""
        import unittest.mock as um
        outer = os.path.join(os.path.realpath(self.base), "outer")
        os.mkdir(outer, 0o700)
        root = os.path.join(outer, "j")
        os.mkdir(root, 0o700)
        evil = os.path.join(os.path.realpath(self.base), "evil")
        os.makedirs(os.path.join(evil, "j"), mode=0o700)
        moved = os.path.join(os.path.realpath(self.base), "moved")
        real_lstat, state = os.lstat, {"done": False}

        def swap_after_check(path, *a, **k):
            res = real_lstat(path, *a, **k)
            if path == root and not state["done"]:  # last ancestor check passed: swap now
                state["done"] = True
                os.rename(outer, moved)
                os.symlink(evil, outer)
            return res

        with um.patch.object(mod.os, "lstat", swap_after_check):
            with self.assertRaises(mod.Stop):
                mod.consume(root, NONCE, BIND, uid=UID, now=10, expires_at=100)
        self.assertTrue(state["done"])
        self.assertEqual(os.listdir(os.path.join(evil, "j")), [])  # nothing written via the swap
        self.assertEqual([f for f in os.listdir(os.path.join(moved, "j"))], [])


if __name__ == "__main__":
    unittest.main()
