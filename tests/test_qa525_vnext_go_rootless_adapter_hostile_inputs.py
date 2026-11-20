"""QA525 real GO verifier port: hostile signers/inputs, fd and temp hygiene. TESTS ONLY.

Complements test_qa525_vnext_go_rootless_adapter.py. allowed_signers is in-memory bytes in the
real module (no path to race), so these target its grammar, fd and temp-dir hygiene.
"""
import glob
import hashlib
import importlib.util
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[1] / "scripts" / "qa"
spec = importlib.util.spec_from_file_location("qa525_port_hostile", ROOT / "qa525-vnext-go-rootless-adapter.py")
port = importlib.util.module_from_spec(spec)
spec.loader.exec_module(port)
SUPPORTED = shutil.which("ssh-keygen") and Path("/usr/bin/ssh-keygen").is_file()
MSG = b"qa525-hostile-message\n"


def _fds():
    return sorted(os.listdir("/proc/self/fd"))


def _tmp_dirs():
    return set(glob.glob(os.path.join(tempfile.gettempdir(), "qa525-go-rootless-*")))


@unittest.skipUnless(SUPPORTED and os.geteuid() != 0, "requires unprivileged Linux with /usr/bin/ssh-keygen")
class HostilePortTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.root = Path(self.tmp.name)
        key = self.root / "k"
        subprocess.run(["/usr/bin/ssh-keygen", "-q", "-t", "ed25519", "-N", "", "-f", str(key)], check=True)
        self.b64 = Path(str(key) + ".pub").read_text().split()[1]
        self.fp = subprocess.run(["/usr/bin/ssh-keygen", "-l", "-f", str(key) + ".pub"], check=True,
                                 capture_output=True, text=True).stdout.split()[1]
        self.allowed = ('qa525-ops namespaces="%s" ssh-ed25519 %s\n' % (port.NAMESPACE, self.b64)).encode()
        self.pin = "sha256:" + hashlib.sha256(Path("/usr/bin/ssh-keygen").read_bytes()).hexdigest()
        (self.root / "m").write_bytes(MSG)
        subprocess.run(["/usr/bin/ssh-keygen", "-Y", "sign", "-q", "-f", str(key), "-n", port.NAMESPACE,
                        str(self.root / "m")], check=True, capture_output=True)
        self.sig = (self.root / "m.sig").read_bytes()

    def verifier(self, allowed="default"):
        return port.verifier_for(trusted_binary_sha256=self.pin, allowed_signers=self.allowed if allowed == "default" else allowed,
                                 expected_fingerprint=self.fp)

    def stop(self, code, fn, *a):
        with self.assertRaises(port.Stop) as err:
            fn(*a)
        self.assertEqual(str(err.exception), code)

    def test_signer_line_grammar_rejects_variants(self):
        good = self.allowed.decode()
        bad = [
            good + good,  # two signers
            good.replace("qa525-ops", "other"),
            good.replace('namespaces="%s"' % port.NAMESPACE, 'cert-authority namespaces="%s"' % port.NAMESPACE),
            good.replace('namespaces="%s"' % port.NAMESPACE, 'namespaces="*"'),
            good.replace("\n", "\r\n"),
            good.rstrip("\n"),
            good.replace("\n", " # c\n"),
            good.replace("ssh-ed25519", "ssh-rsa"),
            good.replace("\n", "\x00\n"),
        ]
        for text in bad:
            try:
                data = text.encode("ascii")
            except UnicodeError:
                continue
            with self.subTest(text=repr(text[:200])):
                with self.assertRaises(port.Stop):
                    self.verifier(data)
        for data in ("é".encode(), "", b"", b"x" * 513, bytearray(self.allowed), None):
            with self.assertRaises(port.Stop):
                self.verifier(data)

    def test_no_fd_or_tempdir_leak_across_accept_and_every_refusal(self):
        verify = self.verifier()
        fds, dirs = _fds(), _tmp_dirs()
        self.assertTrue(verify(MSG, self.sig, self.fp))
        self.assertFalse(verify(MSG + b"x", self.sig, self.fp))
        flipped = bytearray(self.sig)
        flipped[len(port.SIG_BEGIN) + 10] ^= 1
        self.assertFalse(verify(MSG, bytes(flipped), self.fp))
        for args in ((b"", self.sig, self.fp), (MSG, b"junk", self.fp), (MSG, self.sig, "SHA256:" + "A" * 43),
                     (b"\x00" * 65537, self.sig, self.fp), ("str", self.sig, self.fp)):
            with self.assertRaises(port.Stop):
                verify(*args)
        self.assertEqual(_fds(), fds)
        self.assertEqual(_tmp_dirs(), dirs)

    def test_message_with_nul_and_signature_with_trailing_data(self):
        verify = self.verifier()
        self.assertFalse(verify(MSG + b"\x00", self.sig, self.fp))
        with self.assertRaises(port.Stop):
            verify(MSG, self.sig + b"extra", self.fp)
        with self.assertRaises(port.Stop):
            verify(MSG, self.sig + b"\n", self.fp)  # must end exactly at END marker

    def test_timeout_and_exec_failure_are_opaque_and_clean(self):
        import unittest.mock as um
        verify = self.verifier()
        dirs, fds = _tmp_dirs(), _fds()
        with um.patch.object(port.subprocess, "run", side_effect=port.subprocess.TimeoutExpired("x", 30)):
            self.stop("GO_PORT_EXEC_DENIED", verify, MSG, self.sig, self.fp)
        with um.patch.object(port.subprocess, "run", side_effect=PermissionError):
            self.stop("GO_PORT_EXEC_DENIED", verify, MSG, self.sig, self.fp)
        self.assertEqual((_tmp_dirs(), _fds()), (dirs, fds))

    def test_binary_swapped_between_checks_is_refused(self):
        import unittest.mock as um
        verify = self.verifier()
        calls = {"n": 0}
        real = port._binary_sha

        def flaky():
            calls["n"] += 1
            return real() if calls["n"] < 2 else "sha256:" + "0" * 64
        with um.patch.object(port, "_binary_sha", flaky):
            self.stop("GO_PORT_BINARY_CHANGED", verify, MSG, self.sig, self.fp)


if __name__ == "__main__":
    unittest.main()
