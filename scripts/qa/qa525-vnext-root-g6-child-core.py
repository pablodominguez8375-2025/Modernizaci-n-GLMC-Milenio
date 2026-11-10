#!/usr/bin/env python3
"""QA525 G6 child bridge. INACTIVE; verified PRE-4 loading only, never CLI."""
import os
import re
import stat

RUN = "qa525-vnext-g6-v2-run.py"
COMMANDS = frozenset(("preflight", "init", "backup", "restore", "review"))
HEX = re.compile(r"[0-9a-f]{64}\Z")
SOURCE = re.compile(r"[0-9a-f]{40}\Z")
SHA = re.compile(r"sha256:[0-9a-f]{64}\Z")


class Stop(Exception):
    pass


def build(*, command, argv, g6_pins, verified, verified_loader, runner):
    if (type(command) is not str or command not in COMMANDS
            or type(argv) is not list or not argv or any(type(x) is not str for x in argv)
            or type(g6_pins) is not dict or set(g6_pins) != {"g6_command", "g6_operation_sha256"}
            or g6_pins["g6_command"] != command or type(g6_pins["g6_operation_sha256"]) is not str
            or SHA.fullmatch(g6_pins["g6_operation_sha256"]) is None
            or verified is None or not callable(verified_loader)):
        raise Stop("G6CH_BINDING_INVALID")
    record = getattr(verified, "record", None)
    files = record.get("files") if type(record) is dict else None
    siblings = getattr(runner, "SIBLINGS", None)
    if (type(files) is not dict or type(siblings) is not dict
            or RUN not in files or any(name not in files for name in siblings.values())
            or getattr(runner, "ENTRYPOINT", None) != RUN
            or not callable(getattr(runner, "main", None))
            or not callable(getattr(runner, "parse", None))):
        raise Stop("G6CH_MODULE_UNVERIFIED")
    try:
        for name in (RUN, *siblings.values()):
            verified_loader(verified, name)
        parsed, _ = runner.parse(list(argv))
    except Exception:
        raise Stop("G6CH_MODULE_UNVERIFIED") from None
    if parsed != command:
        raise Stop("G6CH_BINDING_INVALID")
    args = tuple(argv)

    def act(*, claims, fd):
        if (type(claims) is not dict
                or set(claims) != {"purpose", "nonce", "app_source_sha", "app_archive_sha256", "g6_command"}
                or claims["purpose"] != "G6_V2_ONLY" or claims["g6_command"] != command
                or type(claims["nonce"]) is not str or HEX.fullmatch(claims["nonce"]) is None
                or type(claims["app_source_sha"]) is not str or SOURCE.fullmatch(claims["app_source_sha"]) is None
                or type(claims["app_archive_sha256"]) is not str
                or SHA.fullmatch(claims["app_archive_sha256"]) is None):
            raise Stop("G6CH_GO_CLAIMS_INVALID")
        try:
            if not stat.S_ISREG(os.fstat(fd).st_mode):
                raise Stop("G6CH_FD_INVALID")
            result = runner.main(list(args), verified=verified, load_module=verified_loader)
        except Stop:
            raise
        except BaseException:
            raise Stop("G6CH_RUN_FAILED") from None
        if type(result) is not int or result != 0:
            raise Stop("G6CH_RUN_FAILED")
        return "G6_%s_DONE" % command.upper()
    return act


if __name__ == "__main__":
    raise SystemExit("QA525_G6_CHILD_CORE_LIBRARY_ONLY_NO_GO")
