#!/usr/bin/env python3
"""Build the canonical `g6-operation.json` and its sha256 pin for ONE G6 command.

The launcher (qa525-vnext-root-launcher.py) accepts a G6_V2_ONLY GO only with a root-owned
`g6-operation.json` whose bytes are exactly the canonical JSON of
{"schema": "qa525-g6-operation-v1", "g6_command": <command>, "argv": [<options>]} and whose
sha256 equals the GO's pinned `g6_operation_sha256`. This tool validates the options with the
SAME closed grammar the G6 runner uses (`qa525-vnext-g6-v2-run.py: parse`), then prints the
canonical bytes to a file chosen by the caller and the pin to stdout.

Pure: no network, no Docker, no root, no host files read; it only writes the output path given.
No secrets exist in these options (hashes, ids, names only). Fixed error codes, never echoing input.
"""
import argparse
import hashlib
import importlib.util
import json
import os
import stat
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
SCHEMA = "qa525-g6-operation-v1"
COMMANDS = ("preflight", "init", "backup", "restore", "review")
MAX_ARGV = 24
MAX_ARG = 256


class Stop(Exception):
    """Fixed code only."""


def _runner():
    spec = importlib.util.spec_from_file_location("g6_run_grammar", HERE / "qa525-vnext-g6-v2-run.py")
    mod = importlib.util.module_from_spec(spec)
    try:
        spec.loader.exec_module(mod)
    except Exception:
        raise Stop("G6OP_RUNNER_UNAVAILABLE") from None
    if not callable(getattr(mod, "parse", None)):
        raise Stop("G6OP_RUNNER_UNAVAILABLE")
    return mod


def build(command, options, runner=None):
    """options: ordered list of (name, value) pairs, e.g. [("restic-sha256", "ab..")]. Returns (bytes, sha256 hex)."""
    if command not in COMMANDS:
        raise Stop("G6OP_COMMAND_INVALID")
    if type(options) is not list or any(type(p) is not tuple or len(p) != 2 for p in options):
        raise Stop("G6OP_OPTIONS_INVALID")
    argv = []
    for name, value in options:
        if type(name) is not str or type(value) is not str:
            raise Stop("G6OP_OPTIONS_INVALID")
        argv += ["--" + name, value]
    if len(argv) > MAX_ARGV or any(len(a) > MAX_ARG for a in argv):
        raise Stop("G6OP_OPTIONS_INVALID")
    runner = runner or _runner()
    try:
        parsed, _ = runner.parse([command, *argv])
    except Exception:
        raise Stop("G6OP_OPTIONS_INVALID") from None
    if parsed != command:
        raise Stop("G6OP_OPTIONS_INVALID")
    doc = {"schema": SCHEMA, "g6_command": command, "argv": argv}
    raw = json.dumps(doc, sort_keys=True, separators=(",", ":"), ensure_ascii=True).encode("ascii")
    return raw, hashlib.sha256(raw).hexdigest()


def write_private(raw, path):
    """Create `path` (must not exist) with mode 0600; never follows links.

    The whole payload is written (write-all loop, short writes are retried) and fsynced. On ANY
    failure the partial file is removed, but only if the path still names the very inode this call
    created (device+inode compared before unlink), so a replaced path is never deleted.
    """
    try:
        fd = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, 0o600)
    except FileExistsError:
        raise Stop("G6OP_OUTPUT_EXISTS") from None
    except OSError:
        raise Stop("G6OP_OUTPUT_FAILED") from None
    ok = False
    try:
        st = os.fstat(fd)
        if not stat.S_ISREG(st.st_mode):
            raise Stop("G6OP_OUTPUT_FAILED")
        view = memoryview(raw)
        while view:
            n = os.write(fd, view)
            if type(n) is not int or n <= 0:
                raise Stop("G6OP_OUTPUT_FAILED")
            view = view[n:]
        os.fsync(fd)
        ok = True
    except Stop:
        raise
    except OSError:
        raise Stop("G6OP_OUTPUT_FAILED") from None
    finally:
        os.close(fd)
        if not ok:
            try:
                cur = os.lstat(path)
                if (cur.st_dev, cur.st_ino) == (st.st_dev, st.st_ino) and stat.S_ISREG(cur.st_mode):
                    os.unlink(path)
            except (OSError, UnboundLocalError):
                pass


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("command", choices=COMMANDS)
    parser.add_argument("--out", required=True, help="new file to create (mode 0600)")
    parser.add_argument("option", nargs="*", help="NAME=VALUE pairs without the leading --, e.g. repo-id=<hex64>")
    args = parser.parse_args(argv)
    try:
        pairs = []
        for item in args.option:
            name, sep, value = item.partition("=")
            if not sep:
                raise Stop("G6OP_OPTIONS_INVALID")
            pairs.append((name, value))
        raw, digest = build(args.command, pairs)
        write_private(raw, args.out)
    except Stop as e:
        print("ERROR " + str(e), file=sys.stderr)
        return 2
    print("g6_command=%s" % args.command)
    print("g6_operation_sha256=%s" % digest)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
