#!/usr/bin/env python3
"""Pure multi-release chain admission for QA525 auto-web V3.2.

This does not read or write the physical journal.  It decides whether a new
release slot is admissible from authenticated terminal capabilities.  The
future root engine must persist the returned slot under its held lock.
"""
import re

DIGEST = re.compile(r"^sha256:[a-f0-9]{64}$")
NONCE = re.compile(r"^[A-Za-z0-9_-]{24,96}$")
POLICY_FIELDS = frozenset({"policy_id", "policy_generation", "first_sequence", "last_sequence"})
ROOT_STATE_FIELDS = frozenset({"active_flights", "recovery_required", "unknown_residues", "orphan_temps"})


class Stop(Exception):
    """Static fail-closed code only."""


def next_slot(policy, terminals, release_id, new_nonce, terminal_module, root_state):
    """Return the next persistable slot after a clean inventory under the held lock.

    ``root_state`` must be produced by the root-owned read-only inventory while
    the same physical plane is held.  Caller-supplied or pre-lock state is not a
    trust input and must never reach this function in the future orchestrator.
    """
    if type(policy) is not dict or set(policy) != POLICY_FIELDS:
        raise Stop("CHAIN_POLICY_INVALID")
    if (type(policy["policy_id"]) is not str or not policy["policy_id"]
            or type(policy["policy_generation"]) is not int or policy["policy_generation"] < 1
            or type(policy["first_sequence"]) is not int or policy["first_sequence"] < 1
            or type(policy["last_sequence"]) is not int
            or policy["last_sequence"] < policy["first_sequence"]
            or policy["last_sequence"] - policy["first_sequence"] + 1 > 5):
        raise Stop("CHAIN_POLICY_INVALID")
    window = policy["last_sequence"] - policy["first_sequence"] + 1
    if type(terminals) is not list or len(terminals) > window:
        raise Stop("CHAIN_HISTORY_INVALID")
    if type(release_id) is not str or DIGEST.fullmatch(release_id) is None:
        raise Stop("CHAIN_RELEASE_INVALID")
    if type(new_nonce) is not str or NONCE.fullmatch(new_nonce) is None:
        raise Stop("CHAIN_NONCE_INVALID")
    if not callable(getattr(terminal_module, "require_capability", None)):
        raise Stop("CHAIN_TERMINAL_MODULE_INVALID")
    if type(root_state) is not dict or set(root_state) != ROOT_STATE_FIELDS:
        raise Stop("CHAIN_ROOT_STATE_INVALID")
    if (type(root_state["active_flights"]) is not int
            or type(root_state["unknown_residues"]) is not int
            or type(root_state["orphan_temps"]) is not int
            or type(root_state["recovery_required"]) is not bool
            or min(root_state["active_flights"], root_state["unknown_residues"],
                   root_state["orphan_temps"]) < 0):
        raise Stop("CHAIN_ROOT_STATE_INVALID")
    if (root_state["active_flights"] != 0 or root_state["recovery_required"]
            or root_state["unknown_residues"] != 0 or root_state["orphan_temps"] != 0):
        raise Stop("CHAIN_ROOT_NOT_CLEAN")

    previous = None
    seen_releases, seen_nonces = set(), set()
    for index, capability in enumerate(terminals):
        try:
            record, fingerprint = terminal_module.require_capability(capability)
        except Exception:
            raise Stop("CHAIN_TERMINAL_UNAUTHENTICATED") from None
        expected_sequence = policy["first_sequence"] + index
        if (record["policy_id"] != policy["policy_id"]
                or record["policy_generation"] != policy["policy_generation"]
                or record["sequence"] != expected_sequence
                or record["previous_terminal_sha256"] != previous):
            raise Stop("CHAIN_LINEAGE_INVALID")
        if record["status"] != "ACTIVE":
            raise Stop("CHAIN_BLOCKED")
        if record["release_id"] in seen_releases or record["nonce"] in seen_nonces:
            raise Stop("CHAIN_REPLAY")
        seen_releases.add(record["release_id"])
        seen_nonces.add(record["nonce"])
        previous = fingerprint
    if len(terminals) >= window:
        raise Stop("CHAIN_POLICY_EXHAUSTED")
    if release_id in seen_releases:
        raise Stop("CHAIN_RELEASE_REPLAY")
    if new_nonce in seen_nonces:
        raise Stop("CHAIN_NONCE_REPLAY")
    return {
        "policy_id": policy["policy_id"],
        "policy_generation": policy["policy_generation"],
        "sequence": policy["first_sequence"] + len(terminals),
        "release_id": release_id,
        "nonce": new_nonce,
        "previous_terminal_sha256": previous,
        "releases_remaining_after": window - len(terminals) - 1,
        "may_apply": False,
        "requires_root_persistence": True,
    }


if __name__ == "__main__":
    raise SystemExit("QA525_AUTOWEB_JOURNAL_LIBRARY_ONLY_NO_EXECUTION")
