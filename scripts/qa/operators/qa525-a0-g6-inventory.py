#!/usr/bin/env python3
"""QA525 A0-G6 physical inventory: ONE read-only, whitelist-only projection.

No writes, no network, no docker/systemctl, no subprocess, no secrets. Every value printed is a closed-form field:
stat facts (type/uid/mode/nlink/size), sha256 of small regular files (never of key/repo content), names matching a fixed
regex, and 64-hex/32-hex identifiers. Any absent or inconsistent mandatory field prints `A0G6_STOP:<CODE>` and exits 1.
Sections: PRE4 (root-source + effective launch-config), LAUNCHER (paths + g6_pins), LOCKS, G6 state tree, EVIDENCE
(receipts/reviews/restores/archives, repo/key epoch), LINK (A0 params), UAT/abort markers.
"""
import hashlib
import json
import os
import posixpath
import re
import stat
import sys

ROOT_SOURCE = "/opt/centenario/qa525-root-source"
LAUNCHER_SHA = "da274d651d8e9e056525413406c0aab62131504f"
CONFIG = ROOT_SOURCE + "/" + LAUNCHER_SHA + "/launch-config.json"
STATE_ROOT = "/var/lib/pmgm-qa525-g6-v2"
SUBDIRS = ("repo-v2", "key-v2", "state-v2", "exports-v2", "restores-v2", "downloads-v2", "stages-v2")
LOCKS = ("/opt/centenario/qa-api-refresh/operation.lock", "/opt/centenario/qa-auto-lite/web-update.lock",
         "/run/lock/pmgm-master-qa-604.lock", "/run/lock/pmgm-qa754-web-hotfix.lock",
         "/run/lock/pmgm-qa754-api-activation.lock")
MARKERS = ("/root/.qa525-uat-credential", "/run/qa525-schema-env", "/root/.qa525-schema-cut-abort")
PATH_KEYS = ("inbox", "temp_root", "socket_path", "journal_root", "lease_dir")
G6_FILES = ("qa525-vnext-g6-v2-run.py", "qa525-vnext-g6-v2-producer.py", "qa525-vnext-g6-v2-captor.py",
            "qa525-vnext-g6-v2-verifier.py")
G6_PINNED = {  # sha256 of the reviewed G6 v2 modules (same table as the manual-cut driver G6_PINS)
    "qa525-vnext-g6-v2-run.py": "3093f577930a87cfdc55d039d31260aa8b3927896540107a004ec4d15550e913",
    "qa525-vnext-g6-v2-producer.py": "08d793a957886c4abdcd9b6f791fde243906224b114906f8beb7c598a66f38e7",
    "qa525-vnext-g6-v2-captor.py": "e24cd5815370e349b2311b0ee64642b13f4d0728e7bda07485e15ccb8b1c9372",
    "qa525-vnext-g6-v2-verifier.py": "cba3cf308d62c6322d400be9df91c82048a1fee92cc119db4593473faa2515c7",
}
HEX64 = re.compile(r"[a-f0-9]{64}\Z")
EPOCH = re.compile(r"[a-f0-9]{32}\Z")
DIGEST = re.compile(r"(sha256:)?[a-f0-9]{64}\Z")
KEY = re.compile(r"[a-z0-9_]{1,40}\Z")
NAME = re.compile(r"[A-Za-z0-9._-]{1,120}\Z")
ARCHIVE = re.compile(r"centenario-qa-g6-v2-CIFRADO-\d{8}T\d{6}Z\.tar\.gz\Z")
EVID = re.compile(r"(receipt-\d{8}T\d{6}Z\.json|review-\d{8}T\d{6}Z\.json|isolated-restore-v2-\d{8}T\d{6}Z\.report\.json"
                  r"|identity\.json|qa525-a0-g6-params[A-Za-z0-9._-]{0,60})\Z")
MAX_JSON = 65536
MAX_NAMES = 64


class Stop(Exception):
    pass


def safe_path(value):
    if (type(value) is not str or not re.fullmatch(r"/[A-Za-z0-9_./-]{1,200}", value)
            or posixpath.normpath(value) != value or "//" in value):
        return False
    return all(p not in (".", "..") for p in value.split("/"))


def lstat(path):
    try:
        return os.lstat(path)
    except FileNotFoundError:
        return None
    except OSError:
        raise Stop("IO") from None


def facts(st):
    kind = "dir" if stat.S_ISDIR(st.st_mode) else "file" if stat.S_ISREG(st.st_mode) else \
        "link" if stat.S_ISLNK(st.st_mode) else "sock" if stat.S_ISSOCK(st.st_mode) else "other"
    return "%s uid=%d mode=%04o nlink=%d" % (kind, st.st_uid, stat.S_IMODE(st.st_mode), st.st_nlink)


