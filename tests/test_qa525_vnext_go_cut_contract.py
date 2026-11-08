"""QA525 GO cut: pure adversarial clock/nonce/receipt binding; never GO."""
import ast
import datetime as dt
import hashlib
import importlib.util
import json
from pathlib import Path
import unittest

HERE = Path(__file__).resolve().parents[1] / "scripts" / "qa" / "qa525-vnext-go-cut-contract.py"
spec = importlib.util.spec_from_file_location("qa525_pure_go_cut_test", HERE)
cut = importlib.util.module_from_spec(spec)
spec.loader.exec_module(cut)

NOW = dt.datetime(2026, 11, 7, 22, 30, 0, tzinfo=dt.timezone.utc)
NONCE = "qa525_GO_20261107_nonce_AAAAAAAAAAAAA"
SIGNATURE = b"synthetic signature never from Sponsor"
FP = "SHA256:" + "a" * 43
SHA = lambda c: "sha256:" + c * 64


def encode(obj):
    return (json.dumps(obj, sort_keys=True, separators=(",", ":"),
                       ensure_ascii=True) + "\n").encode("ascii")


class CutContractTests(unittest.TestCase):
    def setUp(self):
        self.pins = dict(
            ops_source_sha="a" * 40, app_source_sha="b" * 40,
            app_archive_sha256=SHA("1"), plan_sha256=SHA("2"),
            admission_sha256=SHA("3"), policy_hash=SHA("4"),
            go_fingerprint=FP,
        )
        self.go = dict(schema="qa525-go-cut-v1", nonce=NONCE,
                       host="glmdch", environment="QA_SYNTHETIC",
                       scope="API_WEB_QA_ONLY", action="ADMIT_OFFLINE_APPLY",
                       ops_source_sha=self.pins["ops_source_sha"],
                       app_source_sha=self.pins["app_source_sha"],
                       app_archive_sha256=self.pins["app_archive_sha256"],
                       plan_sha256=self.pins["plan_sha256"],
                       admission_sha256=self.pins["admission_sha256"],
                       policy_hash=self.pins["policy_hash"],
                       issued_at="2026-11-07T22:20:00Z",
                       expires_at="2026-11-07T22:40:00Z")
        self.ops = dict(schema="qa525-ops-v3", status="OPS_SIGNED_VERIFIED",
                        ops_source_sha=self.pins["ops_source_sha"],
                        program_sha256=SHA("5"), readiness_sha256=SHA("6"),
                        staging_sha256=SHA("7"),
                        policy_hash=self.pins["policy_hash"],
                        app_archive_sha256=self.pins["app_archive_sha256"])
        self.app = dict(source_sha=self.pins["app_source_sha"],
                        subject_sha256=self.pins["app_archive_sha256"],
                        signer_workflow=cut.APP_WORKFLOW, oidc_issuer=cut.ISSUER)
        self.verify_count = 0
        self.nonce_count = 0

    def verify(self, raw, signature, fingerprint):
        self.verify_count += 1
        return raw == encode(self.go) and signature == SIGNATURE and fingerprint == FP

    def unused(self, nonce, admission):
        self.nonce_count += 1
        return nonce == NONCE and admission == SHA("3")

    def call(self, **changes):
        args=dict(go_bytes=encode(self.go), go_signature_bytes=SIGNATURE,
                  pins=self.pins, ops_v3_receipt=self.ops,
                  app_subject=self.app, now_utc=NOW,
                  verify_go_signature=self.verify, nonce_unused=self.unused)
        args.update(changes)
        return cut.check_cut(**args)

    def denied(self, code, **changes):
        with self.assertRaises(cut.Stop) as error:
            self.call(**changes)
        self.assertEqual(str(error.exception), code)
        self.assertIsNone(error.exception.__cause__)

    def test_valid_consistency_still_has_no_authority(self):
        v=self.call()
        self.assertEqual(v["status"], cut.STATUS)
        self.assertEqual(v["nonce"], NONCE)
        self.assertEqual(v["go_bytes_sha256"],
                         "sha256:" + hashlib.sha256(encode(self.go)).hexdigest())
        self.assertEqual(v["app_archive_sha256"], SHA("1"))
        self.assertEqual((self.verify_count,self.nonce_count),(1,1))
        for field in ("may_apply","may_install","bootstrap_authenticated_here",
                      "nonce_consumption_durable_here",
                      "go_signature_provenance_verified_here",
                      "app_attestation_provenance_verified_here",
                      "ops_signature_provenance_verified_here"):
            self.assertIs(v[field], False)

    def test_signed_cut_cannot_mix_other_source_digests_or_scope(self):
        for field,new in [("ops_source_sha","c"*40),
                          ("app_source_sha","c"*40),
                          ("app_archive_sha256",SHA("8")),
                          ("plan_sha256",SHA("8")),
                          ("admission_sha256",SHA("8")),
                          ("policy_hash",SHA("8")),
                          ("host","other"),("environment","PRODUCTION"),
                          ("scope","ALL"),("action","ROOT_INSTALL"),
                          ("schema","qa525-go-cut-v0"),
                          ("nonce","bad")]:
            with self.subTest(field=field):
                b=dict(self.go, **{field:new})
                self.denied("GO_CUT_MISMATCH",go_bytes=encode(b))
        self.assertEqual(self.verify_count,0)

    def test_replay_nonce_and_truthy_response_fail_closed(self):
        for value in (False,1,"yes",None,{},[]):
            with self.subTest(value=value):
                self.denied("GO_CUT_NONCE_UNSAFE",
                            nonce_unused=lambda *a,v=value:v)
        self.denied("GO_CUT_NONCE_UNSAFE",
                    nonce_unused=lambda *a: (_ for _ in ()).throw(RuntimeError("secret")))
        self.assertEqual(self.verify_count,7)

    def test_missing_signature_and_truthy_verifier_cannot_authorize(self):
        for value in (False,1,"yes",None,{},[]):
            self.denied("GO_CUT_SIGNATURE_DENIED",
                        verify_go_signature=lambda *a,v=value:v)
        self.denied("GO_CUT_SIGNATURE_DENIED",verify_go_signature=lambda *a:
                    (_ for _ in ()).throw(RuntimeError("private key must not leak")))
        self.assertEqual(self.nonce_count,0)

    def test_wrong_release_and_v2_ops_receipt_denied_before_go_callback(self):
        cases=[
            dict(self.ops,schema="qa525-ops-v2"),
            dict(self.ops,app_archive_sha256=SHA("8")),
            dict(self.ops,ops_source_sha="c"*40),
            dict(self.ops,policy_hash=SHA("9")),
            dict(self.ops,program_sha256="sha256:bad"),
            {k:v for k,v in self.ops.items() if k!="app_archive_sha256"},
        ]
        for case in cases:
            with self.subTest(case=case):
                self.denied("GO_CUT_OPS_V3_MISMATCH",ops_v3_receipt=case)
        self.assertEqual(self.verify_count,0)

    def test_bad_app_subject_or_workflow_denied_before_go_callback(self):
        cases=[
            dict(self.app,source_sha="f"*40),
            dict(self.app,subject_sha256=SHA("9")),
            dict(self.app,signer_workflow=cut.APP_WORKFLOW+"/wrong"),
            dict(self.app,oidc_issuer="https://untrusted.invalid"),
        ]
        for case in cases:
            self.denied("GO_CUT_APP_SUBJECT_MISMATCH",app_subject=case)
        self.assertEqual(self.verify_count,0)

    def test_time_boundaries_future_old_expired_oversize_and_timezone(self):
        self.denied("GO_CUT_EXPIRED_OR_FUTURE",
                    now_utc=NOW+dt.timedelta(hours=1))
        self.denied("GO_CUT_EXPIRED_OR_FUTURE",
                    now_utc=NOW-dt.timedelta(hours=1))
        self.assertEqual(self.call(now_utc=NOW+dt.timedelta(minutes=10))["nonce"], NONCE)
        self.assertEqual(self.call(now_utc=NOW-dt.timedelta(minutes=10))["nonce"], NONCE)
        long=dict(self.go,expires_at="2026-11-07T23:00:01Z")
        self.denied("GO_CUT_EXPIRED_OR_FUTURE",go_bytes=encode(long))
        invalid=dict(self.go,expires_at="2026-11-07T22:00:00Z")
        self.denied("GO_CUT_EXPIRED_OR_FUTURE",go_bytes=encode(invalid))
        self.denied("GO_CUT_INPUT_INVALID",now_utc=NOW.replace(tzinfo=None))
        invalid=dict(self.go,issued_at="2026-11-07T22:99:00Z")
        self.denied("GO_CUT_TIME_INVALID",go_bytes=encode(invalid))

    def test_json_duplicate_and_noncanonical_bytes_are_denied(self):
        raw=encode(self.go)
        self.denied("GO_CUT_NOT_CANONICAL",go_bytes=raw[:-1])
        self.denied("GO_CUT_JSON_INVALID",go_bytes=raw[:-2]+b',"nonce":"reuse"}\n')
        self.denied("GO_CUT_JSON_INVALID",go_bytes=b"not json")
        self.denied("GO_CUT_FIELDS_INVALID",go_bytes=b'{}\n')
        self.denied("GO_CUT_INPUT_INVALID",go_bytes=b"")

    def test_invalid_pins_and_callback_mutation_fail_closed(self):
        self.denied("GO_CUT_PIN_INVALID",
                    pins=dict(self.pins,app_archive_sha256="sha256:broken"))
        self.denied("GO_CUT_INPUT_INVALID",
                    pins=dict(self.pins,unexpected=True))
        mutable=dict(self.pins)
        def malicious(*a):
            mutable["app_source_sha"]="f"*40
            return True
        self.denied("GO_CUT_SIGNATURE_DENIED",
                    pins=mutable,verify_go_signature=malicious)
        self.assertEqual(self.nonce_count,0)

    def test_module_has_no_host_or_child_surface(self):
        source=HERE.read_text()
        tree=ast.parse(source)
        imports={n.name for node in ast.walk(tree)
                 if isinstance(node,ast.Import) for n in node.names}
        self.assertEqual(imports,{"datetime","hashlib","json","re"})
        for marker in ("subprocess", "socket", "os.system(", "Popen(", "sudo ",
                       "docker compose", "requests", "urllib"):
            self.assertNotIn(marker,source)
        with self.assertRaises(SystemExit):
            exec(compile(source,"go-cut.py","exec"),{"__name__":"__main__"})


if __name__ == "__main__":
    unittest.main()
