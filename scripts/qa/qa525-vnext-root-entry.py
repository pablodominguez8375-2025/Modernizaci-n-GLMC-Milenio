#!/usr/bin/env python3
"""QA525 pre-root ENTRY composition (single path). INACTIVE library; grants NO authority.

Order is fixed and every step fails closed:
  1. all code comes from `load(name)` = bytes already authenticated by the PRE-4 bootstrap (never disk);
  2. the 5-lock plane + lease is HELD (injected port; this module never takes locks itself);
  3. the signed GO is verified (injected `verify_go`, e.g. the OPS/GO rootless ports) UNDER the plane;
  4. the sealed FD is received (#966) and its digest must equal the AUTHENTICATED digest from the verified
     GO claims (never a sender-supplied pin), with the GO nonce as the frame nonce;
  5. the plane must STILL be held, then the durable nonce (#967) is consumed BEFORE any child exists.
  6. the fixed, authenticated `child(fd)` callback runs INSIDE the plane (locks+lease stay held through it,
     i.e. through verify_state/rollback of the caller's flight); the plane is re-checked after it, and the FD
     is closed before the locks are released. A bare `return fd` would NOT transfer lock ownership.
A failure before step 5 never spends the nonce; after step 5 the nonce stays spent. The FD never outlives
the plane. may_apply/may_install stay False; `child` is the only code that may act, and it is supplied by
the authenticated entrypoint, not by the sender.
"""
import os

RECV = "qa525-vnext-root-boundary-recv.py"
JOURNAL = "qa525-vnext-go-nonce-journal.py"
CLAIM_FIELDS = ("nonce", "app_archive_sha256", "ops", "policy", "plan", "admission", "expires_at")


class Stop(Exception):
    """Opaque STOP code."""


def _claims(raw):
    if type(raw) is not dict or set(raw) != set(CLAIM_FIELDS):
        raise Stop("ENTRY_GO_CLAIMS_INVALID")
    if type(raw["expires_at"]) is not int or isinstance(raw["expires_at"], bool):
        raise Stop("ENTRY_GO_CLAIMS_INVALID")
    return dict(raw)


def run(load, *, plane, verify_go, sock, peer_uid, peer_pid, timeout, journal_root,
        uid, clock, child):
    if not callable(child):
        raise Stop("ENTRY_CHILD_INVALID")
    recv = load(RECV)
    journal = load(JOURNAL)
    with plane:  # step 2: raises Stop if any of the 5 locks / lease cannot be held
        if plane.still_held() is not True:
            raise Stop("ENTRY_PLANE_NOT_HELD")
        claims = _claims(verify_go())  # step 3: signature/scope/TTL/OPS binding verified here
        got = None
        try:
            got = recv.receive(  # step 4: digest authority = verified GO, not the sender
                sock, expected_nonce=claims["nonce"],
                expected_sha256=claims["app_archive_sha256"],
                expected_peer_uid=peer_uid, expected_peer_pid=peer_pid, timeout=timeout)
            if plane.still_held() is not True:  # step 5: lease/lock lost => nothing is spent
                raise Stop("ENTRY_PLANE_LOST")
            binding = {"app": claims["app_archive_sha256"], "ops": claims["ops"],
                       "policy": claims["policy"], "plan": claims["plan"],
                       "admission": claims["admission"]}
            spent = journal.consume(journal_root, claims["nonce"], binding, uid=uid,
                                    now=clock(), expires_at=claims["expires_at"])
            outcome = child(got["fd"])  # step 6: runs while the 5 locks + lease are still held
            if plane.still_held() is not True:  # lost during the flight: do not report success
                raise Stop("ENTRY_PLANE_LOST_DURING_FLIGHT")
        finally:
            if got is not None:
                os.close(got["fd"])  # the FD never outlives the plane
        return {"child_outcome": outcome, "nonce_record_sha256": spent["record_sha256"],
                "sender_pid": got["sender_pid"], "sender_uid": got["sender_uid"],
                "bootstrap_authenticated_here": False, "sponsor_authorized_here": False,
                "may_install": False, "may_apply": False}
