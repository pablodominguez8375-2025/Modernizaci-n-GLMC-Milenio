#!/usr/bin/env python3
"""QA525 vNext admission->journal bridge -- INACTIVE library, NO I/O, NO auth.

Single path from an admission record to a journal lease:

    admission BYTES + external approval BYTES --trust-binding.bind--> digest
    --journal.begin(that exact digest)--> Lease

``BoundJournal`` is a drop-in "journal" for ``qa525-vnext-locks.synthetic_flight``
(it exposes ``begin`` and ``lease_is_held``). It holds the bytes that were
presented for binding; the journal re-hashes the admission FILE and refuses a
different content, so the bytes bound here are the bytes consumed there.

Rules enforced here:
* The binding is re-run INSIDE ``begin`` with a fresh ``now`` (i.e. after the
  locks were taken), so a window that closed while waiting for locks is denied.
* ``begin`` refuses any digest other than the one the binding produced.
* One-shot: a second ``begin`` on the same object is refused before the journal.
* Errors are fixed codes; the binding's own static code is kept only in
  ``last_cause`` (never external text). Nothing here proves signatures, G6,
  Sponsor consent or a physical result; the bootstrap must authenticate the
  approval bytes BEFORE handing them to this class.
"""
import importlib.util
from pathlib import Path

HERE = Path(__file__).resolve().parent
STATUS = "ADMISSION_BRIDGE_NOT_AUTHENTICATED_NOT_AUTHORIZED_FOR_PHYSICAL_APPLY"


class Stop(Exception):
    """Fixed diagnostic code; never carries external content."""


def _load(name: str, filename: str):
    spec = importlib.util.spec_from_file_location(name, HERE / filename)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


binding = _load("qa525_vnext_trust_binding_for_bridge", "qa525-vnext-trust-binding.py")


class BoundJournal:
    def __init__(self, journal, admission_bytes, approval_bytes, *, trusted_policy_hash,
                 clock=None):
        if not (callable(getattr(journal, "begin", None))
                and callable(getattr(journal, "lease_is_held", None))):
            raise Stop("BRIDGE_JOURNAL_INVALID")
        if type(admission_bytes) is not bytes or type(approval_bytes) is not bytes:
            raise Stop("BRIDGE_BYTES_INVALID")
        self._journal = journal
        self._admission, self._approval = admission_bytes, approval_bytes
        if clock is not None and not callable(clock):
            raise Stop("BRIDGE_CLOCK_INVALID")
        self._policy = trusted_policy_hash
        self._clock = clock
        self._used = False
        self.last_cause = None
        self.last_binding = None

    def begin(self, root, admission, expected_sha256, now, checkpoint=None):
        if self._used:
            raise Stop("BRIDGE_ALREADY_USED")
        self._used = True  # even a failed attempt is final for this object
        if self._clock is not None:
            # Read the time NOW, after the caller already holds the locks: the
            # ``now`` it passed was read before waiting for them.
            try:
                now = self._clock()
            except Exception:
                raise Stop("BRIDGE_CLOCK_FAILED") from None
        try:
            result = binding.bind(self._admission, self._approval,
                                  trusted_policy_hash=self._policy, now=now)
        except Exception as exc:
            code = str(exc) if type(exc) is binding.Stop else None
            self.last_cause = code if code and code.startswith("BINDING_") else None
            raise Stop("BRIDGE_BINDING_REJECTED") from None
        if expected_sha256 != result["admission_sha256"]:
            raise Stop("BRIDGE_DIGEST_MISMATCH")
        self.last_binding = result
        try:
            return self._journal.begin(root, admission, result["admission_sha256"],
                                       now, checkpoint)
        except Exception:
            raise Stop("BRIDGE_JOURNAL_BEGIN_DENIED") from None

    def lease_is_held(self, lease):
        return self._journal.lease_is_held(lease)


if __name__ == "__main__":
    raise SystemExit("QA525_ADMISSION_BRIDGE_LIBRARY_ONLY_NO_EXECUTION")
