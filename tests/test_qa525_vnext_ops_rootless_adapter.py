"""QA525 rootless OPS verifier: actual ssh-keygen round trips and pinned port."""
import hashlib
import importlib.util
import os
from pathlib import Path
import shutil
import stat
import subprocess
import tempfile
from types import SimpleNamespace
import unittest
from unittest import mock

import test_qa525_vnext_ops_signature as fixture

SOURCE = Path(__file__).resolve().parents[1] / "scripts/qa/qa525-vnext-ops-rootless-adapter.py"
spec = importlib.util.spec_from_file_location("qa525_ops_rootless_port_test", SOURCE)
port = importlib.util.module_from_spec(spec)
spec.loader.exec_module(port)
KEYGEN = shutil.which("ssh-keygen")


@unittest.skipUnless(KEYGEN and Path("/usr/bin/ssh-keygen").exists() and os.geteuid() != 0,
                     "requires unprivileged Linux with OpenSSH")
class RootlessPort(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.key, self.allowed, self.fp = fixture.make_key(self.temp.name)
        self.receipt = fixture.RECEIPT
        self.signature = fixture.sign(self.key, self.receipt)
        self.pin = "sha256:" + hashlib.sha256(Path("/usr/bin/ssh-keygen").read_bytes()).hexdigest()
        self.app_fingerprints = fixture.APP

    def make(self, **kwargs):
        return port.verifier_for(trusted_binary_sha256=self.pin, **kwargs)

    def signed(self, **changes):
        args = dict(receipt=self.receipt, signature=self.signature,
                    allowed_signers=self.allowed, ops_fingerprint=self.fp,
                    app_fingerprints=self.app_fingerprints, policy_hash=fixture.POLICY,
                    verifier=self.make())
        args.update(changes)
        return fixture.sig.verify_ops_receipt(**args)

    def test_real_signed_ssh_receipt_passes_with_no_host_authority(self):
        result = self.signed()
        self.assertEqual(result["receipt"], fixture.FIELDS)
        self.assertIs(result["signature_verified_here"], True)
        self.assertIs(result["may_apply"], False)
        self.assertIs(result["fingerprint_provenance_verified_here"], False)

    def test_changed_receipt_other_namespace_and_different_key_refused(self):
        for sig, content in (
                (self.signature, self.receipt + b"changed"),
                (fixture.sign(self.key, self.receipt, namespace="wrong"), self.receipt)):
            with self.subTest():
                with self.assertRaises(fixture.sig.Stop):
                    self.signed(signature=sig, receipt=content)
        other, _, _ = fixture.make_key(self.temp.name, "other")
        with self.assertRaises(fixture.sig.Stop):
            self.signed(signature=fixture.sign(other, self.receipt))

    def test_wrong_binary_pin_and_root_process_are_refused_before_child(self):
        with self.assertRaisesRegex(port.Stop, "OPS_PORT_BINARY_PIN_MISMATCH"):
            port.verifier_for(trusted_binary_sha256="sha256:" + "0" * 64)
        with mock.patch.object(port.os, "geteuid", return_value=0):
            with self.assertRaisesRegex(port.Stop, "OPS_PORT_ROOT_FORBIDDEN"):
                self.make()

    def test_fixed_argv_private_modes_and_no_shell_through_injected_runner(self):
        received = []
        expected = ('Good "%s" signature for %s with ED25519 key %s\n'
                    % (fixture.sig.NAMESPACE, fixture.sig.PRINCIPAL, self.fp)).encode()

        def fake(command, **kwargs):
            received.append((list(command), kwargs))
            self.assertEqual(command[0], "/usr/bin/ssh-keygen")
            self.assertEqual(command[1:5], ["-Y", "verify", "-f", command[4]])
            self.assertEqual(command[5:8], ["-I", "qa525-ops", "-n"])
            self.assertEqual(command[8], "qa525-ops-v2@pmgm")
            self.assertEqual(command[9], "-s")
            self.assertEqual(kwargs["input"], self.receipt)
            self.assertEqual(kwargs["env"], {"PATH": "/usr/bin:/bin", "LC_ALL": "C"})
            self.assertEqual(kwargs["timeout"], 30)
            self.assertEqual(kwargs["check"], False)
            self.assertNotIn("shell", kwargs)
            self.assertEqual(stat.S_IMODE(Path(kwargs["cwd"]).stat().st_mode), 0o700)
            for file in (Path(command[4]), Path(command[10])):
                self.assertEqual(stat.S_IMODE(file.stat().st_mode), 0o600)
                self.assertEqual(file.stat().st_nlink, 1)
            kwargs["stdout"].write(expected)
            return SimpleNamespace(returncode=0, stdout=None)

        actual = self.make(runner=fake)(self.allowed, self.signature,
                                         fixture.sig.PRINCIPAL, fixture.sig.NAMESPACE,
                                         self.receipt)
        self.assertEqual(actual, (0, expected))
        self.assertEqual(len(received), 1)
        self.assertFalse(Path(received[0][1]["cwd"]).exists())

    def test_parent_directory_trust_checks_reject_unprivileged_paths(self):
        # A caller cannot point the verifier at a user-owned executable even
        # if its file hash was copied into a claimed pin.
        with mock.patch.object(port, "SSH_KEYGEN",
                               Path(self.temp.name) / "ssh-keygen"):
            with self.assertRaisesRegex(port.Stop, "OPS_PORT_PARENT_UNTRUSTED"):
                self.make()

    def test_oversize_stdout_is_refused_without_an_unbounded_pipe(self):
        def noisy(_argv, **kwargs):
            kwargs["stdout"].write(b"A" * 1025)
            return SimpleNamespace(returncode=0, stdout=None)
        verify = self.make(runner=noisy)
        with self.assertRaisesRegex(port.Stop, "OPS_PORT_CHILD_RESULT_INVALID"):
            verify(self.allowed, self.signature,
                   fixture.sig.PRINCIPAL, fixture.sig.NAMESPACE,
                   self.receipt)

    def test_runner_timeout_exception_and_bad_result_are_fixed_stops(self):
        def fail(*args, **kwargs):
            raise subprocess.TimeoutExpired(args[0], 30)
        with self.assertRaisesRegex(port.Stop, "OPS_PORT_CHILD_FAILED"):
            self.make(runner=fail)(self.allowed, self.signature,
                                   fixture.sig.PRINCIPAL, fixture.sig.NAMESPACE,
                                   self.receipt)
        with self.assertRaisesRegex(port.Stop, "OPS_PORT_CHILD_RESULT_INVALID"):
            self.make(runner=lambda *a, **k: SimpleNamespace(
                returncode=True, stdout=b"good"))(
                    self.allowed, self.signature, fixture.sig.PRINCIPAL,
                    fixture.sig.NAMESPACE, self.receipt)

    def test_inputs_cannot_supply_arbitrary_principal_namespace_or_root(self):
        verify = self.make()
        for principal, namespace in ((1, fixture.sig.NAMESPACE),
                                     ("root", fixture.sig.NAMESPACE),
                                     (fixture.sig.PRINCIPAL, "wrong")):
            with self.subTest(principal=principal):
                with self.assertRaisesRegex(port.Stop, "OPS_PORT_PAYLOAD_INVALID"):
                    verify(self.allowed, self.signature, principal, namespace,
                           self.receipt)
        with self.assertRaisesRegex(port.Stop, "OPS_PORT_INPUT_INVALID"):
            port.verifier_for(trusted_binary_sha256=self.pin, runner=None)
        with mock.patch.object(port.os, "geteuid", return_value=0):
            with self.assertRaisesRegex(port.Stop, "OPS_PORT_ROOT_FORBIDDEN"):
                verify(self.allowed, self.signature, fixture.sig.PRINCIPAL,
                       fixture.sig.NAMESPACE, self.receipt)


if __name__ == "__main__":
    unittest.main()
