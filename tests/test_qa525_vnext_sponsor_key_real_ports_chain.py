"""QA525 pre-root chain with the REAL production ports and the Sponsor builders.

Complements test_qa525_vnext_pre_root_cryptographic_chain.py (synthetic namespaces and ad-hoc
verifiers) with the exact configuration the Sponsor will use: ONE Ed25519 key signing the OPS v3
receipt and the GO cut in their two REAL namespaces, bytes produced by the sponsor-signing
builders, verified through the rootless OPS and GO ports. Synthetic keys in tempdirs only; the
APP callback and the nonce journal are simulated. Nothing here is Sponsor authority or a GO.
"""
import datetime as dt
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[1] / "scripts" / "qa"
SSH = shutil.which("ssh-keygen")
NOW = dt.datetime(2026, 11, 7, 22, 30, tzinfo=dt.timezone.utc)
APP_BYTES = b"test-only APP bundle, NOT a genuine Sigstore attestation"
APP_FP = frozenset({"SHA256:" + "A" * 43})


def load(name, filename):
    spec = importlib.util.spec_from_file_location(name, ROOT / filename)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


signing = load("qa525_real_chain_signing", "qa525-vnext-sponsor-signing.py")
ops = load("qa525_real_chain_ops", "qa525-vnext-ops-signature.py")
dual = load("qa525_real_chain_dual", "qa525-vnext-pre-root-dual-gate.py")
go = load("qa525_real_chain_go", "qa525-vnext-go-cut-contract.py")
ops_port = load("qa525_real_chain_ops_port", "qa525-vnext-ops-rootless-adapter.py")
go_port = load("qa525_real_chain_go_port", "qa525-vnext-go-rootless-adapter.py")


def sha(c):
    return "sha256:" + c * 64


def digest(payload):
    return "sha256:" + hashlib.sha256(payload).hexdigest()


def encode(record):
    return (json.dumps(record, sort_keys=True, separators=(",", ":"),
                       ensure_ascii=True) + "\n").encode("ascii")


