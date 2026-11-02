"""Hermetic QA525 A/B signed-envelope review contract; NO real host or root."""
import hashlib
import importlib.util
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[1]
OPERATORS = ROOT / "scripts/qa/operators"
MODULE = OPERATORS / "qa525-delayed-recovery-signed-evidence-audit.py"
CONTRACT = OPERATORS / "qa525-launcher-replacement-contract.py"
spec = importlib.util.spec_from_file_location("qa525_delayed_evidence_tests", MODULE)
m = importlib.util.module_from_spec(spec)
spec.loader.exec_module(m)
sp = importlib.util.spec_from_file_location("qa525_delayed_policy_tests", CONTRACT)
policy = importlib.util.module_from_spec(sp)
sp.loader.exec_module(policy)


class DelayedDualSignedEvidenceAuditTests(unittest.TestCase):
    """Injected audits are synthetic and MUST NEVER claim Sigstore trust."""

    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory(prefix="qa525-dual-signed-test-")
        self.addCleanup(self.tmp.cleanup)
        root = Path(self.tmp.name)
        self.cut_zip = root / "A.zip"
        self.recovery_zip = root / "B.zip"
        self.a = "a" * 40
        self.b = "b" * 40
        self.da = "c" * 64
        self.db = "d" * 64
        self.now = 5000
        self.original = {
            "status": "LAUNCHER_ATOMIC_SWAP_COMMITTED",
            "source_sha": self.a,
            "release_asset_sha256": "sha256:" + self.da,
            "nonce": "1" * 64,
            "old_launcher_sha256": "2" * 64,
            "new_launcher_sha256": "3" * 64,
            "candidate_operator_sha256": "4" * 64,
        }
        local_sha = hashlib.sha256(CONTRACT.read_bytes()).hexdigest()
        self.signed_a = {
            "status": "SIGNED_INSTALLER_AUDITED_UNPRIVILEGED_NO_INSTALL",
            "source_sha": self.a,
            "release_asset_sha256": "sha256:" + self.da,
            "manifest_sha256": "5" * 64,
            "privileged_sources_sha256": {
                m.ROOT_EXEC_REL: "6" * 64,
                m.CONTRACT_REL: "7" * 64,
            },
            "bootstrap_admission_created": False,
            "may_install": False,
            "may_apply": False,
        }
        self.signed_b = {
            "status": "SIGNED_INSTALLER_AUDITED_UNPRIVILEGED_NO_INSTALL",
            "source_sha": self.b,
            "release_asset_sha256": "sha256:" + self.db,
            "manifest_sha256": "8" * 64,
            "privileged_sources_sha256": {
                m.ROOT_EXEC_REL: "9" * 64,
                m.CONTRACT_REL: local_sha,
            },
            "bootstrap_admission_created": False,
            "may_install": False,
            "may_apply": False,
        }
        self.approval = {
            "action": policy.DELAYED_RECOVERY_ACTION,
            "scope": policy.DELAYED_RECOVERY_SCOPE,
            "environment": policy.ENVIRONMENT,
            "operator": "soporte@glmdch",
            "approved": True,
            "cut_source_sha": self.a,
            "cut_release_asset_sha256": "sha256:" + self.da,
            "cut_nonce": self.original["nonce"],
            "cut_receipt_sha256": policy.frozen_sha(self.original),
            "old_launcher_sha256": self.original["old_launcher_sha256"],
            "new_launcher_sha256": self.original["new_launcher_sha256"],
            "recovery_source_sha": self.b,
            "recovery_release_asset_sha256": "sha256:" + self.db,
            "recovery_manifest_sha256": "8" * 64,
            "recovery_executor_sha256": "9" * 64,
            "nonce": "f" * 64,
            "issued_at": self.now - 1,
            "expires_at": self.now + 120,
        }
        self.calls = []

    def fake_audit(self, filename, source, digest):
        self.calls.append((Path(filename), source, digest))
        return self.signed_a if source == self.a else self.signed_b

    def call(self, *, committed=None, approval=None, pending=False,
             now=5000, live=None, backup=None, consumed=()):
        with patch.object(m.os, "geteuid", return_value=1000):
            return m.review_two_signed_envelopes(
                self.cut_zip, self.a, self.da,
                self.recovery_zip, self.b, self.db,
                self.original if committed is None else committed,
                self.approval if approval is None else approval,
                live_sha256=self.original["new_launcher_sha256"]
                    if live is None else live,
                backup_sha256=self.original["old_launcher_sha256"]
                    if backup is None else backup,
                pending=pending, now=now, consumed_nonces=consumed,
                audit_fn=self.fake_audit)

    def test_two_release_reports_bind_to_separate_original_and_recovery(self):
        result = self.call()
        self.assertEqual("SYNTHETIC_DUAL_ENVELOPES_REVIEW_NO_AUTHENTICATION",
                         result["status"])
        self.assertEqual([self.a, self.b], [x[1] for x in self.calls])
        self.assertFalse(result["release_envelopes_sigstore_checked_unprivileged"])
        self.assertFalse(result["cut_receipt_authenticated_by_audit"])
        self.assertFalse(result["approval_authenticated_by_audit"])
        for k in ("may_apply", "may_install", "may_recover",
                  "bootstrap_admission_created", "physical_host_touched"):
            self.assertFalse(result[k])
        self.assertEqual(self.b, result["recovery_source_sha"])
        self.assertEqual(self.a, result["cut_source_sha"])

    def test_cut_and_recovery_report_tampering_fails_closed(self):
        for field, value, target in (
            ("source_sha", "0" * 40, "signed_a"),
            ("release_asset_sha256", "sha256:" + "0" * 64, "signed_a"),
            ("manifest_sha256", "0", "signed_a"),
            ("source_sha", "0" * 40, "signed_b"),
            ("release_asset_sha256", "sha256:" + "0" * 64, "signed_b"),
            ("manifest_sha256", "0", "signed_b"),
            ("bootstrap_admission_created", True, "signed_b"),
            ("may_install", True, "signed_b"),
        ):
            with self.subTest(field=field, target=target):
                report = getattr(self, target)
                before = report[field]
                report[field] = value
                try:
                    with self.assertRaises(m.Blocked):
                        self.call()
                finally:
                    report[field] = before

    def test_recovery_signed_contract_is_identical_to_local_review_code(self):
        prior = self.signed_b["privileged_sources_sha256"][m.CONTRACT_REL]
        self.signed_b["privileged_sources_sha256"][m.CONTRACT_REL] = "0" * 64
        try:
            with self.assertRaisesRegex(
                    m.Blocked, "DELAYED_LOCAL_CONTRACT_NOT_IN_SIGNED_RECOVERY_RELEASE"):
                self.call()
        finally:
            self.signed_b["privileged_sources_sha256"][m.CONTRACT_REL] = prior

    def test_untrusted_recovery_contract_never_imported_before_manifest_match(self):
        original = self.signed_b["privileged_sources_sha256"][m.CONTRACT_REL]
        self.signed_b["privileged_sources_sha256"][m.CONTRACT_REL] = "0" * 64
        try:
            with patch.object(m, "load_local", wraps=m.load_local) as loaded:
                with self.assertRaisesRegex(
                        m.Blocked,
                        "DELAYED_LOCAL_CONTRACT_NOT_IN_SIGNED_RECOVERY_RELEASE"):
                    self.call()
                self.assertNotIn(
                    "qa525-launcher-replacement-contract.py",
                    [args.args[0] for args in loaded.call_args_list])
        finally:
            self.signed_b["privileged_sources_sha256"][m.CONTRACT_REL] = original

    def test_approval_mutations_replay_and_pending_are_rejected(self):
        for change in ({"nonce": self.original["nonce"]},
                       {"cut_receipt_sha256": "0" * 64},
                       {"recovery_executor_sha256": "0" * 64},
                       {"recovery_manifest_sha256": "0" * 64},
                       {"approved": False},
                       {"scope": "API_WEB"},
                       {"expires_at": self.now},
                       {"extra": "unknown"}):
            with self.subTest(change=change):
                with self.assertRaises(m.Blocked):
                    self.call(approval={**self.approval, **change})
        with self.assertRaises(m.Blocked):
            self.call(pending=True)
        with self.assertRaises(m.Blocked):
            self.call(live="0" * 64)
        with self.assertRaises(m.Blocked):
            self.call(backup="0" * 64)
        with self.assertRaises(m.Blocked):
            self.call(consumed=("f" * 64,))

    def test_no_forged_original_cut_and_root_execution_blocked(self):
        with self.assertRaises(m.Blocked):
            self.call(committed={**self.original, "source_sha": self.b})
        with self.assertRaises(m.Blocked):
            self.call(committed={**self.original, "status": "INCOMPLETE"})
        with patch.object(m.os, "geteuid", return_value=0):
            with self.assertRaisesRegex(
                    m.Blocked, "DELAYED_EVIDENCE_AUDIT_ROOT_FORBIDDEN"):
                m.review_two_signed_envelopes(
                    self.cut_zip, self.a, self.da, self.recovery_zip,
                    self.b, self.db, self.original, self.approval,
                    live_sha256=self.original["new_launcher_sha256"],
                    backup_sha256=self.original["old_launcher_sha256"],
                    pending=False, now=self.now, audit_fn=self.fake_audit)
        self.assertEqual([], list(Path(self.tmp.name).iterdir()))




