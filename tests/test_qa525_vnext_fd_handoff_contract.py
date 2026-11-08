"""QA525 SCM_RIGHTS sealed fd transport, real Linux kernel, never physical GO."""
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

SRC = Path(__file__).resolve().parents[1] / "scripts/qa/qa525-vnext-fd-handoff-contract.py"
spec = importlib.util.spec_from_file_location("qa525_fd_handoff_test", SRC)
mod = importlib.util.module_from_spec(spec)
spec.loader.exec_module(mod)
DATA = b"QA525 sealed real memfd test bytes; not an APP attestation"
SHA = "sha256:" + hashlib.sha256(DATA).hexdigest()
NONCE = "a" * 41 + "-_"


@unittest.skipUnless(sys.platform == "linux" and hasattr(os, "memfd_create")
                     and hasattr(socket, "SOCK_SEQPACKET")
                     and hasattr(fcntl, "F_ADD_SEALS"), "Linux with seals required")
class TestFdHandoff(unittest.TestCase):
    def setUp(self):
        self.fds = []
        self.socks = socket.socketpair(socket.AF_UNIX, socket.SOCK_SEQPACKET)
        self.addCleanup(self.clean)

    def clean(self):
        for fd in self.fds:
            try:
                os.close(fd)
            except OSError:
                pass
        for sock in self.socks:
            sock.close()

    def fd(self, data=DATA, sealed=True):
        fd = os.memfd_create("qa525-test-fd", os.MFD_CLOEXEC | os.MFD_ALLOW_SEALING)
        self.fds.append(fd)
        os.write(fd, data)
        if sealed:
            fcntl.fcntl(fd, fcntl.F_ADD_SEALS, mod.SEALS)
        return fd

    def recv(self, **kwargs):
        return mod.receive_verified_fd(
            self.socks[1], expected_nonce=kwargs.get("nonce", NONCE),
            expected_sha256=kwargs.get("pin", SHA))

    def send(self, fd, **kwargs):
        return mod.send_verified_fd(
            self.socks[0], fd, nonce=kwargs.get("nonce", NONCE),
            sha256=kwargs.get("pin", SHA))

    def frame(self):
        return mod.MAGIC + NONCE.encode() + b":" + SHA[7:].encode()

    def raw_send(self, fds, frame=None):
        control = [] if not fds else [(socket.SOL_SOCKET, socket.SCM_RIGHTS,
                                       array.array("i", fds))]
        self.socks[0].sendmsg([self.frame() if frame is None else frame], control)

    def test_real_sendmsg_recvmsg_one_fd_and_no_authority(self):
        self.send(self.fd())
        result = self.recv()
        self.fds.append(result["fd"])
        self.assertEqual(os.pread(result["fd"], len(DATA), 0), DATA)
        self.assertEqual(result["sha256"], SHA)
        self.assertFalse(os.get_inheritable(result["fd"]))
        for field in ("may_install", "may_apply", "sponsor_authorized_here",
                      "bootstrap_authenticated_here"):
            self.assertIs(result[field], False)
        with self.assertRaises(PermissionError):
            os.pwrite(result["fd"], b"X", 0)

    def test_nonce_mismatch_does_not_grant_fd(self):
        self.send(self.fd(), nonce="b" * 43)
        with self.assertRaises(mod.Stop) as err:
            self.recv()
        self.assertEqual(str(err.exception), "FD_HANDOFF_FRAME_MISMATCH")

    def test_sender_refuses_nonce_outside_signed_go_grammar(self):
        for bad in ("a" * 23, "a" * 97, "a" * 42 + "!"):
            with self.assertRaises(mod.Stop) as err:
                self.send(self.fd(), nonce=bad)
            self.assertEqual(str(err.exception), "FD_HANDOFF_NONCE_INVALID")

    def test_wire_digest_is_not_an_independent_pin(self):
        self.raw_send([self.fd()], mod.MAGIC + NONCE.encode() + b":" + b"b" * 64)
        with self.assertRaises(mod.Stop) as err:
            self.recv()
        self.assertEqual(str(err.exception), "FD_HANDOFF_FRAME_MISMATCH")

    def test_wrong_bytes_even_with_correct_frame_and_seals_are_denied(self):
        self.raw_send([self.fd(data=b"changed bytes")])
        with self.assertRaises(mod.Stop) as err:
            self.recv()
        self.assertEqual(str(err.exception), "FD_HANDOFF_DIGEST_MISMATCH")

    def test_unsealed_fd_is_denied(self):
        self.raw_send([self.fd(sealed=False)])
        with self.assertRaises(mod.Stop) as err:
            self.recv()
        self.assertEqual(str(err.exception), "FD_HANDOFF_FD_UNSAFE")

    def test_multiple_fds_are_denied(self):
        self.raw_send([self.fd(), self.fd()])
        with self.assertRaises(mod.Stop) as err:
            self.recv()
        self.assertEqual(str(err.exception), "FD_HANDOFF_FD_COUNT_INVALID")

    def test_missing_fd_is_denied(self):
        self.raw_send([])
        with self.assertRaises(mod.Stop) as err:
            self.recv()
        self.assertEqual(str(err.exception), "FD_HANDOFF_FD_COUNT_INVALID")

    def test_packet_with_extra_bytes_and_truncated_packet_are_denied(self):
        self.raw_send([self.fd()], self.frame() + b"EXTRA")
        with self.assertRaises(mod.Stop) as err:
            self.recv()
        self.assertEqual(str(err.exception), "FD_HANDOFF_FRAME_MISMATCH")
        self.raw_send([self.fd()], b"Y" * 300)
        with self.assertRaises(mod.Stop) as err:
            self.recv()
        self.assertEqual(str(err.exception), "FD_HANDOFF_TRUNCATED")

    def test_stream_is_rejected_in_favor_of_seqpacket(self):
        left, right = socket.socketpair(socket.AF_UNIX, socket.SOCK_STREAM)
        with left, right:
            with self.assertRaises(mod.Stop) as err:
                mod.send_verified_fd(left, self.fd(), nonce=NONCE, sha256=SHA)
            self.assertEqual(str(err.exception), "FD_HANDOFF_SOCKET_INVALID")

    def test_sender_rejects_wrong_digest_and_unsealed_fd(self):
        with self.assertRaises(mod.Stop) as err:
            self.send(self.fd(), pin="sha256:" + "b" * 64)
        self.assertEqual(str(err.exception), "FD_HANDOFF_DIGEST_MISMATCH")
        with self.assertRaises(mod.Stop) as err:
            self.send(self.fd(sealed=False))
        self.assertEqual(str(err.exception), "FD_HANDOFF_FD_UNSAFE")

    def test_real_sealed_fd_crosses_independent_process_without_path(self):
        child_code = (
            "import socket,sys,os,fcntl,hashlib,array\n"
            "s=socket.socket(fileno=int(sys.argv[1]))\n"
            "fd=os.memfd_create('child',os.MFD_ALLOW_SEALING|os.MFD_CLOEXEC)\n"
            "data=bytes.fromhex(sys.argv[2]);os.write(fd,data)\n"
            "seals=fcntl.F_SEAL_WRITE|fcntl.F_SEAL_SHRINK|fcntl.F_SEAL_GROW|fcntl.F_SEAL_SEAL\n"
            "fcntl.fcntl(fd,fcntl.F_ADD_SEALS,seals)\n"
            "s.sendmsg([b'QA525-FD-V1:'+sys.argv[3].encode()+b':'+hashlib.sha256(data).hexdigest().encode()],"
            "[(socket.SOL_SOCKET,socket.SCM_RIGHTS,array.array('i',[fd]))])\n"
        )
        child = subprocess.Popen(
            [sys.executable, "-I", "-c", child_code, str(self.socks[0].fileno()),
             DATA.hex(), NONCE], pass_fds=(self.socks[0].fileno(),),
            stdout=subprocess.PIPE, stderr=subprocess.PIPE)
        self.socks[0].close()
        result = self.recv()
        self.fds.append(result["fd"])
        stdout, stderr = child.communicate(timeout=8)
        self.assertEqual(child.returncode, 0, stderr)
        self.assertEqual(os.pread(result["fd"], len(DATA), 0), DATA)


if __name__ == "__main__":
    unittest.main()
