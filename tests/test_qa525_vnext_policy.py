import hashlib
import importlib.util
import json
import pathlib
import unittest

ROOT = pathlib.Path(__file__).resolve().parents[1]
SPEC = importlib.util.spec_from_file_location("qa525_policy", ROOT / "scripts/qa/qa525-vnext-policy.py")
policy = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(policy)

def fixture():
    m = {"id": "20261118000001_Example", "additive": True,
         "backward_compatible_with_api_n": True, "arbitrary_sql": False,
         "unbounded_backfill": False, "incompatible_ddl_lock": False,
         "needs_api_n_plus_1": False}
    ids = m["id"] + "\n"
    plan = {"ordered_set_sha256": "sha256:" + hashlib.sha256(ids.encode()).hexdigest(),
            "migrations": [m]}
    release = {"sha": "a" * 40, "baseline_images": {"api": "sha256:" + "1" * 64, "web": "sha256:" + "2" * 64},
               "expected_images": {"api": "sha256:" + "3" * 64, "web": "sha256:" + "4" * 64}}
    pins = {"release_sha": release["sha"], "plan_sha256": policy.plan_digest(plan),
            "baseline_images": dict(release["baseline_images"]), "expected_images": dict(release["expected_images"])}
    return plan, release, pins

class Qa525VnextReleaseBoundPolicyTests(unittest.TestCase):
    def test_exact_binding(self):
        self.assertTrue(policy.plan_attested(*fixture()))
    def test_flag_tampering_rejected(self):
        p,r,t = fixture()
        p["migrations"][0]["additive"] = False
        self.assertFalse(policy.plan_attested(p,r,t))
    def test_id_tampering_rejected(self):
        p,r,t = fixture()
        p["migrations"][0]["id"] = "20261118000002_Example"
        self.assertFalse(policy.plan_attested(p,r,t))
    def test_image_substitution_rejected(self):
        p,r,t = fixture()
        r["baseline_images"]["api"] = "sha256:" + "5" * 64
        self.assertFalse(policy.plan_attested(p,r,t))
    def test_release_substitution_rejected(self):
        p,r,t = fixture()
        r["sha"] = "b" * 40
        self.assertFalse(policy.plan_attested(p,r,t))
    def test_untrusted_missing_pins_rejected(self):
        p,r,t = fixture()
        del t["plan_sha256"]
        self.assertFalse(policy.plan_attested(p,r,t))
    def test_unknown_fields_rejected(self):
        p,r,t = fixture()
        p["migrations"][0]["arbitrary_unreviewed"] = True
        self.assertFalse(policy.plan_attested(p,r,t))
    def test_ordered_set_hash_mismatch_rejected_even_with_matching_plan_pin(self):
        p,r,t = fixture()
        p["ordered_set_sha256"] = "sha256:" + "f" * 64
        t["plan_sha256"] = "sha256:" + hashlib.sha256(policy.canonical(p)).hexdigest()
        self.assertFalse(policy.plan_attested(p,r,t))
    def test_duplicate_migration_id_rejected(self):
        p,r,t = fixture()
        p["migrations"].append(dict(p["migrations"][0]))
        self.assertFalse(policy.plan_attested(p,r,t))
    def test_invalid_migration_id_rejected(self):
        p,r,t = fixture()
        p["migrations"][0]["id"] = "invalid"
        self.assertFalse(policy.plan_attested(p,r,t))
    def test_nonboolean_flag_rejected(self):
        p,r,t = fixture()
        p["migrations"][0]["additive"] = 1
        self.assertFalse(policy.plan_attested(p,r,t))

if __name__ == "__main__":
    unittest.main()