class PendingDualSignedEvidenceAuditTests(DelayedDualSignedEvidenceAuditTests):
    """A is an interrupted intent, never a committed receipt; no root access."""

    def setUp(self):
        super().setUp()
        self.intent = {
            "status": "SWAP_INTENT_NO_DEPLOYMENT",
            "source_sha": self.a,
            "release_asset_sha256": "sha256:" + self.da,
            "old_launcher_sha256": "2" * 64,
            "new_launcher_sha256": "3" * 64,
            "nonce": "1" * 64,
        }
        self.pending_approval = {
            "action": policy.PENDING_RECONCILIATION_ACTION,
            "scope": policy.PENDING_RECONCILIATION_SCOPE,
            "environment": policy.ENVIRONMENT,
            "operator": "soporte@glmdch",
            "approved": True,
            "cut_source_sha": self.a,
            "cut_release_asset_sha256": "sha256:" + self.da,
            "cut_nonce": self.intent["nonce"],
            "cut_intent_sha256": policy.frozen_sha(self.intent),
            "old_launcher_sha256": self.intent["old_launcher_sha256"],
            "new_launcher_sha256": self.intent["new_launcher_sha256"],
            "recovery_source_sha": self.b,
            "recovery_release_asset_sha256": "sha256:" + self.db,
            "recovery_manifest_sha256": self.signed_b["manifest_sha256"],
            "recovery_executor_sha256":
                self.signed_b["privileged_sources_sha256"][m.ROOT_EXEC_REL],
            "nonce": "f" * 64,
            "issued_at": self.now - 1,
            "expires_at": self.now + 120,
        }

    def pending_review(self, *, intent=None, approval=None, live=None,
                       backup=None, receipt=False, now=5000, consumed=()):
        with patch.object(m.os, "geteuid", return_value=1000):
            return m.review_pending_two_signed_envelopes(
                self.cut_zip, self.a, self.da,
                self.recovery_zip, self.b, self.db,
                self.intent if intent is None else intent,
                self.pending_approval if approval is None else approval,
                live_sha256=(self.intent["old_launcher_sha256"]
                             if live is None else live),
                backup_sha256=(self.intent["old_launcher_sha256"]
                               if backup is None else backup),
                receipt_present=receipt, now=now,
                consumed_nonces=consumed, audit_fn=self.fake_audit)

    def test_pending_signed_a_b_review_never_authorizes_both_phases(self):
        for live, phase in (
            (self.intent["old_launcher_sha256"], "PENDING_BEFORE_ATOMIC_SWAP"),
            (self.intent["new_launcher_sha256"],
             "PENDING_AFTER_ATOMIC_SWAP_NO_RECEIPT"),
        ):
            with self.subTest(phase=phase):
                result = self.pending_review(live=live)
                self.assertEqual(phase, result["observed_phase"])
                self.assertEqual(
                    "SYNTHETIC_PENDING_DUAL_REVIEW_NO_AUTHENTICATION",
                    result["status"])
                self.assertEqual(self.a, result["cut_source_sha"])
                self.assertEqual(self.b, result["recovery_source_sha"])
                self.assertEqual(policy.frozen_sha(self.intent),
                                 result["cut_intent_sha256"])
                for key in ("may_install", "may_apply", "may_recover",
                            "cut_intent_authenticated_by_audit",
                            "approval_authenticated_by_audit",
                            "release_envelopes_sigstore_checked_unprivileged",
                            "bootstrap_admission_created",
                            "physical_host_touched"):
                    self.assertIs(result[key], False)

    def test_pending_rejects_mismatched_intent_and_signed_b(self):
        with self.assertRaisesRegex(
                m.Blocked, "PENDING_DUAL_ORIGINAL_NOT_BOUND_TO_INTENT"):
            self.pending_review(intent={**self.intent,
                                        "source_sha": "0" * 40})
        with self.assertRaisesRegex(
                m.Blocked, "PENDING_DUAL_RECONCILIATION_REVIEW_REJECTED"):
            self.pending_review(intent={**self.intent,
                                        "new_launcher_sha256": "0" * 64})
        with self.assertRaises(m.Blocked):
            self.pending_review(approval={**self.pending_approval,
                                          "cut_intent_sha256": "0" * 64})
        with self.assertRaises(m.Blocked):
            self.pending_review(approval={**self.pending_approval,
                                          "recovery_executor_sha256": "0" * 64})
        with self.assertRaises(m.Blocked):
            self.pending_review(receipt=True)
        with self.assertRaises(m.Blocked):
            self.pending_review(live="0" * 64)
        with self.assertRaises(m.Blocked):
            self.pending_review(consumed=("f" * 64,))

    def test_pending_does_not_import_policy_before_manifest_sha_match(self):
        prior = self.signed_b["privileged_sources_sha256"][m.CONTRACT_REL]
        self.signed_b["privileged_sources_sha256"][m.CONTRACT_REL] = "0" * 64
        try:
            with patch.object(m, "load_local", wraps=m.load_local) as loaded:
                with self.assertRaisesRegex(
                        m.Blocked, "PENDING_DUAL_CONTRACT_NOT_IN_SIGNED_B"):
                    self.pending_review()
                self.assertNotIn(
                    "qa525-launcher-replacement-contract.py",
                    [args.args[0] for args in loaded.call_args_list])
        finally:
            self.signed_b["privileged_sources_sha256"][m.CONTRACT_REL] = prior

    def test_pending_root_and_expiration_fail_closed(self):
        with patch.object(m.os, "geteuid", return_value=0):
            with self.assertRaisesRegex(
                    m.Blocked, "PENDING_DUAL_EVIDENCE_ROOT_FORBIDDEN"):
                m.review_pending_two_signed_envelopes(
                    self.cut_zip, self.a, self.da,
                    self.recovery_zip, self.b, self.db,
                    self.intent, self.pending_approval,
                    live_sha256=self.intent["old_launcher_sha256"],
                    backup_sha256=self.intent["old_launcher_sha256"],
                    receipt_present=False, now=self.now,
                    audit_fn=self.fake_audit)
        with self.assertRaises(m.Blocked):
            self.pending_review(approval={**self.pending_approval,
                                          "expires_at": 5000})


if __name__ == "__main__":
    unittest.main()
