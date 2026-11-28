#!/usr/bin/env python3
"""QA525 S2e: read-only whitelist projection of the installed launcher's launch-config.json.

Opens ONE fixed path (O_NOFOLLOW, root-owned regular file, nlink 1, not group/other-writable, <= 8192 bytes), reads it
exactly (loop + one extra byte probe, same FD identity), and prints only: file sha256 and size, top-level key NAMES,
the path-typed values of a fixed whitelist, and g6_pins values only when they are a 64-hex digest or a safe path.
Every failure prints one closed line `S2E_STOP:<CODE>` and exits 1. No writes, network, docker or services.
"""
import hashlib
import json
import os
import posixpath
import re
import stat
import sys

PATH = "/opt/centenario/qa525-root-source/da274d651d8e9e056525413406c0aab62131504f/launch-config.json"
ALLOW = ("inbox", "temp_root", "socket_path", "journal_root", "lease_dir")
MAX = 8192
KEY = re.compile(r"[a-z0-9_]{1,40}\Z")
DIGEST = re.compile(r"(sha256:)?[0-9a-f]{64}\Z")


class Stop(Exception):
    pass


def safe_path(value):
    """Absolute, normalized, no '.'/'..' components, conservative charset."""
    if (type(value) is not str or not re.fullmatch(r"/[A-Za-z0-9_./-]{1,200}", value)
            or posixpath.normpath(value) != value or "//" in value):
        return False
    return all(part not in (".", "..") for part in value.split("/"))


def read_exact(fd, size):
    chunks, left = [], size
    while left:
        chunk = os.read(fd, left)
        if not chunk:
            raise Stop("TRUNCATED")
        chunks.append(chunk)
        left -= len(chunk)
    if os.read(fd, 1):
        raise Stop("GREW")
    return b"".join(chunks)


def project(path=PATH, uid=0, out=sys.stdout):
    fd = None
    try:
        try:
            fd = os.open(path, os.O_RDONLY | os.O_NOFOLLOW | os.O_CLOEXEC | os.O_NONBLOCK)
        except OSError:
            raise Stop("OPEN_FAILED") from None
        st = os.fstat(fd)
        if (not stat.S_ISREG(st.st_mode) or st.st_uid != uid or st.st_nlink != 1
                or stat.S_IMODE(st.st_mode) & 0o022):
            raise Stop("FILE_META")
        if not 0 < st.st_size <= MAX:
            raise Stop("SIZE")
        raw = read_exact(fd, st.st_size)
        st2 = os.fstat(fd)
        if (st2.st_ino, st2.st_dev, st2.st_size) != (st.st_ino, st.st_dev, st.st_size):
            raise Stop("FD_CHANGED")
        try:
            doc = json.loads(raw.decode("utf-8"))
        except (UnicodeDecodeError, ValueError, RecursionError):
            raise Stop("JSON_INVALID") from None
        if type(doc) is not dict or not all(type(k) is str and KEY.fullmatch(k) for k in doc):
            raise Stop("SHAPE_OR_KEY_INVALID")
        lines = ["S2E_FILE_SHA256=" + hashlib.sha256(raw).hexdigest(), "S2E_SIZE=%d" % len(raw),
                 "S2E_TOP_LEVEL_KEYS=" + ",".join(sorted(doc))]
        for k in ALLOW:
            v = doc.get(k)
            lines.append("S2E_%s=%s" % (k.upper(), v if safe_path(v) else "ABSENT_OR_INVALID"))
        pins = doc.get("g6_pins")
        if pins is None:
            lines.append("S2E_G6_PINS=ABSENT")
        elif type(pins) is not dict or not all(type(k) is str and KEY.fullmatch(k) for k in pins):
            raise Stop("G6_PINS_SHAPE")
        else:
            for k in sorted(pins):
                v = pins[k]
                ok = type(v) is str and (DIGEST.fullmatch(v) is not None or safe_path(v))
                lines.append("S2E_G6PIN_%s=%s" % (k, v if ok else "NOT_SHOWN"))
        out.write("\n".join(lines) + "\n")
        return 0
    except Stop as exc:
        out.write("S2E_STOP:%s\n" % exc)
        return 1
    except OSError:
        out.write("S2E_STOP:IO\n")
        return 1
    finally:
        if fd is not None:
            os.close(fd)


if __name__ == "__main__":
    sys.exit(project())
