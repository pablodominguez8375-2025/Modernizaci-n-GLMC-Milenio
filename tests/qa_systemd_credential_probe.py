#!/usr/bin/env python3
"""Ephemeral systemd LoadCredential probe. CI synthetic data only.

Run exclusively in a transient, isolated GitHub-hosted Linux runner.
Never target a production system or copy credentials to logs.
"""
import errno
import importlib.util
import json
import os
from pathlib import Path
import stat
import sys

EXPECTED_SHA = "0123456789abcdef0123456789abcdef01234567"


def require(condition, code):
    if not condition:
        raise AssertionError(code)


def main():
    require(len(sys.argv) == 4, "INVALID_ARGUMENT_COUNT")
    mode, original, module_path = sys.argv[1:]
    require(mode in ("valid", "invalid"), "INVALID_TEST_MODE")
    require(os.geteuid() != 0, "MUST_BE_UNPRIVILEGED")
    directory = os.environ.get("CREDENTIALS_DIRECTORY")
    require(directory is not None and directory.startswith("/"), "CREDENTIALS_DIRECTORY_ABSENT")
    credential = Path(directory) / "qa-baseline"
    require(not credential.is_symlink(), "CREDENTIAL_IS_SYMLINK")
    st = credential.stat()
    require(stat.S_ISREG(st.st_mode), "CREDENTIAL_NOT_REGULAR")
    # New systemd uses root:root ACL-backed mode 0440 on a read-only
    # credential mount, rather than setting the service UID as owner.
    require(st.st_uid == 0, "CREDENTIAL_NOT_ROOT_OWNED")
    require(stat.S_IMODE(st.st_mode) == 0o440, "CREDENTIAL_UNEXPECTED_MODE")
    require(credential.parent.stat().st_uid == 0, "CREDENTIAL_DIRECTORY_NOT_ROOT")
    require(os.statvfs(credential).f_flag & os.ST_RDONLY,
            "CREDENTIAL_MOUNT_NOT_READ_ONLY")
    require(st.st_nlink == 1, "CREDENTIAL_HARDLINK")
    require(st.st_size <= 2048, "CREDENTIAL_TOO_LARGE")

    # The systemd credential must not become writable by the service.
    try:
        fd = os.open(credential, os.O_WRONLY | os.O_CLOEXEC | os.O_NOFOLLOW)
    except OSError as exc:
        require(exc.errno in (errno.EPERM, errno.EACCES, errno.EROFS),
                "UNEXPECTED_CREDENTIAL_WRITE_FAILURE")
    else:
        os.close(fd)
        raise AssertionError("CREDENTIAL_WRITABLE")

    # The original root-only source must remain inaccessible to this UID.
    try:
        fd = os.open(original, os.O_RDONLY | os.O_CLOEXEC | os.O_NOFOLLOW)
    except OSError as exc:
        require(exc.errno in (errno.EPERM, errno.EACCES),
                "UNEXPECTED_ORIGINAL_READ_FAILURE")
    else:
        os.close(fd)
        raise AssertionError("ROOT_SOURCE_ACCESSIBLE")

    spec = importlib.util.spec_from_file_location("qa_antirollback_runtime", module_path)
    require(spec is not None and spec.loader is not None, "MODULE_UNAVAILABLE")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    require(module.is_systemd_credential_copy(credential, st),
            "CREDENTIAL_SOURCE_NOT_VERIFIED")
    if mode == "valid":
        require(module.baseline(credential) == EXPECTED_SHA, "BASELINE_READ_MISMATCH")
    else:
        try:
            module.baseline(credential)
        except module.Blocked as exc:
            require(str(exc) == "BASELINE_SCHEMA_INVALID", "UNEXPECTED_BLOCK_CODE")
        else:
            raise AssertionError("INVALID_BASELINE_ACCEPTED")

    print(json.dumps({"status": "PASS", "case": mode, "scope": "EPHEMERAL_RUNNER_ONLY"}))


if __name__ == "__main__":
    try:
        main()
    except (AssertionError, OSError, ValueError) as exc:
        # Fixed diagnostic codes only, never emit paths or credential content.
        code = str(exc) if isinstance(exc, AssertionError) else "RUNTIME_PROBE_FAILURE"
        print(json.dumps({"status": "FAIL", "code": code}))
        sys.exit(1)
