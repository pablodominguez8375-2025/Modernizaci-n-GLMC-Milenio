#!/usr/bin/env python3
"""QA525 PRE-4 rootless one-command runner for cut 8e851bc3 (AUTHORITY=NONE, no GO, no root).

  python3 -I pre4_run.py --work /home/soporte/pre4-work --outer outer.zip \
      --admission acta.json --admission-sig acta.json.sig --pub qa525-ops.pub \
      --python-sha <64hex> --gh-sha <64hex> --ssh-keygen-sha <64hex>

Runs, in order, and STOPS at the first failure (nothing later runs):
  0 kit integrity vs SHA256SUMS (same directory)      4 manifest generator (48 files, host pins)
  1 check-admission (shape + VALID_NOW)               5 independent verify-manifest (own code)
  2 fill-config (verifies the admission signature)    6 stage (51 files)
  3 check-config (CONFIG_OK keys=17)
Creates only NEW files under --work (0700). Never runs as root, never touches the network, never
signs, never emits a GO, never deletes anything. The printed PIN candidate is NOT authenticated:
the Sponsor must recompute steps 4-5 on an independent device B and compare the 64 hex by eye
before fixing it in Bitwarden (README step 5).
"""
import argparse
import hashlib
import os
import re
import subprocess
import sys

HERE = os.path.dirname(os.path.realpath(__file__))
ASSET_SHA = "f0a018c81150b096d13b048424baa9d3a59b0d9a2e38a3ecb5a8a49284270176"
SOURCE = "8e851bc3d2f1615a94009591f0781f88d6d3fe6f"
# Public prefix/suffix of the physical host pins (README step 3); the full digest comes from the
# independent source via argv and must agree with these anchors.
ANCHORS = {"python": ("52e0a13e", "30cb41"), "gh": ("7469124f", "4c9b"), "ssh-keygen": ("2dd83441", "d051")}
HEX = re.compile(r"[0-9a-f]{64}\Z")


class Stop(Exception):
    pass


def sha_file(p):
    h = hashlib.sha256()
    with open(p, "rb") as f:
        for c in iter(lambda: f.read(1 << 20), b""):
            h.update(c)
    return h.hexdigest()


def run(step, argv):
    p = subprocess.run([sys.executable, "-I"] + argv, capture_output=True, text=True, stdin=subprocess.DEVNULL)
    out = (p.stdout + p.stderr).strip()
    print("[%s] %s" % (step, out.splitlines()[-1] if out else "(no output)"))
    if p.returncode != 0:
        print(out)
        raise Stop("STEP_FAILED:" + step)
    return out


