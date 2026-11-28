#!/usr/bin/env python3
"""QA525 G6 GO VERIFIER (OFF-HOST, read-only).  Independently checks a signed GO BEFORE it is uploaded to glmdch: canonical bytes
(the driver's own g6_go_parse), the pinned OPS signer fingerprint, and the signature with `ssh-keygen -Y verify` in namespace
qa525-go-g6-v1@pmgm; prints the sha256 of go.json and go.json.sig so a second party can compare them with the hashes seen on the host.
Usage: qa525-g6-go-verify.py --go F --go-sig F [--driver PATH] [--now EPOCH] [--expect-command C --expect-op H --expect-driver H]
Writes nothing, contacts nothing; the private key is never needed."""
import argparse
import hashlib
import importlib.util
import os
import subprocess
import sys
import tempfile

HERE = os.path.dirname(os.path.abspath(__file__))


def load_driver(path):
    spec = importlib.util.spec_from_file_location("qa525_driver_for_verify", path)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


def verify(drv, go_bytes, sig_bytes, keygen="ssh-keygen", signers=None, fingerprint=None):
    """Returns the parsed GO or raises drv.Stop with the driver's own fixed code."""
    signers = drv.G6_OPS_ALLOWED_SIGNERS if signers is None else signers
    fingerprint = drv.G6_OPS_FINGERPRINT if fingerprint is None else fingerprint
    if drv._g6_signers_fingerprint(signers) != fingerprint:
        raise drv.Stop("CUT_G6_GO_SIGNER_PIN_MISMATCH")
    with tempfile.TemporaryDirectory(prefix="g6gv-") as tmp:
        os.chmod(tmp, 0o700)
        allowed, sig = os.path.join(tmp, "allowed"), os.path.join(tmp, "go.sig")
        for path, data in ((allowed, signers), (sig, sig_bytes)):
            with open(path, "wb") as fh:
                fh.write(data)
        done = subprocess.run([keygen, "-Y", "verify", "-f", allowed, "-I", drv.G6_GO_PRINCIPAL, "-n", drv.G6_GO_NAMESPACE,
                               "-s", sig], input=go_bytes, capture_output=True, timeout=30, env={})
    if done.returncode != 0 or b"Good" not in done.stdout:
        raise drv.Stop("CUT_G6_GO_SIGNATURE_INVALID")
    return drv.g6_go_parse(go_bytes)


def main(argv=None, out=sys.stdout):
    ap = argparse.ArgumentParser(allow_abbrev=False)
    ap.add_argument("--go", required=True)
    ap.add_argument("--go-sig", required=True)
    ap.add_argument("--driver", default=os.path.join(HERE, "qa525-manual-cut-e5c40d3a.py"))
    ap.add_argument("--now", type=int, default=None, help="host epoch to judge TTL against (date -u +%%s on glmdch)")
    ap.add_argument("--expect-command")
    ap.add_argument("--expect-op")
    ap.add_argument("--expect-driver")
    a = ap.parse_args(argv)
    drv = load_driver(a.driver)
    go_bytes, sig_bytes = open(a.go, "rb").read(), open(a.go_sig, "rb").read()
    try:
        go = verify(drv, go_bytes, sig_bytes)
        for key, want in (("command", a.expect_command), ("op_sha256", a.expect_op), ("driver_sha256", a.expect_driver)):
            if want is not None and go[key] != want:
                raise drv.Stop("CUT_G6_GO_MISMATCH")
        if a.now is not None and not a.now < go["expires_at"] <= a.now + drv.G6_GO_TTL_MAX:
            raise drv.Stop("CUT_G6_GO_TTL_OUT_OF_RANGE")
    except drv.Stop as exc:
        out.write("G6GV_STOP:%s\n" % exc)
        return 1
    out.write("G6GV_GO_SHA256=%s\nG6GV_GOSIG_SHA256=%s\nG6GV_FINGERPRINT=%s\nG6GV_SIGNATURE=GOOD\nG6GV_FORMAT=CANONICAL\n"
              "G6GV_COMMAND=%s\nG6GV_OP_SHA256=%s\nG6GV_DRIVER_SHA256=%s\nG6GV_NONCE=%s\nG6GV_EXPIRES_AT=%d\nG6GV_VERIFY=PASS\n"
              % (hashlib.sha256(go_bytes).hexdigest(), hashlib.sha256(sig_bytes).hexdigest(), drv.G6_OPS_FINGERPRINT,
                 go["command"], go["op_sha256"], go["driver_sha256"], go["nonce"], go["expires_at"]))
    return 0


if __name__ == "__main__":
    sys.exit(main())
