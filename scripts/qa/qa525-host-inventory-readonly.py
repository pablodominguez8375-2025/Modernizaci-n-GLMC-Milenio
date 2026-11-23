#!/usr/bin/env python3
"""One-shot READ-ONLY host inventory for the QA525 cut (glmdch).

Collects, in a single run, the facts ChatGPT/Claude need to sign the G6 chain and the cut plan:
PRE-4 launcher metadata, signed-source/recovery dirs, G6 state dir, Restic binary + repo layout
(presence only), live Docker images/containers (digests), migrator image id, relevant systemd
units/timers, host basics.

Guarantees (enforced by tests):
* Never writes anywhere except the single --out file (O_EXCL, 0600, no symlink); default is stdout.
* Only runs commands from a fixed allowlist, shell=False, constant argv, short timeout, bounded output.
  No docker run/exec/start/stop, no systemctl start/stop/restart, no restic repo access.
* Never reads file CONTENTS except to compute sha256 of regular files <= 64 MiB; files whose name
  looks secret-bearing (cred|secret|passw|token|key|jwt, plus .pat/.env/.pem/.p12/.pfx/.jks/.kdbx, id_rsa/id_ed25519, case-insensitive) are stat-only, never hashed.
* Never touches Keycloak/API/network; lifespan is NOT collected here (F2-check owns that).
* Output has no secrets: stat metadata, hashes, names, Docker ids. Unreadable => {"status":"denied"}.
Run as the normal operator; run with sudo only if the Sponsor authorizes it, to read root-only paths.
"""
import argparse
import hashlib
import json
import os
import re
import stat
import subprocess
import sys
import time

SCHEMA = "qa525-host-inventory-v1"
HASH_MAX = 64 * 1024 * 1024
OUT_MAX = 1024 * 1024
TIMEOUT = 20
SECRETISH = re.compile(
    r"(cred|secret|passw|token|key|jwt|htpasswd|id_rsa|id_ed25519|id_ecdsa|\.kdbx$"
    r"|(^|\.)(pat|env|pem|p12|pfx|jks|keystore|pgpass|netrc)(\.|$))", re.I)
PROJECT = "centenario-qa-lab2"
MIGRATOR_TAG = "workshops104-migrator:16262d47"
HEX64 = re.compile(r"^[0-9a-f]{64}$")

# (label, path, mode) mode: "file" => stat + sha256; "dir" => stat + sorted entry names (+stat of each)
PATHS = (
    ("launcher_pre4", "/usr/local/sbin/pmgm-qa-update", "file"),
    ("launcher_recovery_dir", "/opt/centenario/qa525-launcher-recovery", "dir"),
    ("root_source_dir", "/opt/centenario/qa525-root-source", "dir"),
    ("etc_pmgm_dir", "/etc/pmgm", "dir"),
    ("g6_state_dir", "/var/lib/pmgm-qa525-g6-v2", "dir"),
    ("uat_mint_script", "/home/soporte/qa525-uat-credential-mint.py", "file"),
    ("restic_bin", "/usr/bin/restic", "file"),
    ("restic_bin_local", "/usr/local/bin/restic", "file"),
    ("docker_bin", "/usr/bin/docker", "file"),
)

