"""QA525 G6 v2 producer→evidence integration; purely synthetic, never GO.

This invokes an already-tested FakeRestic in a temporary directory to create
real G6 v2 JSON receipt/restore/review bytes and feeds those same bytes to the
separately merged evidence validator. The fake download is NOT a Drive audit,
the verifier is not PostgreSQL, and no signature, root or host is checked.
"""
import datetime as dt
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import shutil
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[1]


def load(name, filename):
    spec = importlib.util.spec_from_file_location(name, ROOT / filename)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


prod = load("qa525_g6_producer_integration", "scripts/qa/qa525-vnext-g6-v2-producer.py")
evidence = load("qa525_g6_evidence_integration", "scripts/qa/qa525-vnext-g6-v2-evidence.py")
fixtures = load("qa525_g6_producer_fixtures", "tests/test_qa525_vnext_g6_v2_producer.py")


def digest(raw):
    return "sha256:" + hashlib.sha256(raw).hexdigest()


def canonical(record):
    return (json.dumps(record, sort_keys=True, separators=(",", ":"),
                       ensure_ascii=True) + "\n").encode("ascii")


class TestProducerEvidenceBoundary(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.base = os.path.realpath(self.tmp.name)
        self.root = os.path.join(self.base, "g6")
        os.mkdir(self.root, 0o700)
        self.uid = os.getuid()
        self.runner = fixtures.G6V2ProducerFakeRestic()
        self.key_epoch = prod.key_epoch(fixtures.KEY)
        self.repo_id = fixtures.REPO_ID
        self.current = fixtures.NOW
        self.ident = prod.init_repo(
            self.root, runner=self.runner, key_bytes=fixtures.KEY,
            uid=self.uid, now=self.current,
        )
        self.out = prod.backup(
            self.root, runner=self.runner, captor=fixtures.good_captor,
            uid=self.uid, now=self.current, expected_repo_id=self.repo_id,
            expected_key_epoch=self.key_epoch,
        )
        self.exports = Path(self.root, "exports-v2", self.out["archive_name"])
        downloads = Path(self.root, "downloads-v2")
        downloads.mkdir(mode=0o700)
        self.downloaded = downloads / self.out["archive_name"]
        shutil.copyfile(self.exports, self.downloaded)
        self.downloaded.chmod(0o600)

        # Exact same archive is recovered after the live repository is deleted.
        shutil.rmtree(Path(self.root, "repo-v2"))

        def verifier(data_dir):
            self.assertEqual(
                set(os.listdir(data_dir)),
                {"pmgm.dump", "pmgm_keycloak.dump", "globals.sql",
                 "silo", "manifest.json"},
            )
            self.assertEqual(Path(data_dir, "pmgm.dump").read_bytes(), b"pg1")
            return {"pmgm": 12, "pmgm_keycloak": 7}, len(os.listdir(Path(data_dir, "silo")))

        self.restored = prod.restore(
            self.root, runner=self.runner, verifier=verifier,
            archive_name=self.out["archive_name"],
            archive_sha256=self.out["archive_sha256"],
            snapshot_id=self.out["snapshot_id"],
            expected_manifest_sha256=self.out["manifest_sha256"],
            expected_repo_id=self.repo_id, expected_key_epoch=self.key_epoch,
            forbidden_prefixes=[os.path.join(self.base, "production")],
            uid=self.uid, now=self.current + dt.timedelta(hours=1),
        )
        self.review = prod.finalize_review(
            self.root, archive_name=self.out["archive_name"],
            drive_file_id="SyntheticDriveFileId001",
            expected_repo_id=self.repo_id, expected_key_epoch=self.key_epoch,
            uid=self.uid,
        )
        state = Path(self.root, "state-v2")
        stamp = self.out["stamp"]
        self.review_bytes = (state / ("review-" + stamp + ".json")).read_bytes()
        self.receipt_bytes = (state / ("receipt-" + stamp + ".json")).read_bytes()
        self.restore_bytes = (state / ("isolated-restore-v2-" + stamp + ".report.json")).read_bytes()
        self.args = dict(
            review_bytes=self.review_bytes,
            receipt_bytes=self.receipt_bytes,
            restore_bytes=self.restore_bytes,
            pins={
                "review_sha256": digest(self.review_bytes),
                "receipt_sha256": digest(self.receipt_bytes),
                "restore_sha256": digest(self.restore_bytes),
            },
            observed_archive_sha256="sha256:" + self.out["archive_sha256"],
            downloaded_offhost_sha256="sha256:" + self.out["archive_sha256"],
            expected_restic_repo_id=self.repo_id,
            expected_key_epoch=self.key_epoch,
            now=self.current + dt.timedelta(hours=2),
            admission_expires_at=self.current + dt.timedelta(hours=3),
        )

    def denied(self, code, **overrides):
        kw = dict(self.args)
        kw.update(overrides)
        with self.assertRaises(evidence.Stop) as error:
            evidence.inspect(**kw)
        self.assertEqual(str(error.exception), code)

    def test_producer_actual_receipts_pass_validator_without_authorizing_go(self):
        result = evidence.inspect(**self.args)
        self.assertEqual(result["status"], evidence.STATUS)
        self.assertEqual(result["archive_sha256"], self.args["observed_archive_sha256"])
        self.assertEqual(result["restic_repo_id"], self.repo_id)
        self.assertEqual(result["key_epoch"], self.key_epoch)
        self.assertEqual(self.review_bytes, canonical(self.review))
        self.assertEqual(self.restore_bytes, canonical(self.restored))
        self.assertTrue(self.downloaded.is_file())
        self.assertFalse(Path(self.root, "repo-v2").exists())
        for field in ("may_apply", "may_install", "physical_archive_verified_here",
                      "offhost_custody_authenticated_here",
                      "restore_authenticated_here",
                      "repository_identity_authenticated_here",
                      "key_generation_authenticated_here",
                      "sponsor_authorized_here"):
            self.assertIs(result[field], False, field)

    def test_offhost_copy_or_receipt_hash_mismatch_fails(self):
        self.denied(
            "G6_V2_ARCHIVE_OR_OFFHOST_HASH_MISMATCH",
            downloaded_offhost_sha256="sha256:" + "d" * 64,
        )
        altered = dict(self.review, g6_archive_sha256="d" * 64)
        raw = canonical(altered)
        self.denied(
            "G6_V2_ARCHIVE_OR_OFFHOST_HASH_MISMATCH",
            review_bytes=raw,
            pins={**self.args["pins"], "review_sha256": digest(raw)},
        )
        self.denied(
            "G6_V2_RESTORE_PIN_MISMATCH",
            pins={**self.args["pins"], "restore_sha256": "sha256:" + "a" * 64},
        )

    def test_restore_for_other_archive_or_key_epoch_fails(self):
        altered = dict(self.restored, archive_sha256="e" * 64)
        raw = canonical(altered)
        self.denied(
            "G6_V2_RESTORE_MISMATCH",
            restore_bytes=raw,
            pins={**self.args["pins"], "restore_sha256": digest(raw)},
        )
        self.denied("G6_V2_GENERATION_MISMATCH", expected_key_epoch="b" * 32)
        self.denied("G6_V2_GENERATION_MISMATCH", expected_restic_repo_id="d" * 64)

    def test_v1_schema_or_expired_checkpoint_does_not_upgrade(self):
        raw = canonical(dict(self.review, schema="qa525-g6-v1-review"))
        self.denied(
            "G6_V2_SCHEMA_INVALID", review_bytes=raw,
            pins={**self.args["pins"], "review_sha256": digest(raw)},
        )
        self.denied(
            "G6_V2_WINDOW_EXPIRED_OR_UNCOVERED",
            now=self.current + dt.timedelta(hours=48),
            admission_expires_at=self.current + dt.timedelta(hours=49),
        )


if __name__ == "__main__":
    unittest.main()
