"""QA525 boundary receiver: real kernel SCM_RIGHTS/SO_PEERCRED, subprocess peers, never GO."""
import array
import fcntl
import hashlib
import importlib.util
import os
from pathlib import Path
import socket
import subprocess
import sys
import unittest

SRC = Path(__file__).resolve().parents[1] / "scripts/qa/qa525-vnext-root-boundary-recv.py"
spec = importlib.util.spec_from_file_location("qa525_boundary_recv_test", SRC)
mod = importlib.util.module_from_spec(spec)
spec.loader.exec_module(mod)
DATA = b"A" * 1000 + b"B" * 1000
SHA = "sha256:" + hashlib.sha256(DATA).hexdigest()
NONCE = "n" * 43


@unittest.skipUnless(sys.platform == "linux" and hasattr(os, "memfd_create")
                     and hasattr(fcntl, "F_ADD_SEALS"), "Linux with seals required")
class BoundaryRecvTests(unittest.TestCase):
    def setUp(self):
        self.a, self.b = socket.socketpair(socket.AF_UNIX, socket.SOCK_SEQPACKET)
        self.fds = []
        mod.prepare(self.b)
        self.addCleanup(self.clean)

    def clean(self):
        for fd in self.fds:
            try:
                os.close(fd)
            except OSError:
                pass
        self.a.close()
        self.b.close()

    def memfd(self, data=DATA, sealed=True):
        fd = os.memfd_create("t", os.MFD_CLOEXEC | os.MFD_ALLOW_SEALING)
        self.fds.append(fd)
        os.write(fd, data)
        if sealed:
            fcntl.fcntl(fd, fcntl.F_ADD_SEALS, mod.SEALS)
        return fd

    def send(self, fds, frame=None):
        frame = frame or mod.MAGIC + NONCE.encode() + b":" + SHA[7:].encode()
        ctl = [(socket.SOL_SOCKET, socket.SCM_RIGHTS, array.array("i", fds))] if fds else []
        self.a.sendmsg([frame], ctl)

    def recv(self, **kw):
        args = dict(expected_nonce=NONCE, expected_sha256=SHA,
                    expected_peer_uid=os.getuid(), timeout=2)
        args.update(kw)
        return mod.receive(self.b, **args)

    def stop(self, code, **kw):
        with self.assertRaises(mod.Stop) as err:
            self.recv(**kw)
        self.assertEqual(str(err.exception), code)

    def test_ok_private_fd_and_no_authority(self):
        self.send([self.memfd()])
        r = self.recv()
        self.fds.append(r["fd"])
        self.assertEqual(r["sender_uid"], os.getuid())
        self.assertEqual(os.pread(r["fd"], len(DATA), 0), DATA)
        for k in ("may_install", "may_apply", "sponsor_authorized_here",
                  "bootstrap_authenticated_here"):
            self.assertIs(r[k], False)

    def test_sender_cannot_skew_offset_or_flags_after_verification(self):
        src = self.memfd()
        self.send([src])
        r = self.recv()
        self.fds.append(r["fd"])
        os.lseek(src, 1500, os.SEEK_SET)
        fcntl.fcntl(src, fcntl.F_SETFL, fcntl.fcntl(src, fcntl.F_GETFL) | os.O_NONBLOCK)
        self.assertEqual(os.read(r["fd"], 4096), DATA)
        self.assertFalse(fcntl.fcntl(r["fd"], fcntl.F_GETFL) & os.O_NONBLOCK)
        self.assertEqual(fcntl.fcntl(r["fd"], fcntl.F_GETFL) & os.O_ACCMODE, os.O_RDONLY)
        with self.assertRaises(OSError):
            os.pwrite(r["fd"], b"X", 0)

    def test_foreign_uid_is_refused_and_fds_closed(self):
        before = len(os.listdir("/proc/self/fd"))
        self.send([self.memfd()])
        mid = len(os.listdir("/proc/self/fd"))
        self.stop("BND_PEER_REFUSED", expected_peer_uid=os.getuid() + 1)
        self.assertEqual(len(os.listdir("/proc/self/fd")), mid)
        self.assertGreater(mid, before)

    CHILD = ("import socket,sys,os,array,fcntl\n"
             "s=socket.socket(fileno=int(sys.argv[1]))\n"
             "fd=os.memfd_create('c',os.MFD_ALLOW_SEALING)\n"
             "os.write(fd,b'A'*1000+b'B'*1000)\n"
             "fcntl.fcntl(fd,fcntl.F_ADD_SEALS,int(sys.argv[2]))\n"
             "print(os.getpid(),flush=True)\n"
             "s.sendmsg([sys.argv[3].encode()],[(socket.SOL_SOCKET,socket.SCM_RIGHTS,array.array('i',[fd]))])\n")

    def spawn_sender(self):
        frame = (mod.MAGIC + NONCE.encode() + b":" + SHA[7:].encode()).decode()
        return subprocess.Popen([sys.executable, "-I", "-c", self.CHILD, str(self.a.fileno()),
                                 str(mod.SEALS), frame], pass_fds=[self.a.fileno()],
                                stdout=subprocess.PIPE, text=True)

    def test_actual_sending_process_is_attested_per_message(self):
        child = self.spawn_sender()
        pid = int(child.stdout.readline())
        r = self.recv(expected_peer_pid=pid)
        child.wait(5)
        self.fds.append(r["fd"])
        self.assertEqual(r["sender_pid"], pid)
        self.assertNotEqual(pid, os.getpid())
        self.assertEqual(os.pread(r["fd"], 2000, 0), DATA)

    def test_inherited_endpoint_used_by_third_process_is_refused(self):
        """The socketpair was created by THIS process; a different process sends."""
        child = self.spawn_sender()
        child.stdout.readline()
        # Policy pins the creator (this pid) as the only acceptable sender: the child lies outside.
        self.stop("BND_PEER_REFUSED", expected_peer_pid=os.getpid())
        child.wait(5)

    def test_sender_pid_is_reported_for_the_real_sender_not_the_creator(self):
        child = self.spawn_sender()
        pid = int(child.stdout.readline())
        r = self.recv()
        child.wait(5)
        self.fds.append(r["fd"])
        self.assertEqual(r["sender_pid"], pid)

    def test_message_queued_before_prepare_has_no_credentials_and_is_refused(self):
        a, b = socket.socketpair(socket.AF_UNIX, socket.SOCK_SEQPACKET)
        self.addCleanup(a.close)
        self.addCleanup(b.close)
        frame = mod.MAGIC + NONCE.encode() + b":" + SHA[7:].encode()
        fd = self.memfd()
        a.sendmsg([frame], [(socket.SOL_SOCKET, socket.SCM_RIGHTS, array.array("i", [fd]))])
        b.setsockopt(socket.SOL_SOCKET, socket.SO_PASSCRED, 1)  # too late for that message
        with self.assertRaises(mod.Stop) as err:
            mod.receive(b, expected_nonce=NONCE, expected_sha256=SHA,
                        expected_peer_uid=os.getuid(), timeout=2)
        self.assertEqual(str(err.exception), "BND_CREDENTIALS_MISSING")

    def test_receive_without_prepare_is_refused(self):
        a, b = socket.socketpair(socket.AF_UNIX, socket.SOCK_SEQPACKET)
        self.addCleanup(a.close)
        self.addCleanup(b.close)
        with self.assertRaises(mod.Stop) as err:
            mod.receive(b, expected_nonce=NONCE, expected_sha256=SHA,
                        expected_peer_uid=os.getuid(), timeout=1)
        self.assertEqual(str(err.exception), "BND_PASSCRED_NOT_ENABLED")

    def test_timeout_is_mandatory_and_silent_peer_stops(self):
        for bad in (None, 0, -1, 61, True, "2"):
            self.stop("BND_TIMEOUT_REQUIRED", timeout=bad)
        self.stop("BND_RECEIVE_FAILED", timeout=0.2)

    def test_pin_nonce_frame_and_bytes(self):
        self.send([self.memfd()], mod.MAGIC + b"z" * 43 + b":" + SHA[7:].encode())
        self.stop("BND_FRAME_MISMATCH")
        self.send([self.memfd(b"other")])
        self.stop("BND_DIGEST_MISMATCH")
        self.send([self.memfd(sealed=False)])
        self.stop("BND_FD_UNSAFE")

    def test_wrong_fd_counts_and_overflow_leak_nothing(self):
        before = len(os.listdir("/proc/self/fd"))
        self.send([])
        self.stop("BND_FD_COUNT_INVALID")
        extra = [self.memfd() for _ in range(2)]
        mid = len(os.listdir("/proc/self/fd"))
        self.send(extra)
        self.stop("BND_FD_COUNT_INVALID")
        many = [self.memfd() for _ in range(10)]
        mid2 = len(os.listdir("/proc/self/fd"))
        self.send(many)
        self.stop("BND_TRUNCATED")
        self.assertEqual(len(os.listdir("/proc/self/fd")), mid2)
        self.assertGreater(mid, before)

    def test_replay_is_NOT_prevented_here(self):
        for _ in range(2):
            self.send([self.memfd()])
            self.fds.append(self.recv()["fd"])
        # documented: single-use belongs to the durable nonce journal, not this library

    def test_stream_socket_and_bad_types_refused(self):
        l, r = socket.socketpair(socket.AF_UNIX, socket.SOCK_STREAM)
        with l, r:
            with self.assertRaises(mod.Stop):
                mod.receive(l, expected_nonce=NONCE, expected_sha256=SHA,
                            expected_peer_uid=0, timeout=1)
        self.stop("BND_UID_INVALID", expected_peer_uid=True)
        self.stop("BND_NONCE_INVALID", expected_nonce="short")
        self.stop("BND_PIN_INVALID", expected_sha256="sha256:" + "G" * 64)

    def test_socket_timeout_restored_and_no_forbidden_apis(self):
        self.b.settimeout(7)
        self.send([self.memfd()])
        self.fds.append(self.recv()["fd"])
        self.assertEqual(self.b.gettimeout(), 7)
        text = SRC.read_text()
        for bad in ("subprocess", "sudo", "docker", "urllib", "requests", "/opt/centenario", "os.system"):
            self.assertNotIn(bad, text.replace("runs commands, or uses network", ""))


if __name__ == "__main__":
    unittest.main()