# Fixed allowlist. Each value is the full constant argv. Nothing here mutates state.
COMMANDS = {
    "docker_version": ["/usr/bin/docker", "version", "--format", "{{.Server.Version}}"],
    "docker_ps": ["/usr/bin/docker", "ps", "-a", "--filter",
                  "label=com.docker.compose.project=" + PROJECT, "--no-trunc",
                  "--format", "{{.Names}}|{{.Image}}|{{.ID}}|{{.State}}|{{.Status}}"],
    "docker_images": ["/usr/bin/docker", "image", "ls", "--digests", "--no-trunc",
                      "--format", "{{.Repository}}:{{.Tag}}|{{.ID}}|{{.Digest}}|{{.CreatedAt}}"],
    "migrator_image_id": ["/usr/bin/docker", "image", "inspect", "--format", "{{.Id}}", MIGRATOR_TAG],
    "pg_networks": ["/usr/bin/docker", "inspect", "--format",
                    "{{range $k,$v := .NetworkSettings.Networks}}{{$k}}|{{$v.Aliases}}{{println}}{{end}}",
                    "centenario-qa-lab2-postgres-1"],
    "networks": ["/usr/bin/docker", "network", "ls", "--format", "{{.Name}}|{{.Driver}}"],
    "systemd_timers": ["/usr/bin/systemctl", "list-timers", "--all", "--no-pager", "--no-legend"],
    "unit_api_refresh": ["/usr/bin/systemctl", "show", "pmgm-qa-api-refresh-v2.service", "--no-pager",
                         "-p", "LoadState,ActiveState,SubState,UnitFileState,FragmentPath,Result"],
    "unit_g6_watchdog": ["/usr/bin/systemctl", "show", "pmgm-g6-minio-restart.timer", "--no-pager",
                         "-p", "LoadState,ActiveState,SubState,UnitFileState,FragmentPath"],
    "restic_version": ["/usr/bin/restic", "version"],
    "df_root": ["/usr/bin/df", "-P", "-B1", "/", "/var/lib/docker"],
    "uname": ["/usr/bin/uname", "-sr"],
}
FORBIDDEN_WORDS = {"run", "exec", "start", "stop", "restart", "kill", "rm", "rmi", "prune", "pull",
                   "push", "build", "create", "enable", "disable", "mask", "daemon-reload", "snapshots",
                   "backup", "restore", "init", "check", "unlock", "forget"}


def run_cmd(key, runner=None):
    argv = COMMANDS[key]
    if runner is not None:
        return runner(argv)
    try:
        p = subprocess.run(argv, shell=False, stdin=subprocess.DEVNULL, capture_output=True, timeout=TIMEOUT,
                           env={"PATH": "/usr/bin:/bin", "LC_ALL": "C"}, check=False)
    except FileNotFoundError:
        return 127, ""
    except subprocess.TimeoutExpired:
        return 124, ""
    return p.returncode, p.stdout[:OUT_MAX].decode("utf-8", "replace")


def _stat(path):
    try:
        st = os.lstat(path)
    except FileNotFoundError:
        return {"status": "absent"}
    except PermissionError:
        return {"status": "denied"}
    except OSError as e:
        return {"status": "error", "errno": e.errno}
    kind = ("symlink" if stat.S_ISLNK(st.st_mode) else "dir" if stat.S_ISDIR(st.st_mode)
            else "file" if stat.S_ISREG(st.st_mode) else "other")
    return {"status": "ok", "type": kind, "uid": st.st_uid, "gid": st.st_gid,
            "mode": "%04o" % stat.S_IMODE(st.st_mode), "size": st.st_size, "nlink": st.st_nlink,
            "mtime_utc": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime(st.st_mtime))}


def _sha256(path):
    flags = os.O_RDONLY | getattr(os, "O_NOFOLLOW", 0) | getattr(os, "O_NONBLOCK", 0)
    try:
        fd = os.open(path, flags)
    except OSError as e:
        return None, ("denied" if isinstance(e, PermissionError) else "unhashed")
    try:
        st = os.fstat(fd)
        if not stat.S_ISREG(st.st_mode) or st.st_size > HASH_MAX:
            return None, "unhashed"
        h = hashlib.sha256()
        with os.fdopen(fd, "rb", closefd=False) as f:
            for chunk in iter(lambda: f.read(1 << 20), b""):
                h.update(chunk)
        return h.hexdigest(), None
    finally:
        os.close(fd)


def probe_path(path, mode):
    info = _stat(path)
    if info.get("status") != "ok":
        return info
    if mode == "file" and info["type"] == "file":
        if SECRETISH.search(os.path.basename(path)):
            info["sha256"] = None
            info["hash_note"] = "secret-looking name: stat only"
        else:
            digest, note = _sha256(path)
            info["sha256"] = digest
            if note:
                info["hash_note"] = note
    elif mode == "dir" and info["type"] == "dir":
        try:
            names = sorted(os.listdir(path))
        except PermissionError:
            info["entries"] = "denied"
            return info
        entries = []
        for n in names[:200]:
            child = os.path.join(path, n)
            e = _stat(child)
            e["name"] = n
            if e.get("type") == "file" and not SECRETISH.search(n):
                digest, _ = _sha256(child)
                e["sha256"] = digest
            entries.append(e)
        info["entries"] = entries
        info["entries_truncated"] = len(names) > 200
    return info


