"""QA synthetic import preflight: no network, database or writes outside tmp test fixture."""
from __future__ import annotations

import contextlib
import io
import json
import os
import sys
import tempfile
import unittest
from pathlib import Path
from uuid import uuid5

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "scripts" / "qa"))
import qa_synthetic_preflight as preflight  # noqa: E402


def uid(key: str) -> str:
    return str(uuid5(preflight.NAMESPACE, "test:" + key))


def fixture() -> dict:
    import qa_operational_fixtures as generator
    return generator.make_dataset(member_count=12, workshop_count=4, month_count=1)


class QaSyntheticPreflightTests(unittest.TestCase):
    def test_valid_offline_plan_is_explicitly_not_imported(self):
        report = preflight.preflight(fixture(), "f" * 64)
        self.assertEqual("READY_FOR_MAPPING_ONLY", report["status"])
        self.assertEqual("NOT_IMPORTED", report["import_status"])
        self.assertEqual(0, report["writes_performed"])
        self.assertEqual(12, report["first_stage_ef_mapping"]["Member"])
        self.assertEqual(12, report["first_stage_ef_mapping"]["Membership"])
        self.assertGreater(report["not_mapped"]["quota_allocations"], 0)
        self.assertNotIn("persona.qa.", json.dumps(report))
        self.assertNotIn(fixture()["persons"][0]["id"], json.dumps(report))

    def assert_rejected(self, dataset: dict, code: str):
        with self.assertRaises(preflight.PreflightError) as exc:
            preflight.preflight(dataset, "e" * 64)
        self.assertEqual(code, exc.exception.code)

    def test_ef_contract_is_structural_and_never_maps_fictitious_rut(self):
        report = preflight.preflight(fixture(), "f" * 64)
        ef = report["ef_schema_contract"]
        self.assertEqual("workshops", ef["Organization"]["source"])
        self.assertEqual("constant:workshop", ef["Organization"]["fields"]["Type"])
        self.assertIn("currency", ef["Organization"]["excluded"])
        self.assertEqual("constant:null", ef["Person"]["fields"]["Rut"])
        self.assertIn("identity_number", ef["Person"]["excluded"])
        self.assertEqual("id", ef["Member"]["fields"]["Id"])
        self.assertNotEqual(ef["Member"]["fields"]["Id"],
                            ef["Membership"]["fields"]["Id"])
        self.assertEqual("id", ef["Membership"]["fields"]["MemberId"])
        self.assertNotIn("persona.qa.", json.dumps(report))

    def test_rejects_non_synthetic_or_previously_imported(self):
        d = fixture()
        d["manifest"]["classification"] = "PERSONAL_REAL"
        self.assert_rejected(d, "NON_SYNTHETIC_DATA")
        d = fixture()
        d["manifest"]["import_status"] = "IMPORTED"
        self.assert_rejected(d, "IMPORT_STATUS_UNSAFE")

    def test_rejects_personal_identifiers_and_duplicate_ids(self):
        d = fixture()
        d["persons"][0]["email"] = "user@example.com"
        self.assert_rejected(d, "NON_SYNTHETIC_PERSON")
        d = fixture()
        d["persons"][0]["id"] = d["workshops"][0]["id"]
        self.assert_rejected(d, "DUPLICATE_IDENTIFIER")

    def test_rejects_cross_workshop_and_cross_currency(self):
        d = fixture()
        d["cash_receipts"][0]["workshop_id"] = d["workshops"][1]["id"]
        self.assert_rejected(d, "CROSS_WORKSHOP_REFERENCE")
        d = fixture()
        d["quotas"][0]["currency"] = "USD"
        self.assert_rejected(d, "CROSS_CURRENCY_REFERENCE")

    def test_rejects_bad_foreign_keys_and_allocation_mismatch(self):
        d = fixture()
        d["attendance"][0]["member_id"] = uid("unknown-member")
        self.assert_rejected(d, "INVALID_REFERENCE")
        d = fixture()
        d["quota_allocations"][0]["amount"] = 10000
        self.assert_rejected(d, "ALLOCATION_MISMATCH")

    def test_unsafe_sections_and_parameter_mismatch(self):
        d = fixture()
        d["unknown"] = []
        self.assert_rejected(d, "UNEXPECTED_SECTIONS")
        d = fixture()
        d["manifest"]["fixture_parameters"]["members"] = 20
        self.assert_rejected(d, "PARAMETER_MISMATCH")
        d = fixture()
        d["manifest"]["fixture_parameters"]["members"] = True
        self.assert_rejected(d, "PARAMETER_MISMATCH")

    def test_rejects_hidden_personal_field_in_every_section(self):
        for section in preflight.AREAS:
            with self.subTest(section=section):
                dataset = fixture()
                self.assertTrue(dataset[section], section)
                dataset[section][0]["observacionesPrivadas"] = "información ajena al lote"
                self.assert_rejected(dataset, "NON_CANONICAL_SYNTHETIC_DATA")

    def test_rejects_unexpected_manifest_and_parameters(self):
        dataset = fixture()
        dataset["manifest"]["secret_note"] = "dato ajeno"
        self.assert_rejected(dataset, "NON_CANONICAL_SYNTHETIC_DATA")
        dataset = fixture()
        dataset["manifest"]["fixture_parameters"]["extra"] = 10
        self.assert_rejected(dataset, "NON_CANONICAL_SYNTHETIC_DATA")
        dataset = fixture()
        dataset["manifest"]["warnings"].append("Personal data allowed")
        self.assert_rejected(dataset, "NON_CANONICAL_SYNTHETIC_DATA")

    def test_rejects_synthetic_prefix_with_injected_real_value(self):
        cases = (
            ("workshops", "name", "Taller ficticio QA 01 / nombre real"),
            ("persons", "last_names", "Ficticia QA 00001 Nombre Real"),
            ("persons", "identity_number", "QA-NO-REAL-00000112345678"),
            ("memberships", "institutional_number", "QA-M-000001 real"),
            ("offices", "type", "secretary datos privados"),
            ("cash_receipts", "kind", "test_cash_receipt cliente real"),
            ("meetings", "type", "regular nombre personal"),
        )
        for section, field, value in cases:
            with self.subTest(section=section, field=field):
                dataset = fixture()
                dataset[section][0][field] = value
                self.assert_rejected(dataset, "NON_CANONICAL_SYNTHETIC_DATA")

    def test_cli_rejects_unexpected_personal_fields_without_leaking(self):
        dataset = fixture()
        dataset["persons"][0]["notasPrivadas"] = "persona de prueba ajena"
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "sensitive.json"
            path.write_text(json.dumps(dataset), encoding="utf-8")
            stdout = io.StringIO()
            with contextlib.redirect_stdout(stdout):
                result = preflight.main(["--input", str(path)])
            self.assertEqual(2, result)
            report = json.loads(stdout.getvalue())
            self.assertEqual("NON_CANONICAL_SYNTHETIC_DATA", report["error_code"])
            self.assertEqual(0, report["writes_performed"])
            self.assertNotIn("notasPrivadas", stdout.getvalue())
            self.assertNotIn("persona de prueba ajena", stdout.getvalue())
            self.assertNotIn(str(path), stdout.getvalue())

    def test_json_duplicate_keys_are_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "dupe.json"
            path.write_text('{"manifest":{}, "manifest":{}}', encoding="utf-8")
            with self.assertRaises(preflight.PreflightError) as err:
                preflight.load_fixture(path)
            self.assertEqual("DUPLICATE_JSON_KEY", err.exception.code)

    def test_cli_emits_only_counts_and_hash_no_source_values(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "batch.json"
            path.write_text(json.dumps(fixture()), encoding="utf-8")
            stdout = io.StringIO()
            with contextlib.redirect_stdout(stdout):
                status = preflight.main(["--input", str(path)])
            self.assertEqual(0, status)
            output = stdout.getvalue()
            self.assertNotIn("persona.qa.", output)
            self.assertNotIn("QA-NO-REAL-", output)
            self.assertNotIn(uid("member-1"), output)
            self.assertNotIn(str(path), output)
            self.assertEqual(0, json.loads(output)["writes_performed"])

    def test_cli_failure_never_exposes_offending_values(self):
        d = fixture()
        d["persons"][0]["email"] = "private-address@example.com"
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "batch.json"
            path.write_text(json.dumps(d), encoding="utf-8")
            stdout = io.StringIO()
            with contextlib.redirect_stdout(stdout):
                status = preflight.main(["--input", str(path)])
            self.assertEqual(2, status)
            self.assertEqual("NON_SYNTHETIC_PERSON", json.loads(stdout.getvalue())["error_code"])
            self.assertNotIn("private-address", stdout.getvalue())

    def test_does_not_require_or_connect_network_database_or_keycloak(self):
        # This helper's public API has no URL, token, DB connection or write path.
        self.assertEqual(0, preflight.preflight(fixture(), "a" * 64)["writes_performed"])
        self.assertFalse(hasattr(preflight, "apply"))
        self.assertFalse(hasattr(preflight, "import_data"))

    def test_generator_output_when_available(self):
        try:
            import qa_operational_fixtures as generator
        except ImportError:
            return  # PR #679 must land in dev; standalone contract stays testable.
        dataset = generator.make_dataset(member_count=32, workshop_count=4,
                                         month_count=2)
        report = preflight.preflight(dataset, "b" * 64)
        self.assertEqual("READY_FOR_MAPPING_ONLY", report["status"])
        self.assertEqual(32, report["first_stage_ef_mapping"]["Member"])


if __name__ == "__main__":
    unittest.main()
