#!/usr/bin/env python3
"""QA525 schema-cut rollback replacement for the held flight. INACTIVE library; no process execution here.

After EF104 the BASELINE (old) API must never run again. `run_held(rollback_factory=make_rollback_factory(stop_api),
forbid_baseline_api=True)` replaces the baseline `switch_all` rollback with `hold_stopped`: it only stops the API through an
injected, reviewed `stop_api()` (e.g. docker stop of the plan-pinned container id) and starts NOTHING. The flight's own
`ROLLED_BACK` observation compares with the baseline and therefore (correctly) cannot confirm it, so every failure after
APP_STARTED ends as a flight stop with a recovery marker => APP_PARTIAL_STOP. Recovery from there is a G6 restore.
Fixed codes only; nothing from the plan is executed.
"""


class Stop(Exception):
    """Fixed code only."""


def make_rollback_factory(stop_api):
    if not callable(stop_api):
        raise Stop("SCHEMA_HOLD_INPUT_INVALID")

    def factory(*, plan, runner):
        if type(plan) is not dict or not callable(runner):
            raise Stop("SCHEMA_HOLD_INPUT_INVALID")

        def hold_stopped():
            try:
                done = stop_api()
            except Exception:  # noqa: BLE001
                raise Stop("SCHEMA_HOLD_STOP_FAILED") from None
            if done is not True:
                raise Stop("SCHEMA_HOLD_STOP_FAILED")
        return hold_stopped
    return factory


if __name__ == "__main__":
    raise SystemExit("QA525_SCHEMA_HOLD_STOPPED_LIBRARY_ONLY_NO_GO")
