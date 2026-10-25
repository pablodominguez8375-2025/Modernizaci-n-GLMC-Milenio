#!/usr/bin/env python3
"""Read-only SMTP QA key preflight. Never output values or contact Google."""
import argparse
import base64
import binascii
from pathlib import Path
import re
import stat
import sys

KEY = "PMGM_SMTP_ENCRYPTION_KEY"
MAPPING = "SystemMail__EncryptionKey: " + "$" + "{PMGM_SMTP_ENCRYPTION_KEY:-}"


def key_check(path):
    try:
        if path.is_symlink():
            return False, "ENV_SYMLINK_FORBIDDEN"
        st = path.stat()
        if not stat.S_ISREG(st.st_mode):
            return False, "ENV_NOT_REGULAR"
        if stat.S_IMODE(st.st_mode) not in (0o600, 0o640):
            return False, "ENV_PERMISSIONS_UNSAFE"
        lines = path.read_text(encoding="utf-8").splitlines()
    except (OSError, UnicodeError):
        return False, "ENV_UNAVAILABLE"
    values = []
    for line in lines:
        line = line.strip()
        if line.startswith("export "):
            line = line[len("export "):].lstrip()
        if line.startswith(KEY + "="):
            value = line.split("=", 1)[1].strip()
            if value.startswith(("'", '"')) and value.endswith(value[0]):
                value = value[1:-1]
            values.append(value)
    if len(values) != 1:
        return False, "SMTP_KEY_MISSING_OR_DUPLICATED"
    secret = values[0]
    if not secret or len(secret) > 128 or "$" in secret:
        return False, "SMTP_KEY_INVALID_FORMAT"
    try:
        raw = base64.b64decode(secret, validate=True)
    except (ValueError, binascii.Error):
        return False, "SMTP_KEY_INVALID_FORMAT"
    if len(raw) != 32 or not any(raw):
        return False, "SMTP_KEY_NOT_256_BITS"
    return True, "SMTP_KEY_VALID_256_BITS"


def compose_check(path):
    try:
        content = path.read_text(encoding="utf-8")
    except (OSError, UnicodeError):
        return False, "COMPOSE_UNAVAILABLE"
    parts = re.split(r"(?m)^  api:\s*$", content)
    if len(parts) != 2:
        return False, "COMPOSE_API_NOT_UNIQUE"
    api_section = re.split(r"(?m)^  [a-zA-Z0-9_-]+:\s*$", parts[1], maxsplit=1)[0]
    if api_section.count(MAPPING) != 1 or content.count(MAPPING) != 1:
        return False, "SMTP_KEY_MAPPING_NOT_API_ONLY"
    return True, "SMTP_KEY_MAPPED_ONLY_TO_API"


def main(argv=None):
    root = Path(__file__).resolve().parents[2]
    parser = argparse.ArgumentParser(description="Preflight SMTP QA de solo lectura")
    parser.add_argument("--env-file", type=Path, default=Path("/etc/pmgm/srv01.env"))
    parser.add_argument("--compose-file", type=Path,
                        default=root / "infrastructure/docker-compose.srv01.yml")
    args = parser.parse_args(argv)
    checks = (key_check(args.env_file), compose_check(args.compose_file))
    for success, code in checks:
        print(("PASS " if success else "FAIL ") + code)
    ok = all(pair[0] for pair in checks)
    print("SMTP_QA_KEY_PREFLIGHT=" + ("READY_STATIC_ONLY" if ok else "BLOCKED"))
    if ok:
        print("No valida contenedor activo, TLS ni envio Gmail.")
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