def main():
    ap = argparse.ArgumentParser(prog="pre4_run")
    ap.add_argument("--work", required=True, help="NEW absolute dir (created 0700) for config/manifest/stage")
    ap.add_argument("--outer", required=True)
    ap.add_argument("--admission", required=True)
    ap.add_argument("--admission-sig", required=True)
    ap.add_argument("--pub", required=True)
    ap.add_argument("--template", default=os.path.join(HERE, "launch-config.template.json"))
    ap.add_argument("--python-sha", required=True)
    ap.add_argument("--gh-sha", required=True)
    ap.add_argument("--ssh-keygen-sha", required=True)
    ap.add_argument("--python-path", default="/usr/bin/python3.14")
    ap.add_argument("--gh-path", default="/usr/bin/gh")
    ap.add_argument("--ssh-keygen-path", default="/usr/bin/ssh-keygen")
    ap.add_argument("--drop-uid", default=None)
    ap.add_argument("--drop-gid", default=None)
    ap.add_argument("--no-anchor-check", action="store_true", help="off-host rehearsal only")
    a = ap.parse_args()
    if os.geteuid() == 0:
        raise Stop("MUST_NOT_RUN_AS_ROOT")
    for p in (a.work, a.outer, a.admission, a.admission_sig, a.pub, a.template):
        if not os.path.isabs(p):
            raise Stop("PATH_NOT_ABSOLUTE:" + p)
    if os.path.lexists(a.work):
        raise Stop("WORK_DIR_ALREADY_EXISTS")
    for k, v in (("python", a.python_sha), ("gh", a.gh_sha), ("ssh-keygen", a.ssh_keygen_sha)):
        if not HEX.match(v):
            raise Stop("PIN_NOT_64_HEX:" + k)
        if not a.no_anchor_check and not (v.startswith(ANCHORS[k][0]) and v.endswith(ANCHORS[k][1])):
            raise Stop("PIN_DOES_NOT_MATCH_PUBLISHED_ANCHOR:" + k)

    # 0. kit integrity (SHA256SUMS lists the kit files; it is NOT the trust anchor - the anchor is
    #    the commit SHA of the PR plus the hash fixed on the independent device)
    sums = os.path.join(HERE, "SHA256SUMS")
    listed = 0
    for line in open(sums).read().splitlines():
        h, _, name = line.partition("  ")
        if os.path.sep in name or not name:
            raise Stop("SHA256SUMS_BAD_LINE")
        if sha_file(os.path.join(HERE, name)) != h:
            raise Stop("KIT_FILE_HASH_MISMATCH:" + name)
        listed += 1
    if "pre4_run.py" not in open(sums).read():
        raise Stop("RUNNER_NOT_IN_SHA256SUMS")
    print("[0 kit] KIT_INTEGRITY_OK files=%d pre4_run_sha256=%s" % (listed, sha_file(os.path.realpath(__file__))))
    if sha_file(a.outer) != ASSET_SHA:
        raise Stop("OUTER_ASSET_SHA_MISMATCH")
    print("[0 asset] OUTER_ASSET_OK sha256=%s" % ASSET_SHA)

    kit = os.path.join(HERE, "pre4_kit.py")
    gen = os.path.join(HERE, "qa525-vnext-installer-manifest-build.py")
    os.mkdir(a.work, 0o700)
    cfg = os.path.join(a.work, "launch-config.json")
    man = os.path.join(a.work, "manifest-launcher.json")
    stage = os.path.join(a.work, "pre4-stage")

    run("1 check-admission", [kit, "check-admission", a.admission])
    fill = [kit, "fill-config", a.template, cfg, "--pub", a.pub, "--admission", a.admission,
            "--admission-sig", a.admission_sig]
    if a.drop_uid is not None:
        fill += ["--drop-uid", a.drop_uid, "--drop-gid", a.drop_gid or a.drop_uid]
    run("2 fill-config", fill)
    out = run("3 check-config", [kit, "check-config", cfg])
    if "CONFIG_OK keys=17" not in out:
        raise Stop("CONFIG_NOT_OK")
    out = run("4 gen-manifest", [gen, "--asset", a.outer, "--asset-sha256", ASSET_SHA, "--source-sha", SOURCE,
                                 "--config", cfg, "--interpreter", "%s=%s" % (a.python_path, a.python_sha),
                                 "--helper", "gh=%s=%s" % (a.gh_path, a.gh_sha),
                                 "--helper", "ssh-keygen=%s=%s" % (a.ssh_keygen_path, a.ssh_keygen_sha),
                                 "--output", man])
    if "files=48" not in out:
        raise Stop("MANIFEST_NOT_48_FILES")
    out = run("5 verify-manifest", [kit, "verify-manifest", man, cfg, a.outer, ASSET_SHA])
    m = re.search(r"MANIFEST_SHA256=([0-9a-f]{64})", out)
    if "MANIFEST_INDEPENDENT_OK" not in out or not m:
        raise Stop("VERIFY_MANIFEST_NOT_OK")
    pin = m.group(1)
    run("6 stage", [kit, "stage", a.outer, ASSET_SHA, man, cfg, stage])
    print("PRE4_RUN_OK AUTHORITY=NONE GO=NOT_EXECUTED ROOT=NOT_USED")
    print("config_sha256=%s" % sha_file(cfg))
    print("PIN_CANDIDATE=%s" % pin)
    print("NEXT: (1) recompute steps 4-5 on independent device B and compare the 64 hex by eye; "
          "(2) Sponsor fixes the PIN in Bitwarden; (3) pre-root gate 8a with hashes typed from device A; "
          "(4) re-run check-admission right before every privileged step. stage dir: %s" % stage)


if __name__ == "__main__":
    try:
        main()
    except Stop as e:
        print("STOP " + str(e))
        sys.exit(3)
    except (OSError, ValueError) as e:
        print("STOP RUN_ERROR:" + type(e).__name__)
        sys.exit(3)
