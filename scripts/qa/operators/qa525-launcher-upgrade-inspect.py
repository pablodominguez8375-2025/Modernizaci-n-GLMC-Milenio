#!/usr/bin/env python3
"""Read-only inspection of the already-installed QA525 launcher on glmdch.

Does not load the launcher, trust a package, update files, or authorize deploy.
It is phase one of Issue #822's signed launcher-replacement workflow.
"""
import argparse
import ast
import hashlib
import json
import os
from pathlib import Path
import re
import socket
import stat

LAUNCHER = Path("/usr/local/sbin/pmgm-qa-update")
EXPECTED_SOURCE_NAME = "qa525-update.py"
SCOPE = "GLMDCH_LAUNCHER_READ_ONLY_NO_SIGNED_VERIFICATION_NO_INSTALL"


class Blocked(Exception):
    pass


def require(value, reason):
    if not value:
        raise Blocked(reason)


def launcher_source_from_text(text):
    """Accept exactly the launcher template written by qa525-update.py.

    Never execute or import the old source. Reject additional commands, altered
    interpreter, custom arguments, relative paths, and injected Python.
    """
    require(type(text) is str and len(text) < 4096,
            "LAUNCHER_TEXT_INVALID")
    prefix = "#!/usr/bin/python3\nimport os,sys\n"
    require(text.startswith(prefix), "LAUNCHER_TEMPLATE_MISMATCH")
    line = text[len(prefix):]
    m = re.fullmatch(
        r'os\.execv\("/usr/bin/python3", \["/usr/bin/python3", "-B", (.+), \*sys\.argv\[1:\]\]\)\n',
        line)
    require(m is not None, "LAUNCHER_TEMPLATE_MISMATCH")
    try:
        source = ast.literal_eval(m.group(1))
    except (ValueError, SyntaxError, MemoryError, TypeError):
        raise Blocked("LAUNCHER_SOURCE_LITERAL_INVALID") from None
    require(type(source) is str and len(source) < 1024
            and "\x00" not in source, "LAUNCHER_SOURCE_INVALID")
    path = Path(source)
    require(path.is_absolute() and path.name == EXPECTED_SOURCE_NAME
            and ".." not in path.parts and "." not in path.parts,
            "LAUNCHER_SOURCE_PATH_INVALID")
    canonical = (prefix +
        'os.execv("/usr/bin/python3", ["/usr/bin/python3", "-B", ' +
        repr(source) + ', *sys.argv[1:]])\n')
    require(text == canonical, "LAUNCHER_TEMPLATE_MISMATCH")
    return path


def owned_chain(path, *, final_mode=None):
    """Refuse links and group/world writable ancestors before any file read."""
    require(path.is_absolute(), "LAUNCHER_PATH_NOT_ABSOLUTE")
    for index, item in enumerate((path, *path.parents)):
        try:
            info = item.lstat()
        except OSError:
            raise Blocked("LAUNCHER_OR_SOURCE_MISSING") from None
        require(info.st_uid == 0 and not stat.S_ISLNK(info.st_mode)
                and not (info.st_mode & 0o022),
                "LAUNCHER_OR_SOURCE_UNTRUSTED")
        if index == 0:
            require(stat.S_ISREG(info.st_mode) and info.st_nlink == 1,
                    "LAUNCHER_FILE_TYPE_INVALID")
            if final_mode is not None:
                require(stat.S_IMODE(info.st_mode) == final_mode,
                        "LAUNCHER_PERMISSION_INVALID")
        else:
            require(stat.S_ISDIR(info.st_mode), "LAUNCHER_PARENT_NOT_DIRECTORY")
    return path.lstat()


def read_regular_file(path, *, maximum, mode=None):
    before = owned_chain(path, final_mode=mode)
    fd = os.open(path, os.O_RDONLY | os.O_NOFOLLOW | os.O_CLOEXEC)
    try:
        current = os.fstat(fd)
        require(stat.S_ISREG(current.st_mode) and current.st_nlink == 1
                and current.st_uid == 0 and current.st_ino == before.st_ino
                and current.st_dev == before.st_dev
                and current.st_size <= maximum
                and not (current.st_mode & 0o022),
                "LAUNCHER_READ_RACE_OR_PERMISSIONS")
        with os.fdopen(fd, "rb") as stream:
            fd = -1
            data = stream.read(maximum + 1)
        require(len(data) <= maximum and len(data) == current.st_size,
                "LAUNCHER_FILE_CHANGED")
        return data
    finally:
        if fd >= 0:
            os.close(fd)


def inspect():
    require(os.geteuid() == 0 and socket.gethostname() == "glmdch",
            "QA_LAUNCHER_INSPECTION_GLM DCH_ROOT_ONLY".replace(" ", "_"))
    launcher = read_regular_file(LAUNCHER, maximum=4096, mode=0o700)
    try:
        source = launcher_source_from_text(launcher.decode("utf-8"))
    except UnicodeDecodeError:
        raise Blocked("LAUNCHER_TEXT_INVALID") from None
    candidate = read_regular_file(source, maximum=2_000_000)
    # This is a local file hash, NOT a verified release digest or source commit.
    return {
        "status": "LAUNCHER_OBSERVED_UPGRADE_NOT_AUTHORIZED",
        "scope": SCOPE,
        "current_launcher": str(LAUNCHER),
        "current_launcher_sha256": hashlib.sha256(launcher).hexdigest(),
        "current_operator": str(source),
        "current_operator_sha256": hashlib.sha256(candidate).hexdigest(),
        "old_launcher_template_verified": True,
        "signed_release_verified": False,
        "rollback_validated": False,
        "may_install": False,
        "may_apply": False,
    }


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.parse_args(argv)
    try:
        print(json.dumps(inspect(), sort_keys=True))
        return 0
    except (Blocked, OSError, ValueError) as exc:
        code = str(exc) if isinstance(exc, Blocked) else "LAUNCHER_INSPECTION_FAILED"
        print(json.dumps({"status": "BLOCKED_QA525_LAUNCHER_INSPECTION",
                          "scope": SCOPE, "code": code,
                          "may_install": False, "may_apply": False}, sort_keys=True))
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
