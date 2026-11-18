import importlib.util
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]


def load(name, rel):
    spec = importlib.util.spec_from_file_location(name, ROOT / rel)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


journal = load("qa525_autoweb_journal_test", "scripts/qa/qa525-vnext-autoweb-journal.py")
term = load("qa525_autoweb_terminal_for_journal", "scripts/qa/qa525-vnext-autoweb-terminal.py")
root_plane = load("qa525_autoweb_root_for_journal", "scripts/qa/qa525-vnext-autoweb-root-plane.py")
POLICY = {"policy_id": "policy-a", "policy_generation": 4,
          "first_sequence": 11, "last_sequence": 15}
CLEAN = {"active_flights": 0, "recovery_required": False, "unknown_residues": 0, "orphan_temps": 0}
NEW_NONCE = "autoweb-new-nonce-20261114-999999"


class Physical:
    def assert_held(self):
        return {"schema": root_plane.SCHEMA, "legacy_locks_held": 5,
                "physical_root_plane_v1": True, "may_apply": False, "may_install": False}


root_plane.PhysicalPlaneCapability = Physical


def terminal(index, previous=None, status="ACTIVE", release_digit=None, nonce=None):
    digit = release_digit or str(index + 1)
    rollback = None
    stage = "VERIFYING"
    observed = "sha256:" + "4" * 64
    if status == "BLOCKED":
        rollback, stage, observed = "sha256:" + "6" * 64, "ROLLBACK_VERIFYING", "sha256:" + "3" * 64
    value = {
        "schema": term.SCHEMA, "status": status, "policy_id": "policy-a",
        "policy_generation": 4, "sequence": 11 + index,
        "release_id": "sha256:" + digit * 64,
        "nonce": nonce or ("autoweb-chain-nonce-%08d" % index),
        "stage_record_sha256": "sha256:" + "1" * 64,
        "final_stage": stage, "installed_before_sha256": "sha256:" + "3" * 64,
        "expected_after_sha256": "sha256:" + "4" * 64,
        "observed_after_sha256": observed, "health_receipt_sha256": "sha256:" + "5" * 64,
        "rollback_receipt_sha256": rollback, "previous_terminal_sha256": previous,
        "critical": False, "finished_at": "2026-11-14T14:40:00Z",
        "may_apply": False, "authenticated_by": "EXTERNAL_SIGNATURE_VERIFIER_V1",
    }
    stage_binding = {"policy_id": value["policy_id"], "policy_generation": value["policy_generation"],
                     "sequence": value["sequence"], "release_id": value["release_id"],
                     "nonce": value["nonce"], "record_sha256": value["stage_record_sha256"],
                     "stage": value["final_stage"]}
    measurements = {key: value[key] for key in ("installed_before_sha256", "expected_after_sha256",
                    "observed_after_sha256", "health_receipt_sha256", "rollback_receipt_sha256")}
    return term.authenticate(value, b"sig", lambda *_: True, stage_binding, measurements,
                             Physical(), root_plane)


def chain(count):
    out, previous = [], None
    for index in range(count):
        cap = terminal(index, previous)
        previous = term.require_capability(cap)[1]
        out.append(cap)
    return out


class JournalTests(unittest.TestCase):
    def slot(self, history, release="sha256:" + "9" * 64, nonce=NEW_NONCE,
             policy=POLICY, state=CLEAN):
        return journal.next_slot(policy, history, release, nonce, term, state)

    def test_empty_and_four_release_history_return_exact_next_sequence(self):
        first = self.slot([])
        self.assertEqual((first["sequence"], first["previous_terminal_sha256"]), (11, None))
        self.assertEqual(first["nonce"], NEW_NONCE)
        self.assertIs(first["may_apply"], False)
        prior = chain(4)
        fifth = self.slot(prior)
        self.assertEqual(fifth["sequence"], 15)
        self.assertEqual(fifth["releases_remaining_after"], 0)

    def test_policy_exhaustion_and_bounds(self):
        with self.assertRaisesRegex(journal.Stop, "POLICY_EXHAUSTED"):
            self.slot(chain(5))
        bad = (dict(POLICY, last_sequence=10), dict(POLICY, last_sequence=16),
               dict(POLICY, policy_generation=True))
        for policy in bad:
            with self.subTest(policy=policy), self.assertRaisesRegex(journal.Stop, "POLICY_INVALID"):
                self.slot([], policy=policy)

    def test_jump_generation_policy_and_link_mutants_are_rejected(self):
        good = chain(2)
        raw, _ = term.require_capability(good[1])
        mutants = {
            "jump": dict(raw, sequence=14),
            "generation": dict(raw, policy_generation=5),
            "policy": dict(raw, policy_id="other"),
            "link": dict(raw, previous_terminal_sha256="sha256:" + "8" * 64),
        }
        for name, value in mutants.items():
            with self.subTest(name=name):
                stage_binding = {"policy_id": value["policy_id"], "policy_generation": value["policy_generation"],
                                 "sequence": value["sequence"], "release_id": value["release_id"],
                                 "nonce": value["nonce"], "record_sha256": value["stage_record_sha256"],
                                 "stage": value["final_stage"]}
                measurements = {key: value[key] for key in ("installed_before_sha256", "expected_after_sha256",
                                "observed_after_sha256", "health_receipt_sha256", "rollback_receipt_sha256")}
                cap = term.authenticate(value, b"sig", lambda *_: True, stage_binding, measurements,
                                        Physical(), root_plane)
                with self.assertRaisesRegex(journal.Stop, "LINEAGE_INVALID"):
                    self.slot([good[0], cap])

    def test_raw_forged_and_blocked_terminal_do_not_advance(self):
        with self.assertRaisesRegex(journal.Stop, "UNAUTHENTICATED"):
            self.slot([{}])
        blocked = terminal(0, status="BLOCKED")
        with self.assertRaisesRegex(journal.Stop, "CHAIN_BLOCKED"):
            self.slot([blocked])

    def test_release_and_nonce_replay_are_rejected(self):
        prior = chain(1)
        with self.assertRaisesRegex(journal.Stop, "RELEASE_REPLAY"):
            self.slot(prior, release=term.require_capability(prior[0])[0]["release_id"])
        with self.assertRaisesRegex(journal.Stop, "NONCE_REPLAY"):
            self.slot(prior, nonce=term.require_capability(prior[0])[0]["nonce"])
        first = prior[0]
        previous = term.require_capability(first)[1]
        repeat = terminal(1, previous, release_digit="1", nonce="autoweb-chain-nonce-00000000")
        with self.assertRaisesRegex(journal.Stop, "CHAIN_REPLAY"):
            self.slot([first, repeat])

    def test_recovery_active_or_unknown_residue_blocks_before_new_slot(self):
        mutants = [dict(CLEAN, active_flights=1), dict(CLEAN, recovery_required=True),
                   dict(CLEAN, unknown_residues=1), dict(CLEAN, orphan_temps=1)]
        for state in mutants:
            with self.subTest(state=state), self.assertRaisesRegex(journal.Stop, "ROOT_NOT_CLEAN"):
                self.slot([], state=state)

    def test_new_nonce_shape_is_strict(self):
        for nonce in ("short", "bad nonce with spaces", None):
            with self.subTest(nonce=nonce), self.assertRaisesRegex(journal.Stop, "NONCE_INVALID"):
                self.slot([], nonce=nonce)


if __name__ == "__main__":
    unittest.main()
