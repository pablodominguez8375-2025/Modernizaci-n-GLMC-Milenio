"""Separate signed schema scope remains a pure consistency check."""
import datetime as dt
import importlib.util
import json
from pathlib import Path
import unittest

PATH = Path(__file__).resolve().parents[1] / "scripts/qa/qa525-vnext-go-cut-contract.py"
spec = importlib.util.spec_from_file_location("qa525_schema_go", PATH)
cut = importlib.util.module_from_spec(spec)
spec.loader.exec_module(cut)
SHA = lambda c: "sha256:" + c * 64
NOW = dt.datetime(2026, 11, 21, 23, 0, tzinfo=dt.timezone.utc)


def encode(value):
    return (json.dumps(value, sort_keys=True, separators=(",", ":"), ensure_ascii=True) + "\n").encode()


class SchemaGoTests(unittest.TestCase):
    def setUp(self):
        self.pins = dict(ops_source_sha="a" * 40, app_source_sha="b" * 40,
                         app_archive_sha256=SHA("1"), plan_sha256=SHA("2"),
                         admission_sha256=SHA("3"), policy_hash=SHA("4"),
                         go_fingerprint="SHA256:" + "a" * 43)
        self.schema_pins = {k: SHA("5") for k in cut.SCHEMA_PIN_FIELDS}
        self.go = dict(schema="qa525-go-schema-cut-v1", nonce="a" * 64,
                       host="glmdch", environment="QA_SYNTHETIC",
                       scope=cut.SCHEMA_SCOPE, action="ADMIT_SCHEMA_APIWEB_APPLY",
                       issued_at="2026-11-21T22:55:00Z", expires_at="2026-11-21T23:05:00Z",
                       **{k: self.pins[k] for k in cut.PINS if k != "go_fingerprint"},
                       **self.schema_pins)
        self.ops = dict(schema="qa525-ops-v3", status="OPS_SIGNED_VERIFIED",
                        ops_source_sha=self.pins["ops_source_sha"],
                        program_sha256=SHA("6"), readiness_sha256=SHA("7"),
                        staging_sha256=SHA("8"), policy_hash=self.pins["policy_hash"],
                        app_archive_sha256=self.pins["app_archive_sha256"])
        self.app = dict(source_sha=self.pins["app_source_sha"],
                        subject_sha256=self.pins["app_archive_sha256"],
                        signer_workflow=cut.APP_WORKFLOW, oidc_issuer=cut.ISSUER)
        self.calls = []

    def verify(self, raw, signature, fingerprint):
        self.calls.append("signature")
        return raw == encode(self.go) and signature == b"synthetic" and fingerprint == self.pins["go_fingerprint"]

    def unused(self, nonce, admission):
        self.calls.append("nonce")
        return nonce == "a" * 64 and admission == self.pins["admission_sha256"]

    def check(self, **kw):
        args = dict(go_bytes=encode(self.go), go_signature_bytes=b"synthetic",
                    pins=self.pins, schema_pins=self.schema_pins,
                    ops_v3_receipt=self.ops, app_subject=self.app, now_utc=NOW,
                    verify_go_signature=self.verify, nonce_unused=self.unused,
                    purpose=cut.SCHEMA_SCOPE)
        args.update(kw)
        return cut.check_cut(**args)

    def test_exact_shape_and_no_authority(self):
        result = self.check()
        self.assertEqual(["signature", "nonce"], self.calls)
        self.assertEqual(self.schema_pins, result["schema_pins"])
        self.assertIs(result["may_apply"], False)

    def test_legacy_api_go_cannot_gain_schema_fields(self):
        with self.assertRaisesRegex(cut.Stop, "GO_CUT_INPUT_INVALID"):
            self.check(purpose="API_WEB_QA_ONLY")
        self.assertEqual([], self.calls)

    def test_mismatch_does_not_call_signature_or_nonce(self):
        for field, value in (("migrator_image_digest", SHA("9")),
                             ("baseline_history_sha256", SHA("9")),
                             ("scope", "API_WEB_QA_ONLY")):
            with self.subTest(field=field):
                with self.assertRaises(cut.Stop):
                    self.check(go_bytes=encode(dict(self.go, **{field: value})))
        self.assertEqual([], self.calls)

    def test_mutation_by_signature_callback_stops_before_nonce(self):
        def mutate(*_):
            self.schema_pins["migrator_image_digest"] = SHA("9")
            return True
        with self.assertRaisesRegex(cut.Stop, "GO_CUT_SIGNATURE_DENIED"):
            self.check(verify_go_signature=mutate)
        self.assertEqual([], self.calls)

    def test_short_expiry_and_duplicate_fields(self):
        with self.assertRaisesRegex(cut.Stop, "GO_CUT_EXPIRED_OR_FUTURE"):
            self.check(now_utc=NOW + dt.timedelta(minutes=5))
        long = dict(self.go, expires_at="2026-11-21T23:20:01Z")
        with self.assertRaisesRegex(cut.Stop, "GO_CUT_EXPIRED_OR_FUTURE"):
            self.check(go_bytes=encode(long))
        with self.assertRaisesRegex(cut.Stop, "GO_CUT_FIELDS_INVALID"):
            altered = dict(self.go)
            del altered["postgate_sha256"]
            self.check(go_bytes=encode(altered))


if __name__ == "__main__":
    unittest.main()
