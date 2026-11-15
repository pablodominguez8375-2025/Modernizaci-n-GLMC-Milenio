#!/usr/bin/env python3
"""QA525 PRE-4 INSTALL_DIR creator for cut 8e851bc3 (INACTIVE until a human runs it as root).

  sudo python3 -I -S pre4_install.py STAGING_DIR [--parent /opt/centenario/qa525-root-source]

Does exactly one thing: create NEW <parent>/<SHA40>/ (root:root, 0755 at the end) holding the
51 files produced by `pre4_kit.py stage`, after verifying them against the manifest whose
sha256 the Sponsor types as PIN (from Bitwarden; never argv/env/file/pipe). Each staged file is
read ONCE (O_NOFOLLOW, regular, bounded), hashed, and the SAME bytes are written - no
check/copy gap. No network, no GO, no signature, no service start, no deletion of anything.
On any STOP the partially written directory is left (0700, root) for review: nothing is removed.
"""
import getpass
import hashlib
import json
import os
import re
import stat
import sys

SOURCE = "8e851bc3d2f1615a94009591f0781f88d6d3fe6f"
BOOTSTRAP = "qa525-vnext-root-bootstrap.py"
BOOTSTRAP_SHA256 = "40f201d9d84fdfcf72630090d8f591dc0f7dfb204d0e17644871d6a3107017bf"
MANIFEST_NAME = "manifest-launcher.json"
DEFAULT_PARENT = "/opt/centenario/qa525-root-source"
NAME = re.compile(r"[A-Za-z0-9][A-Za-z0-9._-]{0,63}\Z")
PHRASE = "AUTORIZO INSTALL_DIR " + SOURCE


class Stop(Exception):
    pass


def sha(b):
    return hashlib.sha256(b).hexdigest()


def read_regular(path, limit):
    try:
        fd = os.open(path, os.O_RDONLY | os.O_NOFOLLOW | os.O_NONBLOCK)
    except OSError:
        raise Stop("STAGED_FILE_NOT_REGULAR_SINGLE_LINK_OR_TOO_BIG") from None
    try:
        st = os.fstat(fd)
        if not stat.S_ISREG(st.st_mode) or st.st_nlink != 1 or st.st_size > limit:
            raise Stop("STAGED_FILE_NOT_REGULAR_SINGLE_LINK_OR_TOO_BIG")
        data = b""
        while len(data) <= st.st_size:
            chunk = os.read(fd, 1 << 20)
            if not chunk:
                break
            data += chunk
        if len(data) != st.st_size or os.fstat(fd).st_size != st.st_size:
            raise Stop("STAGED_FILE_CHANGED")
        return data
    finally:
        os.close(fd)


def safe_ancestors(path):
    cur = "/"
    for part in [""] + [p for p in path.split("/") if p]:
        cur = os.path.join(cur, part) if part else "/"
        st = os.lstat(cur)
        if stat.S_ISLNK(st.st_mode) or not stat.S_ISDIR(st.st_mode) or st.st_uid != 0 or st.st_mode & 0o022:
            raise Stop("ANCESTOR_NOT_ROOT_OWNED_PLAIN_DIR:" + cur)


def main(argv):
    if os.geteuid() != 0:
        raise Stop("MUST_RUN_AS_ROOT")
    args = argv[1:]
    parent = DEFAULT_PARENT
    if "--parent" in args:
        i = args.index("--parent")
        parent = args[i + 1]
        args = args[:i] + args[i + 2:]
    if len(args) != 1 or not os.path.isabs(args[0]) or not os.path.isabs(parent) or os.path.normpath(parent) != parent:
        raise Stop("USAGE")
    staging = args[0]
    if not (sys.stdin.isatty() and sys.stdout.isatty()):
        raise Stop("TTY_REQUIRED")
    safe_ancestors(parent)
    dest = os.path.join(parent, SOURCE)
    if os.path.lexists(dest):
        raise Stop("INSTALL_DIR_ALREADY_EXISTS")

    mraw = read_regular(os.path.join(staging, MANIFEST_NAME), 1 << 20)
    m = json.loads(mraw)
    if mraw != json.dumps(m, sort_keys=True, separators=(",", ":")).encode():
        raise Stop("MANIFEST_NOT_CANONICAL")
    if set(m) != {"config", "files", "helpers", "interpreter"} or len(m["files"]) != 48 \
            or set(m["config"]) != {"name", "sha256"} or not NAME.match(m["config"]["name"]):
        raise Stop("MANIFEST_SHAPE")
    expect = dict(m["files"])
    if BOOTSTRAP in expect or MANIFEST_NAME == m["config"]["name"]:
        raise Stop("MANIFEST_SHAPE")
    expect[BOOTSTRAP] = BOOTSTRAP_SHA256
    expect[m["config"]["name"]] = m["config"]["sha256"]
    expect[MANIFEST_NAME] = sha(mraw)
    if any(not NAME.match(n) or not re.fullmatch(r"[0-9a-f]{64}", h) for n, h in expect.items()) or len(expect) != 51:
        raise Stop("MANIFEST_NAMES_OR_HASHES")
    if sorted(os.listdir(staging)) != sorted(expect):
        raise Stop("STAGING_FILE_SET_MISMATCH")

    print("PIN candidate must equal the value you fixed in Bitwarden (sha256 of the manifest).")
    print("Type exactly: %s" % PHRASE)
    if input("> ").strip() != PHRASE:
        raise Stop("AUTHORIZATION_PHRASE_MISMATCH")
    pin = getpass.getpass("PIN (hidden): ").strip()
    if pin != sha(mraw):
        raise Stop("PIN_MISMATCH")

    data = {n: read_regular(os.path.join(staging, n), 1 << 22) for n in sorted(expect)}
    for n, b in data.items():
        if sha(b) != expect[n]:
            raise Stop("STAGED_HASH_MISMATCH:" + n)

    old = os.umask(0o077)
    try:
        os.mkdir(dest, 0o700)  # fails if it exists: no-replace by construction
        for n, b in data.items():
            fd = os.open(os.path.join(dest, n), os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, 0o644)
            try:
                os.fchown(fd, 0, 0)
                os.fchmod(fd, 0o644)
                view = memoryview(b)
                while view:
                    view = view[os.write(fd, view):]
                os.fsync(fd)
            finally:
                os.close(fd)
    finally:
        os.umask(old)

    names = sorted(os.listdir(dest))
    if names != sorted(expect):
        raise Stop("DEST_FILE_SET_MISMATCH")
    for n in names:
        st = os.lstat(os.path.join(dest, n))
        if not stat.S_ISREG(st.st_mode) or st.st_uid or st.st_gid or st.st_nlink != 1 or stat.S_IMODE(st.st_mode) != 0o644:
            raise Stop("DEST_FILE_ATTRS:" + n)
        if sha(read_regular(os.path.join(dest, n), 1 << 22)) != expect[n]:
            raise Stop("DEST_HASH_MISMATCH:" + n)
    os.chmod(dest, 0o755)
    safe_ancestors(dest)
    print("INSTALL_DIR_OK dir=%s files=51 manifest_sha256=%s AUTHORITY=NONE" % (dest, sha(mraw)))
    print("Next (as soporte, no sudo): python3 -I -S %s/%s check %s %s/%s <PIN>" % (dest, BOOTSTRAP, dest, dest, MANIFEST_NAME))


if __name__ == "__main__":
    try:
        main(sys.argv)
    except Stop as e:
        print("STOP " + str(e))
        sys.exit(3)
    except (OSError, KeyError, ValueError) as e:
        print("STOP INSTALL_ERROR:" + type(e).__name__)
        sys.exit(3)