def read_small(path, uid, cap=MAX_JSON, dir_fd=None):
    """Exact bounded read through an O_NOFOLLOW fd; root-owned regular file, nlink 1, not group/other-writable."""
    try:
        fd = os.open(path, os.O_RDONLY | os.O_NOFOLLOW | os.O_CLOEXEC | os.O_NONBLOCK, dir_fd=dir_fd)
    except OSError:
        raise Stop("OPEN_FAILED") from None
    try:
        st = os.fstat(fd)
        if (not stat.S_ISREG(st.st_mode) or st.st_uid != uid or st.st_nlink != 1 or stat.S_IMODE(st.st_mode) & 0o022):
            raise Stop("FILE_META")
        if not 0 < st.st_size <= cap:
            raise Stop("SIZE")
        left, chunks = st.st_size, []
        while left:
            c = os.read(fd, left)
            if not c:
                raise Stop("TRUNCATED")
            chunks.append(c)
            left -= len(c)
        if os.read(fd, 1):
            raise Stop("GREW")
        return b"".join(chunks)
    finally:
        os.close(fd)


def require_dir(path, uid, code):
    """Mandatory directory: present, real dir (no symlink), owned by uid, not group/other-writable."""
    st = lstat(path)
    if st is None or not stat.S_ISDIR(st.st_mode) or st.st_uid != uid or stat.S_IMODE(st.st_mode) & 0o022:
        raise Stop(code)
    return st


def safe_dir_flag(st, uid):
    return stat.S_ISDIR(st.st_mode) and st.st_uid == uid and not stat.S_IMODE(st.st_mode) & 0o022


def _open_dir(name, dir_fd=None):
    """Open a directory FD without following symlinks; None if it is not a real directory."""
    try:
        return os.open(name, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW | os.O_CLOEXEC, dir_fd=dir_fd)
    except OSError:
        return None


def _names_fd(fd):
    try:
        found = sorted(os.listdir(fd))
    except OSError:
        raise Stop("LIST_FAILED") from None
    if len(found) > MAX_NAMES:
        raise Stop("TOO_MANY_ENTRIES")
    return found


def names(path):
    try:
        found = sorted(os.listdir(path))
    except OSError:
        raise Stop("LIST_FAILED") from None
    if len(found) > MAX_NAMES:
        raise Stop("TOO_MANY_ENTRIES")
    return found


