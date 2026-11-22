#!/usr/bin/env python3
"""QA525 schema-cut sequencer (phases 0-8). Pure library; NO signature, root, lock, nonce or host authority.

BOUNDARY: everything of phase 0 that can still leave the GO unspent (PRE-4, signatures, pins, expiry, T0/plan,
`check_go_plan`, absence of an incompatible journal) is done by the CALLER before it takes the five historical locks
+ lease and SPENDS THE NONCE. `run()` is only invoked afterwards, so from the first line of `run()` the GO is
already CONSUMED: a failure in `preflight` or in the `RESERVED` write leaves nothing (or nothing usable) journalled
and the GO can never be reused; a new GO is required. This module only sequences reviewed steps and applies the
journal discipline of `qa525-vnext-schema-cut-journal-contract.py`:

* every phase is made durable BEFORE the effect it announces (`journal(phase)` must fsync and return a record
  carrying `failure_class_if_interrupted`, exactly what `schema-cut-journal-store.append` returns);
* the plane is verified before and after every journal write and every effect;
* the first journal write that raises makes the journal AMBIGUOUS: nothing more is ever written (a partial event
  is never removed or retried) and the caller gets `SCHEMA_EXEC_JOURNAL_AMBIGUOUS`;
* any other failure after RESERVED persists, once, the terminal class the contract itself reported for the last
  durable phase (so the terminal is never guessed here); a lost plane writes nothing;
* nothing is retried, reverted or "healed": no EF Down, no container rollback claims;
* diagnostics are fixed codes; collaborator output, argv, paths and exception text are never echoed.

Collaborators are injected callables so the module performs no I/O of its own:
  preflight()          read-only re-check under the held plane, before RESERVED; failure = STOP with nothing
                       journalled, nonce ALREADY SPENT (see BOUNDARY)
  verify_backup()      G6 v2 evidence for THIS cut (hash + isolated restore); after RESERVED
  quiesce_writes()     API refuses writes, zero write sessions on the target DB; after BACKUP_VERIFIED
  schema(journal)      phases 4-6 (e.g. adapter #1074 `run_phases`), receives the sequencer's journal wrapper and
                       must journal MIGRATION_STARTED, SCHEMA_104, GRANTS_VERIFIED, POSTGATE_PASS itself (it
                       persists PARTIAL_SCHEMA_STOP on its own failure; the sequencer never duplicates a terminal)
  revalidate()         read-only re-check of images/pins/G6 after POSTGATE_PASS, BEFORE APP_STARTED
  apply_api_web()      the reviewed API/WEB flight; must return {"terminal": "APPLIED"} to continue
  uat()                authenticated smoke + Talleres + carga inicial on synthetic data; must return True
"""
import re

PHASES_OK = ("RESERVED", "BACKUP_VERIFIED", "WRITES_QUIESCED", "MIGRATION_STARTED", "SCHEMA_104",
             "GRANTS_VERIFIED", "POSTGATE_PASS", "APP_STARTED", "APP_APPLIED", "UAT_PASS")
TERMINAL = frozenset({"PRE_DDL_STOP", "PARTIAL_SCHEMA_STOP", "APP_PARTIAL_STOP", "UAT_STOP"})
STEPS = frozenset({"preflight", "verify_backup", "quiesce_writes", "schema", "revalidate", "apply_api_web", "uat"})
CAUSE = re.compile(r"SCHEMA_ADAPTER_[A-Z0-9_]{1,48}\Z")
SCHEMA_SEQUENCE = ("MIGRATION_STARTED", "SCHEMA_104", "GRANTS_VERIFIED", "POSTGATE_PASS")


class Stop(Exception):
    """Fixed code only. `.cause` is, at most, an allowlisted fixed adapter code."""

    def __init__(self, code, cause=None):
        super().__init__(code)
        self.cause = cause
        self.original = None  # fixed earlier code when a terminal write itself failed


class _StepError(Exception):
    """Internal wrapper: ANY exception raised by an injected step (even our own `Stop`) is collaborator output, so it
    can never be mistaken for a sequencer diagnostic (plane lost / journal ambiguous) nor skip `fail()`."""

    def __init__(self, inner):
        super().__init__("step")
        self.inner = inner


class _State:
    def __init__(self):
        self.last = None          # last durable phase
        self.interrupt = None     # failure class the contract reported for it
        self.broken = False       # a journal write raised: nothing more is ever written
        self.lost = False         # the lock plane was lost: nothing more is ever written
        self.phases = []

    @property
    def terminal(self):
        return self.last in TERMINAL or self.last == "UAT_PASS"


def _cause(error):
    text = error.args[0] if getattr(error, "args", None) and type(error.args[0]) is str else None
    return text if text is not None and CAUSE.fullmatch(text) else None


