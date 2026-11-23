#!/usr/bin/env python3
"""READ-ONLY authenticated check of the G6 v2 Restic repository ("repo B"). Run as root via sudo.

Proves, without printing any secret, that (1) /usr/bin/restic has the pinned sha256, (2) the repository
/var/lib/pmgm-qa525-g6-v2/repo-v2 opens with the password the Sponsor types (hidden, getpass on the
terminal, never echoed/stored/logged) and (3) the observed repo id equals the pinned repo id of repo B.
Only operation: `restic --no-cache --no-lock cat config --json` (reads config; writes nothing).
The password reaches restic through an anonymous pipe fd (`--password-file /dev/fd/N`), never argv/env/disk.
Trust boundary: this script is NOT a second root route. It refuses to run unless `--expect-script-sha256 HEX`
(the hash published in GitHub/#1082 for the exact reviewed head) equals the sha256 of its own bytes, and it should
be run from a ROOT-OWNED copy (`sudo install -m 0700 -o root -g root -t /root <file>`), never from a user-writable
path. The receipt is: script sha256 + the fixed markers below + timestamp. Integration into the signed launcher
(canonical `verify-identity` command) is the planned follow-up; this is the separate operator procedure until then.
Output: fixed marker lines only. Any mismatch/failure => STOP, never `init`, never retry loops.
"""
import argparse
import getpass
import hashlib
import json
import os
import re
import stat
import subprocess
import sys

RESTIC = "/usr/bin/restic"
RESTIC_SHA256 = "e0ba8fd58a7cb76707cbc064abb4ac72d5a91aa26041b9c4f763405684d00a41"
REPO = "/var/lib/pmgm-qa525-g6-v2/repo-v2"
REPO_ID = "b4df75c52a71050a4bbf493d25a7cd5dd5921d1fdb4ab8226eaf7b6b7ffb65ce"
HEX64 = re.compile(r"^[0-9a-f]{64}$")
TIMEOUT = 60


class Stop(Exception):
    """Fixed code only; never carries input or secrets."""


def sha256_file(path):
    h = hashlib.sha256()
    try:
        with open(path, "rb") as f:
            for chunk in iter(lambda: f.read(1 << 20), b""):
                h.update(chunk)
    except OSError:
        raise Stop("G6B_RESTIC_BINARY_UNREADABLE") from None
    return h.hexdigest()


def check_trusted_copy(path, lstat=os.lstat):
    """Root-owned 0700 regular file, single link, no symlink, in a root-owned directory not group/other writable."""
    try:
        st = lstat(path)
        dst = lstat(os.path.dirname(path))
    except OSError:
        raise Stop("G6B_SCRIPT_COPY_UNTRUSTED") from None
    if (not stat.S_ISREG(st.st_mode) or st.st_uid != 0 or stat.S_IMODE(st.st_mode) != 0o700
            or st.st_nlink != 1 or not stat.S_ISDIR(dst.st_mode) or dst.st_uid != 0
            or stat.S_IMODE(dst.st_mode) & 0o022):
        raise Stop("G6B_SCRIPT_COPY_UNTRUSTED")


def check_self(expected, sha_reader=sha256_file, path=None, lstat=os.lstat):
    if not isinstance(expected, str) or not HEX64.match(expected):
        raise Stop("G6B_SCRIPT_PIN_INVALID")
    path = path or os.path.abspath(__file__)
    check_trusted_copy(path, lstat)
    if sha_reader(path) != expected:
        raise Stop("G6B_SCRIPT_SHA_MISMATCH")


def run_restic(password, runner=None):
    """Return restic stdout bytes for `cat config --json`; the password only travels via a pipe fd."""
    if runner is not None:
        return runner(password)
    r, w = os.pipe()
    try:
        data = memoryview(password.encode("utf-8") + b"\n")
        while data:
            n = os.write(w, data)
            if n <= 0:
                raise Stop("G6B_PIPE_WRITE_FAILED")
            data = data[n:]
        os.close(w)
        w = -1
        argv = [RESTIC, "--no-cache", "--no-lock", "--repo", REPO, "--password-file", "/dev/fd/%d" % r,
                "cat", "config", "--json"]
        p = subprocess.run(argv, shell=False, stdin=subprocess.DEVNULL, capture_output=True, timeout=TIMEOUT,
                           pass_fds=(r,), env={"PATH": "/usr/bin:/bin", "LC_ALL": "C"}, check=False)
    except subprocess.TimeoutExpired:
        raise Stop("G6B_RESTIC_TIMEOUT") from None
    except OSError:
        raise Stop("G6B_RESTIC_NOT_RUNNABLE") from None
    finally:
        for fd in (r, w):
            if fd != -1:
                try:
                    os.close(fd)
                except OSError:
                    pass
    if p.returncode != 0:
        raise Stop("G6B_REPO_AUTH_OR_OPEN_FAILED")
    return p.stdout


def verify(password, sha_reader=sha256_file, runner=None):
    if not password:
        raise Stop("G6B_PASSWORD_EMPTY")
    if sha_reader(RESTIC) != RESTIC_SHA256:
        raise Stop("G6B_RESTIC_SHA_MISMATCH")
    out = run_restic(password, runner)
    try:
        doc = json.loads(out.decode("utf-8"))
        observed = doc["id"]
    except (ValueError, KeyError, TypeError, UnicodeDecodeError):
        raise Stop("G6B_CONFIG_UNPARSEABLE") from None
    if not isinstance(observed, str) or not HEX64.match(observed):
        raise Stop("G6B_CONFIG_ID_INVALID")
    if observed != REPO_ID:
        raise Stop("G6B_REPO_ID_MISMATCH")
    return observed


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--expect-script-sha256", required=True)
    a = ap.parse_args(argv)
    if os.geteuid() != 0 or not sys.stdin.isatty():
        print("G6B_REPO_VERIFY=STOP G6B_CODE=NEEDS_ROOT_AND_TTY")
        return 2
    try:
        check_self(a.expect_script_sha256)
        pw = getpass.getpass("Restic password for repo B (hidden): ")
        observed = verify(pw)
    except Stop as e:
        print("G6B_REPO_VERIFY=STOP G6B_CODE=%s" % e)
        return 1
    finally:
        pw = None
    print("G6B_SCRIPT_SHA256=PASS script_sha256=%s" % a.expect_script_sha256)
    print("G6B_RESTIC_SHA256=PASS")
    print("G6B_REPO_OPEN=PASS")
    print("G6B_REPO_ID_MATCH=PASS observed_repo_id=%s" % observed)
    print("G6B_REPO_VERIFY=PASS NO_WRITE NO_INIT NO_BACKUP")
    return 0


if __name__ == "__main__":
    sys.exit(main())
