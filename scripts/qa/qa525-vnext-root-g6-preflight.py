#!/usr/bin/env python3
"""Fixed preflight child, no authority on import; only PRE-4 verified launcher."""
COMMAND = "preflight"


def act_for(*, bridge, runner, argv, g6_pins, verified, verified_loader):
    if not callable(getattr(bridge, "build", None)):
        raise ValueError("G6CH_BRIDGE_MISSING")
    return bridge.build(command=COMMAND, runner=runner, argv=argv,
                        g6_pins=g6_pins, verified=verified,
                        verified_loader=verified_loader)


if __name__ == "__main__":
    raise SystemExit("QA525_G6_CHILD_NOT_A_COMMAND_NO_GO")
