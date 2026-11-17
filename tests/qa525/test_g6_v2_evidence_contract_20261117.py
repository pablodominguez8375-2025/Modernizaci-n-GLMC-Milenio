#!/usr/bin/env python3
"""Offline G6 v2 evidence regression tests. No host, Docker, GO or network access."""
import datetime as dt
import hashlib
import importlib.util
import json
from pathlib import Path
import unittest

MODULE = Path(__file__).resolve().parents[2] / "scripts" / "qa" / "qa525-vnext-g6-v2-evidence.py"
spec = importlib.util.spec_from_file_location("qa525_g6_v2_evidence", MODULE)
evidence = importlib.util.module_from_spec(spec)
spec.loader.exec_module(evidence)

STAMP = "20261117T120000Z"
ARCHIVE = f"centenario-qa-g6-v2-CIFRADO-{STAMP}.tar.gz"
RESTORE = f"isolated-restore-v2-{STAMP}.report.json"
SHA = "a" * 64
REPO = "b" * 64
EPOCH = "c" * 32
NOW = dt.datetime(2026, 11, 17, 12, 15, tzinfo=dt.timezone.utc)
END = NOW + dt.timedelta(minutes=20)


def canonical(value):
    return (json.dumps(value, sort_keys=True, separators=(",", ":"),
                       ensure_ascii=True, allow_nan=False) + "\n").encode("ascii")


def fixture():
    common = {"restic_repo_id": REPO, "key_epoch": EPOCH}
    review = dict(common, schema="qa525-g6-v2-review",
                  g6_archive_name=ARCHIVE, g6_archive_sha256=SHA,
                  g6_restore_report_name=RESTORE, g6_offhost_sha256_verified=True,
                  g6_drive_file_id="A" * 24)
    receipt = dict(common, schema="qa525-g6-v2-receipt",
                   archive_name=ARCHIVE, sha256=SHA,
                   status="ENCRYPTED_LOCAL_ARCHIVE_READY_OFFHOST_PENDING")
    restore = dict(common, schema="qa525-g6-v2-restore",
                   result="G6_SAME_HOST_ISOLATED_RESTORE_PASS_NOT_DISASTER_RECOVERY",
                   archive_filename=ARCHIVE, archive_sha256=SHA,
                   postgres_tables_by_database={"pmgm": 12, "pmgm_keycloak": 10},
                   silo_objects_verified=34, production_touched=False)
    return review, receipt, restore


def invoke(records=None, **overrides):
    records = fixture() if records is None else records
    raw = [canonical(item) for item in records]
    args = dict(review_bytes=raw[0], receipt_bytes=raw[1], restore_bytes=raw[2],
                pins={name + "_sha256": "sha256:" + hashlib.sha256(data).hexdigest()
                      for name, data in zip(("review", "receipt", "restore"), raw)},
                observed_archive_sha256="sha256:" + SHA,
                downloaded_offhost_sha256="sha256:" + SHA,
                expected_restic_repo_id=REPO, expected_key_epoch=EPOCH,
                now=NOW, admission_expires_at=END)
    args.update(overrides)
    return evidence.inspect(**args)


class G6V2EvidenceContract(unittest.TestCase):
    def test_valid_coherence_never_grants_go(self):
        result = invoke()
        self.assertEqual(result["status"], evidence.STATUS)
        self.assertIs(result["may_apply"], False)
        self.assertIs(result["may_install"], False)
        for field in ("physical_archive_verified_here",
                      "offhost_custody_authenticated_here",
                      "restore_authenticated_here",
                      "repository_identity_authenticated_here",
                      "key_generation_authenticated_here",
                      "sponsor_authorized_here"):
            self.assertIs(result[field], False)

    def test_wrong_offhost_hash_rejected(self):
        with self.assertRaises(evidence.Stop):
            invoke(downloaded_offhost_sha256="sha256:" + "0" * 64)

    def test_false_restore_rejected(self):
        records = list(fixture())
        records[2]["production_touched"] = True
        with self.assertRaises(evidence.Stop):
            invoke(records)

    def test_zero_tables_rejected(self):
        records = list(fixture())
        records[2]["postgres_tables_by_database"]["pmgm"] = 0
        with self.assertRaises(evidence.Stop):
            invoke(records)

    def test_generation_mismatch_rejected(self):
        records = list(fixture())
        records[1]["key_epoch"] = "d" * 32
        with self.assertRaises(evidence.Stop):
            invoke(records)

    def test_expired_checkpoint_rejected(self):
        with self.assertRaises(evidence.Stop):
            invoke(now=NOW + dt.timedelta(days=3),
                   admission_expires_at=NOW + dt.timedelta(days=3, minutes=5))

    def test_tampered_json_pin_rejected(self):
        raw = [canonical(item) for item in fixture()]
        raw[0] += b" "
        with self.assertRaises(evidence.Stop):
            invoke(review_bytes=raw[0])

    def test_duplicate_json_key_rejected(self):
        raw = canonical(fixture()[0])
        tampered = raw[:-2] + b',"schema":"qa525-g6-v2-review"}\n'
        with self.assertRaises(evidence.Stop):
            invoke(review_bytes=tampered, pins={
                **invoke_pins(), "review_sha256": "sha256:" + hashlib.sha256(tampered).hexdigest()})

def invoke_pins():
    raw = [canonical(item) for item in fixture()]
    return {name + "_sha256": "sha256:" + hashlib.sha256(data).hexdigest()
            for name, data in zip(("review", "receipt", "restore"), raw)}


if __name__ == "__main__":
    unittest.main()
