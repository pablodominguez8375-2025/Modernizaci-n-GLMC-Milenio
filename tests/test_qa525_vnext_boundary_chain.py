"""QA525 boundary chain, synthetic: real processes, real kernel, no root, never a GO.

child sender --SCM_RIGHTS--> boundary receiver (#966) --pin--> GO-nonce journal (#967).
`consumed` is a replay mark only; signatures/authority are out of scope and never implied.
"""
import array
import fcntl
import hashlib
import importlib.util
import os
from pathlib import Path
import shutil
import socket
import subprocess
import sys
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[1]


def load(name, rel):
    spec = importlib.util.spec_from_file_location(name, ROOT / rel)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


recv = load("qa525_chain_recv", "scripts/qa/qa525-vnext-root-boundary-recv.py")
journal = load("qa525_chain_journal", "scripts/qa/qa525-vnext-go-nonce-journal.py")
ZIP = b"PK\x03\x04 synthetic signed-release bytes " * 50
PIN = "sha256:" + hashlib.sha256(ZIP).hexdigest()
FRAME_NONCE = "F" * 43          # transport label
GO_NONCE = "G" * 43             # signed-GO nonce (journal), never taken from the wire
BIND = {k: "sha256:" + c * 64 for k, c in zip(journal.FIELDS, "abcde")}
UID = os.getuid()

SENDER = """
import array, fcntl, os, socket, sys
sock = socket.socket(fileno=int(sys.argv[1]))
data = sys.stdin.buffer.read()
fd = os.memfd_create('zip', os.MFD_ALLOW_SEALING)
os.write(fd, data)
fcntl.fcntl(fd, fcntl.F_ADD_SEALS, int(sys.argv[2]))
print(os.getpid(), flush=True)
sock.sendmsg([sys.argv[3].encode()],
             [(socket.SOL_SOCKET, socket.SCM_RIGHTS, array.array('i', [fd]))])
"""


@unittest.skipUnless(sys.platform == "linux" and hasattr(os, "memfd_create")
                     and hasattr(fcntl, "F_ADD_SEALS"), "Linux with seals required")
