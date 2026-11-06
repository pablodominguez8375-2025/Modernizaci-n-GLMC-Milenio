"""QA525 G6-to-approval byte binding: offline tests, no real host."""
import ast
import datetime as dt
import hashlib
import importlib.util
import json
from pathlib import Path
import unittest

import test_qa525_vnext_trust_binding as trust

ROOT = Path(__file__).resolve().parents[1]
SRC = ROOT / "scripts" / "qa" / "qa525-vnext-g6-anchor.py"
spec = importlib.util.spec_from_file_location("qa525_vnext_g6_anchor_test", SRC)
unit = importlib.util.module_from_spec(spec)
spec.loader.exec_module(unit)


def raw(record):
    return (json.dumps(record, sort_keys=True) + "\n").encode()


def sha(contents):
    return "sha256:" + hashlib.sha256(contents).hexdigest()


class G6AnchorTests(unittest.TestCase):
    def setUp(self):
        self.admission = trust.admission_record()
        self.name = "centenario-qa-g6-CIFRADO-20261105T180000Z.tar.gz"
        self.review = {
            "g6_archive_name": self.name,
            "g6_archive_sha256": "a" * 64,
            "g6_restore_report_name":
                "isolated-restore-20261105T180000Z.report.json",
            "g6_offhost_sha256_verified": True,
        }
        self.receipt = {
            "archive_name": self.name, "sha256": "a" * 64,
            "status": "ENCRYPTED_LOCAL_ARCHIVE_READY_OFFHOST_PENDING",
        }
        self.restore = {
            "result": "G6_SAME_HOST_ISOLATED_RESTORE_PASS_NOT_DISASTER_RECOVERY",
            "archive_filename": self.name, "archive_sha256": "a" * 64,
            "postgres_tables_by_database": {"pmgm": 7, "pmgm_keycloak": 4},
            "silo_objects_verified": 2,
            "production_touched": False,
        }
        self.anchor = trust.approval_record(self.admission)
        self.anchor["g6_review_sha256"] = sha(raw(self.review))

    def compare(self, *, admission=None, anchor=None, review=None,
                receipt=None, restore=None, pinned=None, now=trust.NOW,
                policy=trust.POLICY):
        a = raw(self.admission if admission is None else admission)
        b = trust.canonical(self.anchor if anchor is None else anchor)
        c = raw(self.review if review is None else review)
        d = raw(self.receipt if receipt is None else receipt)
        e = raw(self.restore if restore is None else restore)
        pins = [sha(a), sha(b), sha(c), sha(d), sha(e)]
        if pinned is not None:
            pins = list(pinned)
        return unit.compare(
            admission_bytes=a, approval_bytes=b,
            review_bytes=c, receipt_bytes=d, restore_bytes=e,
            pinned_admission_sha256=pins[0],
            pinned_approval_sha256=pins[1],
            pinned_review_sha256=pins[2],
            pinned_receipt_sha256=pins[3],
            pinned_restore_sha256=pins[4],
            observed_archive_sha256="sha256:" + "a" * 64,
            trusted_policy_hash=policy, now=now)

    def test_matching_bytes_are_only_consistency_not_permission(self):
        result = self.compare()
        self.assertEqual(result["status"], unit.STATUS)
        self.assertEqual(result["g6_review_sha256"], self.anchor["g6_review_sha256"])
        self.assertEqual(result["approval_sha256"], sha(trust.canonical(self.anchor)))
        for key in ("may_apply", "may_install", "signature_verified_here",
                    "physical_archive_verified_here",
                    "offhost_custody_authenticated_here",
                    "independent_restore_verified_here",
                    "sponsor_approved_here"):
            self.assertIs(result[key], False)

    def test_review_sha_mismatch_with_anchor_is_rejected_even_if_rehashed(self):
        wrong = dict(self.anchor, g6_review_sha256="sha256:" + "9" * 64)
        with self.assertRaisesRegex(unit.Stop, "^G6_ANCHOR_REVIEW_MISMATCH$"):
            self.compare(anchor=wrong)
        modified_review = dict(self.review, g6_archive_sha256="b" * 64)
        with self.assertRaises(unit.Stop):
            self.compare(review=modified_review)

    def test_expiring_g6_before_admission_window_is_rejected(self):
        admission = dict(self.admission, g6_expires_at="2026-11-05T18:10:00Z")
        anchor = trust.approval_record(admission)
        anchor["g6_review_sha256"] = sha(raw(self.review))
        with self.assertRaisesRegex(
                unit.Stop, "^G6_ANCHOR_EXPIRES_BEFORE_ADMISSION$"):
            self.compare(admission=admission, anchor=anchor)

    def test_checkpoint_48h_must_cover_whole_admission_not_just_now(self):
        # At NOW=Nov 5 18:05 the archive is <48h old, but it expires
        # at 18:06, before the admitted window closes at 18:25.
        stamp = "20261103T180600Z"
        name = "centenario-qa-g6-CIFRADO-" + stamp + ".tar.gz"
        review = dict(self.review, g6_archive_name=name,
                      g6_restore_report_name="isolated-restore-" + stamp + ".report.json")
        receipt = dict(self.receipt, archive_name=name)
        restore = dict(self.restore, archive_filename=name)
        anchor = dict(self.anchor, g6_review_sha256=sha(raw(review)))
        with self.assertRaisesRegex(
                unit.Stop, "^G6_ANCHOR_CHECKPOINT_EXPIRES_BEFORE_ADMISSION$"):
            self.compare(review=review, receipt=receipt, restore=restore,
                         anchor=anchor)
        # Exact end-of-window boundary is safe: validity is exclusive.
        stamp = "20261103T182500Z"
        name = "centenario-qa-g6-CIFRADO-" + stamp + ".tar.gz"
        review = dict(review, g6_archive_name=name,
                      g6_restore_report_name="isolated-restore-" + stamp + ".report.json")
        receipt = dict(receipt, archive_name=name)
        restore = dict(restore, archive_filename=name)
        anchor = dict(anchor, g6_review_sha256=sha(raw(review)))
        self.assertIs(self.compare(review=review, receipt=receipt,
                                   restore=restore, anchor=anchor)["may_apply"], False)

    def test_pinned_bytes_detect_tampering_before_binding(self):
        a = raw(self.admission)
        b = trust.canonical(self.anchor)
        c = raw(self.review)
        d = raw(self.receipt)
        e = raw(self.restore)
        pins = [sha(a), sha(b), sha(c), sha(d), sha(e)]
        tampered = dict(self.anchor, g6_review_sha256="sha256:" + "9" * 64)
        with self.assertRaisesRegex(unit.Stop, "^G6_ANCHOR_APPROVAL_PIN_MISMATCH$"):
            self.compare(anchor=tampered, pinned=pins)
        with self.assertRaisesRegex(unit.Stop, "^G6_ANCHOR_ADMISSION_PIN_MISMATCH$"):
            self.compare(admission=dict(self.admission, artifact_id=999),
                         pinned=pins)

    def test_bad_approval_or_policy_does_not_succeed(self):
        with self.assertRaisesRegex(unit.Stop, "^G6_ANCHOR_BINDING_REJECTED$"):
            self.compare(policy="sha256:" + "9" * 64)
        corrupted = dict(self.anchor, sponsor_approval_sha256="not-a-hash")
        with self.assertRaisesRegex(unit.Stop, "^G6_ANCHOR_BINDING_REJECTED$"):
            self.compare(anchor=corrupted)

    def test_tampered_restore_or_receipt_fails_before_approval(self):
        with self.assertRaisesRegex(unit.Stop, "^G6_ANCHOR_G6_METADATA_REJECTED$"):
            self.compare(restore=dict(self.restore, production_touched=True))
        with self.assertRaisesRegex(unit.Stop, "^G6_ANCHOR_G6_METADATA_REJECTED$"):
            self.compare(receipt=dict(self.receipt, sha256="b" * 64))
        with self.assertRaisesRegex(unit.Stop, "^G6_ANCHOR_G6_METADATA_REJECTED$"):
            self.compare(now=trust.NOW + dt.timedelta(days=4))

    def test_reject_duplicate_anchor_keys(self):
        a = raw(self.admission)
        b = trust.canonical(self.anchor)
        duplicate = b.replace(b'"scope": "API_WEB_QA_ONLY"',
                              b'"scope": "API_WEB_QA_ONLY", "scope": "API_WEB_QA_ONLY"')
        self.assertNotEqual(duplicate, b)
        with self.assertRaises(unit.Stop):
            unit.compare(
                admission_bytes=a, approval_bytes=duplicate,
                review_bytes=raw(self.review), receipt_bytes=raw(self.receipt),
                restore_bytes=raw(self.restore),
                pinned_admission_sha256=sha(a),
                pinned_approval_sha256=sha(duplicate),
                pinned_review_sha256=sha(raw(self.review)),
                pinned_receipt_sha256=sha(raw(self.receipt)),
                pinned_restore_sha256=sha(raw(self.restore)),
                observed_archive_sha256="sha256:" + "a" * 64,
                trusted_policy_hash=trust.POLICY, now=trust.NOW)

    def test_source_is_pure_and_cannot_run_as_host_script(self):
        tree = ast.parse(SRC.read_text())
        imported = set()
        for node in ast.walk(tree):
            if isinstance(node, ast.Import):
                imported.update(x.name.split(".")[0] for x in node.names)
            elif isinstance(node, ast.ImportFrom):
                imported.add((node.module or "").split(".")[0])
        self.assertEqual(imported, {
            "__future__", "datetime", "hashlib", "hmac", "importlib", "json",
            "pathlib", "re"})
        with self.assertRaises(SystemExit):
            exec(compile(SRC.read_text(), str(SRC), "exec"),
                 {"__name__": "__main__", "__file__": str(SRC)})


if __name__ == "__main__":
    unittest.main()
