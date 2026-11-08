"""QA525 real OpenSSH GO verification (throwaway keys): NO Sponsor/host authority."""
import datetime as dt
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
from types import SimpleNamespace
import unittest
from unittest import mock

ROOT = Path(__file__).resolve().parents[1] / "scripts" / "qa"


def load(name, file):
    spec = importlib.util.spec_from_file_location(name, ROOT / file)
    obj = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(obj)
    return obj


port = load("qa525_test_go_port", "qa525-vnext-go-rootless-adapter.py")
go = load("qa525_test_go_cut", "qa525-vnext-go-cut-contract.py")
SHEET = load("qa525_test_go_sheet", "qa525-vnext-sponsor-signing.py")
SUPPORTED = shutil.which("ssh-keygen") and Path("/usr/bin/ssh-keygen").is_file()
D = lambda v: "sha256:" + v * 64


@unittest.skipUnless(SUPPORTED and os.geteuid() != 0,
                     "requires unprivileged Linux with /usr/bin/ssh-keygen")
class GoPortRealSignatureTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.root = Path(self.tmp.name)
        self.key = self.root / "signer"
        subprocess.run(["/usr/bin/ssh-keygen", "-q", "-t", "ed25519", "-N", "",
                        "-C", "synthetic-no-go", "-f", str(self.key)], check=True)
        pub = Path(str(self.key) + ".pub").read_text().split()
        self.fp = subprocess.run(["/usr/bin/ssh-keygen", "-l", "-f", str(self.key) + ".pub"],
                                 check=True, capture_output=True, text=True).stdout.split()[1]
        self.allowed = ('qa525-ops namespaces="%s" ssh-ed25519 %s\n'
                        % (port.NAMESPACE, pub[1])).encode()
        self.pin = "sha256:" + hashlib.sha256(
            Path("/usr/bin/ssh-keygen").read_bytes()).hexdigest()
        self.go_record = SHEET.build_go_cut(
            nonce="N" * 43, ops_source_sha="a" * 40, app_source_sha="b" * 40,
            app_archive_sha256=D("3"), plan_sha256=D("4"),
            admission_sha256=D("5"), policy_hash=D("6"),
            issued_at="2026-11-08T10:00:00Z", expires_at="2026-11-08T10:20:00Z")
        self.signature = self.sign(self.go_record, port.NAMESPACE)

    def sign(self, content, namespace):
        path = self.root / ("message-" + str(len(list(self.root.iterdir()))))
        path.write_bytes(content)
        subprocess.run(["/usr/bin/ssh-keygen", "-Y", "sign", "-q", "-f", str(self.key),
                        "-n", namespace, str(path)], check=True, capture_output=True)
        return Path(str(path) + ".sig").read_bytes()

    def verifier(self, **kwargs):
        params = {"trusted_binary_sha256": self.pin, "allowed_signers": self.allowed}
        params.update(kwargs)
        return port.verifier_for(**params)

    def denied(self, fn, code, *args, **kwargs):
        with self.assertRaises(port.Stop) as caught:
            fn(*args, **kwargs)
        self.assertEqual(str(caught.exception), code)

    def test_real_go_signature_accepts_exact_bytes_and_denies_tampering(self):
        verify = self.verifier()
        self.assertIs(verify(self.go_record, self.signature, self.fp), True)
        self.assertIs(verify(self.go_record + b"\n", self.signature, self.fp), False)
        self.assertIs(verify(self.go_record, self.sign(self.go_record, SHEET.OPS_NAMESPACE),
                             self.fp), False)
        self.denied(verify, "GO_PORT_INPUT_INVALID", self.go_record,
                    self.signature, "SHA256:" + "A" * 43)
        self.denied(verify, "GO_PORT_INPUT_INVALID", self.go_record,
                    b"not-an-ssh-signature", self.fp)

    def test_go_cut_can_use_real_verifier_but_remains_not_authorized(self):
        verify = self.verifier()
        fields = json.loads(self.go_record)
        pins = {k: fields[k] for k in ("ops_source_sha", "app_source_sha",
                "app_archive_sha256", "plan_sha256", "admission_sha256", "policy_hash")}
        pins["go_fingerprint"] = self.fp
        receipt = {
            "schema": "qa525-ops-v3", "status": "OPS_SIGNED_VERIFIED",
            "ops_source_sha": pins["ops_source_sha"],
            "program_sha256": D("1"), "readiness_sha256": D("2"),
            "staging_sha256": D("7"), "policy_hash": pins["policy_hash"],
            "app_archive_sha256": pins["app_archive_sha256"]}
        subject = {
            "source_sha": pins["app_source_sha"],
            "subject_sha256": pins["app_archive_sha256"],
            "signer_workflow": go.APP_WORKFLOW, "oidc_issuer": go.ISSUER}
        now = dt.datetime(2026, 11, 8, 10, 3, tzinfo=dt.timezone.utc)
        ret = go.check_cut(
            go_bytes=self.go_record, go_signature_bytes=self.signature,
            pins=pins, ops_v3_receipt=receipt, app_subject=subject,
            now_utc=now, verify_go_signature=verify,
            nonce_unused=lambda nonce, admission: True)
        self.assertIs(ret["may_apply"], False)
        self.assertIs(ret["may_install"], False)
        self.assertIs(ret["nonce_consumption_durable_here"], False)
        with self.assertRaises(go.Stop) as caught:
            go.check_cut(
                go_bytes=self.go_record, go_signature_bytes=self.signature,
                pins=pins, ops_v3_receipt=receipt, app_subject=subject,
                now_utc=dt.datetime(2026, 11, 8, 10, 21, tzinfo=dt.timezone.utc),
                verify_go_signature=verify, nonce_unused=lambda nonce, admission: True)
        self.assertEqual(str(caught.exception), "GO_CUT_EXPIRED_OR_FUTURE")

    def test_wrong_signer_key_and_namespace_or_bad_binary_refused(self):
        other = self.root / "other"
        subprocess.run(["/usr/bin/ssh-keygen", "-q", "-t", "ed25519", "-N", "",
                        "-f", str(other)], check=True)
        path = self.root / "wrong"
        path.write_bytes(self.go_record)
        subprocess.run(["/usr/bin/ssh-keygen", "-Y", "sign", "-q", "-f", str(other),
                        "-n", port.NAMESPACE, str(path)], check=True, capture_output=True)
        self.assertIs(self.verifier()(self.go_record,
                      Path(str(path) + ".sig").read_bytes(), self.fp), False)
        self.denied(self.verifier, "GO_PORT_BINARY_PIN_MISMATCH",
                    trusted_binary_sha256=D("0"))
        self.denied(self.verifier, "GO_PORT_SIGNERS_INVALID",
                    allowed_signers=self.allowed.replace(
                        port.NAMESPACE.encode(), SHEET.OPS_NAMESPACE.encode()))
        self.denied(self.verifier, "GO_PORT_SIGNERS_INVALID",
                    allowed_signers=self.allowed * 2)

    def test_public_runner_override_rejected_and_root_denied(self):
        with self.assertRaises(TypeError):
            self.verifier(runner=lambda *a, **k: SimpleNamespace(returncode=0))
        with mock.patch.object(port.os, "geteuid", return_value=0):
            self.denied(self.verifier, "GO_PORT_ROOT_FORBIDDEN")
        verify = self.verifier()
        with mock.patch.object(port.os, "geteuid", return_value=0):
            self.denied(verify, "GO_PORT_ROOT_FORBIDDEN", self.go_record,
                        self.signature, self.fp)

    def test_forged_subprocess_output_and_timeout_rejected(self):
        verify = self.verifier()
        expected = ('Good "%s" signature for %s with ED25519 key %s\n'
                    % (port.NAMESPACE, port.PRINCIPAL, self.fp)).encode()

        def bad_stdout(_args, **kwargs):
            kwargs["stdout"].write(expected + b"garbage")
            return SimpleNamespace(returncode=0)
        with mock.patch.object(port.subprocess, "run", side_effect=bad_stdout):
            self.assertFalse(verify(self.go_record, self.signature, self.fp))

        def timer(*_a, **_kwargs):
            raise subprocess.TimeoutExpired("ssh-keygen", 30)
        with mock.patch.object(port.subprocess, "run", side_effect=timer):
            self.denied(verify, "GO_PORT_EXEC_DENIED", self.go_record,
                        self.signature, self.fp)

    def test_binary_chain_rejects_untrusted_path(self):
        bad = self.root / "keygen"
        bad.write_bytes(Path("/usr/bin/ssh-keygen").read_bytes())
        bad.chmod(0o755)
        with mock.patch.object(port, "KEYGEN", bad):
            self.denied(self.verifier, "GO_PORT_BINARY_PARENT_UNTRUSTED")


if __name__ == "__main__":
    unittest.main()