class BoundaryChainTests(unittest.TestCase):
    def setUp(self):
        self.base = tempfile.mkdtemp(prefix="chain")
        os.chmod(self.base, 0o700)
        self.jroot = os.path.join(os.path.realpath(self.base), "journal")
        os.mkdir(self.jroot, 0o700)
        self.addCleanup(shutil.rmtree, self.base, True)
        self.parent, self.child = socket.socketpair(socket.AF_UNIX, socket.SOCK_SEQPACKET)
        recv.prepare(self.parent)
        self.addCleanup(self.parent.close)
        self.addCleanup(self.child.close)
        self.fds = []
        self.addCleanup(lambda: [os.close(f) for f in self.fds])

    def frame(self, nonce=FRAME_NONCE, pin=PIN):
        return (recv.MAGIC + nonce.encode() + b":" + pin[7:].encode()).decode()

    def send_from_child(self, data=ZIP, frame=None, seals=recv.SEALS):
        proc = subprocess.Popen(
            [sys.executable, "-I", "-c", SENDER, str(self.child.fileno()), str(seals),
             frame or self.frame()], stdin=subprocess.PIPE, stdout=subprocess.PIPE,
            pass_fds=[self.child.fileno()])
        proc.stdin.write(data)
        proc.stdin.close()
        pid = int(proc.stdout.readline())
        proc.stdout.close()
        proc.wait(10)
        return pid

    def receive(self, **kw):
        args = dict(expected_nonce=FRAME_NONCE, expected_sha256=PIN, expected_peer_uid=UID,
                    timeout=5)
        args.update(kw)
        return recv.receive(self.parent, **args)

    def consume(self, nonce=GO_NONCE, root=None):
        return journal.consume(root or self.jroot, nonce, BIND, uid=UID, now=10, expires_at=100)

    def stop(self, mod, code, fn):
        with self.assertRaises(mod.Stop) as err:
            fn()
        self.assertEqual(str(err.exception), code)

    def spent(self):
        return [f for f in os.listdir(self.jroot) if f.endswith(".spent")]

    def test_happy_path_bytes_then_replay_mark_and_no_authority(self):
        pid = self.send_from_child()
        got = self.receive(expected_peer_pid=pid)
        self.fds.append(got["fd"])
        self.assertEqual(os.pread(got["fd"], len(ZIP) + 1, 0), ZIP)
        self.assertEqual(got["sender_pid"], pid)
        self.assertNotEqual(pid, os.getpid())
        result = self.consume()
        self.assertTrue(result["consumed"])
        for part in (got, result):
            for key in ("may_install", "may_apply", "sponsor_authorized_here",
                        "bootstrap_authenticated_here"):
                self.assertIs(part[key], False)
        self.assertEqual(len(self.spent()), 1)

    def test_foreign_or_inherited_sender_is_refused_and_nothing_is_spent(self):
        pid = self.send_from_child()
        self.stop(recv, "BND_PEER_REFUSED", lambda: self.receive(expected_peer_pid=os.getpid()))
        self.assertEqual(self.spent(), [])
        self.assertNotEqual(pid, os.getpid())

    def test_wrong_uid_policy_refuses(self):
        self.send_from_child()
        self.stop(recv, "BND_PEER_REFUSED", lambda: self.receive(expected_peer_uid=UID + 1))

    def test_false_pin_and_substituted_zip_stop_before_any_nonce_is_spent(self):
        self.send_from_child(data=ZIP + b"tampered")
        self.stop(recv, "BND_DIGEST_MISMATCH", self.receive)
        self.send_from_child()
        self.stop(recv, "BND_FRAME_MISMATCH",
                  lambda: self.receive(expected_sha256="sha256:" + "0" * 64))
        self.assertEqual(self.spent(), [])

    def test_wire_digest_or_nonce_cannot_stand_in_for_the_independent_pin(self):
        self.send_from_child(frame=self.frame(pin="sha256:" + "9" * 64))
        self.stop(recv, "BND_FRAME_MISMATCH", self.receive)
        self.send_from_child(frame=self.frame(nonce="Z" * 43))
        self.stop(recv, "BND_FRAME_MISMATCH", self.receive)

    def test_unsealed_bytes_are_refused(self):
        self.send_from_child(seals=0)
        self.stop(recv, "BND_FD_UNSAFE", self.receive)

    def test_message_queued_before_prepare_has_no_attested_sender(self):
        a, b = socket.socketpair(socket.AF_UNIX, socket.SOCK_SEQPACKET)
        self.addCleanup(a.close)
        self.addCleanup(b.close)
        fd = os.memfd_create("z", os.MFD_ALLOW_SEALING)
        self.fds.append(fd)
        os.write(fd, ZIP)
        fcntl.fcntl(fd, fcntl.F_ADD_SEALS, recv.SEALS)
        a.sendmsg([self.frame().encode()],
                  [(socket.SOL_SOCKET, socket.SCM_RIGHTS, array.array("i", [fd]))])
        recv.prepare(b)
        self.stop(recv, "BND_CREDENTIALS_MISSING", lambda: recv.receive(
            b, expected_nonce=FRAME_NONCE, expected_sha256=PIN, expected_peer_uid=UID, timeout=2))

    def test_receiver_without_prepare_or_timeout_refuses(self):
        a, b = socket.socketpair(socket.AF_UNIX, socket.SOCK_SEQPACKET)
        self.addCleanup(a.close)
        self.addCleanup(b.close)
        self.stop(recv, "BND_PASSCRED_NOT_ENABLED", lambda: recv.receive(
            b, expected_nonce=FRAME_NONCE, expected_sha256=PIN, expected_peer_uid=UID, timeout=1))
        self.stop(recv, "BND_TIMEOUT_REQUIRED", lambda: self.receive(timeout=None))
        self.stop(recv, "BND_RECEIVE_FAILED", lambda: self.receive(timeout=0.2))

    def test_replay_of_the_go_nonce_is_refused_even_with_fresh_valid_bytes(self):
        self.send_from_child()
        self.fds.append(self.receive()["fd"])
        self.consume()
        self.send_from_child()
        self.fds.append(self.receive()["fd"])  # the transport accepts the same bytes again...
        self.stop(journal, "NONCE_REPLAY", self.consume)  # ...the journal does not
        self.assertEqual(len(self.spent()), 1)

    def test_replay_is_refused_across_processes(self):
        self.consume()
        code = ("import importlib.util,sys\n"
                "s=importlib.util.spec_from_file_location('j',sys.argv[1]);m=importlib.util.module_from_spec(s);"
                "s.loader.exec_module(m)\n"
                "b={k:'sha256:'+c*64 for k,c in zip(m.FIELDS,'abcde')}\n"
                "try:\n m.consume(sys.argv[2],sys.argv[3],b,uid=int(sys.argv[4]),now=10,expires_at=100)\n print('OK')\n"
                "except m.Stop as e:\n print(e)\n")
        out = subprocess.run([sys.executable, "-I", "-c", code,
                              str(ROOT / "scripts/qa/qa525-vnext-go-nonce-journal.py"),
                              self.jroot, GO_NONCE, str(UID)], capture_output=True, text=True,
                             timeout=20)
        self.assertEqual(out.stdout.strip(), "NONCE_REPLAY")

    def test_journal_never_receives_the_wire_label_as_the_go_nonce(self):
        self.send_from_child()
        self.fds.append(self.receive()["fd"])
        self.assertEqual(self.spent(), [])  # receiving alone spends nothing
        name = hashlib.sha256(FRAME_NONCE.encode()).hexdigest() + ".spent"
        self.consume()
        self.assertNotIn(name, self.spent())

    def test_fd_budget_does_not_leak_over_failures(self):
        before = len(os.listdir("/proc/self/fd"))
        for data in (ZIP + b"x", ZIP + b"y", ZIP + b"z"):
            self.send_from_child(data=data)
            with self.assertRaises(recv.Stop):
                self.receive()
        self.assertEqual(len(os.listdir("/proc/self/fd")), before)


if __name__ == "__main__":
    unittest.main()