@unittest.skipUnless(SSH, "OpenSSH ssh-keygen is required for real signature tests")
class SponsorKeyRealPortsChainTests(unittest.TestCase):
    def setUp(self):
        if os.geteuid() == 0:
            self.skipTest("the production ports refuse to run as root by design")
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.dir = Path(self.tmp.name)
        self.key, self.pub, self.fp = self.make_key("sponsor")
        self.other_key, self.other_pub, self.other_fp = self.make_key("other")
        self.ops_allowed = signing.allowed_signers_line(
            self.pub, signing.OPS_NAMESPACE, self.fp).encode("ascii")
        self.go_allowed = signing.allowed_signers_line(
            self.pub, signing.GO_NAMESPACE, self.fp).encode("ascii")
        self.pin = digest(Path(ops_port.SSH_KEYGEN).read_bytes())
        self.receipt_bytes = signing.build_ops_v3_receipt(
            ops_source_sha="a" * 40, program_sha256=sha("1"), readiness_sha256=sha("2"),
            staging_sha256=sha("3"), policy_hash=sha("4"), app_archive_sha256=sha("5"))
        self.receipt = json.loads(self.receipt_bytes)
        self.nonce = signing.new_nonce(bytes(range(32)))
        self.cut_bytes = self.cut()
        self.ops_signature = self.sign(self.key, signing.OPS_NAMESPACE, self.receipt_bytes)
        self.go_signature = self.sign(self.key, signing.GO_NAMESPACE, self.cut_bytes)
        self.dual_pins = dict(
            ops_source_sha="a" * 40, app_source_sha="b" * 40, ops_fingerprint=self.fp,
            policy_hash=sha("4"), program_sha256=sha("1"), readiness_sha256=sha("2"),
            staging_sha256=sha("3"), app_archive_sha256=sha("5"),
            app_bundle_sha256=digest(APP_BYTES), repository=dual.REPO,
            app_signer_workflow=dual.WORKFLOW, app_oidc_issuer=dual.ISSUER)
        self.cut_pins = dict(
            ops_source_sha="a" * 40, app_source_sha="b" * 40, app_archive_sha256=sha("5"),
            plan_sha256=sha("6"), admission_sha256=sha("7"), policy_hash=sha("4"),
            go_fingerprint=self.fp)
        self.app_subject = dict(source_sha="b" * 40, subject_sha256=sha("5"),
                                signer_workflow=go.APP_WORKFLOW, oidc_issuer=go.ISSUER)

    # -- helpers -------------------------------------------------------------------------
    def make_key(self, label):
        path = self.dir / (label + "-PRIVATE-TEST-ONLY")
        subprocess.run([SSH, "-q", "-t", "ed25519", "-N", "", "-f", str(path), "-C", ""],
                       check=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
                       timeout=10)
        pub = Path(str(path) + ".pub").read_text().strip()
        listed = subprocess.run([SSH, "-lf", str(path) + ".pub"], check=True,
                                capture_output=True, text=True, timeout=10).stdout.split()[1]
        self.assertEqual(signing.fingerprint_of_public_key(pub), listed)
        return path, pub, listed

    def cut(self, **over):
        args = dict(nonce=self.nonce, ops_source_sha="a" * 40, app_source_sha="b" * 40,
                    app_archive_sha256=sha("5"), plan_sha256=sha("6"),
                    admission_sha256=sha("7"), policy_hash=sha("4"),
                    issued_at="2026-11-07T22:20:00Z", expires_at="2026-11-07T22:40:00Z")
        args.update(over)
        return signing.build_go_cut(**args)

    def sign(self, private, namespace, raw):
        path = self.dir / ("payload-%d" % len(list(self.dir.glob("payload-*"))))
        path.write_bytes(raw)
        subprocess.run([SSH, "-Y", "sign", "-q", "-f", str(private), "-n", namespace,
                        str(path)], check=True, capture_output=True, timeout=10)
        signature = Path(str(path) + ".sig").read_bytes()
        path.unlink()
        Path(str(path) + ".sig").unlink()
        return signature

    def ops_callback(self, receipt, signature, allowed, fingerprint, policy, archive):
        return ops.verify_ops_receipt(
            receipt=receipt, signature=signature, allowed_signers=allowed,
            ops_fingerprint=fingerprint, app_fingerprints=APP_FP, policy_hash=policy,
            expected_app_archive_sha256=archive,
            verifier=ops_port.verifier_for(trusted_binary_sha256=self.pin))

    def app_simulated(self, bundle, challenge):
        return bundle == APP_BYTES and challenge["archive_sha256"] == sha("5")

    def dual_call(self, **changes):
        request = dict(
            receipt_bytes=self.receipt_bytes, signature_bytes=self.ops_signature,
            allowed_signers_bytes=self.ops_allowed, app_bundle_bytes=APP_BYTES,
            pins=self.dual_pins, verify_ops=self.ops_callback, verify_app=self.app_simulated)
        request.update(changes)
        return dual.verify_dual(**request)

    def go_callback(self, allowed=None, fingerprint=None):
        return go_port.verifier_for(
            trusted_binary_sha256=self.pin, allowed_signers=allowed or self.go_allowed,
            expected_fingerprint=fingerprint or self.fp)

    def go_call(self, **changes):
        request = dict(
            go_bytes=self.cut_bytes, go_signature_bytes=self.go_signature,
            pins=self.cut_pins, ops_v3_receipt=self.receipt, app_subject=self.app_subject,
            now_utc=NOW, verify_go_signature=self.go_callback(),
            nonce_unused=lambda nonce, admission: nonce == self.nonce)
        request.update(changes)
        return go.check_cut(**request)

    # -- tests ---------------------------------------------------------------------------
    def test_one_key_in_two_real_namespaces_passes_the_real_ports_end_to_end(self):
        both = self.dual_call()
        cut = self.go_call()
        self.assertIs(both["two_verifier_ports_accepted"], True)
        self.assertEqual(both["ops_receipt_sha256"], digest(self.receipt_bytes))
        self.assertEqual(cut["nonce"], self.nonce)
        for key in ("may_apply", "may_install", "sponsor_approval_verified_here",
                    "signed_bootstrap_verified_here"):
            self.assertIs(both[key], False, key)
        for key in ("may_apply", "may_install", "bootstrap_authenticated_here",
                    "nonce_consumption_durable_here"):
            self.assertIs(cut[key], False, key)

    def test_builder_bytes_are_exactly_the_chain_canonical_encoding(self):
        self.assertEqual(self.receipt_bytes, encode(json.loads(self.receipt_bytes)))
        self.assertEqual(self.cut_bytes, encode(json.loads(self.cut_bytes)))
        self.assertEqual(self.receipt["schema"], "qa525-ops-v3")
        self.assertEqual(len(self.receipt), 8)

    def test_a_signature_made_in_one_namespace_is_denied_in_the_other(self):
        # Same key, same bytes, wrong namespace: the namespace is the anti-replay barrier.
        ops_over_cut = self.sign(self.key, signing.OPS_NAMESPACE, self.cut_bytes)
        with self.assertRaises(go.Stop) as ctx:
            self.go_call(go_signature_bytes=ops_over_cut)
        self.assertEqual(str(ctx.exception), "GO_CUT_SIGNATURE_DENIED")
        go_over_receipt = self.sign(self.key, signing.GO_NAMESPACE, self.receipt_bytes)
        with self.assertRaises(dual.Stop) as ctx:
            self.dual_call(signature_bytes=go_over_receipt)
        self.assertEqual(str(ctx.exception), "DUAL_OPS_SIGNATURE_DENIED")

    def test_each_port_only_accepts_a_signers_line_scoped_to_its_own_namespace(self):
        with self.assertRaises(go_port.Stop) as ctx:
            self.go_callback(allowed=self.ops_allowed)
        self.assertEqual(str(ctx.exception), "GO_PORT_SIGNERS_INVALID")
        with self.assertRaises(dual.Stop) as ctx:
            self.dual_call(allowed_signers_bytes=self.go_allowed)
        self.assertEqual(str(ctx.exception), "DUAL_OPS_SIGNATURE_DENIED")

    def test_the_external_pin_must_be_the_sponsor_key_not_a_look_alike(self):
        with self.assertRaises(go_port.Stop) as ctx:
            self.go_callback(fingerprint=self.other_fp)
        self.assertEqual(str(ctx.exception), "GO_PORT_FINGERPRINT_PIN_MISMATCH")
        with self.assertRaises(dual.Stop) as ctx:
            self.dual_call(pins=dict(self.dual_pins, ops_fingerprint=self.other_fp))
        self.assertEqual(str(ctx.exception), "DUAL_OPS_SIGNATURE_DENIED")
        with self.assertRaises(signing.Stop) as ctx:
            signing.allowed_signers_line(self.other_pub, signing.GO_NAMESPACE, self.fp)
        self.assertEqual(str(ctx.exception), "SIGNSHEET_FINGERPRINT_MISMATCH")

    def test_a_signature_by_another_key_is_denied_even_under_the_right_namespace(self):
        forged_go = self.sign(self.other_key, signing.GO_NAMESPACE, self.cut_bytes)
        with self.assertRaises(go.Stop) as ctx:
            self.go_call(go_signature_bytes=forged_go)
        self.assertEqual(str(ctx.exception), "GO_CUT_SIGNATURE_DENIED")
        forged_ops = self.sign(self.other_key, signing.OPS_NAMESPACE, self.receipt_bytes)
        with self.assertRaises(dual.Stop) as ctx:
            self.dual_call(signature_bytes=forged_ops)
        self.assertEqual(str(ctx.exception), "DUAL_OPS_SIGNATURE_DENIED")

    def test_changing_a_signed_byte_that_no_pin_covers_breaks_the_signature(self):
        other_nonce = signing.new_nonce(bytes(reversed(range(32))))
        with self.assertRaises(go.Stop) as ctx:
            self.go_call(go_bytes=self.cut(nonce=other_nonce),
                         nonce_unused=lambda nonce, admission: True)
        self.assertEqual(str(ctx.exception), "GO_CUT_SIGNATURE_DENIED")

    def test_a_replayed_nonce_is_denied_by_the_contract_with_the_real_signature(self):
        with self.assertRaises(go.Stop) as ctx:
            self.go_call(nonce_unused=lambda nonce, admission: False)
        self.assertEqual(str(ctx.exception), "GO_CUT_NONCE_UNSAFE")


if __name__ == "__main__":
    unittest.main()
