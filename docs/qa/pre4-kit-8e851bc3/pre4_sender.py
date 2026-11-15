#!/usr/bin/env python3
"""QA525 LAUNCHER_ONLY sender (rootless, run as soporte, AUTHORITY=NONE, NEVER a GO).

  python3 -I pre4_sender.py --outer outer.zip --inner inner.zip --go go.json --socket /run/pmgm-qa525/launcher-8e851bc3.sock

Waits (up to --wait s) for the launcher's socket, then sends ONE sealed memfd holding the pinned APP inner ZIP
via SCM_RIGHTS with the frame the ROOT BOOT RECEIVER expects (QA525-BND-V1:<nonce>:<sha-hex>, MAGIC read from the
boundary-recv module of the sha-pinned release; v2.9 wrongly used the unrelated fd-handoff FD-V1 frame). nonce = the GO's nonce, sha = the GO's app_archive_sha256 (both read from go.json; the
receiver compares them to ITS OWN independently verified values, so this file grants nothing).
"""
import argparse
import array
import fcntl
import hashlib
import importlib.util
import json
import os
import socket
import sys
import time

HERE = os.path.dirname(os.path.realpath(__file__))
ASSET = "f0a018c81150b096d13b048424baa9d3a59b0d9a2e38a3ecb5a8a49284270176"
INNER = "9dbd8d5bec1aa86c07664d1c78137b8798ecfdbd49972f9de531d26f0a0207a5"
SOCKET = "/run/pmgm-qa525/launcher-8e851bc3.sock"


class Stop(Exception):
    pass


for _n, _v in (("F_SEAL_SEAL", 1), ("F_SEAL_SHRINK", 2), ("F_SEAL_GROW", 4), ("F_SEAL_WRITE", 8)):
    if not hasattr(fcntl, _n):  # Linux ABI values; python3.14 already defines them (no-op on the host)
        setattr(fcntl, _n, _v)
for _n, _v in (("F_ADD_SEALS", 1033), ("F_GET_SEALS", 1034)):
    if not hasattr(fcntl, _n):
        setattr(fcntl, _n, _v)


def main():
    ap = argparse.ArgumentParser(prog="pre4_sender")
    ap.add_argument("--outer", required=True)
    ap.add_argument("--inner", required=True)
    ap.add_argument("--go", required=True)
    ap.add_argument("--socket", default=SOCKET)
    ap.add_argument("--wait", type=int, default=240)
    a = ap.parse_args()
    if os.geteuid() == 0:
        raise Stop("MUST_NOT_RUN_AS_ROOT")
    spec = importlib.util.spec_from_file_location("pre4_kit", os.path.join(HERE, "pre4_kit.py"))
    k = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(k)
    code = k._member(k._inner(a.outer, ASSET), "qa525-vnext-root-boundary-recv.py")
    ns = {"__name__": "qa525_boundary_recv"}
    exec(compile(code, "qa525-vnext-root-boundary-recv.py", "exec"), ns)
    magic = ns["MAGIC"]
    if magic != b"QA525-BND-V1:":
        raise Stop("UNEXPECTED_BOUNDARY_MAGIC")
    go = json.load(open(a.go))
    nonce = go["nonce"]
    if go["app_archive_sha256"] != "sha256:" + INNER:
        raise Stop("GO_APP_ARCHIVE_MISMATCH")
    data = open(a.inner, "rb").read()
    if hashlib.sha256(data).hexdigest() != INNER:
        raise Stop("INNER_SHA_MISMATCH")
    fd = os.memfd_create("qa525-app", os.MFD_ALLOW_SEALING)
    os.write(fd, data)
    fcntl.fcntl(fd, fcntl.F_ADD_SEALS, fcntl.F_SEAL_WRITE | fcntl.F_SEAL_SHRINK | fcntl.F_SEAL_GROW | fcntl.F_SEAL_SEAL)
    end = time.time() + a.wait
    while True:
        s = socket.socket(socket.AF_UNIX, socket.SOCK_SEQPACKET)
        try:
            s.connect(a.socket)
            break
        except (FileNotFoundError, ConnectionRefusedError, PermissionError):
            s.close()
            if time.time() > end:
                raise Stop("SOCKET_NOT_AVAILABLE")
            time.sleep(0.25)
    try:
        if len(nonce) < 24 or any(c not in "0123456789abcdefABCDEF" for c in nonce):
            raise Stop("GO_NONCE_FORMAT")
        frame = magic + nonce.encode() + b":" + INNER.encode()
        s.sendmsg([frame], [(socket.SOL_SOCKET, socket.SCM_RIGHTS, array.array("i", [fd]))])
    finally:
        s.close()
    print("SENDER_SENT nonce=%s sha256=%s AUTHORITY=NONE" % (nonce, INNER))


if __name__ == "__main__":
    try:
        main()
    except Stop as e:
        print("STOP " + str(e))
        sys.exit(3)
    except Exception as e:  # noqa: BLE001
        print("STOP SENDER_ERROR:" + type(e).__name__ + ":" + str(e)[:80])
        sys.exit(3)
