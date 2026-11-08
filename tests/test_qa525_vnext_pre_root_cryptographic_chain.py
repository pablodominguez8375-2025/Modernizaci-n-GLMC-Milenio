"""QA525 #936 cryptographic CROSS-MODULE tests: synthetic keys, never physical GO.

A real OpenSSH signer is used for TEST receipts and GO signatures in tempdirs.
The APP verification callback remains synthetic, and the nonce callback is NOT
a durable journal. Nothing in this file is Sponsor or host authority.
"""
import datetime as dt
import hashlib
import importlib.util
import json
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[1] / "scripts" / "qa"
SSH = shutil.which("ssh-keygen")
APP_BYTES = b"test-only APP bundle, NOT a genuine Sigstore attestation"
APP_FP = frozenset({"SHA256:" + "A" * 43})
NOW = dt.datetime(2026, 11, 7, 22, 30, tzinfo=dt.timezone.utc)
GO_NS = "qa525-go-synthetic-test@pmgm"
GO_PRINCIPAL = "qa525-go-synthetic-test"


def load(name, filename):
    spec = importlib.util.spec_from_file_location(name, ROOT / filename)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


ops = load("qa525_ops_real_chain", "qa525-vnext-ops-signature.py")
dual = load("qa525_dual_real_chain", "qa525-vnext-pre-root-dual-gate.py")
go = load("qa525_go_real_chain", "qa525-vnext-go-cut-contract.py")


def digest(payload):
    return "sha256:" + hashlib.sha256(payload).hexdigest()


def encode(record):
    return (json.dumps(record, sort_keys=True, separators=(",", ":"),
                       ensure_ascii=True) + "\n").encode("ascii")