def inventory(uid=0, out=sys.stdout, config=CONFIG, root_source=ROOT_SOURCE, state_root=STATE_ROOT,
              locks=LOCKS, markers=MARKERS, pins=G6_PINNED):
    L = []
    unsafe = []
    try:
        # ---- PRE4 / root-source
        st = lstat(root_source)
        if st is None or not stat.S_ISDIR(st.st_mode) or st.st_uid != uid or stat.S_IMODE(st.st_mode) & 0o022:
            raise Stop("PRE4_ROOT_SOURCE")
        L.append("A0G6_PRE4_ROOT_SOURCE=" + facts(st))
        sources = [n for n in names(root_source) if re.fullmatch(r"[a-f0-9]{40}", n)]
        L.append("A0G6_PRE4_SOURCE_DIRS=" + ",".join(sources))
        cdir = posixpath.dirname(config)
        cst = lstat(cdir)
        if cst is None or not stat.S_ISDIR(cst.st_mode):
            raise Stop("PRE4_LAUNCHER_DIR")
        L.append("A0G6_PRE4_LAUNCHER_DIR=" + facts(cst))
        L.append("A0G6_PRE4_LAUNCHER_ENTRIES=" + ",".join(n for n in names(cdir) if NAME.fullmatch(n)))
        for f in G6_FILES:
            p = posixpath.join(cdir, f)
            fst = lstat(p)
            if fst is None:
                raise Stop("G6_FILE_ABSENT")
            digest = hashlib.sha256(read_small(p, uid, 1 << 20)).hexdigest()
            if digest != pins.get(f):
                raise Stop("G6_PIN_MISMATCH")
            L.append("A0G6_PRE4_G6_FILE_%s=%s sha256=%s PIN=MATCH" % (f, facts(fst), digest))
        # ---- launcher / effective config
        raw = read_small(config, uid, 8192)
        try:
            doc = json.loads(raw.decode("utf-8"))
        except (UnicodeDecodeError, ValueError, RecursionError):
            raise Stop("CONFIG_JSON") from None
        if type(doc) is not dict or not all(type(k) is str and KEY.fullmatch(k) for k in doc):
            raise Stop("CONFIG_SHAPE")
        L.append("A0G6_CONFIG_SHA256=" + hashlib.sha256(raw).hexdigest())
        L.append("A0G6_CONFIG_KEYS=" + ",".join(sorted(doc)))
        for k in PATH_KEYS:
            v = doc.get(k)
            if not safe_path(v):
                raise Stop("CONFIG_PATH_" + k.upper())
            if k == "socket_path":
                s = lstat(v)
                L.append("A0G6_LAUNCHER_%s=%s %s" % (k.upper(), v, facts(s) if s else "ABSENT"))
            else:
                L.append("A0G6_LAUNCHER_%s=%s %s" % (k.upper(), v, facts(require_dir(v, uid, "LAUNCHER_DIR_" + k.upper()))))
        pins = doc.get("g6_pins")
        if pins is None:
            L.append("A0G6_G6_PINS=ABSENT")
        elif type(pins) is dict and all(type(k) is str and KEY.fullmatch(k) for k in pins):
            for k in sorted(pins):
                v = pins[k]
                L.append("A0G6_G6PIN_%s=%s" % (k, v if type(v) is str and (DIGEST.fullmatch(v) or safe_path(v)) else "NOT_SHOWN"))
        else:
            raise Stop("G6_PINS_SHAPE")
        inbox = doc["inbox"]
        ents = names(inbox)
        L.append("A0G6_INBOX_ENTRY_COUNT=%d" % len(ents))
        L.append("A0G6_INBOX_ENTRIES=" + ",".join(n for n in ents if NAME.fullmatch(n)))
        # ---- locks and markers (stat only)
        for p in locks:
            s = lstat(p)
            L.append("A0G6_LOCK_%s=%s" % (p, facts(s) if s else "ABSENT"))
        for p in markers:
            s = lstat(p)
            L.append("A0G6_MARKER_%s=%s" % (p, facts(s) if s else "ABSENT"))
        # ---- G6 state tree: every access goes through directory FDs opened O_NOFOLLOW|O_DIRECTORY, so a validated
        # directory cannot be swapped for a symlink between the check and the listing.
        rst = lstat(state_root)
        if rst is None:
            L.append("A0G6_STATE_ROOT=ABSENT")
            unsafe.append("root")  # missing evidence tree can never certify the inventory
        else:
            rfd = _open_dir(state_root)
            try:
                rok = rfd is not None and safe_dir_flag(os.fstat(rfd), uid) and os.path.samestat(rst, os.fstat(rfd))
                L.append("A0G6_STATE_ROOT=%s %s" % (facts(rst), "SAFE" if rok else "UNSAFE"))
                if not rok:
                    unsafe.append("root")  # root metadata only: no subdirectory is evaluated or listed
                else:
                    for d in SUBDIRS:
                        try:
                            s = os.stat(d, dir_fd=rfd, follow_symlinks=False)
                        except FileNotFoundError:
                            L.append("A0G6_DIR_%s=ABSENT" % d)
                            unsafe.append(d)  # every mandatory subdirectory must exist
                            continue
                        except OSError:
                            raise Stop("IO") from None
                        dfd = _open_dir(d, rfd)
                        try:
                            ok = dfd is not None and safe_dir_flag(os.fstat(dfd), uid) and os.path.samestat(s, os.fstat(dfd))
                            L.append("A0G6_DIR_%s=%s %s" % (d, facts(s), "SAFE" if ok else "UNSAFE"))
                            if not ok:
                                unsafe.append(d)
                                continue
                            if d in ("state-v2", "exports-v2", "restores-v2", "downloads-v2", "stages-v2"):
                                ents = _names_fd(dfd)
                                L.append("A0G6_DIR_%s_COUNT=%d" % (d, len(ents)))
                                for n in ents:
                                    if d == "state-v2" and EVID.fullmatch(n):
                                        data = read_small(n, uid, dir_fd=dfd)
                                        line = "A0G6_EVIDENCE_%s=sha256=%s size=%d" % (
                                            n, hashlib.sha256(data).hexdigest(), len(data))
                                        try:
                                            j = json.loads(data.decode("utf-8"))
                                        except (UnicodeDecodeError, ValueError, RecursionError):
                                            j = None
                                        if type(j) is dict:
                                            for f, rx in (("restic_repo_id", HEX64), ("key_epoch", EPOCH)):
                                                v = j.get(f)
                                                if type(v) is str and rx.fullmatch(v):
                                                    line += " %s=%s" % (f, v)
                                        L.append(line)
                                    elif d == "exports-v2" and ARCHIVE.fullmatch(n):
                                        try:
                                            s2 = os.stat(n, dir_fd=dfd, follow_symlinks=False)
                                        except OSError:
                                            raise Stop("IO") from None
                                        L.append("A0G6_ARCHIVE_%s=%s size=%d" % (n, facts(s2), s2.st_size))
                        finally:
                            if dfd is not None:
                                os.close(dfd)
            finally:
                if rfd is not None:
                    os.close(rfd)
        if unsafe:  # diagnostic: all closed metadata gathered so far, then the closed STOP (never a PASS)
            out.write("\n".join(L) + "\nA0G6_UNSAFE_DIRS=" + ",".join(unsafe) + "\nA0G6_STOP:STATE_DIR_UNSAFE\n")
            return 1
        out.write("\n".join(L) + "\nA0G6_INVENTORY_COMPLETE=1\n")
        return 0
    except Stop as exc:
        out.write("A0G6_STOP:%s\n" % exc)
        return 1
    except OSError:
        out.write("A0G6_STOP:IO\n")
        return 1


if __name__ == "__main__":
    sys.exit(inventory())
