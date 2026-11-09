"""QA525 pre-root entry composition: real bootstrap, real receiver, real journal, real sockets."""
import array
import fcntl
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import shutil
import socket
import sys
import tempfile
import unittest
import unittest.mock

ROOT = Path(__file__).resolve().parents[1] / "scripts/qa"
NAMES = ("qa525-vnext-root-boundary-recv.py", "qa525-vnext-go-nonce-journal.py",
         "qa525-vnext-root-entry.py")
UID = os.getuid()
DATA = b"signed-app-archive-bytes" * 50
SHA = "sha256:" + hashlib.sha256(DATA).hexdigest()
NONCE = "N" * 40
D = {k: "sha256:" + c * 64 for k, c in zip(("ops", "policy", "plan", "admission"), "bcde")}


def rd(p):
    with open(p, "rb") as f:
        return f.read()


def load_file(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    m = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(m)
    return m


boot = load_file("qa525_entry_boot", ROOT / "qa525-vnext-root-bootstrap.py")


class Plane:
    """Injected 5-lock plane double: can be busy or lost mid-flight."""
    def __init__(self, busy=False, lose_after=None):
        self.busy, self.lose_after, self.checks, self.entered = busy, lose_after, 0, False

    def __enter__(self):
        if self.busy:
            raise boot.Stop("PLANE_BUSY")
        self.entered = True
        return self

    def __exit__(self, *a):
        self.entered = False

    def still_held(self):
        self.checks += 1
        return self.lose_after is None or self.checks <= self.lose_after


@unittest.skipUnless(sys.platform == "linux" and hasattr(os, "memfd_create"), "Linux required")
class EntryTests(unittest.TestCase):
    def setUp(self):
        self.base = os.path.realpath(tempfile.mkdtemp(prefix="ent"))
        os.chmod(self.base, 0o755)
        self.addCleanup(shutil.rmtree, self.base, True)
        self.inst = os.path.join(self.base, "inst")
        os.mkdir(self.inst, 0o755)
        for n in NAMES:
            shutil.copy(ROOT / n, os.path.join(self.inst, n))
            os.chmod(os.path.join(self.inst, n), 0o644)
        self.jroot = os.path.join(self.base, "journal")
        os.mkdir(self.jroot, 0o700)
        self.a, self.b = socket.socketpair(socket.AF_UNIX, socket.SOCK_SEQPACKET)
        self.fds = []
        self.addCleanup(self.clean)
        files = {n: hashlib.sha256(rd(os.path.join(self.inst, n))).hexdigest() for n in NAMES}
        real = os.path.realpath(sys.executable)
        self.manifest = json.dumps({"files": files, "interpreter": {
            "path": real, "sha256": hashlib.sha256(rd(real)).hexdigest()}},
            sort_keys=True, separators=(",", ":")).encode()
        self.pin = hashlib.sha256(self.manifest).hexdigest()
        self.verified = self.verify()
        self.entry = boot.load_module(self.verified, NAMES[2])
        self.load = lambda n: boot.load_module(self.verified, n)
        self.recv = self.load(NAMES[0])
        self.recv.prepare(self.b)

    def verify(self):
        flags = type("F", (), dict(isolated=1, no_site=1, ignore_environment=1))()
        with unittest.mock.patch.object(boot.sys, "flags", flags):
            return boot.verify_install(self.inst, self.manifest, self.pin, uid=UID, anchor=self.base)

    def clean(self):
        for fd in self.fds:
            try:
                os.close(fd)
            except OSError:
                pass
        self.a.close()
        self.b.close()

    def send(self, data=DATA, sha=SHA, nonce=NONCE, sealed=True):
        fd = os.memfd_create("t", os.MFD_CLOEXEC | os.MFD_ALLOW_SEALING)
        self.fds.append(fd)
        os.write(fd, data)
        if sealed:
            fcntl.fcntl(fd, fcntl.F_ADD_SEALS, self.recv.SEALS)
        frame = self.recv.MAGIC + nonce.encode() + b":" + sha[7:].encode()
        self.a.sendmsg([frame], [(socket.SOL_SOCKET, socket.SCM_RIGHTS, array.array("i", [fd]))])

    def claims(self, **over):
        c = dict(D, nonce=NONCE, app_archive_sha256=SHA, expires_at=2000)
        c.update(over)
        return c

    def run_entry(self, plane=None, claims=None, verify_go=None, peer_uid=None, load=None):
        plane = plane or Plane()
        verify_go = verify_go or (lambda: claims if claims is not None else self.claims())
        return self.entry.run(load or self.load, plane=plane, verify_go=verify_go, sock=self.b,
                              peer_uid=UID if peer_uid is None else peer_uid, peer_pid=None,
                              timeout=2, journal_root=self.jroot, uid=UID, clock=lambda: 1000)

    def spent(self):
        return [n for n in os.listdir(self.jroot) if n.endswith(".spent")]

    def stop(self, code, **kw):
        with self.assertRaises(Exception) as ctx:
            self.run_entry(**kw)
        self.assertEqual(str(ctx.exception), code)

    def test_happy_path_returns_private_fd_spends_nonce_and_grants_nothing(self):
        self.send()
        out = self.run_entry()
        self.fds.append(out["fd"])
        self.assertEqual(os.pread(out["fd"], len(DATA), 0), DATA)
        self.assertEqual(len(self.spent()), 1)
        for k in ("may_apply", "may_install", "sponsor_authorized_here", "bootstrap_authenticated_here"):
            self.assertIs(out[k], False)

    def test_fd_and_sender_pin_match_but_signed_go_backs_another_zip_stops_unspent(self):
        evil = b"other-zip" * 50
        evil_sha = "sha256:" + hashlib.sha256(evil).hexdigest()
        self.send(data=evil, sha=evil_sha)  # sender is self-consistent
        self.stop("BND_FRAME_MISMATCH")  # authority = GO digest (SHA of DATA), not the sender's
        self.assertEqual(self.spent(), [])

    def test_wrong_bytes_under_the_right_digest_stop_unspent(self):
        self.send(data=b"x" * len(DATA))
        with self.assertRaises(Exception):
            self.run_entry()
        self.assertEqual(self.spent(), [])

    def test_unsigned_or_invalid_go_never_reaches_receive_or_the_journal(self):
        def deny():
            raise RuntimeError("GO_CUT_SIGNATURE_DENIED")
        self.send()
        with self.assertRaises(RuntimeError):
            self.run_entry(verify_go=deny)
        self.assertEqual(self.spent(), [])
        self.assertEqual(self.b.recv(1, socket.MSG_PEEK | socket.MSG_DONTWAIT) != b"", True)  # untouched

    def test_malformed_claims_are_refused(self):
        for bad in ({}, dict(self.claims(), extra=1), self.claims(expires_at="1"), None):
            self.stop("ENTRY_GO_CLAIMS_INVALID", claims=bad, verify_go=lambda b=bad: b)

    def test_busy_plane_stops_before_anything(self):
        self.send()
        with self.assertRaises(boot.Stop):
            self.run_entry(plane=Plane(busy=True))
        self.assertEqual(self.spent(), [])

    def test_plane_not_held_or_lost_before_consume_spends_nothing(self):
        self.send()
        self.stop("ENTRY_PLANE_NOT_HELD", plane=Plane(lose_after=0))
        self.send()
        self.stop("ENTRY_PLANE_LOST", plane=Plane(lose_after=1))
        self.assertEqual(self.spent(), [])

    def test_fake_peer_is_refused_and_does_not_spend(self):
        self.send()
        self.stop("BND_PEER_REFUSED", peer_uid=UID + 1)
        self.assertEqual(self.spent(), [])

    def test_unsealed_fd_is_refused(self):
        self.send(sealed=False)
        self.stop("BND_FD_UNSAFE")
        self.assertEqual(self.spent(), [])

    def test_replay_after_success_is_refused_and_the_spent_mark_is_permanent(self):
        self.send()
        out = self.run_entry()
        os.close(out["fd"])
        self.send()
        self.stop("NONCE_REPLAY")
        self.assertEqual(len(self.spent()), 1)  # the spent mark is permanent: a crash cannot reuse it

    def test_expired_go_does_not_consume(self):
        self.send()
        self.stop("NONCE_EXPIRED", claims=self.claims(expires_at=1000))
        self.assertEqual(self.spent(), [])

    def test_forged_loader_cannot_supply_code(self):
        marker = os.path.join(self.base, "pwned")
        evil = ("open(%r,'w').write('x')\n" % marker).encode()
        with self.assertRaises(boot.Stop) as ctx:
            boot.load_module({NAMES[0]: evil}, NAMES[0])
        self.assertEqual(str(ctx.exception), "BOOT_VERIFIED_FORGED")
        self.assertFalse(os.path.exists(marker))

    def test_sibling_swapped_on_disk_after_verification_is_never_executed(self):
        marker = os.path.join(self.base, "pwned")
        p = os.path.join(self.inst, NAMES[1])
        os.unlink(p)
        with open(p, "w") as f:
            f.write("open(%r,'w').write('x')\n" % marker)
        self.send()
        out = self.run_entry()  # still uses the verified bytes
        os.close(out["fd"])
        self.assertFalse(os.path.exists(marker))
        self.assertEqual(len(self.spent()), 1)
        with self.assertRaises(boot.Stop) as ctx:  # and a NEW verification now refuses the install
            self.verify()
        self.assertEqual(str(ctx.exception), "BOOT_HASH_MISMATCH")

    def test_entry_module_imports_no_repo_code_from_disk(self):
        src = (ROOT / NAMES[2]).read_text()
        for bad in ("importlib", "exec_module", "spec_from_file_location", "subprocess", "sys.path"):
            self.assertNotIn(bad, src)


if __name__ == "__main__":
    unittest.main()