def run(journal, plane, steps):
    """Run phases 0-8. Returns a bounded, non-authoritative summary or raises `Stop`."""
    if (not callable(journal) or not callable(getattr(plane, "assert_held", None)) or type(steps) is not dict
            or set(steps) != STEPS or any(not callable(steps[k]) for k in STEPS)):
        raise Stop("SCHEMA_EXEC_INPUT_INVALID")
    state = _State()

    def _held(held_plane):
        try:
            held_plane.assert_held()
        except Exception:  # noqa: BLE001
            state.lost = True
            raise Stop("SCHEMA_EXEC_PLANE_LOST") from None

    def write(phase):
        if state.broken:
            raise Stop("SCHEMA_EXEC_JOURNAL_AMBIGUOUS")
        _held(plane)  # outside the journal call: the only error here is a lost plane
        try:
            record = journal(phase)
        except Exception:  # noqa: BLE001  # ANY error of the journal call (even a Stop of ours) is ambiguity
            state.broken = True
            raise Stop("SCHEMA_EXEC_JOURNAL_AMBIGUOUS") from None
        if (type(record) is not dict or record.get("phase") != phase
                or type(record.get("failure_class_if_interrupted")) is not str):
            state.broken = True
            raise Stop("SCHEMA_EXEC_JOURNAL_AMBIGUOUS")
        state.last, state.interrupt = phase, record["failure_class_if_interrupted"]
        state.phases.append(phase)
        try:
            _held(plane)
        except Stop:
            raise
        return record

    def effect(name, *args):
        _held(plane)  # outside the step call: the only error here is a lost plane
        try:
            result = steps[name](*args)
        except Exception as inner:  # noqa: BLE001
            raise _StepError(inner) from None
        _held(plane)
        return result

    def fail(code, error=None):
        """Persist the contract's own terminal class once, unless the journal is ambiguous or already terminal."""
        if state.lost:
            code, error = "SCHEMA_EXEC_PLANE_LOST", None
        elif state.broken:
            code, error = "SCHEMA_EXEC_JOURNAL_AMBIGUOUS", None
        if (not state.lost and not state.broken and state.last is not None and not state.terminal
                and state.interrupt in TERMINAL):
            try:
                write(state.interrupt)
            except Stop as terminal_failure:
                # The terminal could not be made durable: report THAT (ambiguity or lost plane), not the earlier
                # failure; the original fixed code is kept only as `.original`. Nothing is retried.
                terminal_failure.original = code
                raise terminal_failure from None
        error = getattr(error, "inner", error)
        raise Stop(code, _cause(error) if error is not None else None) from None

    try:
        # Read-only re-check under the held plane. The nonce is already spent by the caller (BOUNDARY): a failure
        # here writes nothing, but the GO is consumed and must not be reused.
        try:
            if effect("preflight") is not None:
                raise Stop("SCHEMA_EXEC_PREFLIGHT_FAILED")
        except Stop:
            raise
        except Exception:  # noqa: BLE001
            raise Stop("SCHEMA_EXEC_PREFLIGHT_FAILED") from None
        write("RESERVED")
        try:
            if effect("verify_backup") is not True:
                fail("SCHEMA_EXEC_BACKUP_FAILED")
        except Stop:
            raise
        except Exception as error:  # noqa: BLE001
            fail("SCHEMA_EXEC_BACKUP_FAILED", error)
        write("BACKUP_VERIFIED")
        try:
            if effect("quiesce_writes") is not True:
                fail("SCHEMA_EXEC_QUIESCE_FAILED")
        except Stop:
            raise
        except Exception as error:  # noqa: BLE001
            fail("SCHEMA_EXEC_QUIESCE_FAILED", error)
        write("WRITES_QUIESCED")
        # Phases 4-6: the callee journals its own phases through our wrapper (so ambiguity is tracked here).
        try:
            effect("schema", write)
        except Stop:
            raise
        except Exception as error:  # noqa: BLE001
            fail("SCHEMA_EXEC_SCHEMA_FAILED", error)
        if tuple(state.phases[3:]) != SCHEMA_SEQUENCE:
            fail("SCHEMA_EXEC_SCHEMA_INCOMPLETE")
        # Phase 7: read-only revalidation, then the durable START boundary, then the only API/WEB effect.
        try:
            if effect("revalidate") is not True:
                fail("SCHEMA_EXEC_REVALIDATION_FAILED")
        except Stop:
            raise
        except Exception as error:  # noqa: BLE001
            fail("SCHEMA_EXEC_REVALIDATION_FAILED", error)
        write("APP_STARTED")
        try:
            result = effect("apply_api_web")
        except Stop:
            raise
        except Exception as error:  # noqa: BLE001
            fail("SCHEMA_EXEC_APP_FAILED", error)
        if type(result) is not dict or result.get("terminal") != "APPLIED":
            fail("SCHEMA_EXEC_APP_NOT_APPLIED")
        write("APP_APPLIED")
        # Phase 8
        try:
            if effect("uat") is not True:
                fail("SCHEMA_EXEC_UAT_FAILED")
        except Stop:
            raise
        except Exception as error:  # noqa: BLE001
            fail("SCHEMA_EXEC_UAT_FAILED", error)
        write("UAT_PASS")
    except Stop:
        raise
    except Exception:  # noqa: BLE001
        fail("SCHEMA_EXEC_UNEXPECTED")
    return {"status": "QA525_SCHEMA_CUT_SEQUENCE_COMPLETE_NO_AUTHORITY", "phases": tuple(state.phases),
            "may_apply": False}


if __name__ == "__main__":
    raise SystemExit("QA525_SCHEMA_EXECUTOR_LIBRARY_ONLY_NO_GO")
