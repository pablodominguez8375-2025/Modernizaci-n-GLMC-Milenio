#!/usr/bin/env python3
"""QA525: closed, pure schema env-file renderer. No I/O, no authority or secrets in diagnostics.

The root host must read the root-owned 0600 source under its held plane and create
0600 files in a verified root-only tmpfs directory, with cleanup in finally.
This library never writes files and must not be used as a launcher or CLI.
"""
import json
import re

PASSWORD_KEY = b"PMGM_QA_MIGRATOR_DB_PASSWORD"
MIGRATION = re.compile(r"20[0-9]{12}_[A-Za-z0-9_]+\\Z")
MAX_SOURCE = 65536

class Stop(Exception):
    """Opaque diagnostic; no secret content."""

def _password(source):
    if type(source) is not bytes or not 0 < len(source) <= MAX_SOURCE:
        raise Stop("SCHEMA_ENV_SOURCE_INVALID")
    found = None
    for line in source.splitlines():
        if not line or line.startswith(b"#"):
            continue
        if b"=" not in line:
            raise Stop("SCHEMA_ENV_SOURCE_INVALID")
        key, value = line.split(b"=", 1)
        if key == PASSWORD_KEY:
            if found is not None or not 0 < len(value) <= 256 or any(
                x in value for x in (b"\\r", b"\\n", b"\\x00", b"\\", b"'", b'"', b";", b" ", b"\\t")
            ):
                raise Stop("SCHEMA_ENV_PASSWORD_INVALID")
            if any(ch < 33 or ch > 126 for ch in value):
                raise Stop("SCHEMA_ENV_PASSWORD_INVALID")
            found = value
    if found is None:
        raise Stop("SCHEMA_ENV_PASSWORD_MISSING")
    return found

def render(source, history):
    """Return exact Docker env-file bytes; never print or serialize the return value."""
    if (type(history) is not list or len(history) != 100 or len(set(history)) != 100
            or history != sorted(history) or any(type(x) is not str or MIGRATION.fullmatch(x) is None for x in history)):
        raise Stop("SCHEMA_ENV_HISTORY_INVALID")
    password = _password(source)
    expected = json.dumps(history, ensure_ascii=True, separators=(",", ":")).encode("ascii")
    # Docker --env-file accepts unquoted values; forbid special syntax in the password.
    migrator = (b"QA525_DDL_CONNECTION=Host=postgres;Port=5432;Database=pmgm;"
                b"Username=pmgm_migrator;Password=" + password + b"\\n"
                b"QA525_EXPECTED_EF_HISTORY=" + expected + b"\\n")
    grants = b"PGPASSWORD=" + password + b"\\n"
    return migrator, grants

if __name__ == "__main__":
    raise SystemExit("QA525_SCHEMA_ENV_LIBRARY_ONLY_NO_ROOT")