@unittest.skipUnless(SSH, "OpenSSH ssh-keygen is required for real signature tests")
class TestRealOpsToGoChain(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.root = Path(self.tmp.name)
        self.ops_key, self.signers, self.ops_fp = self.key("ops", ops.PRINCIPAL, ops.NAMESPACE)
        self.go_key, self.go_signers, self.go_fp = self.key("go", GO_PRINCIPAL, GO_NS)
        self.sha = lambda c: "sha256:" + c * 64

        self.receipt = dict(
            schema="qa525-ops-v3", status="OPS_SIGNED_VERIFIED",
            ops_source_sha="a" * 40,
            program_sha256=self.sha("1"), readiness_sha256=self.sha("2"),
            staging_sha256=self.sha("3"), policy_hash=self.sha("4"),
            app_archive_sha256=self.sha("5"),
        )
        self.receipt_bytes = encode(self.receipt)
        self.ops_signature = self.sign(self.ops_key, ops.NAMESPACE, self.receipt_bytes)
        self.dual_pins = dict(
            ops_source_sha=self.receipt["ops_source_sha"],
            app_source_sha="b" * 40,
            ops_fingerprint=self.ops_fp,
            policy_hash=self.receipt["policy_hash"],
            program_sha256=self.receipt["program_sha256"],
            readiness_sha256=self.receipt["readiness_sha256"],
            staging_sha256=self.receipt["staging_sha256"],
            app_archive_sha256=self.receipt["app_archive_sha256"],
            app_bundle_sha256=digest(APP_BYTES),
            repository=dual.REPO,
            app_signer_workflow=dual.WORKFLOW,
            app_oidc_issuer=dual.ISSUER,
        )

        self.cut_pins = dict(
            ops_source_sha=self.receipt["ops_source_sha"],
            app_source_sha="b" * 40,
            app_archive_sha256=self.receipt["app_archive_sha256"],
            plan_sha256=self.sha("6"), admission_sha256=self.sha("7"),
            policy_hash=self.receipt["policy_hash"], go_fingerprint=self.go_fp,
        )
        self.cut = dict(
            schema="qa525-go-cut-v1",
            nonce="QA525-synthetic-20261107-nonce-abcdefghijklmnop",
            host="glmdch", environment="QA_SYNTHETIC",
            scope="API_WEB_QA_ONLY", action="ADMIT_OFFLINE_APPLY",
            ops_source_sha=self.cut_pins["ops_source_sha"],
            app_source_sha=self.cut_pins["app_source_sha"],
            app_archive_sha256=self.cut_pins["app_archive_sha256"],
            plan_sha256=self.cut_pins["plan_sha256"],
            admission_sha256=self.cut_pins["admission_sha256"],
            policy_hash=self.cut_pins["policy_hash"],
            issued_at="2026-11-07T22:20:00Z", expires_at="2026-11-07T22:40:00Z",
        )
        self.cut_bytes = encode(self.cut)
        self.go_signature = self.sign(self.go_key, GO_NS, self.cut_bytes)
        self.app_subject = dict(
            source_sha="b" * 40,
            subject_sha256=self.receipt["app_archive_sha256"],
            signer_workflow=go.APP_WORKFLOW, oidc_issuer=go.ISSUER,
        )
        self.nonce_used = False

    def key(self, label, principal, namespace):
        path = self.root / (label + "-PRIVATE-TEST-ONLY")
        subprocess.run([SSH, "-q", "-t", "ed25519", "-N", "", "-f", str(path),
                        "-C", ""], check=True, stdout=subprocess.DEVNULL,
                       stderr=subprocess.DEVNULL, timeout=10)
        pub = Path(str(path) + ".pub").read_text().split()[1]
        allowed = (principal + ' namespaces="' + namespace +
                   '" ssh-ed25519 ' + pub + "\n").encode("ascii")
        fp = subprocess.run([SSH, "-lf", str(path) + ".pub"], check=True,
                            capture_output=True, text=True, timeout=10).stdout.split()[1]
        return path, allowed, fp

    def sign(self, private, namespace, raw):
        path = self.root / ("payload-%s-%d" % (namespace, len(list(self.root.glob("payload-*")))))
        path.write_bytes(raw)
        subprocess.run([SSH, "-Y", "sign", "-q", "-f", str(private), "-n",
                        namespace, str(path)], check=True,
                       capture_output=True, timeout=10)
        result = Path(str(path) + ".sig").read_bytes()
        path.unlink()
        Path(str(path) + ".sig").unlink()
        return result

    def verify_ssh(self, allowed, signature, data, principal, namespace):
        allowed_path, sig_path = self.root / "allowed-now", self.root / "signature-now"
        allowed_path.write_bytes(allowed)
        sig_path.write_bytes(signature)
        command = (SSH, "-Y", "verify", "-f", str(allowed_path), "-I", principal,
                   "-n", namespace, "-s", str(sig_path))
        done = subprocess.run(command, input=data, capture_output=True, timeout=10)
        return done.returncode, done.stdout

    def verify_ops_real(self, allowed, signature, principal, namespace, receipt):
        return self.verify_ssh(allowed, signature, receipt, principal, namespace)

    def ops_port(self, receipt, signature, allowed, fingerprint, policy, archive):
        return ops.verify_ops_receipt(
            receipt=receipt, signature=signature, allowed_signers=allowed,
            ops_fingerprint=fingerprint, app_fingerprints=APP_FP,
            policy_hash=policy, expected_app_archive_sha256=archive,
            verifier=self.verify_ops_real,
        )

    def app_port_simulated(self, bundle, challenge):
        # Deliberately NOT Sigstore. A true return is only syntactic test input.
        return bundle == APP_BYTES and challenge == {
            "repository": dual.REPO, "signer_workflow": dual.WORKFLOW,
            "oidc_issuer": dual.ISSUER,
            "source_sha": "b" * 40,
            "archive_sha256": self.receipt["app_archive_sha256"],
            "bundle_sha256": digest(APP_BYTES),
        }

    def dual_call(self, **changes):
        request = dict(
            receipt_bytes=self.receipt_bytes, signature_bytes=self.ops_signature,
            allowed_signers_bytes=self.signers, app_bundle_bytes=APP_BYTES,
            pins=self.dual_pins, verify_ops=self.ops_port,
            verify_app=self.app_port_simulated,
        )
        request.update(changes)
        return dual.verify_dual(**request)

    def verify_go_real(self, raw, signature, fingerprint):
        if fingerprint != self.go_fp:
            return False
        code, stdout = self.verify_ssh(
            self.go_signers, signature, raw, GO_PRINCIPAL, GO_NS
        )
        exact = ('Good "%s" signature for %s with ED25519 key %s\n'
                 % (GO_NS, GO_PRINCIPAL, self.go_fp)).encode()
        return code == 0 and stdout == exact

    def nonce_port_simulated(self, nonce, admission):
        # NO durable anti-replay journal. Never an authority to APPLY.
        return (not self.nonce_used and nonce == self.cut["nonce"]
                and admission == self.cut_pins["admission_sha256"])

    def go_call(self, **changes):
        request = dict(
            go_bytes=self.cut_bytes, go_signature_bytes=self.go_signature,
            pins=self.cut_pins, ops_v3_receipt=self.receipt,
            app_subject=self.app_subject, now_utc=NOW,
            verify_go_signature=self.verify_go_real,
            nonce_unused=self.nonce_port_simulated,
        )
        request.update(changes)
        return go.check_cut(**request)

    def test_real_ops_v3_and_go_signatures_still_grant_no_authority(self):
        both = self.dual_call()
        cut = self.go_call()
        self.assertIs(both["two_verifier_ports_accepted"], True)
        self.assertIs(both["ops_v3_app_archive_challenge_matched_by_ports"], True)
        self.assertEqual(both["ops_receipt_sha256"], digest(self.receipt_bytes))
        self.assertEqual(cut["app_archive_sha256"], self.receipt["app_archive_sha256"])
        self.assertEqual(cut["nonce"], self.cut["nonce"])
        for key in ("may_apply", "may_install",
                    "ops_fingerprint_provenance_verified_here",
                    "app_attestation_provenance_verified_here",
                    "sponsor_approval_verified_here",
                    "signed_bootstrap_verified_here"):
            self.assertIs(both[key], False, key)
        for key in ("may_apply", "may_install", "bootstrap_authenticated_here",
                    "nonce_consumption_durable_here",
                    "app_attestation_provenance_verified_here",
                    "ops_signature_provenance_verified_here"):
            self.assertIs(cut[key], False, key)

    def test_ops_receipt_swapped_after_signing_is_denied(self):
        evil = dict(self.receipt, program_sha256=self.sha("8"))
        with self.assertRaises(dual.Stop) as c:
            self.dual_call(receipt_bytes=encode(evil))
        self.assertEqual(str(c.exception), "DUAL_OPS_SIGNATURE_DENIED")

    def test_signed_v2_ops_cannot_downgrade_v3_chain(self):
        old = dict(self.receipt, schema="qa525-ops-v2")
        del old["app_archive_sha256"]
        raw = encode(old)
        old_signature = self.sign(self.ops_key, ops.NAMESPACE, raw)
        with self.assertRaises(dual.Stop) as c:
            self.dual_call(receipt_bytes=raw, signature_bytes=old_signature)
        self.assertEqual(str(c.exception), "DUAL_OPS_SIGNATURE_DENIED")

    def test_wrong_ops_fingerprint_or_mixed_app_archive_is_denied(self):
        with self.assertRaises(dual.Stop) as c:
            self.dual_call(pins=dict(self.dual_pins, ops_fingerprint=self.go_fp))
        self.assertEqual(str(c.exception), "DUAL_OPS_SIGNATURE_DENIED")
        with self.assertRaises(dual.Stop) as c:
            self.dual_call(pins=dict(self.dual_pins,
                                     app_archive_sha256=self.sha("e")))
        self.assertEqual(str(c.exception), "DUAL_OPS_SIGNATURE_DENIED")

    def test_go_signature_cannot_be_reused_for_different_release(self):
        evil = dict(self.cut, plan_sha256=self.sha("8"))
        with self.assertRaises(go.Stop) as c:
            self.go_call(go_bytes=encode(evil))
        self.assertEqual(str(c.exception), "GO_CUT_MISMATCH")
        with self.assertRaises(go.Stop) as c:
            self.go_call(pins=dict(self.cut_pins, go_fingerprint=self.ops_fp))
        self.assertEqual(str(c.exception), "GO_CUT_SIGNATURE_DENIED")

    def test_expired_go_nonce_replay_and_other_app_subject_are_denied(self):
        with self.assertRaises(go.Stop) as c:
            self.go_call(now_utc=NOW + dt.timedelta(hours=1))
        self.assertEqual(str(c.exception), "GO_CUT_EXPIRED_OR_FUTURE")
        self.nonce_used = True
        with self.assertRaises(go.Stop) as c:
            self.go_call()
        self.assertEqual(str(c.exception), "GO_CUT_NONCE_UNSAFE")
        self.nonce_used = False
        other = dict(self.app_subject, subject_sha256=self.sha("9"))
        with self.assertRaises(go.Stop) as c:
            self.go_call(app_subject=other)
        self.assertEqual(str(c.exception), "GO_CUT_APP_SUBJECT_MISMATCH")


if __name__ == "__main__":
    unittest.main()
