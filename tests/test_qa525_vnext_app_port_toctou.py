"""QA525 APP rootless port: anti-TOCTOU properties of the verified sealed FD.

Hermetic, fake GH runner (NOT Sigstore proof). These tests pin what a future authenticated
pre-root bootstrap (#936/#822) may rely on: after verification the exact bytes live in a sealed,
non-inheritable, one-shot memfd that no path or file swap can change. They authorize nothing.
"""
import errno
import fcntl
import hashlib
import importlib.util
import os
from pathlib import Path
import subprocess
import sys
import tempfile
from types import SimpleNamespace
import unittest
from unittest import mock

SRC = Path(__file__).resolve().parents[1] / "scripts/qa/qa525-vnext-app-rootless-port.py"
spec = importlib.util.spec_from_file_location("qa525_app_port_toctou_under_test", SRC)
app = importlib.util.module_from_spec(spec)
spec.loader.exec_module(app)

INNER = b"TOCTOU TEST INNER ZIP BYTES - NOT A GENUINE SIGNED ARTIFACT"
EVIL = b"EVIL REPLACEMENT BYTES THAT MUST NEVER BE USED AFTER VERIFICATION"
BUNDLE = b'{"synthetic":"not-a-Sigstore-attestation"}'
APP_SHA = "a" * 40
BINARY_PIN = "sha256:" + "f" * 64
SEALS = ("F_ADD_SEALS", "F_GET_SEALS", "F_SEAL_WRITE", "F_SEAL_SEAL", "F_SEAL_SHRINK",
         "F_SEAL_GROW")
HAVE_SEALS = hasattr(os, "memfd_create") and all(hasattr(fcntl, n) for n in SEALS)


def digest(data):
    return "sha256:" + hashlib.sha256(data).hexdigest()


