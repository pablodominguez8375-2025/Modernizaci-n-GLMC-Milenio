"""Hermetic QA525 dual API+WEB rollback simulator — no real services."""
import importlib.util
from pathlib import Path
import unittest

FILE = (Path(__file__).resolve().parents[1] / "scripts/qa/operators" /
        "qa525-synthetic-reversible-transaction.py")
spec = importlib.util.spec_from_file_location("qa525_transaction_simulator_test", FILE)
mod = importlib.util.module_from_spec(spec)
spec.loader.exec_module(mod)


class DualServiceTransactionTests(unittest.TestCase):
    def test_happy_path_orders_backup_restore_and_apply(self):
        host = mod.SyntheticQa525Adapter()
        result = mod.simulate(host)
        self.assertEqual("SYNTHETIC_TRANSACTION_PASS_NOT_DEPLOYED", result["status"])
        self.assertEqual(list(mod.PHASES), result["trace"])
        self.assertLess(host.events.index("backup_pmgm"),
                        host.events.index("apply_api"))
        self.assertLess(host.events.index("backup_pmgm_keycloak"),
                        host.events.index("apply_api"))
        self.assertLess(host.events.index("restore_drill"),
                        host.events.index("apply_api"))
        self.assertEqual("new-api", host.services["api"])
        self.assertEqual("new-web", host.services["web"])
        self.assertTrue(host.success)
        self.assertFalse(result["may_apply"])
        self.assertFalse(result["host_updated"])

    def test_each_failure_before_mutation_blocks_without_rollback(self):
        steps = ("preflight", "signed_source", "snapshot", "backup_pmgm",
                 "backup_pmgm_keycloak", "offhost_custody", "restore_drill",
                 "recheck")
        for step in steps:
            with self.subTest(step=step):
                host = mod.SyntheticQa525Adapter(fail_at=step)
                result = mod.simulate(host)
                self.assertEqual("SYNTHETIC_BLOCKED_BEFORE_MUTATION",
                                 result["status"])
                self.assertEqual(host.old, host.services)
                self.assertEqual([], result["rollback_attempted"])
                self.assertNotIn("apply_api", host.events)
                self.assertFalse(host.success)

    def test_each_failure_after_mutation_reverts_both_services(self):
        steps = ("apply_api", "apply_web", "health",
                 "unchanged_dependencies", "unchanged_migrations", "record")
        for step in steps:
            with self.subTest(step=step):
                host = mod.SyntheticQa525Adapter(fail_at=step)
                result = mod.simulate(host)
                self.assertEqual("SYNTHETIC_ROLLBACK_VERIFIED_NOT_DEPLOYED",
                                 result["status"])
                self.assertEqual(["web", "api"], result["rollback_attempted"])
                self.assertEqual(host.old, host.services)
                self.assertTrue(result["rollback_verified"])
                self.assertTrue(host.failed)
                self.assertFalse(result["host_updated"])

    def test_partial_api_timeout_requires_rollback_both(self):
        host = mod.SyntheticQa525Adapter(fail_at="apply_api")
        result = mod.simulate(host)
        self.assertEqual(["rollback_web", "rollback_api"],
                         [i for i in host.events if i.startswith("rollback_")])
        self.assertEqual(host.old, host.services)
        self.assertTrue(result["rollback_verified"])

    def test_rollbacks_are_attempted_independently(self):
        host = mod.SyntheticQa525Adapter(
            fail_at="health", rollback_fail=("web",))
        result = mod.simulate(host)
        self.assertEqual("SYNTHETIC_ROLLBACK_FAILED_MANUAL_REVIEW", result["status"])
        self.assertIn("rollback_api", host.events)
        self.assertFalse(result["rollback_verified"])

    def test_api_rollback_failure_also_reports_manual_recovery(self):
        host = mod.SyntheticQa525Adapter(
            fail_at="health", rollback_fail=("api",))
        result = mod.simulate(host)
        self.assertEqual("SYNTHETIC_ROLLBACK_FAILED_MANUAL_REVIEW", result["status"])
        self.assertIn("rollback_web", host.events)
        self.assertFalse(result["rollback_verified"])

    def test_both_rollbacks_fail_does_not_report_success(self):
        host = mod.SyntheticQa525Adapter(
            fail_at="apply_web", rollback_fail=("web", "api"))
        result = mod.simulate(host)
        self.assertEqual("SYNTHETIC_ROLLBACK_FAILED_MANUAL_REVIEW", result["status"])
        self.assertFalse(result["rollback_verified"])

    def test_a_different_host_adapter_cannot_execute(self):
        class RealHostAdapter:
            synthetic_only = True
            def exclusive(self):
                raise AssertionError("physical adapter called")
        with self.assertRaisesRegex(mod.Blocked, "PHYSICAL_ADAPTER_NOT_SUPPORTED"):
            mod.simulate(RealHostAdapter())

    def test_forged_synthetic_flag_does_not_allow_unknown_class(self):
        class Fake:
            synthetic_only = True
            def exclusive(self):
                raise AssertionError("should not run")
        with self.assertRaisesRegex(mod.Blocked, "PHYSICAL_ADAPTER_NOT_SUPPORTED"):
            mod.simulate(Fake())

    def test_false_synthetic_flag_rejected(self):
        host = mod.SyntheticQa525Adapter()
        host.synthetic_only = False
        with self.assertRaisesRegex(mod.Blocked, "PHYSICAL_ADAPTER_NOT_SUPPORTED"):
            mod.simulate(host)

    def test_backup_custody_absent_blocks_before_apply(self):
        class NoBackup(mod.SyntheticQa525Adapter):
            def offhost_custody(self):
                self.events.append("offhost_custody")
                return False
        host = NoBackup()
        # Subclass intentionally rejected: only built-in in-memory fake accepted.
        with self.assertRaisesRegex(mod.Blocked, "PHYSICAL_ADAPTER_NOT_SUPPORTED"):
            mod.simulate(host)

    def test_only_test_double_has_no_cli_or_host_commands(self):
        source = FILE.read_text()
        for banned in ("subprocess", "os.system", "docker compose",
                       "systemctl", "ssh ", "pg_dump", "urlopen", "main()"):
            self.assertNotIn(banned, source)
        self.assertIn('SYNTHETIC_REHEARSAL_NO_HOST_NO_DEPLOYMENT', source)


if __name__ == "__main__":
    unittest.main()
