#!/usr/bin/env python3
"""Off-host rehearsal of the PRE-4 kit against the REAL signed 8e851bc3 release asset.

Run as root in a disposable container (NEVER on glmdch):  python3 rehearse.py OUTER_ZIP
Uses a throwaway ed25519 key + placeholder admission/app-subject/paths, so it proves the
PROCEDURE (config -> manifest -> independent verify -> stage -> install -> bootstrap check)
and its refusals, not any authority. Prints one PASS/FAIL line per step.
"""
import json
import os
import pty
import shutil
import subprocess
import sys

KIT = os.path.dirname(os.path.abspath(__file__))
W = "/tmp/pre4-rehearsal"
PAR = "/opt/pre4-rehearsal/qa525-root-source"
SRC = "8e851bc3d2f1615a94009591f0781f88d6d3fe6f"
ASSET_SHA = "f0a018c81150b096d13b048424baa9d3a59b0d9a2e38a3ecb5a8a49284270176"
PY = os.path.realpath(sys.executable)
UNPRIV = ["setpriv", "--reuid=65534", "--regid=65534", "--clear-groups", "env", "HOME=/tmp"]
results = []


def sh(cmd, **kw):
    return subprocess.run(cmd, capture_output=True, text=True, **kw)


def step(name, ok, detail=""):
    results.append(ok)
    print("%s  %s %s" % ("PASS" if ok else "FAIL", name, detail))


def sha(p):
    return sh(["sha256sum", os.path.realpath(p)]).stdout.split()[0]


ROOTDIR = "/root/qa525-pre4-reh"


def run_install(staging, parent, phrase, pin, script=None):
    """Drive pre4_install.py through a pty (it demands a TTY and a hidden PIN)."""
    pid, fd = pty.fork()
    if pid == 0:
        os.execv(PY, [PY, "-I", "-S", script or ROOTDIR + "/pre4_install.py", staging, "--parent", parent])
    out, sent = b"", 0
    while True:
        try:
            chunk = os.read(fd, 4096)
        except OSError:
            break
        if not chunk:
            break
        out += chunk
        if b"> " in out and sent == 0:
            os.write(fd, (phrase + "\n").encode()); sent = 1
        elif b"PIN (hidden)" in out and sent == 1:
            os.write(fd, (pin + "\n").encode()); sent = 2
    _, status = os.waitpid(pid, 0)
    return os.waitstatus_to_exitcode(status), out.decode(errors="replace")


def run_check(dest, pin):
    """pre4_check.py as an unprivileged user through a pty; returns (code, output, argv-at-prompt)."""
    pid, fd = pty.fork()
    if pid == 0:
        os.setgid(65534); os.setgroups([]); os.setuid(65534)
        os.execve(PY, [PY, "-I", "-S", W + "/k/pre4_check.py", dest], {"HOME": "/tmp"})
    out, argv, sent = b"", b"", False
    while True:
        try:
            chunk = os.read(fd, 4096)
        except OSError:
            break
        if not chunk:
            break
        out += chunk
        if b"PIN (hidden)" in out and not sent:
            argv = open("/proc/%d/cmdline" % pid, "rb").read()
            os.write(fd, (pin + "\n").encode()); sent = True
    _, status = os.waitpid(pid, 0)
    return os.waitstatus_to_exitcode(status), out.decode(errors="replace"), argv.decode(errors="replace")


