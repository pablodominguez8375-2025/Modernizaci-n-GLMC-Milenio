"""Contract tests for QA fixtures. Never touches a real DB or QA host."""
import importlib.util
import json
import stat
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

SCRIPT = Path(__file__).resolve().parents[1] / "scripts" / "qa" / "qa_operational_fixtures.py"
spec = importlib.util.spec_from_file_location("qa_operational_fixtures", SCRIPT)
fixtures = importlib.util.module_from_spec(spec)
spec.loader.exec_module(fixtures)


class OperationalFixtureTests(unittest.TestCase):
    def test_deterministic_output_and_provenance(self):
        one = fixtures.make_dataset(35, 3, 12)
        two = fixtures.make_dataset(35, 3, 12)
        self.assertEqual(fixtures.serialize(one), fixtures.serialize(two))
        self.assertEqual(one["manifest"]["classification"], "SYNTHETIC_ONLY")
        self.assertEqual(one["manifest"]["import_status"], "NOT_IMPORTED")
        self.assertEqual(len(one["persons"]), 35)
        self.assertEqual(len(one["workshops"]), 3)
        self.assertTrue(all(x["email"].endswith("@pmgm.invalid") for x in one["persons"]))
        self.assertTrue(all(x["identity_type"] == "other" for x in one["persons"]))

    def test_foreign_keys_and_no_cross_currency(self):
        d = fixtures.make_dataset(64, 4, 18)
        fixtures.validate(d)
        dues = {q["id"]: q for q in d["quotas"]}
        receipts = {r["id"]: r for r in d["cash_receipts"]}
        self.assertEqual(len(d["quota_allocations"]), len(d["cash_receipts"]))
        self.assertEqual(sum(r["amount"] for r in d["cash_receipts"]),
                         sum(i["amount"] for i in d["quota_allocations"]))
        for a in d["quota_allocations"]:
            due, receipt = dues[a["quota_id"]], receipts[a["receipt_id"]]
            self.assertEqual(due["member_id"], receipt["member_id"])
            self.assertEqual(due["workshop_id"], receipt["workshop_id"])
            self.assertEqual(due["currency"], receipt["currency"])

    def test_rejects_cross_workshop_allocation(self):
        d = fixtures.make_dataset(16, 3, 2)
        receipts = {r["id"]: r for r in d["cash_receipts"]}
        a = d["quota_allocations"][0]
        # Changing a receipt's workshop is invalid even for shared CLP currency.
        receipt = receipts[a["receipt_id"]]
        other = next(w for w in d["workshops"]
                     if w["id"] != receipt["workshop_id"]
                     and w["currency"] == receipt["currency"])
        receipt["workshop_id"] = other["id"]
        with self.assertRaises(ValueError):
            fixtures.validate(d)

    def test_cash_date_is_independent_from_quota_period(self):
        d = fixtures.make_dataset(42, 2, 24)
        receipts = {r["id"]: r for r in d["cash_receipts"]}
        dues = {q["id"]: q for q in d["quotas"]}
        self.assertTrue(any(dues[i["quota_id"]]["period"][:4] == "2025"
                            and receipts[i["receipt_id"]]["received_on"][:4] == "2026"
                            for i in d["quota_allocations"]))
        self.assertTrue(any(dues[i["quota_id"]]["period"] !=
                            receipts[i["receipt_id"]]["received_on"][:7]
                            for i in d["quota_allocations"]))

    def test_past_active_only_annual_hospitalaria(self):
        d = fixtures.make_dataset(40, 4, 12)
        past = {m["id"] for m in d["memberships"]
                if m["category"] == "past_active" and m["status"] == "active"}
        self.assertTrue(past)
        self.assertFalse(past.intersection({q["member_id"] for q in d["quotas"]}))
        self.assertEqual(past, {h["member_id"] for h in d["hospitalaria"]})

    def test_rejects_nonfake_person_identifiers(self):
        d = fixtures.make_dataset(8, 2, 1)
        d["persons"][0]["email"] = "someone@example.com"
        with self.assertRaises(ValueError):
            fixtures.validate(d)
        d["persons"][0]["email"] = "persona.qa.000001@pmgm.invalid"
        d["persons"][0]["identity_number"] = "12345678-5"
        with self.assertRaises(ValueError):
            fixtures.validate(d)

    def test_limits_fail_closed(self):
        for arguments in ((0, 4, 12), (2001, 4, 12),
                          (10, 1, 1), (10, 31, 1),
                          (10, 2, 0), (10, 2, 25)):
            with self.subTest(arguments=arguments):
                with self.assertRaises(ValueError):
                    fixtures.make_dataset(*arguments)

    def test_cli_no_default_export_and_private_no_overwrite(self):
        with tempfile.TemporaryDirectory() as td:
            path = Path(td) / "synthetic.json"
            base = [sys.executable, str(SCRIPT), "--members", "6",
                    "--workshops", "2", "--months", "2"]
            dry = subprocess.run(base, capture_output=True, text=True, check=True)
            self.assertFalse(json.loads(dry.stdout)["written"])
            self.assertFalse(path.exists())
            result = subprocess.run(base + ["--output", str(path)],
                                    capture_output=True, text=True, check=True)
            self.assertTrue(json.loads(result.stdout)["written"])
            self.assertEqual(stat.S_IMODE(path.stat().st_mode), 0o600)
            self.assertEqual(json.loads(path.read_text())["manifest"]["import_status"],
                             "NOT_IMPORTED")
            retry = subprocess.run(base + ["--output", str(path)],
                                   capture_output=True, text=True)
            self.assertNotEqual(retry.returncode, 0)


if __name__ == "__main__":
    unittest.main()
