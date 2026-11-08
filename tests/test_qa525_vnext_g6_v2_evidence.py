"""QA525 G6 v2: pure metadata and adversarial fixtures, NO host/GO."""
import ast
import datetime as dt
import hashlib
import importlib.util
import json
from pathlib import Path
import unittest

SRC = Path(__file__).resolve().parents[1] / "scripts" / "qa" / "qa525-vnext-g6-v2-evidence.py"
spec = importlib.util.spec_from_file_location("qa525_g6_v2_evidence_test", SRC)
unit = importlib.util.module_from_spec(spec)
spec.loader.exec_module(unit)

NOW = dt.datetime(2026, 11, 7, 22, 30, tzinfo=dt.timezone.utc)
END = dt.datetime(2026, 11, 7, 23, 0, tzinfo=dt.timezone.utc)
NAME = "centenario-qa-g6-v2-CIFRADO-20261107T220000Z.tar.gz"
REPORT = "isolated-restore-v2-20261107T220000Z.report.json"
REPO = "a" * 64
EPOCH = "b" * 32
ARCHIVE_SHA = "c" * 64


def raw(record):
    return (json.dumps(record, sort_keys=True, separators=(",", ":"),
                       ensure_ascii=True) + "\n").encode("ascii")


def pin(contents):
    return "sha256:" + hashlib.sha256(contents).hexdigest()