def main():
    asset = sys.argv[1]
    shutil.rmtree(W, ignore_errors=True)
    shutil.rmtree("/opt/pre4-rehearsal", ignore_errors=True)
    os.makedirs(W + "/k"); os.makedirs(W + "/u")
    os.makedirs(PAR, mode=0o755)
    os.chmod("/opt/pre4-rehearsal", 0o755)
    for f in os.listdir(KIT):
        if f.endswith(".py") or f.endswith(".json"):
            shutil.copy(os.path.join(KIT, f), W + "/k/" + f)
    shutil.copy(asset, W + "/u/outer.zip")
    # rehearsal copy of the kit pins ITS OWN throwaway ops key (production kit pins the real fingerprint)
    sh(["ssh-keygen", "-q", "-t", "ed25519", "-N", "", "-f", W + "/u/key", "-C", "t"])
    pub = " ".join(open(W + "/u/key.pub").read().split()[:2])
    fp = sh(["ssh-keygen", "-lf", W + "/u/key.pub"]).stdout.split()[1]
    src = open(W + "/k/pre4_kit.py").read().replace("SHA256:DN64Bjj/7SMjP7LX9Nwrcp3lPRBpT2cKPs1NmaL1Y5k", fp)
    open(W + "/k/pre4_kit_reh.py", "w").write(src)
    cfg = json.load(open(W + "/k/launch-config.template.json"))
    # fill authority placeholders with throwaway-but-shape-valid values
    cfg["pins"]["admission_sha256"] = "sha256:" + "5" * 64
    cfg["app_subject_sha256"] = "sha256:" + "6" * 64
    cfg["allowed_signers"] = 'qa525-go namespaces="qa525-go-cut-v1@pmgm" %s\n' % pub
    cfg["go_fingerprint"] = fp
    cfg["authenticity"]["ops_allowed_signers"] = 'qa525-ops namespaces="qa525-ops-v2@pmgm" %s\n' % pub
    cfg["authenticity"]["app_archive_path"] = "/opt/centenario/qa525-signed-stage/%s/inner.zip" % SRC
    cfg.update(inbox="/var/lib/pmgm-qa525/inbox-reh/in", temp_root="/var/lib/pmgm-qa525/reh/tmp",
               socket_path="/run/pmgm-qa525-reh/s.sock", journal_root="/var/lib/pmgm-qa525/reh/journal",
               lease_dir="/var/lib/pmgm-qa525/reh/lease", peer_uid=1000)
    cfg["authenticity"].update(drop_uid=65534, drop_gid=65534)
    json.dump(cfg, open(W + "/u/launch-config.json", "w"), indent=1)
    sh(["chown", "-R", "65534:65534", W + "/u"]); sh(["chmod", "-R", "a+rX", W]); os.chmod(W, 0o755)

    def U(*a):
        return sh(UNPRIV + [PY, "-I"] + list(a))

    r = U(W + "/k/pre4_kit.py", "check-config", W + "/k/launch-config.template.json")
    step("template has OPEN placeholders and is refused", r.returncode == 3 and "CONFIG_OPEN" in r.stdout, r.stdout.count("OPEN ") and "(%d open lines)" % r.stdout.count("  OPEN "))
    r = U(W + "/k/pre4_kit.py", "check-config", W + "/u/launch-config.json")
    step("production kit refuses a config whose OPS key is not the pinned fingerprint", r.returncode == 3 and "OPS_SIGNERS_KEY_NOT_THE_PINNED_FINGERPRINT" in r.stdout)
    r = U(W + "/k/pre4_kit_reh.py", "check-config", W + "/u/launch-config.json")
    step("rehearsal kit: config 17 keys OK (shape + public facts + key cross-checks)", r.returncode == 0 and "CONFIG_OK keys=17" in r.stdout, r.stdout.strip().splitlines()[-1][:60])
    bad = json.loads(json.dumps(cfg)); bad["launcher_pins"]["release_asset_sha256"] = "sha256:" + "0" * 64
    json.dump(bad, open(W + "/u/bad.json", "w")); sh(["chown", "65534", W + "/u/bad.json"])
    r = U(W + "/k/pre4_kit_reh.py", "check-config", W + "/u/bad.json")
    step("wrong public fact (release asset) -> MISMATCH refused", r.returncode == 3 and "MISMATCH" in r.stdout)

    import time
    now = int(time.time())
    adm = {"schema": "qa525-launcher-admission-v1", "purpose": "LAUNCHER_ONLY", "source_sha": SRC,
           "release_asset_sha256": cfg["launcher_pins"]["release_asset_sha256"], "app_archive_sha256": cfg["pins"]["app_archive_sha256"],
           "plan_sha256": cfg["pins"]["plan_sha256"], "ops_v3_sha256": cfg["ops_v3_sha256"], "issuer": "PO-rehearsal", "issued_at": now, "expires_at": now + 43200}
    def wadm(name, d):
        open(W + "/u/" + name, "wb").write((json.dumps(d, sort_keys=True, separators=(",", ":")) + "\n").encode()); sh(["chown", "65534", W + "/u/" + name])
    wadm("adm.json", adm)
    r = U(W + "/k/pre4_kit.py", "check-admission", W + "/u/adm.json")
    step("proposed admission schema: canonical, fields match the cut, 12h validity", r.returncode == 0 and "ADMISSION_SHAPE_OK VALID_NOW" in r.stdout)
    wadm("adm2.json", dict(adm, source_sha="0" * 40)); r = U(W + "/k/pre4_kit.py", "check-admission", W + "/u/adm2.json")
    step("admission for another source_sha -> STOP", r.returncode == 3 and "ADMISSION_FIELD_MISMATCH:source_sha" in r.stdout)
    wadm("adm3.json", dict(adm, expires_at=now + 600)); r = U(W + "/k/pre4_kit.py", "check-admission", W + "/u/adm3.json")
    step("admission with <900 s validity -> STOP (would expire before manifest/PIN/install)", r.returncode == 3 and "VALIDITY_OUT_OF_RANGE" in r.stdout)

    H = lambda p: sha(p)
    gh = os.path.realpath(shutil.which("gh"))
    build = [W + "/k/qa525-vnext-installer-manifest-build.py", "--asset", W + "/u/outer.zip", "--asset-sha256", ASSET_SHA,
             "--source-sha", SRC, "--config", W + "/u/launch-config.json", "--interpreter", "%s=%s" % (PY, H(PY)),
             "--helper", "gh=%s=%s" % (gh, H(gh)), "--helper", "ssh-keygen=/usr/bin/ssh-keygen=%s" % H("/usr/bin/ssh-keygen")]
    r = U(*(build + ["--output", W + "/u/manifest-launcher.json"]))
    cand = r.stdout.split("candidate_manifest_sha256=")[-1].strip()
    step("generator builds manifest (48 files, AUTHORITY=NONE)", r.returncode == 0 and "files=48" in r.stdout and len(cand) == 64, cand[:16])
    r2 = U(*(build + ["--output", W + "/u/manifest-2.json"]))
    step("deterministic: second build is byte-identical", open(W + "/u/manifest-launcher.json", "rb").read() == open(W + "/u/manifest-2.json", "rb").read())
    r = U(W + "/k/pre4_kit.py", "verify-manifest", W + "/u/manifest-launcher.json", W + "/u/launch-config.json", W + "/u/outer.zip", ASSET_SHA)
    step("independent verifier recomputes every file hash and the pin", r.returncode == 0 and cand in r.stdout, "pin=" + cand[:16])
    tam = json.load(open(W + "/u/manifest-launcher.json"))
    k0 = sorted(tam["files"])[0]; tam["files"][k0] = "0" * 64
    open(W + "/u/tam.json", "w").write(json.dumps(tam, sort_keys=True, separators=(",", ":"))); sh(["chown", "65534", W + "/u/tam.json"])
    r = U(W + "/k/pre4_kit.py", "verify-manifest", W + "/u/tam.json", W + "/u/launch-config.json", W + "/u/outer.zip", ASSET_SHA)
    step("tampered manifest hash -> STOP", r.returncode == 3 and "MISMATCH" in r.stdout)

    r = U(W + "/k/pre4_kit.py", "stage", W + "/u/outer.zip", ASSET_SHA, W + "/u/manifest-launcher.json", W + "/u/launch-config.json", W + "/u/stage")
    step("stage extracts 48 modules + bootstrap + config + manifest (51)", r.returncode == 0 and "STAGE_OK files=51" in r.stdout)

    code, out = run_install(W + "/u/stage", PAR, "AUTORIZO INSTALL_DIR " + SRC, cand, script=W + "/k/pre4_install.py")
    step("installer run from a user-writable path as root -> STOP before reading staging", code == 3 and "STOP" in out and ("ANCESTOR_NOT_ROOT_OWNED" in out or "INSTALLER_NOT_ROOT_OWNED_COPY" in out) and "PIN (hidden)" not in out and not os.path.exists(PAR + "/" + SRC))
    # PRE-ROOT GATE (README step 8a): root copies the installer, then sha256sum -c against the hash typed from the independent device
    shutil.rmtree(ROOTDIR, ignore_errors=True)
    inst_hash = sha(W + "/k/pre4_install.py")
    gate = lambda h: sh(["sh", "-c", 'install -d -m 0700 -o root -g root %s && install -m 0600 -o root -g root -T %s %s/pre4_install.py && echo "%s  %s/pre4_install.py" | sha256sum -c -' % (ROOTDIR, W + "/u/kitcopy/pre4_install.py", ROOTDIR, h, ROOTDIR)])
    os.makedirs(W + "/u/kitcopy"); shutil.copy(W + "/k/pre4_install.py", W + "/u/kitcopy/pre4_install.py")
    open(W + "/u/kitcopy/pre4_install.py", "a").write("\nopen('/tmp/CANARY','w').write('pwned')\n")  # user swaps in a malicious installer
    r = gate(inst_hash)
    step("pre-root gate: tampered installer fails sha256sum -c, so it is never executed", r.returncode != 0 and not os.path.exists("/tmp/CANARY"))
    shutil.copy(W + "/k/pre4_install.py", W + "/u/kitcopy/pre4_install.py")
    r = gate(inst_hash)
    step("pre-root gate: genuine installer copy passes sha256sum -c (root-owned 0600 copy)", r.returncode == 0 and oct(os.stat(ROOTDIR + "/pre4_install.py").st_mode & 0o777) == "0o600", "sha=" + inst_hash[:16])
    code, out = run_install(W + "/u/stage", PAR, "AUTORIZO INSTALL_DIR " + SRC, "0" * 64)
    step("wrong PIN -> STOP, nothing created", code == 3 and "PIN_MISMATCH" in out and not os.path.exists(PAR + "/" + SRC))
    code, out = run_install(W + "/u/stage", PAR, "si", cand)
    step("wrong authorization phrase -> STOP, nothing created", code == 3 and "AUTHORIZATION_PHRASE_MISMATCH" in out and not os.path.exists(PAR + "/" + SRC))
    open(W + "/u/stage/extra.txt", "w").write("x")
    code, out = run_install(W + "/u/stage", PAR, "AUTORIZO INSTALL_DIR " + SRC, cand)
    step("extra file in staging -> STOP", code == 3 and "STAGING_FILE_SET_MISMATCH" in out and not os.path.exists(PAR + "/" + SRC))
    os.unlink(W + "/u/stage/extra.txt")
    victim = W + "/u/stage/" + k0
    saved = open(victim, "rb").read(); os.unlink(victim); os.symlink("/etc/passwd", victim)
    code, out = run_install(W + "/u/stage", PAR, "AUTORIZO INSTALL_DIR " + SRC, cand)
    step("symlink swapped into staging -> STOP (no copy of /etc/passwd)", code == 3 and "STAGED_FILE_NOT_REGULAR" in out and not os.path.exists(PAR + "/" + SRC))
    os.unlink(victim); open(victim, "wb").write(saved); os.chown(victim, 65534, 65534); os.chmod(victim, 0o600)
    open(victim, "ab").write(b"#")
    code, out = run_install(W + "/u/stage", PAR, "AUTORIZO INSTALL_DIR " + SRC, cand)
    step("modified module in staging -> STOP", code == 3 and "STAGED_HASH_MISMATCH" in out and not os.path.exists(PAR + "/" + SRC))
    open(victim, "wb").write(saved)

    code, out = run_install(W + "/u/stage", PAR, "AUTORIZO INSTALL_DIR " + SRC, cand)
    step("install: INSTALL_DIR_OK, 51 files root:root", code == 0 and "INSTALL_DIR_OK" in out and len(os.listdir(PAR + "/" + SRC)) == 51, out.strip().splitlines()[-2][:70] if out.strip() else "")
    code, out = run_install(W + "/u/stage", PAR, "AUTORIZO INSTALL_DIR " + SRC, cand)
    step("second install on existing dir -> STOP (no replace)", code == 3 and "INSTALL_DIR_ALREADY_EXISTS" in out)

    d = PAR + "/" + SRC
    code, out, argv = run_check(d, cand)
    step("pre4_check (hidden PIN) => BOOT_CHECK_OK files=48", code == 0 and "BOOT_CHECK_OK" in out and "files=48" in out, out.strip().splitlines()[-1][:80])
    step("PIN never in argv (process cmdline at the prompt)", cand not in argv and argv.count("\0") <= 5, argv.replace("\0", " ")[:80])
    code, out, argv = run_check(d, "0" * 64)
    step("pre4_check with a wrong PIN is refused", code != 0 and "BOOT_CHECK_OK" not in out)
    st = os.stat(d + "/qa525-vnext-root-bootstrap.py")
    step("installed files root:root 0644, dir 0755", st.st_uid == 0 and oct(st.st_mode & 0o777) == "0o644" and oct(os.stat(d).st_mode & 0o777) == "0o755")
    print("\nRESULT %s (%d/%d)" % ("PASS" if all(results) else "FAIL", sum(results), len(results)))
    return 0 if all(results) else 1


if __name__ == "__main__":
    sys.exit(main())
