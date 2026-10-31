"""Hermetic no-host policy tests for a supervised root-owned launcher swap."""
import copy
import importlib.util
from pathlib import Path
import sys
import unittest

SOURCE = Path(__file__).resolve().parents[1] / "scripts/qa/operators/qa525-launcher-replacement-contract.py"
spec = importlib.util.spec_from_file_location("qa525_launcher_replacement_contract", SOURCE)
c = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = c
spec.loader.exec_module(c)

OLD, NEW = "a" * 64, "b" * 64
SOURCE_SHA = "c" * 40
DIGEST = "sha256:" + "d" * 64
NONCE = "e" * 64
NOW = 1793488800

def preflight():
    return dict(status="SIGNED_LAUNCHER_CANDIDATE_VERIFIED_NO_INSTALL",
                source_sha=SOURCE_SHA, release_asset_sha256=DIGEST,
                old_launcher_sha256=OLD, candidate_operator_sha256=NEW,
                sigstore_verified_in_this_operation=True,
                signed_manifest_verified=True, old_launcher_checked_twice=True,
                replacement_applied=False, may_install=False, may_apply=False)

def approval():
    return dict(source_sha=SOURCE_SHA, release_asset_sha256=DIGEST,
                old_launcher_sha256=OLD, candidate_operator_sha256=NEW,
                scope=c.SCOPE, action=c.ACTION, environment=c.ENVIRONMENT,
                operator="soporte@glmdch", approved=True,
                issued_at=NOW - 100, expires_at=NOW + 100, nonce=NONCE)

