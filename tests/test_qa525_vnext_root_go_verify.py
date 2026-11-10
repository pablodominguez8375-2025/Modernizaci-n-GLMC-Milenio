"""QA525 root GO verifier: real ssh-keygen; the binary is pinned and executed through its verified fd."""
import hashlib
import importlib.util
import os
from pathlib import Path
import shutil
import stat
import subprocess
import tempfile
import unittest
import unittest.mock

SRC = Path(__file__).resolve().parents[1] / "scripts/qa/qa525-vnext-root-go-verify.py"
KEYGEN = "/usr/bin/ssh-keygen"
UID = os.getuid()
NS = "qa525-go-cut-v1@pmgm"
GO = b'{"schema":"qa525-go-launcher-v1","nonce":"x"}\n'


OPS_NS = "qa525-ops-v2@pmgm"
OPS_SIG_SRC = Path(__file__).resolve().parents[1] / "scripts/qa/qa525-vnext-ops-signature.py"
DIG = lambda c: "sha256:" + c * 64
POLICY = DIG("4")
ARCHIVE = DIG("8")
RECEIPT = ('{"app_archive_sha256":"%s","policy_hash":"%s","program_sha256":"%s","readiness_sha256":"%s",'
           '"schema":"qa525-ops-v3","staging_sha256":"%s","status":"OPS_SIGNED_VERIFIED",'
           '"ops_source_sha":"%s"}' % (ARCHIVE, POLICY, DIG("1"), DIG("2"), DIG("3"), "a" * 40)).encode()


def load_ops_signature():
    spec = importlib.util.spec_from_file_location("qa525_ops_sig_test", OPS_SIG_SRC)
    m = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(m)
    return m


def load():
    spec = importlib.util.spec_from_file_location("qa525_gorv_test", SRC)
    m = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(m)
    return m


def run(*argv, **kw):
    return subprocess.run(argv, check=True, capture_output=True, **kw)


def sha(path):
    return "sha256:" + hashlib.sha256(Path(path).read_bytes()).hexdigest()


