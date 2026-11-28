#!/usr/bin/env python3
"""QA525 G6 BACKUP gate (READ-ONLY, host files + systemd; no Docker, no restic, no writes).

Run as root on glmdch (`sudo python3 -I qa525-g6-backup-gate-ro.py --repo-id H --key-epoch H --restic-sha256 H`)
BEFORE any G6 backup watchdog or GO.  It never opens key-v2 for reading and never prints secrets.

Checks (each printed as one `G6BG_*` line; any failure => STOP, exit 1):
  1. key-v2 / repo-v2 / state-v2 / identity.json: type, owner root, exact mode, nlink; key-v2 size 40 only;
     identity.json canonical with schema/restic_repo_id/key_epoch equal to the pins from argv.
  2. /usr/bin/restic: regular, root, mode, nlink 1, sha256 == pin.
  3. Silo source: real directory (no symlink), dev:ino, owner and mode (reported), file count and exact byte size
     (regular files only), free space on the G6 state filesystem >= 3 x size + 2 GiB.
  4. Previous watchdog (`pmgm-g6-minio-restart.timer`/`.service`): state reported read-only; STOP if loaded.
"""
import hashlib
import json
import os
import re
import shutil
import stat
import subprocess
import sys

STATE_ROOT = "/var/lib/pmgm-qa525-g6-v2"
RESTIC = "/usr/bin/restic"
SILO_SOURCE = "/var/lib/docker/volumes/centenario-qa-lab2_pmgm_srv01_minio_data/_data"
SYSTEMCTL = "/usr/bin/systemctl"
WATCHDOG_UNITS = ("pmgm-g6-minio-restart.timer", "pmgm-g6-minio-restart.service")
SCHEMA_IDENTITY = "qa525-g6-v2-identity"
HEX64 = re.compile(r"[a-f0-9]{64}\Z")
EPOCH = re.compile(r"[a-f0-9]{32}\Z")
KEY_SIZE = 40
MAX_FILES = 2_000_000
GIB = 1024 ** 3


class Stop(Exception):
    pass


def _lstat(path):
    try:
        return os.lstat(path)
    except OSError:
        raise Stop("G6BG_MISSING") from None


def _meta(path, kind, mode, nlink=None):
    info = _lstat(path)
    ok = (stat.S_ISREG(info.st_mode) if kind == "file" else stat.S_ISDIR(info.st_mode))
    if not ok or info.st_uid != 0 or stat.S_IMODE(info.st_mode) != mode:
        raise Stop("G6BG_META_MISMATCH")
    if nlink is not None and info.st_nlink != nlink:
        raise Stop("G6BG_META_MISMATCH")
    return info


def _read_regular(path, limit):
    fd = os.open(path, os.O_RDONLY | os.O_NOFOLLOW | os.O_CLOEXEC)
    try:
        info = os.fstat(fd)
        if not stat.S_ISREG(info.st_mode) or info.st_size > limit:
            raise Stop("G6BG_META_MISMATCH")
        data = b""
        while len(data) < info.st_size:
            block = os.read(fd, info.st_size - len(data))
            if not block:
                break
            data += block
        return data
    finally:
        os.close(fd)


def _sha256_regular(path):
    fd = os.open(path, os.O_RDONLY | os.O_NOFOLLOW | os.O_CLOEXEC)
    try:
        if not stat.S_ISREG(os.fstat(fd).st_mode):
            raise Stop("G6BG_META_MISMATCH")
        digest = hashlib.sha256()
        while True:
            block = os.read(fd, 1 << 20)
            if not block:
                return digest.hexdigest()
            digest.update(block)
    finally:
        os.close(fd)


def parse(argv):
    names = {"--repo-id": HEX64, "--key-epoch": EPOCH, "--restic-sha256": HEX64}
    if len(argv) != 6:
        raise Stop("G6BG_USAGE")
    opts = {}
    for flag, value in zip(argv[0::2], argv[1::2]):
        if flag not in names or flag[2:] in opts or names[flag].fullmatch(value) is None:
            raise Stop("G6BG_USAGE")
        opts[flag[2:]] = value
    return opts