class LauncherReplacementPolicyTests(unittest.TestCase):
    def test_all_exact_claims_still_never_authorize_install(self):
        x=c.assess_review(preflight(),approval(),now=NOW,pending=False)
        self.assertEqual("REVIEWED_SIGNED_LAUNCHER_PLAN_NO_INSTALL", x["status"])
        self.assertIs(x["may_install"],False)
        self.assertIs(x["may_apply"],False)
        self.assertIs(x["rollback_complete"],False)
        self.assertEqual(100, x["approval_expires_at"]-NOW)

    def test_all_signed_preflight_flags_required(self):
        for key,value in (
                ("status", "SIGNATURE_VERIFIED_STAGED_NOT_DEPLOYED"),
                ("sigstore_verified_in_this_operation", False),
                ("signed_manifest_verified", False),
                ("old_launcher_checked_twice", False),
                ("replacement_applied", True),
                ("may_install", True),
                ("may_apply", True),
                ("source_sha", "unknown"),
                ("release_asset_sha256", "sha256:bad"),
                ("old_launcher_sha256", "other"),
                ("candidate_operator_sha256", "none")):
            v=preflight()
            v[key]=value
            with self.subTest(key=key),self.assertRaises(c.Blocked):
                c.assess_review(v,approval(),now=NOW,pending=False)

    def test_any_unapproved_identity_or_scope_change_blocks(self):
        for key,value in (
                ("source_sha","a"*40),
                ("release_asset_sha256","sha256:"+"a"*64),
                ("old_launcher_sha256",NEW),
                ("candidate_operator_sha256",OLD),
                ("scope","QA_DOCKER_ALLOWED"),
                ("action","deploy-api-and-web"),
                ("environment","PRODUCTION"),
                ("operator","unknown"),
                ("approved",False)):
            r=approval()
            r[key]=value
            with self.subTest(key=key),self.assertRaisesRegex(
                 c.Blocked,"REPLACE_EXACT_APPROVAL_REQUIRED"):
                c.assess_review(preflight(),r,now=NOW,pending=False)

    def test_expired_future_and_overlong_approval_block(self):
        for issued,expires,now in (
            (NOW-100,NOW,NOW),
            (NOW+1,NOW+200,NOW),
            (NOW-901,NOW+100,NOW),
            (NOW-100,NOW+901,NOW),
            (NOW,NOW,NOW)):
            r=approval()
            r.update(issued_at=issued,expires_at=expires)
            with self.subTest(issued=issued,expires=expires), self.assertRaisesRegex(
                c.Blocked,"REPLACE_APPROVAL_EXPIRED_OR_INVALID"):
                c.assess_review(preflight(),r,now=now,pending=False)

    def test_nonce_replay_and_pending_marker_block(self):
        with self.assertRaisesRegex(c.Blocked,"REPLACE_APPROVAL_NONCE_CONSUMED_OR_INVALID"):
            c.assess_review(preflight(),approval(),now=NOW,pending=False,
                            consumed_nonces=(NONCE,))
        with self.assertRaisesRegex(c.Blocked,"REPLACE_UNFINISHED_JOURNAL_BLOCKS"):
            c.assess_review(preflight(),approval(),now=NOW,pending=True)
        with self.assertRaises(c.Blocked):
            c.assess_review(preflight(),approval(),now=NOW,pending="False")
        r=approval(); r["nonce"]="invalid"
        with self.assertRaisesRegex(c.Blocked,"REPLACE_APPROVAL_EXPIRED_OR_INVALID"):
            c.assess_review(preflight(),r,now=NOW,pending=False)

    def test_bool_is_not_timestamp(self):
        r=approval(); r["issued_at"]=True
        with self.assertRaises(c.Blocked):
            c.assess_review(preflight(),r,now=NOW,pending=False)
        with self.assertRaises(c.Blocked):
            c.assess_review(preflight(),approval(),now=True,pending=False)

    def test_untrusted_object_types_block(self):
        for value in ([],None,"text",True):
            with self.subTest(value=value),self.assertRaises(c.Blocked):
                c.assess_review(value,approval(),now=NOW,pending=False)
            with self.subTest(value=value),self.assertRaises(c.Blocked):
                c.assess_review(preflight(),value,now=NOW,pending=False)

    def test_recovery_no_marker_old_launcher_not_authorized(self):
        x=c.assess_interrupted_swap(old_sha=OLD,new_sha=NEW,
                                    live_sha=OLD,backup_sha=OLD,pending=False)
        self.assertEqual("REPLACE_NOT_STARTED_NO_AUTHORIZATION",x["status"])
        self.assertFalse(x["may_install"])

    def test_recovery_after_marker_before_rename_blocks(self):
        x=c.assess_interrupted_swap(old_sha=OLD,new_sha=NEW,
                                    live_sha=OLD,backup_sha=OLD,pending=True)
        self.assertEqual("REPLACE_OLD_PRESENT_PENDING_MANUAL_RECONCILIATION",x["status"])
        self.assertTrue(x["requires_operator_reconciliation"])
        self.assertFalse(x["may_recover_automatically"])

    def test_recovery_after_rename_before_receipt_blocks(self):
        x=c.assess_interrupted_swap(old_sha=OLD,new_sha=NEW,
                                    live_sha=NEW,backup_sha=OLD,pending=True)
        self.assertEqual("REPLACE_NEW_PRESENT_PENDING_MANUAL_RECONCILIATION",x["status"])
        self.assertTrue(x["requires_operator_reconciliation"])

    def test_unexpected_binary_and_backup_tampering(self):
        x=c.assess_interrupted_swap(old_sha=OLD,new_sha=NEW,
                                    live_sha="f"*64,backup_sha=OLD,pending=True)
        self.assertEqual("REPLACE_UNKNOWN_LAUNCHER_PENDING_MANUAL_RECONCILIATION",
                         x["status"])
        with self.assertRaisesRegex(c.Blocked,"REPLACE_BACKUP_HASH_DRIFT"):
            c.assess_interrupted_swap(old_sha=OLD,new_sha=NEW,
                                       live_sha=NEW,backup_sha=NEW,pending=True)

    def test_committed_receipt_still_not_deployment_authorization(self):
        receipt=dict(status="LAUNCHER_ATOMIC_SWAP_COMMITTED",
                     old_launcher_sha256=OLD,new_launcher_sha256=NEW)
        x=c.assess_interrupted_swap(old_sha=OLD,new_sha=NEW,
                                    live_sha=NEW,backup_sha=OLD,
                                    pending=False,receipt=receipt)
        self.assertEqual("REPLACE_COMMITTED_VERIFIED_NO_DEPLOYMENT",x["status"])
        self.assertFalse(x["may_apply"])
        self.assertFalse(x["may_install"])

    def test_replayed_receipt_after_drift_blocks(self):
        receipt=dict(status="LAUNCHER_ATOMIC_SWAP_COMMITTED",
                     old_launcher_sha256=OLD,new_launcher_sha256=NEW)
        x=c.assess_interrupted_swap(old_sha=OLD,new_sha=NEW,
                                    live_sha=OLD,backup_sha=OLD,
                                    pending=False,receipt=receipt)
        self.assertEqual("REPLACE_RECEIPT_LAUNCHER_DIVERGED_MANUAL_RECONCILIATION",
                         x["status"])
        for name,value in (("old_launcher_sha256",NEW),
                           ("status","FAKE_SUCCESS")):
            r=dict(receipt);r[name]=value
            with self.assertRaisesRegex(c.Blocked,"REPLACE_COMMIT_RECEIPT_NOT_THIS_CUT"):
                c.assess_interrupted_swap(old_sha=OLD,new_sha=NEW,
                                           live_sha=NEW,backup_sha=OLD,
                                           pending=False,receipt=r)

    def test_pending_marker_after_committed_receipt_still_blocks(self):
        receipt=dict(status="LAUNCHER_ATOMIC_SWAP_COMMITTED",
                     old_launcher_sha256=OLD,new_launcher_sha256=NEW)
        x=c.assess_interrupted_swap(old_sha=OLD,new_sha=NEW,
                                    live_sha=NEW,backup_sha=OLD,
                                    pending=True,receipt=receipt)
        self.assertTrue(x["requires_operator_reconciliation"])
        self.assertFalse(x["may_recover_automatically"])

    def test_json_receipt_hash_is_deterministic_and_not_signature(self):
        one=dict(b=2,a=1)
        self.assertEqual(c.frozen_sha(one),c.frozen_sha(dict(a=1,b=2)))
        self.assertNotEqual(c.frozen_sha(one),c.frozen_sha(dict(a=1,b=3)))

if __name__=="__main__":
    unittest.main()
