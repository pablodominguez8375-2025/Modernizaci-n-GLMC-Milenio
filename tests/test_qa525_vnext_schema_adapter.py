"""Schema adapter phases 4-6: scripted fake runner/journal, entirely synthetic and rootless."""
import hashlib
import importlib.util
from pathlib import Path
import unittest

PATH = Path(__file__).resolve().parents[1] / "scripts/qa/qa525-vnext-schema-adapter.py"
SPEC = importlib.util.spec_from_file_location("qa525_schema_adapter", PATH)
ad = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(ad)

PREVIOUS = "20261030223000_SeedSupportWorkshop00"
IMAGE = "sha256:" + "b" * 64
GRANT = b"#!/usr/bin/env bash\necho grants\n"
GATE = b"BEGIN READ ONLY;\nSELECT 1;\nROLLBACK;\n"


def digest(data):
    return "sha256:" + hashlib.sha256(data).hexdigest()


def params(**over):
    base = sorted([f"20260101{i:06d}_Synthetic{i}" for i in range(99)] + [PREVIOUS])
    p = {
        "network": "centenario-qa-lab2_default", "pg_container": "centenario-qa-lab2-postgres-1",
        "migrator_env_file": "/root/qa525/migrator.env", "grants_env_file": "/root/qa525/grants.env",
        "migrator_image_id": IMAGE, "baseline_history_ids": base,
        "grant_script": GRANT, "gate_sql": GATE,
        "grant_script_sha256": digest(GRANT), "postgate_sha256": digest(GATE),
    }
    p.update(over)
    return p


class Fake:
    """Scripted runner; answers by command name and records every call."""

    def __init__(self, p, **over):
        self.cmds = ad.plan_commands(p)
        self.by_argv = {v: k for k, v in self.cmds.items() if k not in ("gate", "acl", "history")}
        self.expected = ad.expected_history_after(p["baseline_history_ids"])
        self.answers = {
            "image_id": (0, (IMAGE + "\n").encode()),
            "migrate": (0, ad.MIGRATED),
            "history": (0, ("\n".join(self.expected) + "\n").encode()),
            "grants": (0, (ad.GRANTS_MARKER + "\n").encode()),
            "acl": (0, ad.ACL_OK),
            "gate": (0, (ad.GATE_PASS + "\n").encode()),
        }
        self.answers.update(over)
        self.calls = []
        self.stdins = []

    def name(self, argv):
        for k, v in self.cmds.items():
            if argv == v:
                return k
        raise AssertionError("unexpected argv")

    def __call__(self, argv, timeout, stdin):
        k = self.name(argv)
        self.calls.append(k)
        self.stdins.append(stdin)
        a = self.answers[k]
        if isinstance(a, Exception):
            raise a
        return a


class Journal:
    def __init__(self, fail_on=None, events=None):
        self.phases = []
        self.fail_on = fail_on
        self.events = events

    def __call__(self, phase):
        if self.events is not None:
            self.events.append(("journal", phase))
        if phase == self.fail_on:
            raise OSError("disk")
        self.phases.append(phase)


