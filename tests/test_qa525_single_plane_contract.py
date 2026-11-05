"""QA525 static single-plane contract regression. No host or network access.

Checks Git source/JSON agreement. NEVER proves runtime locks, permissions,
a signed Sponsor approval, the actual host entrypoint, or permission to apply.
"""
import copy
import json
from pathlib import Path
import re
import unittest

ROOT = Path(__file__).resolve().parents[1]
CONTRACT = ROOT / "docs/qa/qa525-single-plane-v1.json"

SHARED = [
    "/opt/centenario/qa-api-refresh/operation.lock",
    "/opt/centenario/qa-auto-lite/web-update.lock",
    "/run/lock/pmgm-master-qa-604.lock",
    "/run/lock/pmgm-qa754-web-hotfix.lock",
    "/run/lock/pmgm-qa754-api-activation.lock",
]
TX_ROOT = "/opt/centenario/qa525-signed-prepare"
ROOT_LOCK = TX_ROOT + "/.qa525-transaction.lock"
REQUIRED_BARRIERS = frozenset({
    "ONE_PHYSICAL_ENTRYPOINT_AFTER_SIGNED_CUTOVER",
    "LEGACY_DISABLED_BY_VERIFIED_ROUTE_SUDOERS_OR_SYSTEMD_BEFORE_VNEXT_ENABLED",
    "NO_SECOND_WRITER_EVEN_IF_BOTH_LOCK_SETS_EXIST",
    "ALL_FIVE_LEGACY_LOCKS_THEN_VNEXT_ROOT_LOCK_UNDER_SAME_OPERATION",
    "LEGACY_AND_VNEXT_NONCE_MARKERS_MUST_NEVER_BE_CREATED_FOR_SAME_AUTHORIZATION",
    "ANY_PRIOR_VNEXT_NONCE_JOURNAL_OR_RECOVERY_BLOCKS_NEXT_FLIGHT",
    "ANY_UNRESOLVED_LEGACY_DUAL_BLOCKS_NEXT_FLIGHT",
    "ANY_EXISTING_LEGACY_APPROVAL_MARKER_REQUIRES_OPERATOR_ARCHIVE_AND_REAL_VERIFICATION",
    "ARCHIVE_ONLY_OFFLINE_WITH_EXTERNAL_SIGNATURE_VERIFICATION_AND_DURABLE_EVIDENCE",
    "PREPARED_RETAINED_AS_EVIDENCE_NOT_APPLY_AUTHORITY",
    "NO_AUTOMATIC_REPLAY_RETRY_OR_CLEANUP",
    "NO_APPLY_NO_MAIN_NO_PRODUCTION_NO_DATABASE_NO_IDENTITY",
    "NO_HOST_INFERENCE_FROM_REPO_CHECKS_OR_SYNTHETIC_FIXTURES",
})
UNVERIFIED = (
    "legacy_entrypoint_disable_verified",
    "single_root_verified_on_host",
    "admission_external_digest_authentication_verified",
    "terminal_state_verifier_verified",
    "orphan_process_scope_verified",
    "live_apply_authorized",
)


def validate_contract(data):
    """Strict static policy invariant; result is NEVER an install approval."""
    if type(data) is not dict:
        raise ValueError("CONTRACT_NOT_OBJECT")
    exact = {
        "schema": "qa525-single-plane-contract-v1",
        "status": "DESIGN_CONTRACT_ONLY_NO_HOST_INSTALL_NO_APPLY",
        "host": "glmdch",
        "environment": "QA_SYNTHETIC",
        "transaction_root": TX_ROOT,
        "root_lock": ROOT_LOCK,
        "legacy_entrypoint": "/usr/local/sbin/pmgm-qa-update",
        "legacy_source": "scripts/qa/operators/qa525-supervised-dual-entrypoint.py",
        "legacy_root_installer_source": "scripts/qa/operators/qa525-launcher-supervised-root-install.py",
        "vnext_journal_source": "scripts/qa/qa525-vnext-journal.py",
        "legacy_journal_pattern": "dual-{app_sha}.json",
        "legacy_nonce_marker_pattern": "approval-consumed-{nonce}",
        "vnext_nonce_marker_pattern": "nonce-consumed-{nonce}.json",
        "vnext_journal_pattern": "journal-{nonce}.json",
        "vnext_recovery_pattern": "recovery-{nonce}.json",
        "prepared_receipt_name": "prepared.json",
        "legacy_archive_pending_name": "expired-prepared-archive-pending.json",
    }
    for key, expected in exact.items():
        if data.get(key) != expected:
            raise ValueError("CONTRACT_BINDING_INVALID_" + key.upper())
    if data.get("legacy_shared_locks") != SHARED:
        raise ValueError("LEGACY_FIVE_LOCKS_CHANGED")
    if data.get("future_lock_acquisition_order") != SHARED + [ROOT_LOCK]:
        raise ValueError("NONCOOPERATIVE_LOCK_ORDER")
    if any(data.get(key) is not False for key in UNVERIFIED):
        raise ValueError("UNVERIFIED_GUARD_MISREPRESENTED")
    if type(data.get("policy")) is not list or set(data["policy"]) != REQUIRED_BARRIERS:
        raise ValueError("CONTRACT_BARRIERS_CHANGED")
    needs = data.get("future_activation_preconditions")
    if (type(needs) is not list or len(needs) < 10
            or len(set(needs)) != len(needs)):
        raise ValueError("CUTOVER_PRECONDITIONS_INVALID")
    if (type(data.get("physical_qa_last_operator_source_sha")) is not str
            or not re.fullmatch(r"[0-9a-f]{40}", data["physical_qa_last_operator_source_sha"])):
        raise ValueError("HISTORICAL_REFERENCE_INVALID")
    return "STATIC_DESIGN_CONSISTENT_NO_HOST_AUTHORITY"


class SinglePlaneContractTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.contract = json.loads(CONTRACT.read_text(encoding="utf-8"))

    def test_contract_self_consistency_no_activation(self):
        self.assertEqual(validate_contract(self.contract),
                         "STATIC_DESIGN_CONSISTENT_NO_HOST_AUTHORITY")
        self.assertFalse(self.contract["live_apply_authorized"])

    def test_legacy_five_lock_paths_match_actual_source_and_order(self):
        legacy = (ROOT / self.contract["legacy_root_installer_source"]).read_text()
        block = legacy.split("LOCKS = (", 1)[1].split("\n)", 1)[0]
        real = re.findall(r'Path\("([^"]+)"\)', block)
        self.assertEqual(real, self.contract["legacy_shared_locks"])
        self.assertIn('PREFLIGHT_STATE = Path("' + TX_ROOT + '")', legacy)
        self.assertIn('LAUNCHER = Path("/usr/local/sbin/pmgm-qa-update")', legacy)
        self.assertIn("fcntl.flock(fd, fcntl.LOCK_EX | fcntl.LOCK_NB)", legacy)

    def test_legacy_and_vnext_markers_are_distinct_existing_code(self):
        legacy = (ROOT / self.contract["legacy_source"]).read_text()
        update = (ROOT / "scripts/qa/operators/qa525-update.py").read_text()
        vnext = (ROOT / self.contract["vnext_journal_source"]).read_text()
        self.assertIn('STATE = Path("' + TX_ROOT + '")', legacy)
        self.assertIn('"approval-consumed-" + approve["nonce"]', legacy)
        self.assertIn("'approval-consumed-' + nonce", update)
        self.assertIn("'dual-' + args.source + '.json'", update)
        self.assertIn('".qa525-transaction.lock"', vnext)
        self.assertIn('"nonce-consumed-" + nonce + ".json"', vnext)
        self.assertIn('"journal-" + nonce + ".json"', vnext)
        self.assertIn('"recovery-" + nonce + ".json"', vnext)

    def test_policy_denies_install_and_detects_weakened_mappings(self):
        cases = []
        def mutation(fn):
            value = copy.deepcopy(self.contract)
            fn(value)
            cases.append(value)
        mutation(lambda x: x.update(live_apply_authorized=True))
        mutation(lambda x: x.update(legacy_entrypoint_disable_verified=True))
        mutation(lambda x: x.update(root_lock="/tmp/qa525.lock"))
        mutation(lambda x: x.update(transaction_root="/tmp"))
        mutation(lambda x: x["legacy_shared_locks"].pop())
        mutation(lambda x: x["future_lock_acquisition_order"].reverse())
        mutation(lambda x: x["policy"].remove(
            "LEGACY_AND_VNEXT_NONCE_MARKERS_MUST_NEVER_BE_CREATED_FOR_SAME_AUTHORIZATION"))
        mutation(lambda x: x.update(status="READY_TO_INSTALL"))
        mutation(lambda x: x.update(vnext_nonce_marker_pattern="approval-consumed-{nonce}"))
        for i, changed in enumerate(cases):
            with self.subTest(case=i), self.assertRaises(ValueError):
                validate_contract(changed)

    def test_no_execution_or_host_access_is_added_by_this_contract(self):
        self.assertTrue(CONTRACT.is_file())
        self.assertTrue(set(self.contract["policy"]) == REQUIRED_BARRIERS)
        self.assertFalse(self.contract["single_root_verified_on_host"])
        self.assertFalse(self.contract["admission_external_digest_authentication_verified"])
        self.assertFalse(self.contract["terminal_state_verifier_verified"])
        self.assertFalse(self.contract["orphan_process_scope_verified"])
        self.assertNotIn("apply", self.contract["status"].lower().replace("no_apply", ""))


if __name__ == "__main__":
    unittest.main()