class PureEvidence(unittest.TestCase):
    def setUp(self):
        self.review = {
            "schema": "qa525-g6-v2-review",
            "g6_archive_name": NAME, "g6_archive_sha256": ARCHIVE_SHA,
            "g6_restore_report_name": REPORT,
            "g6_offhost_sha256_verified": True,
            "g6_drive_file_id": "syntheticFakeDriveId01234",
            "restic_repo_id": REPO, "key_epoch": EPOCH,
        }
        self.receipt = {
            "schema": "qa525-g6-v2-receipt",
            "archive_name": NAME, "sha256": ARCHIVE_SHA,
            "status": "ENCRYPTED_LOCAL_ARCHIVE_READY_OFFHOST_PENDING",
            "restic_repo_id": REPO, "key_epoch": EPOCH,
        }
        self.restore = {
            "schema": "qa525-g6-v2-restore",
            "result": "G6_SAME_HOST_ISOLATED_RESTORE_PASS_NOT_DISASTER_RECOVERY",
            "archive_filename": NAME, "archive_sha256": ARCHIVE_SHA,
            "restic_repo_id": REPO, "key_epoch": EPOCH,
            "postgres_tables_by_database": {"pmgm": 7, "pmgm_keycloak": 4},
            "silo_objects_verified": 3, "production_touched": False,
        }

    def call(self, review=None, receipt=None, restore=None, **kwargs):
        rb = raw(self.review if review is None else review)
        cb = raw(self.receipt if receipt is None else receipt)
        sb = raw(self.restore if restore is None else restore)
        args = dict(review_bytes=rb, receipt_bytes=cb, restore_bytes=sb,
                    pins={"review_sha256": pin(rb), "receipt_sha256": pin(cb),
                          "restore_sha256": pin(sb)},
                    observed_archive_sha256="sha256:" + ARCHIVE_SHA,
                    downloaded_offhost_sha256="sha256:" + ARCHIVE_SHA,
                    expected_restic_repo_id=REPO, expected_key_epoch=EPOCH,
                    now=NOW, admission_expires_at=END)
        args.update(kwargs)
        return unit.inspect(**args)

    def denied(self, code, **kwargs):
        with self.assertRaises(unit.Stop) as err:
            self.call(**kwargs)
        self.assertEqual(str(err.exception), code)
        self.assertIsNone(err.exception.__cause__)

    def test_matching_evidence_is_never_authority(self):
        result = self.call()
        self.assertEqual(result["status"], unit.STATUS)
        self.assertEqual(result["archive_sha256"], "sha256:" + ARCHIVE_SHA)
        self.assertEqual(result["restic_repo_id"], REPO)
        for flag in ("may_apply", "may_install", "physical_archive_verified_here",
                     "offhost_custody_authenticated_here", "restore_authenticated_here",
                     "repository_identity_authenticated_here",
                     "key_generation_authenticated_here", "sponsor_authorized_here"):
            self.assertIs(result[flag], False, flag)

    def test_schema_exact_v1_and_extra_fields_rejected(self):
        for item in ("review", "receipt", "restore"):
            with self.subTest(item=item):
                original = getattr(self, item)
                self.denied("G6_V2_SCHEMA_INVALID",
                            **{item: dict(original, schema="qa525-g6-v1")})
                self.denied("G6_V2_SCHEMA_INVALID",
                            **{item: dict(original, unexpected=True)})
                old = dict(original)
                del old["schema"]
                self.denied("G6_V2_SCHEMA_INVALID", **{item: old})

    def test_repo_and_key_epoch_cannot_mix_between_cuts(self):
        for item in ("review", "receipt", "restore"):
            with self.subTest(item=item):
                other = dict(getattr(self, item), restic_repo_id="d" * 64)
                self.denied("G6_V2_GENERATION_MISMATCH", **{item: other})
                wrong_epoch = dict(getattr(self, item), key_epoch="e" * 32)
                self.denied("G6_V2_GENERATION_MISMATCH", **{item: wrong_epoch})
        self.denied("G6_V2_GENERATION_PIN_INVALID", expected_key_epoch="bad")
        self.denied("G6_V2_GENERATION_PIN_INVALID", expected_restic_repo_id=42)

    def test_local_or_downloaded_copy_digest_mismatch_denied(self):
        for field in ("observed_archive_sha256", "downloaded_offhost_sha256"):
            self.denied("G6_V2_ARCHIVE_OR_OFFHOST_HASH_MISMATCH",
                        **{field: "sha256:" + "d" * 64})
            self.denied("G6_V2_ARCHIVE_OR_OFFHOST_HASH_MISMATCH",
                        **{field: True})
        self.denied("G6_V2_ARCHIVE_HASH_INVALID",
                    review=dict(self.review, g6_archive_sha256="bad"))
        self.denied("G6_V2_RECEIPT_OR_DRIVE_MISMATCH",
                    receipt=dict(self.receipt, sha256="d" * 64))

    def test_offhost_claim_is_not_truthy_boolean(self):
        for value in (False, 1, "true", {}, None):
            with self.subTest(value=value):
                self.denied("G6_V2_RECEIPT_OR_DRIVE_MISMATCH",
                            review=dict(self.review, g6_offhost_sha256_verified=value))
        self.denied("G6_V2_RECEIPT_OR_DRIVE_MISMATCH",
                    review=dict(self.review, g6_drive_file_id="bad"))
        self.denied("G6_V2_RECEIPT_OR_DRIVE_MISMATCH",
                    receipt=dict(self.receipt, status="PUBLISHED_NO_RESTORE"))

    def test_restore_must_match_same_archive_and_metadata(self):
        examples = [
            dict(self.restore, archive_sha256="d" * 64),
            dict(self.restore, archive_filename="other.tar.gz"),
            dict(self.restore, result="MIGRATION_SUCCESS"),
            dict(self.restore, production_touched=True),
            dict(self.restore, production_touched=0),
            dict(self.restore, silo_objects_verified=False),
            dict(self.restore, silo_objects_verified=-1),
            dict(self.restore, postgres_tables_by_database={"pmgm": 2}),
            dict(self.restore, postgres_tables_by_database={"pmgm": True, "pmgm_keycloak": 4}),
        ]
        for case in examples:
            with self.subTest(case=case):
                self.denied("G6_V2_RESTORE_MISMATCH", restore=case)

    def test_restore_filename_must_equal_archive_capture(self):
        self.denied("G6_V2_CUT_MISMATCH",
                    review=dict(self.review, g6_restore_report_name=
                                "isolated-restore-v2-20261106T220000Z.report.json"))
        self.denied("G6_V2_RESTORE_NAME_INVALID",
                    review=dict(self.review, g6_restore_report_name="unknown"))
        self.denied("G6_V2_ARCHIVE_NAME_INVALID",
                    review=dict(self.review, g6_archive_name="unknown"))

    def test_legacy_v1_filenames_are_rejected_even_with_v2_metadata(self):
        self.denied("G6_V2_ARCHIVE_NAME_INVALID",
                    review=dict(self.review, g6_archive_name=
                                "centenario-qa-g6-CIFRADO-20261107T220000Z.tar.gz"))
        self.denied("G6_V2_RESTORE_NAME_INVALID",
                    review=dict(self.review, g6_restore_report_name=
                                "isolated-restore-20261107T220000Z.report.json"))

    def test_stale_future_and_window_fail_closed(self):
        self.denied("G6_V2_WINDOW_EXPIRED_OR_UNCOVERED",
                    now=NOW + dt.timedelta(days=3))
        checkpoint_end = dt.datetime(2026, 11, 9, 22, 0, tzinfo=dt.timezone.utc)
        self.denied("G6_V2_WINDOW_EXPIRED_OR_UNCOVERED",
                    now=checkpoint_end,
                    admission_expires_at=checkpoint_end + dt.timedelta(seconds=1))
        self.denied("G6_V2_WINDOW_EXPIRED_OR_UNCOVERED",
                    now=NOW - dt.timedelta(days=1))
        self.denied("G6_V2_WINDOW_EXPIRED_OR_UNCOVERED",
                    admission_expires_at=NOW)
        self.denied("G6_V2_WINDOW_EXPIRED_OR_UNCOVERED",
                    admission_expires_at=NOW + dt.timedelta(days=3))
        close = dt.datetime(2026, 11, 9, 21, 59, tzinfo=dt.timezone.utc)
        self.assertIs(self.call(now=close, admission_expires_at=
                                close + dt.timedelta(seconds=30))["may_apply"], False)
        self.denied("G6_V2_WINDOW_EXPIRED_OR_UNCOVERED", now=close,
                    admission_expires_at=close + dt.timedelta(minutes=2))
        self.denied("G6_V2_CLOCK_INVALID", now=NOW.replace(tzinfo=None))

    def test_duplicate_noncanonical_and_nan_json_rejected(self):
        data = raw(self.receipt)
        duplicate = data.replace(b'"schema":"qa525-g6-v2-receipt"',
                                 b'"schema":"qa525-g6-v2-receipt","schema":"qa525-g6-v2-receipt"')
        self.assertNotEqual(duplicate, data)
        self.denied("G6_V2_DUPLICATE_JSON_KEY",
                    receipt_bytes=duplicate, pins={
                        "review_sha256": pin(raw(self.review)),
                        "receipt_sha256": pin(duplicate),
                        "restore_sha256": pin(raw(self.restore))})
        formatted = json.dumps(self.receipt, sort_keys=True, indent=2).encode() + b"\n"
        self.denied("G6_V2_RECEIPT_NOT_CANONICAL",
                    receipt_bytes=formatted, pins={
                        "review_sha256": pin(raw(self.review)),
                        "receipt_sha256": pin(formatted),
                        "restore_sha256": pin(raw(self.restore))})
        nan_data = data.replace(b'"sha256":"' + ARCHIVE_SHA.encode() + b'"',
                                b'"sha256":NaN')
        self.denied("G6_V2_NONFINITE_JSON",
                    receipt_bytes=nan_data, pins={
                        "review_sha256": pin(raw(self.review)),
                        "receipt_sha256": pin(nan_data),
                        "restore_sha256": pin(raw(self.restore))})

    def test_pin_must_bind_exact_bytes_not_just_record(self):
        self.denied("G6_V2_PINS_INVALID", pins={"review_sha256": "sha256:" + "0" * 64})
        self.denied("G6_V2_REVIEW_PIN_MISMATCH",
                    pins={"review_sha256": "sha256:" + "0" * 64,
                          "receipt_sha256": pin(raw(self.receipt)),
                          "restore_sha256": pin(raw(self.restore))})
        self.denied("G6_V2_RECEIPT_BYTES_INVALID", receipt_bytes=b"")

    def test_no_host_import_or_executable_entrypoint(self):
        data = SRC.read_text()
        imports = {alias.name.split(".")[0] for node in ast.walk(ast.parse(data))
                   if isinstance(node, ast.Import) for alias in node.names}
        self.assertEqual(imports, {"datetime", "hashlib", "hmac", "json", "re"})
        for item in ("subprocess", "socket", "os.system", "os.open", "sudo ",
                     "docker compose", "urllib", "requests"):
            self.assertNotIn(item, data)
        with self.assertRaises(SystemExit):
            exec(compile(data, str(SRC), "exec"), {"__name__": "__main__"})

    def test_attacker_supplied_identical_fake_evidence_never_means_go(self):
        # All metadata and supplied pins might be fabricated by an attacker.
        # This module still must not set any privilege or physical provenance.
        fake = self.call()
        self.assertIs(fake["may_apply"], False)
        self.assertIs(fake["offhost_custody_authenticated_here"], False)
        self.assertIs(fake["restore_authenticated_here"], False)


if __name__ == "__main__":
    unittest.main()
