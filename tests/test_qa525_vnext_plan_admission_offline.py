"""QA525 evidence contracts: 9 pure tests; fake hashes never authorize anything."""
import importlib.util
from pathlib import Path
import unittest

path = Path(__file__).resolve().parents[1] / "scripts" / "qa" / "qa525-plan-admission-offline.py"
spec = importlib.util.spec_from_file_location("qa525_evidence", path)
m = importlib.util.module_from_spec(spec)
spec.loader.exec_module(m)

class ContractTests(unittest.TestCase):
    def plan(self):
        return {"schema":"qa525-plan-launcher-only-v1","state":"PENDING_REVIEW_NO_GO",
                "host":"glmdch","environment":"QA_GLMDCH_ONLY","scope":m.SCOPE,
                "operator":"soporte@glmdch","source_sha":"a"*40,
                "app_archive_sha256":"sha256:"+"b"*64,"old_launcher_sha256":"c"*64,
                "candidate_operator_sha256":"d"*64,"ops_receipt_sha256":"sha256:"+"e"*64,
                "release_asset_sha256":"sha256:"+"f"*64,"policy_hash":"sha256:"+"1"*64}
    def admission(self,p):
        return {"schema":"qa525-admission-launcher-only-v1",
                "state":"PENDING_EVIDENCE_NO_GO",
                "plan_sha256":m.check_plan(m.canonical(p)),
                "source_sha":p["source_sha"],"environment":"QA_GLMDCH_ONLY",
                "scope":m.SCOPE,
                "gates":{k:{"status":"PENDING","evidence_sha256":None,
                            "checked_at_utc":None,"reference":None} for k in m.GATES}}
    def test_pending_never_approved(self):
        p=self.plan()
        r=m.check_admission(m.canonical(self.admission(p)),m.canonical(p))
        self.assertFalse(r["may_apply"])
        self.assertEqual(len(r["pending_gates"]),7)
    def test_bad_source_sha(self):
        p=self.plan();p["source_sha"]="a"*39
        with self.assertRaises(m.Reject):m.check_plan(m.canonical(p))
    def test_wrong_environment(self):
        p=self.plan();p["environment"]="QA_SYNTHETIC"
        with self.assertRaises(m.Reject):m.check_plan(m.canonical(p))
    def test_wrong_plan_binding(self):
        p=self.plan();a=self.admission(p);a["plan_sha256"]="sha256:"+"0"*64
        with self.assertRaises(m.Reject):m.check_admission(m.canonical(a),m.canonical(p))
    def test_extra_field(self):
        p=self.plan();p["go"]=True
        with self.assertRaises(m.Reject):m.check_plan(m.canonical(p))
    def test_noncanonical(self):
        with self.assertRaises(m.Reject):m.check_plan(m.canonical(self.plan())+b"\n")
    def test_missing_gate(self):
        p=self.plan();a=self.admission(p);a["gates"].pop("minio")
        with self.assertRaises(m.Reject):m.check_admission(m.canonical(a),m.canonical(p))
    def test_fake_pass(self):
        p=self.plan();a=self.admission(p);a["gates"]["minio"]["status"]="PASS"
        with self.assertRaises(m.Reject):m.check_admission(m.canonical(a),m.canonical(p))
    def test_all_pass_never_grants_go(self):
        p=self.plan();a=self.admission(p)
        for k in m.GATES:
            a["gates"][k]={"status":"PASS","evidence_sha256":"sha256:"+"a"*64,
                           "checked_at_utc":"2026-11-11T14:00:00Z",
                           "reference":"SYNTHETIC_TEST_ONLY"}
        r=m.check_admission(m.canonical(a),m.canonical(p))
        self.assertFalse(r["may_apply"]);self.assertFalse(r["ready_for_review"])
        self.assertEqual(r["pending_gates"],[])

if __name__ == "__main__":
    unittest.main()
