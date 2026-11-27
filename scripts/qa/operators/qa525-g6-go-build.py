#!/usr/bin/env python3
"""QA525 G6 GO builder (OFF-HOST, Sponsor's Termux).  Holds no key, signs nothing, touches no host, makes no network call.

Produces the exact canonical bytes of ONE signed-GO document for the route-C driver (`--g6 <command> --go ... --go-sig ...`):
five fields {command, op_sha256, nonce, expires_at, driver_sha256}, sorted keys, compact separators, ASCII.  The Sponsor then
signs THOSE bytes with his OPS key:  ssh-keygen -Y sign -f <OPS private key> -n qa525-go-g6-v1@pmgm <file>.
A GO is NOT authority by itself: the driver verifies the signature against the pinned OPS key, the command, the operation
hash, the driver hash and a TTL <= 900 s measured on the host clock; the nonce is single-use.
Usage: qa525-g6-go-build.py --command C --op-sha256 H --driver-sha256 H [--ttl 600] [--out PATH]
"""
import argparse
import json
import os
import re
import secrets
import sys
import time

COMMANDS = ("preflight", "init", "backup", "restore", "review")
NAMESPACE = "qa525-go-g6-v1@pmgm"
TTL_MAX = 900
HEX64 = re.compile(r"[a-f0-9]{64}\Z")
NONCE = re.compile(r"[a-f0-9]{32,64}\Z")


class BuildError(Exception):
    pass


def canonical(go):
    return json.dumps(go, sort_keys=True, separators=(",", ":"), ensure_ascii=True).encode("ascii")


def build(command, op_sha256, driver_sha256, now, ttl=600, nonce=None):
    nonce = secrets.token_hex(16) if nonce is None else nonce
    if (command not in COMMANDS or HEX64.fullmatch(op_sha256 or "") is None or HEX64.fullmatch(driver_sha256 or "") is None
            or NONCE.fullmatch(nonce) is None or type(ttl) is not int or isinstance(ttl, bool) or not 0 < ttl <= TTL_MAX
            or type(now) is not int or isinstance(now, bool)):
        raise BuildError("GO_BUILD_INVALID")
    return canonical({"command": command, "op_sha256": op_sha256, "nonce": nonce, "expires_at": now + ttl,
                      "driver_sha256": driver_sha256})


def main(argv=None):
    ap = argparse.ArgumentParser(allow_abbrev=False)
    ap.add_argument("--command", required=True)
    ap.add_argument("--op-sha256", required=True)
    ap.add_argument("--driver-sha256", required=True)
    ap.add_argument("--ttl", type=int, default=600)
    ap.add_argument("--out", default=None)
    a = ap.parse_args(argv)
    try:
        data = build(a.command, a.op_sha256, a.driver_sha256, int(time.time()), a.ttl)
    except BuildError as exc:
        sys.stdout.write("QA525_G6_GO=STOP %s\n" % exc)
        return 1
    go = json.loads(data)
    path = a.out or ("qa525-g6-go-%s-%s.json" % (a.command, go["nonce"][:8]))
    fd = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, 0o600)  # never overwrites
    try:
        os.write(fd, data)
        os.fsync(fd)
    finally:
        os.close(fd)
    sys.stdout.write("QA525_G6_GO_FILE=%s\nQA525_G6_GO_EXPIRES_AT=%d\nQA525_G6_GO_SIGN=ssh-keygen -Y sign -f <OPS_KEY> -n %s %s\n"
                     % (path, go["expires_at"], NAMESPACE, path))
    return 0


if __name__ == "__main__":
    sys.exit(main())