def check_state(state_root, repo_id, key_epoch, out):
    _meta(state_root, "dir", 0o700)
    key = _meta(os.path.join(state_root, "key-v2"), "file", 0o600, 1)
    if key.st_size != KEY_SIZE:
        raise Stop("G6BG_KEY_SIZE")
    _meta(os.path.join(state_root, "repo-v2"), "dir", 0o700)
    _meta(os.path.join(state_root, "state-v2"), "dir", 0o700)
    ident = os.path.join(state_root, "state-v2", "identity.json")
    _meta(ident, "file", 0o600, 1)
    raw = _read_regular(ident, 4096)
    try:
        doc = json.loads(raw.decode("utf-8"))
    except (ValueError, UnicodeError):
        raise Stop("G6BG_IDENTITY_INVALID") from None
    canonical = (json.dumps(doc, sort_keys=True, separators=(",", ":"), ensure_ascii=True,
                            allow_nan=False) + "\n").encode("ascii")
    if (type(doc) is not dict or set(doc) != {"schema", "restic_repo_id", "key_epoch"}
            or doc["schema"] != SCHEMA_IDENTITY or canonical != raw):
        raise Stop("G6BG_IDENTITY_INVALID")
    if doc["restic_repo_id"] != repo_id or doc["key_epoch"] != key_epoch:
        raise Stop("G6BG_IDENTITY_MISMATCH")
    out("G6BG_STATE=OK")
    out("G6BG_IDENTITY=MATCHES_PINS")


def check_restic(path, pin, out):
    info = _lstat(path)
    if (not stat.S_ISREG(info.st_mode) or info.st_uid != 0 or info.st_nlink != 1
            or stat.S_IMODE(info.st_mode) & 0o022):
        raise Stop("G6BG_META_MISMATCH")
    out("G6BG_RESTIC_MODE=%o" % stat.S_IMODE(info.st_mode))
    digest = _sha256_regular(path)
    out("G6BG_RESTIC_SIZE=%d" % info.st_size)
    if digest != pin:
        raise Stop("G6BG_RESTIC_HASH")
    out("G6BG_RESTIC=OK")


def check_silo(silo, state_root, out):
    info = _lstat(silo)
    if (not stat.S_ISDIR(info.st_mode) or os.path.realpath(silo) != silo or os.path.islink(silo)):
        raise Stop("G6BG_SILO_INVALID")
    out("G6BG_SILO_OWNER_MODE=%d:%o" % (info.st_uid, stat.S_IMODE(info.st_mode)))
    files = size = 0
    for base, dirs, leaves in os.walk(silo, followlinks=False):
        for name in dirs + leaves:
            entry = os.lstat(os.path.join(base, name))
            if stat.S_ISDIR(entry.st_mode):
                continue
            if not stat.S_ISREG(entry.st_mode):
                raise Stop("G6BG_SILO_INVALID")
            files += 1
            size += entry.st_size
            if files > MAX_FILES:
                raise Stop("G6BG_SILO_INVALID")
    free = shutil.disk_usage(state_root).free
    need = 3 * size + 2 * GIB
    out("G6BG_SILO_DEV_INO=%d:%d" % (info.st_dev, info.st_ino))
    out("G6BG_SILO_FILES=%d" % files)
    out("G6BG_SILO_BYTES=%d" % size)
    out("G6BG_FREE_BYTES=%d" % free)
    out("G6BG_NEEDED_BYTES=%d" % need)
    if free < need:
        raise Stop("G6BG_SPACE")
    out("G6BG_SILO=OK")


def check_watchdog(runner, out):
    for unit in WATCHDOG_UNITS:
        code, text = runner([SYSTEMCTL, "show", "--no-pager", unit,
                             "--property=LoadState,ActiveState,SubState"])
        props = dict(line.split("=", 1) for line in text.splitlines() if "=" in line)
        if code != 0 or props.get("LoadState") != "not-found" or props.get("ActiveState") != "inactive":
            out("G6BG_WATCHDOG_%s=%s/%s" % (unit.rsplit(".", 1)[1].upper(),
                                            props.get("LoadState", "?"), props.get("ActiveState", "?")))
            raise Stop("G6BG_WATCHDOG_PRESENT")
    out("G6BG_WATCHDOG=ABSENT")


def _run(argv):
    try:
        done = subprocess.run(argv, capture_output=True, timeout=15, env={"LC_ALL": "C"}, check=False)
    except (OSError, subprocess.SubprocessError):
        return 1, ""
    return done.returncode, done.stdout.decode("ascii", "replace")


def main(argv=None, deps=None):
    d = deps or {}
    argv = sys.argv[1:] if argv is None else argv
    out = d.get("out") or (lambda line: print(line, flush=True))
    try:
        if d.get("euid", os.geteuid()) != 0:
            raise Stop("G6BG_NOT_ROOT")
        opts = parse(argv)
        state = d.get("state_root", STATE_ROOT)
        check_state(state, opts["repo-id"], opts["key-epoch"], out)
        check_restic(d.get("restic", RESTIC), opts["restic-sha256"], out)
        check_silo(d.get("silo", SILO_SOURCE), state, out)
        check_watchdog(d.get("runner", _run), out)
    except Stop as exc:
        out("G6BG_STOP:%s" % exc)
        return 1
    except OSError:
        out("G6BG_STOP:G6BG_IO")
        return 1
    out("G6BG_GATE=PASS")
    return 0


if __name__ == "__main__":
    sys.exit(main())