@unittest.skipUnless(os.path.exists(KEYGEN), "ssh-keygen not installed")
class RootGoVerifyTests(unittest.TestCase):
    def setUp(self):
        self.m = load()
        self.dir = tempfile.mkdtemp(prefix="gorv")
        self.addCleanup(shutil.rmtree, self.dir, True)
        os.chmod(self.dir, 0o700)
        self.tmp = os.path.join(self.dir, "t")
        os.mkdir(self.tmp, 0o700)
        self.bin = os.path.join(self.dir, "ssh-keygen")  # owned by the test uid: stands for the OS owner
        shutil.copy(KEYGEN, self.bin)
        os.chmod(self.bin, 0o755)
        self.key, self.other = self.keypair("k1"), self.keypair("k2")
        self.kw = dict(keygen_path=self.bin, trusted_binary_sha256=sha(self.bin),
                       allowed_signers=self.key["allowed"], expected_fingerprint=self.key["fp"],
                       temp_root=self.tmp, required_euid=UID, os_uid=UID)

    def keypair(self, name):
        path = os.path.join(self.dir, name)
        run(KEYGEN, "-q", "-t", "ed25519", "-N", "", "-f", path)
        pub = Path(path + ".pub").read_text().split()
        fp = run(KEYGEN, "-lf", path + ".pub").stdout.decode().split()[1]
        return {"path": path, "fp": fp,
                "allowed": ('qa525-ops namespaces="%s" ssh-ed25519 %s\n' % (NS, pub[1])).encode()}

    def sign(self, key, data=GO, ns=NS):
        doc = os.path.join(self.dir, "doc")
        Path(doc).write_bytes(data)
        if os.path.exists(doc + ".sig"):
            os.unlink(doc + ".sig")
        run(KEYGEN, "-Y", "sign", "-f", key["path"], "-n", ns, doc)
        return Path(doc + ".sig").read_bytes()

    def verifier(self, **over):
        return self.m.verifier_for(**dict(self.kw, **over))

    def stop(self, code, fn, *a, **kw):
        with self.assertRaises(self.m.Stop) as ctx:
            fn(*a, **kw)
        self.assertEqual(str(ctx.exception), code)

    def test_good_signature_verifies_and_leaves_no_temp_files(self):
        v = self.verifier()
        self.assertIs(v(GO, self.sign(self.key), self.key["fp"]), True)
        self.assertEqual(os.listdir(self.tmp), [])

    def test_tampered_document_wrong_namespace_or_other_signer_is_false(self):
        v = self.verifier()
        sig = self.sign(self.key)
        self.assertIs(v(GO + b" ", sig, self.key["fp"]), False)
        self.assertIs(v(GO, self.sign(self.key, ns="qa525-ops-v2@pmgm"), self.key["fp"]), False)
        self.assertIs(v(GO, self.sign(self.other), self.key["fp"]), False)

    def test_fingerprint_must_be_the_external_pin_and_signers_exactly_one_scoped_line(self):
        self.stop("GORV_FINGERPRINT_PIN_MISMATCH", self.verifier, expected_fingerprint=self.other["fp"])
        self.stop("GORV_FINGERPRINT_PIN_INVALID", self.verifier, expected_fingerprint="nope")
        self.stop("GORV_SIGNERS_INVALID", self.verifier, allowed_signers=self.key["allowed"] * 2)
        self.stop("GORV_SIGNERS_INVALID", self.verifier,
                  allowed_signers=self.key["allowed"].replace(b"qa525-ops", b"someone"))
        self.stop("GORV_SIGNERS_INVALID", self.verifier,
                  allowed_signers=self.key["allowed"].replace(NS.encode(), b"*"))
        v = self.verifier()
        self.stop("GORV_INPUT_INVALID", v, GO, self.sign(self.key), self.other["fp"])  # caller cannot repin

    def test_binary_pin_is_checked_at_every_call_and_a_swap_after_build_is_refused(self):
        self.stop("GORV_BINARY_PIN_INVALID", self.verifier, trusted_binary_sha256="sha256:zz")
        v = self.verifier()
        self.assertIs(v(GO, self.sign(self.key), self.key["fp"]), True)
        os.unlink(self.bin)
        Path(self.bin).write_bytes(b"#!/bin/sh\necho Good\n")
        os.chmod(self.bin, 0o755)
        self.stop("GORV_BINARY_PIN_MISMATCH", v, GO, self.sign(self.key), self.key["fp"])
        self.stop("GORV_BINARY_PIN_MISMATCH", self.verifier(), GO, self.sign(self.key), self.key["fp"])

    def test_untrusted_binary_writable_symlinked_hardlinked_foreign_owner_relative(self):
        v = self.verifier()
        sig = self.sign(self.key)
        os.chmod(self.bin, 0o775)
        self.stop("GORV_BINARY_UNTRUSTED", v, GO, sig, self.key["fp"])
        os.chmod(self.bin, 0o755)
        os.link(self.bin, self.bin + ".l")
        self.stop("GORV_BINARY_UNTRUSTED", v, GO, sig, self.key["fp"])
        os.unlink(self.bin + ".l")
        self.assertIs(v(GO, sig, self.key["fp"]), True)
        link = os.path.join(self.dir, "kg-link")
        os.symlink(self.bin, link)
        self.stop("GORV_BINARY_UNAVAILABLE", self.verifier(keygen_path=link), GO, sig, self.key["fp"])
        if UID == 0:  # a root-run test can give the binary a foreign owner
            os.chown(self.bin, 4242, 4242)
            self.stop("GORV_BINARY_UNTRUSTED", v, GO, sig, self.key["fp"])
            os.chown(self.bin, 0, 0)
        self.stop("GORV_BINARY_PATH_INVALID", self.verifier(keygen_path="ssh-keygen"), GO, sig, self.key["fp"])

    def test_needs_the_required_euid_and_a_private_temp_root(self):
        self.stop("GORV_ROOT_REQUIRED", self.verifier, required_euid=UID + 1)
        self.stop("GORV_TEMP_ROOT_UNSAFE", self.verifier, temp_root=os.path.join(self.dir, "missing"))
        os.chmod(self.tmp, 0o770)
        self.stop("GORV_TEMP_ROOT_UNSAFE", self.verifier)
        os.chmod(self.tmp, 0o700)
        self.stop("GORV_TEMP_ROOT_UNSAFE", self.verifier, os_uid=UID + 1)

    def test_child_gets_empty_env_fixed_cwd_no_shell_and_the_verified_fd(self):
        v = self.verifier()
        sig = self.sign(self.key)
        seen = {}
        real = subprocess.run

        def spy(argv, **kw):
            seen.update(kw, argv=argv)
            return real(argv, **kw)
        with unittest.mock.patch.object(self.m.subprocess, "run", spy):
            self.assertIs(v(GO, sig, self.key["fp"]), True)
        self.assertEqual(seen["env"], {})
        self.assertIs(seen["shell"], False)
        self.assertTrue(seen["executable"].startswith("/proc/self/fd/"))
        self.assertEqual(seen["input"], GO)
        self.assertTrue(seen["cwd"].startswith(self.tmp + os.sep))
        self.assertEqual(seen["argv"][:3], ["ssh-keygen", "-Y", "verify"])
        self.assertIn(NS, seen["argv"])

    def test_oversized_child_output_or_a_failing_child_is_not_success(self):
        Path(self.bin).unlink()
        Path(self.bin).write_bytes(b"#!/bin/sh\nhead -c 5000 /dev/zero\n")
        os.chmod(self.bin, 0o755)
        v = self.verifier(trusted_binary_sha256=sha(self.bin))
        self.stop("GORV_CHILD_RESULT_INVALID", v, GO, self.sign(self.key), self.key["fp"])
        Path(self.bin).unlink()
        Path(self.bin).write_bytes(b"#!/bin/sh\nexit 1\n")
        os.chmod(self.bin, 0o755)
        v = self.verifier(trusted_binary_sha256=sha(self.bin))
        self.assertIs(v(GO, self.sign(self.key), self.key["fp"]), False)

    def test_malformed_inputs_are_refused_before_any_process(self):
        v = self.verifier()
        sig = self.sign(self.key)
        with unittest.mock.patch.object(self.m.subprocess, "run") as boom:
            for args in ((b"", sig, self.key["fp"]), (GO, b"junk", self.key["fp"]),
                         (GO, sig[:-1], self.key["fp"]), ("x", sig, self.key["fp"]),
                         (b"x" * (self.m.MAX_DOCUMENT + 1), sig, self.key["fp"])):
                self.stop("GORV_INPUT_INVALID", v, *args)
            boom.assert_not_called()

    def test_a_temp_dir_that_cannot_be_removed_stops_instead_of_returning_a_verdict(self):
        v = self.verifier()
        sig = self.sign(self.key)
        with unittest.mock.patch.object(self.m.shutil, "rmtree", side_effect=OSError("busy")):
            self.stop("GORV_TEMP_CLEANUP_FAILED", v, GO, sig, self.key["fp"])

    def test_production_code_has_no_runner_injection_or_shell(self):
        import inspect
        self.assertNotIn("runner", inspect.signature(self.m.verifier_for).parameters)
        src = SRC.read_text()
        self.assertNotIn("shell=True", src)
        self.assertNotIn("os.system", src)