class SchemaCutAdapterTests(unittest.TestCase):
    def run_case(self, p=None, **over):
        p = p or params()
        runner = Fake(p, **over)
        journal = Journal()
        try:
            res = ad.run_phases(journal, runner, p)
            return res, None, runner, journal
        except ad.Stop as exc:
            return None, str(exc), runner, journal

    def test_positive_sequence_and_journal_order(self):
        events = []
        p = params()
        runner = Fake(p)
        orig = runner.__call__

        def logged(argv, timeout, stdin):
            events.append(("effect", runner.name(argv)))
            return orig(argv, timeout, stdin)

        res = ad.run_phases(Journal(events=events), logged, p)
        self.assertEqual(res["status"], "QA525_SCHEMA_PHASES_4_6_COMPLETE_NO_AUTHORITY")
        self.assertIs(res["may_apply"], False)
        self.assertEqual(res["migration_count_after"], 104)
        names = [e[1] for e in events]
        self.assertEqual(names[:3], ["image_id", "MIGRATION_STARTED", "migrate"])  # read-only precheck, then durable start
        self.assertEqual(names, ["image_id", "MIGRATION_STARTED", "migrate", "history", "SCHEMA_104",
                                 "grants", "acl", "GRANTS_VERIFIED", "gate", "POSTGATE_PASS"])

    def test_scripts_are_piped_as_pinned_bytes_and_no_secret_in_argv(self):
        p = params()
        res, err, runner, _ = self.run_case(p)
        self.assertIsNone(err)
        self.assertEqual(runner.stdins[runner.calls.index("grants")], GRANT)
        self.assertEqual(runner.stdins[runner.calls.index("gate")], GATE)
        for argv in ad.plan_commands(p).values():
            self.assertTrue(all(type(a) is str for a in argv))
            self.assertNotIn("PASSWORD", " ".join(argv).upper())
            self.assertNotIn("Password", " ".join(argv))
        self.assertIn("--env-file", ad.plan_commands(p)["migrate"])
        self.assertIn("--read-only", ad.plan_commands(p)["migrate"])

    def test_every_failure_stops_closed_after_journalling_partial_and_never_continues(self):
        cases = {

            "migrate_rc": ((2, ad.MIGRATED), "SCHEMA_ADAPTER_MIGRATION_FAILED", ["image_id", "migrate"]),
            "migrate_out": ((0, b'{"status":"WORKSHOPS104_MIGRATION_BLOCKED_RECONCILE_REQUIRED"}\n'),
                            "SCHEMA_ADAPTER_MIGRATION_FAILED", ["image_id", "migrate"]),
        }
        for label, (answer, code, calls) in cases.items():
            key = "migrate"
            with self.subTest(label):
                res, err, runner, journal = self.run_case(**{key: answer})
                self.assertEqual(err, code)
                self.assertEqual(runner.calls, calls)
                self.assertEqual(journal.phases, ["MIGRATION_STARTED", "PARTIAL_SCHEMA_STOP"])

    def test_wrong_image_id_is_pre_ddl_stop_with_no_journal_and_no_migration(self):
        for answer in ((0, ("sha256:" + "c" * 64 + "\n").encode()), (1, b"")):
            with self.subTest(answer):
                res, err, runner, journal = self.run_case(image_id=answer)
                self.assertEqual(err, "SCHEMA_ADAPTER_IMAGE_ID_MISMATCH")
                self.assertEqual(runner.calls, ["image_id"])
                self.assertEqual(journal.phases, [])

    def test_history_mismatch_is_partial_and_does_not_reach_grants(self):
        p = params()
        wrong = ad.expected_history_after(p["baseline_history_ids"])[:-1]
        res, err, runner, journal = self.run_case(p, history=(0, ("\n".join(wrong) + "\n").encode()))
        self.assertEqual(err, "SCHEMA_ADAPTER_HISTORY_MISMATCH")
        self.assertNotIn("grants", runner.calls)
        self.assertNotIn("SCHEMA_104", journal.phases)
        self.assertEqual(journal.phases[-1], "PARTIAL_SCHEMA_STOP")

    def test_grants_acl_and_gate_failures(self):
        for key, answer, code in (
            ("grants", (1, b""), "SCHEMA_ADAPTER_GRANTS_FAILED"),
            ("grants", (0, b"otra cosa\n"), "SCHEMA_ADAPTER_GRANTS_FAILED"),
            ("acl", (0, b"true|true|true|false|false\n"), "SCHEMA_ADAPTER_ACL_INVALID"),
            ("acl", (0, b"true|true|false|false|true\n"), "SCHEMA_ADAPTER_ACL_INVALID"),
            ("acl", (1, ad.ACL_OK), "SCHEMA_ADAPTER_ACL_INVALID"),
            ("gate", (0, b"QA525_WORKSHOP_POSTMIGRATION_READ_ONLY_FAIL\n"), "SCHEMA_ADAPTER_GATE_FAILED"),
            ("gate", (3, (ad.GATE_PASS + "\n").encode()), "SCHEMA_ADAPTER_GATE_FAILED"),
        ):
            with self.subTest((key, answer)):
                res, err, runner, journal = self.run_case(**{key: answer})
                self.assertEqual(err, code)
                self.assertEqual(journal.phases[-1], "PARTIAL_SCHEMA_STOP")
                self.assertNotIn("POSTGATE_PASS", journal.phases)
                self.assertEqual(journal.phases.count("PARTIAL_SCHEMA_STOP"), 1)

    def test_timeout_and_runner_errors_are_opaque(self):
        for exc in (TimeoutError("secret-detail"), OSError("/root/qa525/migrator.env"), RuntimeError("x")):
            with self.subTest(type(exc).__name__):
                res, err, runner, journal = self.run_case(migrate=exc)
                self.assertEqual(err, "SCHEMA_ADAPTER_EFFECT_FAILED")
                self.assertNotIn("secret", err)
                self.assertEqual(journal.phases, ["MIGRATION_STARTED", "PARTIAL_SCHEMA_STOP"])

    def test_oversized_or_malformed_runner_results_are_rejected(self):
        for bad in ((0, b"x" * (ad.MAX_OUT + 1)), ("0", b""), (True, b""), (0, "texto"), None):
            with self.subTest(repr(bad)[:30]):
                res, err, runner, journal = self.run_case(migrate=bad)
                self.assertIn(err, ("SCHEMA_ADAPTER_EFFECT_FAILED", "SCHEMA_ADAPTER_UNEXPECTED"))
                self.assertEqual(journal.phases[-1], "PARTIAL_SCHEMA_STOP")

    def test_journal_failure_before_the_ddl_boundary_runs_no_ddl_effect(self):
        p = params()
        runner = Fake(p)
        with self.assertRaises(ad.Stop) as ctx:
            ad.run_phases(Journal(fail_on="MIGRATION_STARTED"), runner, p)
        self.assertEqual(str(ctx.exception), "SCHEMA_ADAPTER_UNEXPECTED")
        self.assertEqual(runner.calls, ["image_id"])  # only the read-only precheck ran; no DDL effect

    def test_journal_failure_after_ddl_is_reported_without_retry(self):
        p = params()
        runner = Fake(p)
        with self.assertRaises(ad.Stop):
            ad.run_phases(Journal(fail_on="SCHEMA_104"), runner, p)
        self.assertNotIn("grants", runner.calls)

    def test_pinned_bytes_must_match(self):
        for over in ({"grant_script": GRANT + b"#"}, {"gate_sql": GATE + b"--"}):
            with self.subTest(list(over)[0]):
                with self.assertRaises(ad.Stop) as ctx:
                    ad.plan_commands(params(**over))
                self.assertEqual(str(ctx.exception), "SCHEMA_ADAPTER_SQL_PIN_MISMATCH")

    def test_invalid_parameters_are_refused_before_any_effect(self):
        base = params()["baseline_history_ids"]
        bad = (
            {"network": "net; rm -rf /"}, {"network": "-x"}, {"pg_container": "pg container"},
            {"migrator_env_file": "relative.env"}, {"migrator_env_file": "/root/../etc/shadow"},
            {"grants_env_file": "/root/a b"}, {"migrator_image_id": "latest"},
            {"migrator_image_id": "sha256:" + "B" * 64},
            {"baseline_history_ids": base[:-1]}, {"baseline_history_ids": base[:-1] + [base[0]]},
            {"baseline_history_ids": list(reversed(base))},
            {"baseline_history_ids": base[:-1] + [ad.REQUIRED[0]]},
            {"baseline_history_ids": [b if i else "no-valida" for i, b in enumerate(base)]},
            {"grant_script": ""}, {"gate_sql": b""},
        )
        for over in bad:
            with self.subTest(str(over)[:40]):
                runner = Fake(params())
                with self.assertRaises(ad.Stop):
                    ad.run_phases(Journal(), runner, params(**over))
                self.assertEqual(runner.calls, [])
        with self.assertRaises(ad.Stop):
            ad.plan_commands({**params(), "extra": 1})
        with self.assertRaises(ad.Stop):
            ad.run_phases(None, Fake(params()), params())

    def test_expected_history_after_is_sorted_104(self):
        p = params()
        after = ad.expected_history_after(p["baseline_history_ids"])
        self.assertEqual(len(after), 104)
        self.assertEqual(after, sorted(after))
        self.assertTrue(set(ad.REQUIRED) <= set(after))

    def test_stop_messages_never_echo_inputs(self):
        res, err, runner, journal = self.run_case(migrate=(0, b"password=hunter2\n"))
        self.assertEqual(err, "SCHEMA_ADAPTER_MIGRATION_FAILED")
        self.assertNotIn("hunter2", err)


if __name__ == "__main__":
    unittest.main()