@unittest.skipIf(os.geteuid() == 0, "root is prohibited: run as ordinary CI user")
@unittest.skipUnless(HAVE_SEALS, "host Python lacks memfd sealing constants")
class AppPortToctouTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.private = Path(self.tmp.name, "private")
        self.private.mkdir(mode=0o700)
        self.path = self.private / "inner.zip"
        self.path.write_bytes(INNER)
        self.path.chmod(0o600)
        self.challenge = {
            "repository": app.REPO, "signer_workflow": app.WORKFLOW,
            "oidc_issuer": app.ISSUER, "source_sha": APP_SHA,
            "archive_sha256": digest(INNER), "bundle_sha256": digest(BUNDLE)}
        self.fds = []
        self.addCleanup(self.close_all)

    def close_all(self):
        for fd in self.fds:
            try:
                os.close(fd)
            except OSError:
                pass

    def verified(self, on_run=None):
        ver = app.verifier_for(
            signed_inner_zip=self.path, trusted_gh_sha256=BINARY_PIN,
            expected_source_sha=APP_SHA, expected_inner_sha256=digest(INNER),
            expected_bundle_sha256=digest(BUNDLE))

        def runner(argv, **kwargs):
            if on_run:
                on_run(argv)
            return SimpleNamespace(returncode=0)

        with mock.patch.object(app, "_check_binary", return_value=None), \
                mock.patch.object(app.subprocess, "run", side_effect=runner):
            self.assertTrue(ver(BUNDLE, dict(self.challenge)))
        return ver

    def take(self, ver):
        fd, sha = ver.take_verified_fd()
        self.fds.append(fd)
        self.assertEqual(sha, digest(INNER))
        return fd

    def read_all(self, fd):
        size = os.fstat(fd).st_size
        return os.pread(fd, size, 0)

    def test_swapping_the_path_after_verification_does_not_change_the_fd_bytes(self):
        ver = self.verified()
        replacement = self.private / "other.zip"
        replacement.write_bytes(EVIL)
        os.replace(replacement, self.path)             # atomic path swap
        fd = self.take(ver)
        self.assertEqual(self.read_all(fd), INNER)

    def test_rewriting_or_deleting_the_source_in_place_does_not_change_the_fd_bytes(self):
        ver = self.verified()
        self.path.write_bytes(EVIL)
        fd = self.take(ver)
        self.assertEqual(self.read_all(fd), INNER)
        self.path.unlink()
        self.assertEqual(self.read_all(fd), INNER)

    def test_replacing_the_private_directory_with_a_symlink_does_not_affect_the_fd(self):
        ver = self.verified()
        attacker = Path(self.tmp.name, "attacker")
        attacker.mkdir(mode=0o700)
        Path(attacker, "inner.zip").write_bytes(EVIL)
        self.path.unlink()
        self.private.rmdir()
        os.symlink(attacker, self.private)
        fd = self.take(ver)
        self.assertEqual(self.read_all(fd), INNER)

    def test_the_fd_is_sealed_against_write_truncate_and_growth(self):
        fd = self.take(self.verified())
        for attempt in (lambda: os.pwrite(fd, b"X", 0),
                        lambda: os.ftruncate(fd, 1),
                        lambda: os.ftruncate(fd, 4096),
                        lambda: os.pwrite(fd, b"X", os.fstat(fd).st_size + 10)):
            with self.assertRaises(OSError) as err:
                attempt()
            self.assertIn(err.exception.errno, (errno.EPERM, errno.EACCES))
        self.assertEqual(self.read_all(fd), INNER)
        want = (fcntl.F_SEAL_WRITE | fcntl.F_SEAL_SEAL | fcntl.F_SEAL_SHRINK
                | fcntl.F_SEAL_GROW)
        self.assertEqual(fcntl.fcntl(fd, fcntl.F_GET_SEALS) & want, want)
        with self.assertRaises(OSError):                 # SEAL_SEAL: seals can not be removed
            fcntl.fcntl(fd, fcntl.F_ADD_SEALS, 0)

    def test_the_fd_is_one_shot_and_not_retained_by_the_verifier(self):
        ver = self.verified()
        self.take(ver)
        with self.assertRaises(app.Stop) as err:
            ver.take_verified_fd()
        self.assertEqual(str(err.exception), "APP_PORT_VERIFIED_FD_UNAVAILABLE")

    def test_the_fd_is_not_inheritable_and_a_child_only_gets_it_through_pass_fds(self):
        fd = self.take(self.verified())
        self.assertFalse(os.get_inheritable(fd))
        code = ("import os,sys;fd=int(sys.argv[1]);"
                "print(os.pread(fd,64,0).decode('ascii'))")
        plain = subprocess.run([sys.executable, "-I", "-c", code, str(fd)],
                               capture_output=True, text=True)
        self.assertNotEqual(plain.returncode, 0)        # closed in the child by default
        passed = subprocess.run([sys.executable, "-I", "-c", code, str(fd)],
                                capture_output=True, text=True, pass_fds=(fd,))
        self.assertEqual(passed.returncode, 0, passed.stderr)
        self.assertEqual(passed.stdout.strip(), INNER.decode("ascii"))

    def test_a_child_that_is_given_the_fd_still_cannot_modify_it(self):
        fd = self.take(self.verified())
        code = ("import os,sys;fd=int(sys.argv[1])\n"
                "try:\n os.pwrite(fd,b'X',0);print('WROTE')\n"
                "except OSError as e:\n print('DENIED')\n")
        out = subprocess.run([sys.executable, "-I", "-c", code, str(fd)],
                             capture_output=True, text=True, pass_fds=(fd,))
        self.assertEqual(out.stdout.strip(), "DENIED")
        self.assertEqual(self.read_all(fd), INNER)

    def test_an_unsealed_memfd_with_identical_bytes_is_not_accepted(self):
        loose = os.memfd_create("loose", os.MFD_CLOEXEC | os.MFD_ALLOW_SEALING)
        self.fds.append(loose)
        os.write(loose, INNER)
        with self.assertRaises(app.Stop) as err:
            app._recheck_immutable_fd(loose, digest(INNER))
        self.assertEqual(str(err.exception), "APP_PORT_IMMUTABLE_FD_INVALID")

    def test_a_sealed_memfd_with_other_bytes_is_not_accepted(self):
        other = os.memfd_create("other", os.MFD_CLOEXEC | os.MFD_ALLOW_SEALING)
        self.fds.append(other)
        os.write(other, EVIL)
        fcntl.fcntl(other, fcntl.F_ADD_SEALS, fcntl.F_SEAL_WRITE | fcntl.F_SEAL_SEAL
                    | fcntl.F_SEAL_SHRINK | fcntl.F_SEAL_GROW)
        with self.assertRaises(app.Stop) as err:
            app._recheck_immutable_fd(other, digest(INNER))
        self.assertEqual(str(err.exception), "APP_PORT_IMMUTABLE_FD_INVALID")

    def test_a_source_swapped_while_the_verifier_runs_never_yields_the_swapped_bytes(self):
        swapped = []

        def swap(argv):
            swapped.append(True)
            replacement = self.private / "swap.zip"
            replacement.write_bytes(EVIL)
            os.replace(replacement, self.path)
        ver = self.verified(on_run=swap)
        self.assertEqual(swapped, [True])               # the swap really happened mid-run
        fd = self.take(ver)
        self.assertEqual(self.read_all(fd), INNER)

    def test_the_fd_content_hash_is_the_pinned_one_not_a_path(self):
        fd = self.take(self.verified())
        self.assertEqual(digest(self.read_all(fd)), digest(INNER))
        self.assertTrue(os.path.islink("/proc/self/fd/%d" % fd))
        self.assertNotIn(str(self.private), os.readlink("/proc/self/fd/%d" % fd))


if __name__ == "__main__":
    unittest.main()
