"""QA525 read-only PostgreSQL post-migration host probe: hermetic tests."""
import importlib.util
from pathlib import Path
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[1]
FILE = ROOT / "scripts/qa/operators/qa525-post-db-readonly.py"
spec = importlib.util.spec_from_file_location("qa525_post_db_host_test", FILE)
host = importlib.util.module_from_spec(spec)
spec.loader.exec_module(host)

gate_file = ROOT / "scripts/qa/operators/qa525-member-history-schema-gate.py"
gate_spec = importlib.util.spec_from_file_location("qa525_post_db_gate_test", gate_file)
gate = importlib.util.module_from_spec(gate_spec)
gate_spec.loader.exec_module(gate)

BASE = ["2026_Original_%03d" % i for i in range(98)]
TABLES = list(gate.TABLES)
INDEXES = list(gate.INDEXES)


def fake_docker(*, history=None, tables=None, indexes=None):
    data = {
        host.HISTORY_SQL: BASE + [gate.MIGRATION_ID] if history is None else history,
        host.TABLES_SQL: TABLES if tables is None else tables,
        host.INDEXES_SQL: INDEXES if indexes is None else indexes,
    }
    called = []
    def docker(args):
        called.append(args)
        if args[:2] != ["exec", host.PG_CONTAINER] or args[-1] not in data:
            raise AssertionError("unexpected Docker command")
        return "\n".join(data[args[-1]])
    return docker, called


class ReadonlyPostDBTests(unittest.TestCase):
    def test_exact_99_and_expected_tables_indexes(self):
        docker, calls = fake_docker()
        result = host.inspect({"database_migrations": BASE}, docker, gate)
        self.assertEqual(result["history_count"], 99)
        self.assertFalse(result["may_apply"])
        self.assertFalse(result["may_migrate"])
        self.assertEqual(len(calls), 3)
        self.assertTrue(all(c[:2] == ["exec", host.PG_CONTAINER] for c in calls))
        self.assertTrue(all(c[-1].startswith("BEGIN READ ONLY") for c in calls))
        self.assertTrue(all(c[-1].endswith("COMMIT;") for c in calls))

    def test_missing_and_rogue_history_fail(self):
        for history in (BASE, BASE + ["another"],
                        BASE + [BASE[0]], BASE[:-1] + [gate.MIGRATION_ID]):
            with self.subTest(history=history[-1]):
                docker, _ = fake_docker(history=history)
                with self.assertRaises(gate.Blocked):
                    host.inspect({"database_migrations": BASE}, docker, gate)

    def test_missing_table_and_index_fail(self):
        for kw in ({"tables": TABLES[:1]}, {"indexes": INDEXES[:1]}):
            docker, _ = fake_docker(**kw)
            with self.assertRaises(gate.Blocked):
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
            with self.assertRaisesRegex(host.Blocked, "ONLY_GLM DCH_ROOT_QA".replace(" ","_")):
                host.main(["--inspect-readonly"])

    def test_docker_result_size_bounded(self):
        with self.assertRaisesRegex(host.Blocked, "QUERY_RESULT_INVALID"):
            host.checked_query(lambda _: "x" * 32768, host.HISTORY_SQL)


if __name__ == "__main__":
    unittest.main()
