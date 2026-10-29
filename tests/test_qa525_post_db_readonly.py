"""QA525 exact read-only PostgreSQL post-migration host probe tests."""
import importlib.util
from pathlib import Path
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[1]


def load(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    value = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(value)
    return value


host = load("qa525_post_db_host_test", ROOT / "scripts/qa/operators/qa525-post-db-readonly.py")
gate = load("qa525_post_db_gate_test", ROOT / "scripts/qa/operators/qa525-member-history-schema-gate.py")
BASE = ["2026_Original_%03d" % i for i in range(98)]


def fake_docker(**overrides):
    data = {
        host.HISTORY_SQL: BASE + [gate.MIGRATION_ID],
        host.TABLES_SQL: list(gate.TABLES),
        host.INDEXES_SQL: list(gate.INDEX_DEFINITIONS),
        host.COLUMNS_SQL: list(gate.EXPECTED_COLUMNS),
        host.CONSTRAINTS_SQL: list(gate.EXPECTED_CONSTRAINTS),
    }
    for name, value in overrides.items():
        data[getattr(host, name)] = value
    called = []
    def docker(args):
        called.append(args)
        if args[:2] != ["exec", host.PG_CONTAINER] or args[-1] not in data:
            raise AssertionError("unexpected Docker command")
        return "\n".join(data[args[-1]])
    return docker, called


class ReadonlyPostDBTests(unittest.TestCase):
    def test_exact_99_and_complete_physical_schema(self):
        docker, calls = fake_docker()
        result = host.inspect({"database_migrations": BASE}, docker, gate)
        self.assertEqual(result["history_count"], 99)
        self.assertEqual(result["expected_columns_present"], 22)
        self.assertEqual(result["expected_constraints_present"], 3)
        self.assertFalse(result["may_apply"])
        self.assertFalse(result["may_migrate"])
        self.assertEqual(len(calls), 5)
        self.assertTrue(all(c[-1].startswith("BEGIN READ ONLY;") for c in calls))
        self.assertTrue(all(c[-1].endswith("COMMIT;") for c in calls))

    def test_missing_and_rogue_history_fail(self):
        for history in (BASE, BASE + ["another"], BASE + [BASE[0]],
                        BASE[:-1] + [gate.MIGRATION_ID]):
            docker, _ = fake_docker(HISTORY_SQL=history)
            with self.assertRaises(gate.Blocked):
                host.inspect({"database_migrations": BASE}, docker, gate)

    def test_column_type_or_nullability_drift_fails(self):
        columns = list(gate.EXPECTED_COLUMNS)
        columns[2] = columns[2].replace("character varying(20)|t", "text|f")
        docker, _ = fake_docker(COLUMNS_SQL=columns)
        with self.assertRaisesRegex(gate.Blocked, "COLUMNS_MISMATCH"):
            host.inspect({"database_migrations": BASE}, docker, gate)

    def test_missing_pk_or_fk_without_cascade_fails(self):
        docker, _ = fake_docker(CONSTRAINTS_SQL=list(gate.EXPECTED_CONSTRAINTS[1:]))
        with self.assertRaisesRegex(gate.Blocked, "CONSTRAINTS_MISMATCH"):
            host.inspect({"database_migrations": BASE}, docker, gate)
        constraints = list(gate.EXPECTED_CONSTRAINTS)
        constraints[0] = constraints[0][:-1] + "a"
        docker, _ = fake_docker(CONSTRAINTS_SQL=constraints)
        with self.assertRaisesRegex(gate.Blocked, "CONSTRAINTS_MISMATCH"):
            host.inspect({"database_migrations": BASE}, docker, gate)

    def test_index_column_order_drift_fails(self):
        indexes = list(gate.INDEX_DEFINITIONS)
        indexes[1] = indexes[1].replace("MemberId,Status", "Status,MemberId")
        docker, _ = fake_docker(INDEXES_SQL=indexes)
        with self.assertRaisesRegex(gate.Blocked, "INDEXES_MISMATCH"):
            host.inspect({"database_migrations": BASE}, docker, gate)

    def test_incomplete_installed_receipt_fails_before_docker(self):
        docker, calls = fake_docker()
        with self.assertRaisesRegex(host.Blocked, "INSTALLED_API_EF_RECEIPT_INVALID"):
            host.inspect({"database_migrations": None}, docker, gate)
        self.assertEqual(calls, [])

    def test_unknown_query_or_attempted_write_denied(self):
        docker, calls = fake_docker()
        for query in ("BEGIN; SELECT 1; COMMIT;",
                      "BEGIN READ ONLY; DELETE FROM core.members; COMMIT;",
                      "BEGIN READ ONLY; SELECT 1; COMMIT;"):
            with self.assertRaisesRegex(host.Blocked, "QUERY_NOT_READ_ONLY_ALLOWLISTED"):
                host.checked_query(docker, query)
        self.assertEqual(calls, [])

    def test_only_named_readonly_cli_and_glmdch_root(self):
        with self.assertRaisesRegex(host.Blocked, "ONLY_EXPLICIT_READ_ONLY_PROBE"):
            host.main(["--apply-supervised"])
        with patch.object(host.os, "geteuid", return_value=1000):
            with self.assertRaisesRegex(host.Blocked, "ONLY_GLM_DCH_ROOT_QA"):
                host.main(["--inspect-readonly"])

    def test_docker_result_size_bounded(self):
        with self.assertRaisesRegex(host.Blocked, "QUERY_RESULT_INVALID"):
            host.checked_query(lambda _: "x" * 32768, host.HISTORY_SQL)


if __name__ == "__main__":
    unittest.main()
