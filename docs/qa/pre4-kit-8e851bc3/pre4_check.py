#!/usr/bin/env python3
"""QA525 PRE-4 post-install check WITHOUT exposing the PIN (INACTIVE, AUTHORITY=NONE).

  python3 -I -S pre4_check.py INSTALL_DIR     (run as soporte, no sudo; PIN typed hidden on a TTY)

The bootstrap's own `check` takes the PIN in argv (visible in ps/history). This wrapper runs the
SAME gate (`verify_install` + `materialize_config`, from the bootstrap bytes pinned below) but reads
the PIN with a hidden prompt. It executes no verified module and grants nothing. Not a GO.
"""
import getpass
import hashlib
import os
import sys
import types

BOOTSTRAP = "qa525-vnext-root-bootstrap.py"
BOOTSTRAP_SHA256 = "40f201d9d84fdfcf72630090d8f591dc0f7dfb204d0e17644871d6a3107017bf"
MANIFEST = "manifest-launcher.json"


def main(argv):
    if len(argv) != 2 or not os.path.isabs(argv[1]) or not sys.stdin.isatty():
        print("PRE4_CHECK_USAGE (absolute INSTALL_DIR, TTY required)")
        return 2
    if sys.flags.isolated != 1 or sys.flags.no_site != 1:
        print("PRE4_CHECK_NOT_ISOLATED (use python3 -I -S)")
        return 2
    dest = argv[1]
    try:  # (Stop is the bootstrap's own exception class: matched by name below)
        with open(os.path.join(dest, BOOTSTRAP), "rb") as f:
            code = f.read(1 << 22)
        if hashlib.sha256(code).hexdigest() != BOOTSTRAP_SHA256:
            print("PRE4_CHECK_BOOTSTRAP_HASH_MISMATCH")
            return 1
        mod = types.ModuleType("qa525_bootstrap_pinned")
        exec(compile(code, BOOTSTRAP, "exec"), mod.__dict__)
        with open(os.path.join(dest, MANIFEST), "rb") as f:
            manifest = f.read(1 << 20)
        pin = getpass.getpass("PIN (hidden): ").strip()
        verified = mod.verify_install(dest, manifest, pin, uid=0)
        mod.materialize_config(verified)
        print("BOOT_CHECK_OK manifest_pin=%s files=%d config=%s" % (
            verified.record["manifest_pin"], len(verified.record["files"]), verified.record["config"]["sha256"]))
        return 0
    except Exception as exc:  # fixed codes only; never echo anything else
        if type(exc).__name__ == "Stop":
            print(str(exc))
        else:
            print("PRE4_CHECK_IO_ERROR")
    return 1


if __name__ == "__main__":
    sys.exit(main(sys.argv))
