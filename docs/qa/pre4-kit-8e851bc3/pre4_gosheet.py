#!/usr/bin/env python3
"""QA525 LAUNCHER_ONLY GO sheet builder (rootless, NO keys, NO signature, NO GO emitted).

  python3 -I pre4_gosheet.py --outer outer.zip --config launch-config.json --out NEWDIR [--valid 900]

Builds ONLY the canonical bytes of the LAUNCHER_ONLY GO the Sponsor will sign on his phone, using the
sponsor-signing module taken from the sha-pinned release zip (the same code the verifier family comes from).
Every pinned field comes from the installed launch-config (checked here against the pinned sha256);
only nonce (fresh 32 random bytes, hex64) and the <=900 s window are new. The window starts NOW, so run
this immediately before signing; the file is useless after expiry.
"""
import argparse
import hashlib
import importlib.util
import json
import os
import secrets
import sys
import time

HERE = os.path.dirname(os.path.realpath(__file__))
ASSET = "f0a018c81150b096d13b048424baa9d3a59b0d9a2e38a3ecb5a8a49284270176"
CONFIG_SHA = "7a059c56607a485b3dfaa7b8d27ac98c6767c8ed6fa69438f3de39a284729c98"
MEMBER = "scripts/qa/qa525-vnext-sponsor-signing.py"
NS = "qa525-go-cut-v1@pmgm"


class Stop(Exception):
    pass


def load_kit():
    sp = importlib.util.spec_from_file_location("pre4_kit", os.path.join(HERE, "pre4_kit.py"))
    k = importlib.util.module_from_spec(sp)
    sp.loader.exec_module(k)
    return k


def main():
    ap = argparse.ArgumentParser(prog="pre4_gosheet")
    ap.add_argument("--outer", required=True)
    ap.add_argument("--config", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--valid", type=int, default=900)
    ap.add_argument("--config-sha", default=CONFIG_SHA, help="off-host rehearsal only")
    a = ap.parse_args()
    if os.geteuid() == 0:
        raise Stop("MUST_NOT_RUN_AS_ROOT")
    if not 60 <= a.valid <= 900:
        raise Stop("VALID_OUT_OF_RANGE")
    if os.path.lexists(a.out) or not os.path.isabs(a.out):
        raise Stop("OUT_EXISTS_OR_NOT_ABSOLUTE")
    craw = open(a.config, "rb").read()
    if hashlib.sha256(craw).hexdigest() != a.config_sha:
        raise Stop("CONFIG_SHA_MISMATCH")
    cfg = json.loads(craw)
    k = load_kit()
    zi = k._inner(a.outer, ASSET)
    src = None
    for n in zi.namelist():
        if n.endswith("/" + MEMBER) or n == MEMBER:
            src = zi.read(n)
            break
    if src is None:
        raise Stop("SPONSOR_SIGNING_MODULE_MISSING")
    spec = importlib.util.spec_from_loader("qa525_sponsor_signing", loader=None)
    mod = importlib.util.module_from_spec(spec)
    exec(compile(src, "qa525-vnext-sponsor-signing.py", "exec"), mod.__dict__)
    p, lp = cfg["pins"], cfg["launcher_pins"]
    now = int(time.time())
    fmt = lambda t: time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime(t))
    raw = mod.build_go_cut(
        nonce=secrets.token_hex(32), ops_source_sha=p["ops_source_sha"], app_source_sha=p["app_source_sha"],
        app_archive_sha256=p["app_archive_sha256"], plan_sha256=p["plan_sha256"],
        admission_sha256=p["admission_sha256"], policy_hash=p["policy_hash"],
        issued_at=fmt(now), expires_at=fmt(now + a.valid), purpose="LAUNCHER_ONLY",
        release_asset_sha256=lp["release_asset_sha256"], old_launcher_sha256=lp["old_launcher_sha256"],
        candidate_operator_sha256=lp["candidate_operator_sha256"])
    card = mod.review_card(raw)
    os.mkdir(a.out, 0o700)
    fd = os.open(os.path.join(a.out, "go.json"), os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, 0o600)
    with os.fdopen(fd, "wb") as f:
        f.write(raw)
    print(card if isinstance(card, str) else json.dumps(card, indent=1, sort_keys=True))
    print("GO_SHEET_BUILT AUTHORITY=NONE NOT_SIGNED NOT_EMITTED go_sha256=%s expires_at=%s" % (
        hashlib.sha256(raw).hexdigest(), fmt(now + a.valid)))
    print("SHORT_CODE=%s  (compare on two devices before signing)" % mod.short_code(raw))
    print("SIGN (Sponsor, Termux): ssh-keygen -Y sign -f ~/.ssh/qa525-ops -n %s %s/go.json" % (NS, a.out))


if __name__ == "__main__":
    try:
        main()
    except Stop as e:
        print("STOP " + str(e))
        sys.exit(3)
    except (OSError, KeyError, ValueError) as e:
        print("STOP GOSHEET_ERROR:" + type(e).__name__)
        sys.exit(3)
