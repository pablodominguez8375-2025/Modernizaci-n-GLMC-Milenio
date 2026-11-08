"""QA525 durable GO-nonce journal: real files, real concurrent processes, crash semantics."""
import importlib.util
import multiprocessing
import os
from pathlib import Path
import shutil
import sys
import tempfile
import unittest

SRC = Path(__file__).resolve().parents[1] / "scripts/qa/qa525-vnext-go-nonce-journal.py"
spec = importlib.util.spec_from_file_location("qa525_nonce_journal_test", SRC)
mod = importlib.util.module_from_spec(spec)
spec.loader.exec_module(mod)
D = "sha256:" + "a" * 64
BIND = {k: "sha256:" + c * 64 for k, c in zip(mod.FIELDS, "abcde")}
NONCE = "N" * 40
UID = os.getuid()


def _worker(args):
    root, nonce = args
    try:
        mod.consume(root, nonce, BIND, uid=UID, now=10, expires_at=100)
        return "OK"
    except mod.Stop as exc:
        return str(exc)


class NonceJournalTests(unittest.TestCase):
    def setUp(self):
        self.base = tempfile.mkdtemp(prefix="nj")
        os.chmod(self.base, 0o700)
        self.root = os.path.join(os.path.realpath(self.base), "j")
        os.mkdir(self.root, 0o700)
        self.addCleanup(shutil.rmtree, self.base, True)

    def go(self, nonce=NONCE, **kw):
        args = dict(uid=UID, now=10, expires_at=100)
        args.update(kw)
        return mod.consume(self.root, nonce, BIND, **args)

    def stop(self, code, fn):
        with self.assertRaises(mod.Stop) as err:
            fn()
        self.assertEqual(str(err.exception), code)

    def test_first_consume_ok_no_authority_and_durable_record(self):
        r = self.go()
        self.assertTrue(r["consumed"])
        for k in ("may_install", "may_apply", "sponsor_authorized_here", "bootstrap_authenticated_here"):
            self.assertIs(r[k], False)
        files = [f for f in os.listdir(self.root) if f.endswith(".spent")]
        self.assertEqual(len(files), 1)
        self.assertEqual(oct(os.stat(os.path.join(self.root, files[0])).st_mode & 0o777), "0o600")
        self.assertNotIn(NONCE, files[0])  # nonce never stored in clear in the name

    def test_replay_is_refused_even_with_different_binding(self):
        self.go()
        self.stop("NONCE_REPLAY", self.go)
        other = dict(BIND, plan=D)
        self.stop("NONCE_REPLAY", lambda: mod.consume(self.root, NONCE, other, uid=UID, now=11, expires_at=100))

    def test_crash_after_creation_leaves_nonce_spent(self):
        name = mod.hashlib.sha256(NONCE.encode()).hexdigest() + ".spent"
        open(os.path.join(self.root, name), "x").close()  # crashed before writing the record
        self.stop("NONCE_REPLAY", self.go)

    def test_survives_new_process_state(self):
        self.go()
        with multiprocessing.get_context("spawn").Pool(1) as pool:
            self.assertEqual(pool.map(_worker, [(self.root, NONCE)]), ["NONCE_REPLAY"])

    def test_concurrent_processes_exactly_one_wins(self):
        with multiprocessing.get_context("spawn").Pool(8) as pool:
            res = pool.map(_worker, [(self.root, NONCE)] * 16)
        self.assertEqual(res.count("OK"), 1)
        self.assertTrue(all(r in ("OK", "NONCE_REPLAY", "NONCE_LEASE_BUSY") for r in res), res)

    def test_distinct_nonces_do_not_collide(self):
        self.go("A" * 40)
        self.go("B" * 40)
        self.assertEqual(len([f for f in os.listdir(self.root) if f.endswith(".spent")]), 2)

    def test_expiry_clock_and_grammar(self):
        self.stop("NONCE_EXPIRED", lambda: self.go(now=100))
        self.stop("NONCE_CLOCK_INVALID", lambda: self.go(now=True))
        self.stop("NONCE_CLOCK_INVALID", lambda: self.go(expires_at=100.5))
        for bad in ("short", "a" * 97, "a" * 40 + "!", None, b"a" * 40):
            self.stop("NONCE_INVALID", lambda b=bad: self.go(b))
        self.assertEqual([f for f in os.listdir(self.root) if f.endswith(".spent")], [])

    def test_binding_must_be_exact_digests(self):
        for bad in ({}, dict(BIND, extra=D), dict(BIND, app="x"), {k: BIND[k] for k in list(BIND)[:4]}, None):
            self.stop("NONCE_BINDING_INVALID",
                      lambda b=bad: mod.consume(self.root, NONCE, b, uid=UID, now=10, expires_at=100))

    def test_root_must_be_private_real_directory(self):
        os.chmod(self.root, 0o755)
        self.stop("NONCE_ROOT_UNTRUSTED", self.go)
        os.chmod(self.root, 0o700)
        link = os.path.join(self.base, "link")
        os.symlink(self.root, link)
        self.stop("NONCE_ROOT_UNTRUSTED", lambda: mod.consume(link, NONCE, BIND, uid=UID, now=10, expires_at=100))
        self.stop("NONCE_ROOT_INVALID", lambda: mod.consume("relative/j", NONCE, BIND, uid=UID, now=10, expires_at=100))
        self.stop("NONCE_ROOT_INVALID", lambda: mod.consume(self.root + "/../j", NONCE, BIND, uid=UID, now=10, expires_at=100))
        self.stop("NONCE_UID_INVALID", lambda: mod.consume(self.root, NONCE, BIND, uid=-1, now=10, expires_at=100))

    def test_symlink_planted_as_spent_file_does_not_escape(self):
        victim = os.path.join(self.base, "victim")
        name = mod.hashlib.sha256(NONCE.encode()).hexdigest() + ".spent"
        os.symlink(victim, os.path.join(self.root, name))
        self.stop("NONCE_REPLAY", self.go)  # EEXIST on the link: refused, victim never created
        self.assertFalse(os.path.exists(victim))

    def test_lease_held_by_another_open_description_is_busy(self):
        import fcntl
        fd = os.open(os.path.join(self.root, ".lease"), os.O_RDWR | os.O_CREAT, 0o600)
        self.addCleanup(os.close, fd)
        fcntl.flock(fd, fcntl.LOCK_EX)
        self.stop("NONCE_LEASE_BUSY", self.go)
        self.assertEqual([f for f in os.listdir(self.root) if f.endswith(".spent")], [])

    def test_no_forbidden_apis(self):
        text = SRC.read_text()
        for bad in ("subprocess", "sudo", "docker", "urllib", "socket", "/opt/centenario", "os.system"):
            self.assertNotIn(bad, text.replace("No sudo, docker, network, or path", ""))


if __name__ == "__main__":
    unittest.main()
