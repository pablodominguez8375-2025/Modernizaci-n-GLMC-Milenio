"""QA525 dual-signature seam: real OPS parser + synthetic ports; never a GO."""
import ast
import base64
import copy
import hashlib
import importlib.util
import json
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1] / "scripts" / "qa"


def load(key, filename):
    sp = importlib.util.spec_from_file_location(key, ROOT / filename)
    m = importlib.util.module_from_spec(sp)
    sp.loader.exec_module(m)
    return m


gate = load("qa525_pre_root_dual_gate_t", "qa525-vnext-pre-root-dual-gate.py")
ops = load("qa525_ops_signature_for_dual_t", "qa525-vnext-ops-signature.py")
APP_FINGERPRINTS = frozenset({"SHA256:" + "A" * 43})


def hash_bytes(value):
    return "sha256:" + hashlib.sha256(value).hexdigest()


class DualGateContractTests(unittest.TestCase):
    def setUp(self):
        self.pub_blob = ops.BLOB_PREFIX + bytes(range(32))
        self.signers = ('qa525-ops namespaces="qa525-ops-v2@pmgm" ssh-ed25519 %s\n'
                        % base64.b64encode(self.pub_blob).decode()).encode()
        self.fp = ops.fingerprint_of(self.signers)
        self.receipt = {
            "schema": "qa525-ops-v2", "status": "OPS_SIGNED_VERIFIED",
            "ops_source_sha": "a" * 40,
            "program_sha256": "sha256:" + "1" * 64,
            "readiness_sha256": "sha256:" + "2" * 64,
            "staging_sha256": "sha256:" + "3" * 64,
            "policy_hash": "sha256:" + "4" * 64,
        }
        self.raw = (json.dumps(self.receipt, sort_keys=True) + "\n").encode()
        self.signature = ops.SIG_BEGIN + b"AAA=\n" + ops.SIG_END
        self.bundle = b"synthetic dsse bundle (not a verified Sigstore claim)"
        self.pins = dict(
            ops_source_sha=self.receipt["ops_source_sha"],
            app_source_sha="b" * 40,
            ops_fingerprint=self.fp,
            policy_hash=self.receipt["policy_hash"],
            program_sha256=self.receipt["program_sha256"],
            readiness_sha256=self.receipt["readiness_sha256"],
            staging_sha256=self.receipt["staging_sha256"],
            app_archive_sha256="sha256:" + "5" * 64,
            app_bundle_sha256=hash_bytes(self.bundle),
            repository=gate.REPO, app_signer_workflow=gate.WORKFLOW,
            app_oidc_issuer=gate.ISSUER,
        )
        self.ops_calls = 0
        self.app_calls = 0
        self.app_challenge = None

    def ssh_verifier(self, allowed, signature, principal, namespace, receipt):
        self.ops_calls += 1
        if (allowed != self.signers or signature != self.signature or
                principal != ops.PRINCIPAL or namespace != ops.NAMESPACE or
                receipt != self.raw):
            return 1, b""
        return 0, ('Good "%s" signature for %s with ED25519 key %s\n'
                   % (namespace, principal, self.fp)).encode()

    def verify_ops(self, raw, signature, signers, fp, policy):
        return ops.verify_ops_receipt(
            receipt=raw, signature=signature, allowed_signers=signers,
            ops_fingerprint=fp, app_fingerprints=APP_FINGERPRINTS,
            policy_hash=policy, verifier=self.ssh_verifier)

    def verify_app(self, bundle, challenge):
        self.app_calls += 1
        self.app_challenge = dict(challenge)
        return bundle == self.bundle

    def call(self, **changes):
        kw = dict(receipt_bytes=self.raw, signature_bytes=self.signature,
                  allowed_signers_bytes=self.signers, app_bundle_bytes=self.bundle,
                  pins=self.pins, verify_ops=self.verify_ops,
                  verify_app=self.verify_app)
        kw.update(changes)
        return gate.verify_dual(**kw)

    def denied(self, expected, **changes):
        with self.assertRaises(gate.Stop) as error:
            self.call(**changes)
        self.assertEqual(str(error.exception), expected)
        self.assertIsNone(error.exception.__cause__)

    def test_two_ports_accept_exact_material_but_grant_nothing(self):
        result = self.call()
        self.assertEqual(self.ops_calls, 1)
        self.assertEqual(self.app_calls, 1)
        self.assertEqual(result["status"], gate.STATUS)
        self.assertEqual(result["ops_source_sha"], "a" * 40)
        self.assertEqual(result["app_source_sha"], "b" * 40)
        self.assertEqual(result["ops_receipt_sha256"], hash_bytes(self.raw))
        self.assertEqual(self.app_challenge, {
            "repository": gate.REPO, "signer_workflow": gate.WORKFLOW,
            "oidc_issuer": gate.ISSUER, "source_sha": "b" * 40,
            "archive_sha256": "sha256:" + "5" * 64,
            "bundle_sha256": hash_bytes(self.bundle),
        })
        self.assertIs(result["two_verifier_ports_accepted"], True)
        for key in ("may_install", "may_apply",
                    "ops_fingerprint_provenance_verified_here",
                    "app_attestation_provenance_verified_here",
                    "sponsor_approval_verified_here",
                    "signed_bootstrap_verified_here"):
            self.assertIs(result[key], False)

    def test_source_replay_and_trust_pins_are_rejected_before_verifiers(self):
        cases = [
            dict(ops_source_sha="not-a-sha"),
            dict(app_source_sha="not-a-sha"),
            dict(ops_fingerprint="SHA256:" + "Z" * 43),
            dict(app_signer_workflow=gate.WORKFLOW + "/unreviewed"),
            dict(app_oidc_issuer="https://untrusted.invalid"),
            dict(repository="other/repo"),
            dict(app_archive_sha256="sha256:invalid"),
            dict(app_bundle_sha256="sha256:" + "0" * 64),
            dict(policy_hash="sha256:" + "g" * 64),
        ]
        for change in cases:
            with self.subTest(change=change):
                self.ops_calls = self.app_calls = 0
                pinned = dict(self.pins, **change)
                expected = ("DUAL_APP_BUNDLE_HASH_MISMATCH" if
                            "app_bundle_sha256" in change else
                            "DUAL_OPS_SIGNATURE_DENIED" if
                            "ops_fingerprint" in change else "DUAL_TRUST_PINS_INVALID")
                self.denied(expected, pins=pinned)
                self.assertEqual((self.ops_calls, self.app_calls), (0, 0))

    def test_source_sha_may_match_when_code_and_app_share_commit_but_no_go(self):
        # SHA denotes a source revision, NOT the signer identity. Same source
        # is legal; only separate cryptographic verification can grant trust.
        pins = dict(self.pins, app_source_sha=self.pins["ops_source_sha"])
        result = self.call(pins=pins)
        self.assertEqual(result["ops_source_sha"], result["app_source_sha"])
        self.assertIs(result["may_install"], False)
        self.assertIs(result["may_apply"], False)
        self.assertIs(result["ops_app_signed_artifacts_bound_here"], False)

    def test_ops_receipt_source_must_match_pinned_ops_code_commit(self):
        pins = dict(self.pins, ops_source_sha="c" * 40)
        self.denied("DUAL_OPS_RECEIPT_MISMATCH", pins=pins)
        self.assertEqual(self.app_calls, 0)

    def test_wrong_receipt_byte_hash_from_ops_port_is_rejected(self):
        valid = self.verify_ops(self.raw, self.signature, self.signers,
                                self.fp, self.pins["policy_hash"])
        forged = dict(valid, receipt_sha256="sha256:" + "e" * 64)
        self.denied("DUAL_OPS_RESULT_UNTRUSTED",
                    verify_ops=lambda *args: forged)
        self.assertEqual(self.app_calls, 0)

    def test_bytes_modified_or_wrong_signature_never_reach_app_verifier(self):
        modified = self.raw.replace(b"qa525-ops-v2", b"qa525-ops-v3")
        self.denied("DUAL_OPS_SIGNATURE_DENIED", receipt_bytes=modified)
        self.denied("DUAL_OPS_SIGNATURE_DENIED", signature_bytes=
                    ops.SIG_BEGIN + b"BAD=\n" + ops.SIG_END)
        self.denied("DUAL_OPS_SIGNATURE_DENIED", allowed_signers_bytes=
                    self.signers.replace(b"qa525-ops", b"other-key"))
        self.assertEqual(self.app_calls, 0)

    def test_ops_claim_not_a_valid_verifier_receipt_is_dismissed(self):
        self.denied("DUAL_OPS_RESULT_UNTRUSTED", verify_ops=lambda *a: {
            "signature_verified_here": True, "may_apply": False})
        self.assertEqual(self.app_calls, 0)

    def test_ops_receipt_shape_and_pins_cannot_be_changed(self):
        response = self.verify_ops(self.raw, self.signature, self.signers,
                                   self.fp, self.pins["policy_hash"])
        altered = copy.deepcopy(response)
        altered["receipt"]["ops_source_sha"] = "b" * 40
        self.denied("DUAL_OPS_RECEIPT_MISMATCH", verify_ops=lambda *a: altered)
        self.assertEqual(self.app_calls, 0)

    def test_app_false_truthy_and_callback_exception_all_refuse(self):
        for value in (False, 1, "yes", {}, None):
            with self.subTest(value=value):
                self.denied("DUAL_APP_ATTESTATION_DENIED",
                            verify_app=lambda *a, v=value: v)
        self.denied("DUAL_APP_ATTESTATION_DENIED",
                    verify_app=lambda *a: (_ for _ in ()).throw(
                        RuntimeError("private secret must not leak")))

    def test_app_callback_cannot_modify_pins_or_challenge(self):
        def mutate_challenge(_bundle, challenge):
            challenge["source_sha"] = "f" * 40
            return True
        self.denied("DUAL_APP_ATTESTATION_DENIED", verify_app=mutate_challenge)
        pins = dict(self.pins)
        def mutate_pins(_bundle, _challenge):
            pins["app_source_sha"] = "f" * 40
            return True
        self.denied("DUAL_APP_ATTESTATION_DENIED",
                    pins=pins, verify_app=mutate_pins)

    def test_input_shapes_and_no_process_network_or_cli(self):
        self.denied("DUAL_INPUT_INVALID", pins=None)
        self.denied("DUAL_INPUT_INVALID", signature_bytes=b"")
        self.denied("DUAL_INPUT_INVALID", app_bundle_bytes="not bytes")
        self.denied("DUAL_INPUT_INVALID", verify_app=None)
        self.denied("DUAL_INPUT_INVALID", pins=dict(self.pins, fake=True))
        raw = (ROOT / "qa525-vnext-pre-root-dual-gate.py").read_text()
        tree = ast.parse(raw)
        imports = {alias.name for node in ast.walk(tree)
                   if isinstance(node, ast.Import) for alias in node.names}
        self.assertEqual(imports, {"hashlib", "re"})
        for marker in ("subprocess", "socket", "urllib", "os.system(", "Popen("):
            self.assertNotIn(marker, raw)
        with self.assertRaises(SystemExit):
            exec(compile(raw, "dual.py", "exec"), {"__name__": "__main__"})


if __name__ == "__main__":
    unittest.main()
