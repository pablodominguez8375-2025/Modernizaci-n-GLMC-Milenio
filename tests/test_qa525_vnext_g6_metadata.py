"""QA525 G6 metadata binder -- hermetic tests, no host, no secrets."""
import ast
import datetime as dt
import hashlib
import importlib.util
import json
from pathlib import Path
import unittest

SOURCE = Path(__file__).resolve().parents[1] / "scripts/qa/qa525-vnext-g6-metadata.py"
spec = importlib.util.spec_from_file_location("qa525_vnext_g6_metadata_test", SOURCE)
gate = importlib.util.module_from_spec(spec)
spec.loader.exec_module(gate)
UTC = dt.timezone.utc
NOW = dt.datetime(2026, 11, 6, 12, 30, tzinfo=UTC)
CUT = NOW - dt.timedelta(hours=1)


def encode(record):
    return (json.dumps(record, sort_keys=True, separators=(",", ":")) + "\n").encode()


def digest(raw):
    return "sha256:" + hashlib.sha256(raw).hexdigest()


class G6MetadataTests(unittest.TestCase):
    def setUp(self):
        stamp = CUT.strftime("%Y%m%dT%H%M%SZ")
        name = "centenario-qa-g6-CIFRADO-" + stamp + ".tar.gz"
        sha = "a" * 64
        self.review = {
            "g6_archive_name": name, "g6_archive_sha256": sha,
            "g6_offhost_sha256_verified": True,
            "g6_restore_report_name": "isolated-restore-" + stamp + ".report.json",
        }
        self.receipt = {
            "status": "ENCRYPTED_LOCAL_ARCHIVE_READY_OFFHOST_PENDING",
            "archive_name": name, "sha256": sha,
        }
        self.restore = {
            "result": "G6_SAME_HOST_ISOLATED_RESTORE_PASS_NOT_DISASTER_RECOVERY",
            "archive_filename": name, "archive_sha256": sha,
            "postgres_tables_by_database": {"pmgm": 12, "pmgm_keycloak": 8},
            "silo_objects_verified": 0, "production_touched": False,
        }

    def check(self, review=None, receipt=None, restore=None, *, raw_review=None,
              raw_receipt=None, raw_restore=None, clock=NOW, observed=None, pins=None):
        a = raw_review if raw_review is not None else encode(
            review if review is not None else self.review)
        b = raw_receipt if raw_receipt is not None else encode(
            receipt if receipt is not None else self.receipt)
        c = raw_restore if raw_restore is not None else encode(
            restore if restore is not None else self.restore)
        pinned = list(pins) if pins is not None else [digest(a), digest(b), digest(c)]
        return gate.inspect(
            a, b, c, pinned_review_sha256=pinned[0],
            pinned_receipt_sha256=pinned[1], pinned_restore_sha256=pinned[2],
            observed_archive_sha256=observed or ("sha256:" + "a" * 64),
            now=clock)

    def test_valid_metadata_does_not_prove_physical_go(self):
        result = self.check()
        self.assertEqual(result["status"], gate.STATUS)
        self.assertEqual(result["checkpoint_age_seconds"], 3600)
        self.assertEqual(result["checkpoint_expires_at"],
                         (CUT + dt.timedelta(hours=48)).strftime("%Y-%m-%dT%H:%M:%SZ"))
        self.assertEqual(result["archive_sha256"], "sha256:" + "a" * 64)
        for key in ("may_apply", "may_install", "signature_verified_here",
                    "physical_archive_verified_here",
                    "offhost_custody_authenticated_here",
                    "independent_restore_verified_here",
                    "sponsor_approved_here"):
            self.assertIs(result[key], False)

    def test_freshness_rejects_future_and_exact_48_hour_cutoff(self):
        for clock in (CUT - dt.timedelta(seconds=1),
                      CUT + dt.timedelta(hours=48),
                      CUT + dt.timedelta(hours=100)):
            with self.subTest(clock=clock):
                with self.assertRaisesRegex(
                        gate.Stop, "^G6_CHECKPOINT_EXPIRED_OR_FUTURE$"):
                    self.check(clock=clock)
        self.assertEqual(
            self.check(clock=CUT + dt.timedelta(hours=48, seconds=-1))[
                "checkpoint_age_seconds"], 172799)

    def test_missing_or_false_offhost_claim_is_not_accepted(self):
        for value in (False, 1, None, "true"):
            record = dict(self.review, g6_offhost_sha256_verified=value)
            with self.subTest(value=value):
                with self.assertRaisesRegex(gate.Stop, "^G6_OFFHOST_CLAIM_MISSING$"):
                    self.check(review=record)

    def test_actual_archive_digest_must_match_review(self):
        with self.assertRaisesRegex(gate.Stop, "^G6_ARCHIVE_DIGEST_MISMATCH$"):
            self.check(observed="sha256:" + "b" * 64)
        with self.assertRaisesRegex(gate.Stop, "^G6_OBSERVED_ARCHIVE_HASH_INVALID$"):
            self.check(observed="untrusted")

    def test_review_digest_must_be_pinned_before_parsing(self):
        old = encode(self.review)
        modified = dict(self.review, extra="after-pin")
        with self.assertRaisesRegex(gate.Stop, "^G6_REVIEW_DIGEST_MISMATCH$"):
            self.check(review=modified, pins=[digest(old), digest(encode(self.receipt)),
                                               digest(encode(self.restore))])

    def test_duplicate_keys_are_rejected_even_with_matching_digest(self):
        bad = b'{"g6_archive_name":"a","g6_archive_name":"b"}\n'
        with self.assertRaisesRegex(gate.Stop, "^G6_DUPLICATE_JSON_KEY$"):
            self.check(raw_review=bad)

    def test_non_finite_and_malformed_json_fail_closed(self):
        for raw in (b'{"value": NaN}', b"{", b"[]", b"\xff"):
            with self.subTest(raw=raw):
                with self.assertRaises(gate.Stop):
                    self.check(raw_review=raw)

    def test_local_receipt_status_name_and_sha_all_must_match(self):
        for key, value in (("status", "RESTORED"), ("archive_name", "other.tar.gz"),
                           ("sha256", "f" * 64)):
            with self.subTest(key=key):
                with self.assertRaisesRegex(gate.Stop, "^G6_LOCAL_RECEIPT_MISMATCH$"):
                    self.check(receipt=dict(self.receipt, **{key: value}))

    def test_restoration_must_cover_both_databases_and_not_touch_production(self):
        changes = (
            {"postgres_tables_by_database": {"pmgm": 12}},
            {"postgres_tables_by_database": {"pmgm": True, "pmgm_keycloak": 1}},
            {"postgres_tables_by_database": {"pmgm": 12, "pmgm_keycloak": 0}},
            {"production_touched": True},
            {"silo_objects_verified": False},
            {"archive_sha256": "f" * 64},
            {"result": "PASS"},
        )
        for change in changes:
            with self.subTest(change=change):
                with self.assertRaisesRegex(gate.Stop, "^G6_RESTORE_METADATA_MISMATCH$"):
                    self.check(restore=dict(self.restore, **change))

    def test_archive_and_restore_names_are_strict(self):
        for name in ("../archive.tar.gz", "centenario-qa-g6-CIFRADO-99999999T999999Z.tar.gz"):
            with self.subTest(name=name):
                with self.assertRaisesRegex(gate.Stop, "^G6_ARCHIVE_NAME_INVALID$"):
                    self.check(review=dict(self.review, g6_archive_name=name))
        with self.assertRaisesRegex(gate.Stop, "^G6_RESTORE_NAME_INVALID$"):
            self.check(review=dict(self.review, g6_restore_report_name="../evil.json"))

    def test_timezone_and_size_type_and_pin_are_rejected(self):
        with self.assertRaisesRegex(gate.Stop, "^G6_CLOCK_INVALID$"):
            self.check(clock=dt.datetime(2026, 11, 6, 12, 30))
        with self.assertRaisesRegex(gate.Stop, "^G6_REVIEW_BYTES_INVALID$"):
            self.check(raw_review=b"x" * (gate.MAX_BYTES + 1))
        with self.assertRaisesRegex(gate.Stop, "^G6_REVIEW_PIN_INVALID$"):
            self.check(pins=["not-a-digest", digest(encode(self.receipt)),
                             digest(encode(self.restore))])

    def test_module_never_imports_host_or_network_effects(self):
        tree = ast.parse(SOURCE.read_text())
        imports = set()
        for item in ast.walk(tree):
            if isinstance(item, ast.Import):
                imports.update(alias.name.split(".")[0] for alias in item.names)
            if isinstance(item, ast.ImportFrom):
                imports.add((item.module or "").split(".")[0])
        self.assertEqual(imports, {"datetime", "hashlib", "hmac", "json", "re", "__future__"})
        with self.assertRaises(SystemExit):
            exec(compile(SOURCE.read_text(), str(SOURCE), "exec"),
                 {"__name__": "__main__", "__file__": str(SOURCE)})


if __name__ == "__main__":
    unittest.main()
