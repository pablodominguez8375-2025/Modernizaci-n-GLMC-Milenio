#!/usr/bin/env python3
"""QA525: inspect retained SHA-pinned source bytes, NEVER execute or install.

This library is only a read-only consistency check for a FUTURE separately
signed root installer. Matching an expected hash supplied by the caller does
NOT authenticate that hash, the signed GitHub release, or the installed tree.
"""
import hashlib
import os
from pathlib import Path
import re
import socket
import stat

ROOT = Path("/opt/centenario/qa525-signed-stage")
SOURCE_REL = Path("scripts/qa/operators/qa525-update.py")
SHA = re.compile(r"[0-9a-f]{40}")
HASH = re.compile(r"[0-9a-f]{64}")
MAX_BYTES = 2_000_000
SCOPE = "QA525_RETAINED_SOURCE_READONLY_NOT_SIGSTORE_VERIFICATION"


class Blocked(Exception):
    """Fixed, non-sensitive failure reason."""


def require(value, code):
    if not value:
        raise Blocked(code)


def source_path(source_sha):
    """Refuse caller-selected files, relative paths and mutable branch names."""
    require(type(source_sha) is str and SHA.fullmatch(source_sha) is not None,
            "RETAINED_SOURCE_SHA_INVALID")
    return ROOT / source_sha / SOURCE_REL


def trusted_chain(path):
    """Reject links and writable ancestors before opening source."""
    require(isinstance(path, Path) and path.is_absolute()
            and path.name == SOURCE_REL.name, "RETAINED_SOURCE_PATH_INVALID")
    require(path == source_path(path.parents[3].name),
            "RETAINED_SOURCE_PATH_NOT_PINNED")
    before = None
    for idx, item in enumerate((path, *path.parents)):
        try:
            st = item.lstat()
        except OSError:
            raise Blocked("RETAINED_SOURCE_PATH_MISSING") from None
        require(st.st_uid == 0 and not stat.S_ISLNK(st.st_mode)
                and not (st.st_mode & 0o022),
                "RETAINED_SOURCE_OWNER_OR_LINK_INVALID")
        if idx == 0:
            require(stat.S_ISREG(st.st_mode) and st.st_nlink == 1
                    and 0 < st.st_size <= MAX_BYTES,
                    "RETAINED_SOURCE_TYPE_OR_SIZE_INVALID")
            before = st
        else:
            require(stat.S_ISDIR(st.st_mode),
                    "RETAINED_SOURCE_ANCESTOR_NOT_DIRECTORY")
            if item == ROOT or item == ROOT / path.parents[3].name:
                require(stat.S_IMODE(st.st_mode) == 0o700,
                        "RETAINED_STAGE_NOT_PRIVATE")
    return before


def read_exact(path, before):
    """Read bounded file without following its final link; reject races."""
    fd = os.open(path, os.O_RDONLY | os.O_NOFOLLOW | os.O_CLOEXEC)
    try:
        st = os.fstat(fd)
        require(stat.S_ISREG(st.st_mode) and st.st_uid == 0
                and st.st_nlink == 1 and not (st.st_mode & 0o022)
                and st.st_dev == before.st_dev
                and st.st_ino == before.st_ino
                and st.st_size == before.st_size
                and st.st_mtime_ns == before.st_mtime_ns
                and 0 < st.st_size <= MAX_BYTES,
                "RETAINED_SOURCE_CHANGED_BEFORE_READ")
        with os.fdopen(fd, "rb") as stream:
            fd = -1
            data = stream.read(MAX_BYTES + 1)
        require(len(data) == st.st_size and len(data) <= MAX_BYTES,
                "RETAINED_SOURCE_CHANGED_DURING_READ")
        return data
    finally:
        if fd >= 0:
            os.close(fd)


def inspect_retained(source_sha, expected_operator_sha256):
    """Inspect SHA-derived retained operator without import, execute or writes.

    The real installer MUST separately authenticate expected_operator_sha256
    from fresh GitHub Sigstore/manifest evidence while holding all QA locks.
    """
    require(os.geteuid() == 0 and socket.gethostname() == "glmdch",
            "RETAINED_SOURCE_ONLY_GLMDCH_ROOT")
    path = source_path(source_sha)
    require(type(expected_operator_sha256) is str
            and HASH.fullmatch(expected_operator_sha256) is not None,
            "RETAINED_SOURCE_EXPECTED_HASH_INVALID")
    before = trusted_chain(path)
    observed = hashlib.sha256(read_exact(path, before)).hexdigest()
    require(observed == expected_operator_sha256,
            "RETAINED_SOURCE_BYTES_DIVERGED")
    return {
        "status": "ROOT_OWNED_PINNED_SOURCE_HASH_MATCH_NOT_SIGNED",
        "scope": SCOPE,
        "source_sha": source_sha,
        "pinned_operator_path": str(path),
        "operator_sha256": observed,
        "signature_verified_by_inspector": False,
        "manifest_verified_by_inspector": False,
        "package_persisted_by_inspector": False,
        "may_install": False,
        "may_apply": False,
    }


if __name__ == "__main__":
    raise SystemExit("RETAINED_SOURCE_LIBRARY_ONLY_NO_INSTALL_ENTRYPOINT")