@unittest.skipUnless(os.path.exists(KEYGEN), "ssh-keygen not installed")
class RootOpsPortTests(RootGoVerifyTests):
    """The OPS-receipt port: same pinned ssh-keygen runner, DISTINCT namespace, fed to verify_ops_receipt."""

    def setUp(self):
        super().setUp()
        self.ops = load_ops_signature()
        pub = Path(self.key["path"] + ".pub").read_text().split()
        self.ops_allowed = ('qa525-ops namespaces="%s" ssh-ed25519 %s\n' % (OPS_NS, pub[1])).encode()
        self.okw = dict(self.kw, allowed_signers=self.ops_allowed)

    def port(self, **over):
        return self.m.ops_port_for(**dict(self.okw, **over))

    def verify(self, receipt=RECEIPT, signature=None, **over):
        kw = dict(receipt=receipt, signature=signature or self.sign(self.key, RECEIPT, ns=OPS_NS),
                  allowed_signers=self.ops_allowed, ops_fingerprint=self.key["fp"],
                  app_fingerprints=frozenset({self.other["fp"]}), policy_hash=POLICY,
                  verifier=self.port(), expected_app_archive_sha256=ARCHIVE)
        kw.update(over)
        return self.ops.verify_ops_receipt(**kw)

    def test_signed_v3_receipt_verifies_end_to_end_with_the_real_contract(self):
        out = self.verify()
        self.assertEqual((out["signature_verified_here"], out["may_apply"], out["app_archive_bound"]),
                         (True, False, True))
        self.assertEqual(os.listdir(self.tmp), [])

    def test_go_namespace_signature_is_not_an_ops_signature_and_vice_versa(self):
        with self.assertRaises(self.ops.Stop) as err:
            self.verify(signature=self.sign(self.key, RECEIPT, ns=NS))
        self.assertEqual(str(err.exception), "OPSSIG_NOT_VERIFIED")
        v = self.verifier()  # the GO callback must not accept an OPS-namespace signature either
        self.assertIs(v(GO, self.sign(self.key, GO, ns=OPS_NS), self.key["fp"]), False)

    def test_tampered_receipt_or_other_signer_is_refused(self):
        sig = self.sign(self.key, RECEIPT, ns=OPS_NS)
        for kw in (dict(receipt=RECEIPT.replace(b"a" * 40, b"b" * 40), signature=sig),
                   dict(signature=self.sign(self.other, RECEIPT, ns=OPS_NS))):
            with self.assertRaises(self.ops.Stop) as err:
                self.verify(**kw)
            self.assertEqual(str(err.exception), "OPSSIG_NOT_VERIFIED")

    def test_caller_cannot_choose_signers_principal_or_namespace(self):
        port = self.port()
        sig = self.sign(self.key, RECEIPT, ns=OPS_NS)
        for args in ((self.other["allowed"], sig, "qa525-ops", OPS_NS, RECEIPT),
                     (self.ops_allowed, sig, "someone", OPS_NS, RECEIPT),
                     (self.ops_allowed, sig, "qa525-ops", NS, RECEIPT),
                     ("text", sig, "qa525-ops", OPS_NS, RECEIPT)):
            self.stop("GORV_INPUT_INVALID", port, *args)

    def test_pins_are_enforced_for_the_ops_namespace(self):
        self.stop("GORV_FINGERPRINT_PIN_MISMATCH", self.port, expected_fingerprint=self.other["fp"])
        self.stop("GORV_SIGNERS_INVALID", self.port, allowed_signers=self.key["allowed"])  # GO-namespace line
        self.stop("GORV_BINARY_PIN_MISMATCH", self.port(trusted_binary_sha256=DIG("0")), self.ops_allowed,
                  self.sign(self.key, RECEIPT, ns=OPS_NS), "qa525-ops", OPS_NS, RECEIPT)
        self.stop("GORV_ROOT_REQUIRED", self.port, required_euid=UID + 1)
        self.stop("GORV_TEMP_ROOT_UNSAFE", self.port, os_uid=UID + 1)

    def test_malformed_signature_never_reaches_ssh_keygen(self):
        port = self.port()
        for sig in (b"", b"junk", b"-----BEGIN SSH SIGNATURE-----\n"):
            self.stop("GORV_INPUT_INVALID", port, self.ops_allowed, sig, "qa525-ops", OPS_NS, RECEIPT)
        self.assertEqual(os.listdir(self.tmp), [])


for _name in dir(RootGoVerifyTests):  # inherit only the fixtures: the GO tests run once, in their own class
    if _name.startswith("test_") and _name not in RootOpsPortTests.__dict__:
        setattr(RootOpsPortTests, _name, None)


if __name__ == "__main__":
    unittest.main()
