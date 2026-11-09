"""QA525/Silo: el primer alias IAM admite solo un reintento acotado; nunca se salta ACL."""
import importlib.util
from pathlib import Path
from types import SimpleNamespace
import unittest

SRC = Path(__file__).with_name("test_silo_real_iam.py")
SPEC = importlib.util.spec_from_file_location("qa525_silo_iam_contract", SRC)
iam = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(iam)


class InitialAdminAliasTests(unittest.TestCase):
    def make_cli(self, results, seen):
        values = iter(results)

        def cli(*argv, check=True):
            self.assertIs(check, False)
            seen.append(argv)
            result = next(values)
            return None if result is None else SimpleNamespace(returncode=result)
        return cli

    def test_first_success_has_no_retry_or_delay(self):
        seen, delays = [], []
        iam.configure_initial_admin_alias(
            self.make_cli([0], seen), "synthetic-admin", "secret-only-test",
            sleep=delays.append, attempts=4)
        self.assertEqual(len(seen), 1)
        self.assertEqual(delays, [])

    def test_temporary_alias_failure_retries_only_initial_alias(self):
        seen, delays = [], []
        iam.configure_initial_admin_alias(
            self.make_cli([1, None, 0], seen), "synthetic-admin", "secret-only-test",
            sleep=delays.append, attempts=4)
        self.assertEqual(len(seen), 3)
        self.assertEqual(delays, [2, 2])
        self.assertTrue(all(argv[:5] == (
            "alias", "set", "--", "admin", "http://127.0.0.1:9000") for argv in seen))
        self.assertTrue(all(argv[-2:] == (
            "synthetic-admin", "secret-only-test") for argv in seen))

    def test_persistent_failure_stops_after_four_calls_without_secret_output(self):
        seen, delays = [], []
        with self.assertRaisesRegex(
            AssertionError, "^SILO_IAM_ADMIN_ALIAS_NOT_READY$"
        ) as ctx:
            iam.configure_initial_admin_alias(
                self.make_cli([1, 1, 2, None], seen),
                "synthetic-admin", "unprinted-secret",
                sleep=delays.append, attempts=4)
        self.assertEqual(len(seen), 4)
        self.assertEqual(delays, [2, 2, 2])
        self.assertNotIn("unprinted-secret", str(ctx.exception))

    def test_invalid_retry_budget_is_rejected_before_any_call(self):
        seen = []
        with self.assertRaisesRegex(ValueError, "SILO_IAM_ALIAS_ATTEMPTS_INVALID"):
            iam.configure_initial_admin_alias(
                self.make_cli([], seen), "synthetic-admin", "secret-only-test",
                sleep=lambda _: None, attempts=0)
        self.assertEqual(seen, [])


if __name__ == "__main__":
    unittest.main()