def _lines(out):
    return [ln for ln in out.splitlines() if ln.strip()]


def _kv(out):
    d = {}
    for ln in _lines(out):
        k, _, v = ln.partition("=")
        d[k] = v
    return d


def collect(paths=PATHS, runner=None, now=None):
    now = now or time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime())
    res = {"schema": SCHEMA, "collected_utc": now, "uid": os.geteuid(), "read_only": True}
    res["paths"] = {label: dict(path=p, **probe_path(p, mode)) for label, p, mode in paths}

    def cmd(key):
        code, out = run_cmd(key, runner)
        return code, out

    res["host"] = {}
    for k in ("uname", "df_root", "docker_version"):
        c, o = cmd(k)
        res["host"][k] = {"rc": c, "out": _lines(o)}
    c, o = cmd("docker_ps")
    res["containers"] = {"rc": c, "rows": _lines(o)}
    c, o = cmd("docker_images")
    res["images"] = {"rc": c, "rows": _lines(o)}
    c, o = cmd("migrator_image_id")
    mid = o.strip()
    res["migrator"] = {"tag": MIGRATOR_TAG, "rc": c, "id": mid if HEX64.match(mid.replace("sha256:", "")) else None,
                       "expected_pin": "sha256:a3d78cc8d4c021695d6b5b637ce1bb38b1a21ef4e8afeba68181e791764c76b3"}
    res["migrator"]["matches_expected_pin"] = res["migrator"]["id"] == res["migrator"]["expected_pin"]
    c, o = cmd("pg_networks")
    res["postgres_networks"] = {"rc": c, "rows": _lines(o),
                                "needs": "centenario-qa-lab2_pmgm_srv01_internal with alias postgres"}
    c, o = cmd("networks")
    res["networks"] = {"rc": c, "rows": [r for r in _lines(o) if PROJECT in r]}
    c, o = cmd("systemd_timers")
    res["timers"] = {"rc": c, "rows": [r for r in _lines(o) if re.search(r"pmgm|qa525|g6", r)]}
    res["units"] = {}
    for k in ("unit_api_refresh", "unit_g6_watchdog"):
        c, o = cmd(k)
        res["units"][k] = {"rc": c, "props": _kv(o)}
    c, o = cmd("restic_version")
    res["restic"] = {"rc": c, "version": _lines(o)[:1],
                     "repo_note": "repo_id/key_epoch need the repo passphrase; NOT read here. They come from the "
                                  "authorized G6 preflight/init evidence."}
    res["not_collected"] = ["keycloak_lifespan (F2-check)", "restic repo_id/key_epoch", "secrets", "api/web tokens"]
    return res


def canonical(res):
    return json.dumps(res, sort_keys=True, separators=(",", ":"), ensure_ascii=True).encode() + b"\n"


def write_new(raw, path):
    fd = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL | getattr(os, "O_NOFOLLOW", 0), 0o600)
    try:
        view = memoryview(raw)
        while view:
            n = os.write(fd, view)
            if n <= 0:
                raise OSError("short write")
            view = view[n:]
        os.fsync(fd)
    finally:
        os.close(fd)


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--out", help="new file (must not exist); default stdout")
    a = ap.parse_args(argv)
    raw = canonical(collect())
    digest = hashlib.sha256(raw).hexdigest()
    if a.out:
        try:
            write_new(raw, a.out)
        except OSError:
            print("INVENTORY_OUT_FAILED")
            return 2
        print("inventory_out=%s" % a.out)
        print("inventory_sha256=%s" % digest)
    else:
        sys.stdout.write(raw.decode())
        print("inventory_sha256=%s" % digest, file=sys.stderr)
    return 0


if __name__ == "__main__":
    sys.exit(main())
