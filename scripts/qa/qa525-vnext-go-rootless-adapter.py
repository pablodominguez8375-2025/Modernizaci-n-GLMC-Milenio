#!/usr/bin/env python3
"""QA525 GO Ed25519 rootless verifier port -- INACTIVE, never physical GO.

This verifies exact canonical GO document bytes signed by a Sponsor-owned
Ed25519 SSH key under namespace qa525-go-cut-v1@pmgm. It returns a boolean
for go-cut-contract.check_cut, not a permission, nonce lease, or root bootstrap.
The externally pinned public fingerprint and ssh-keygen binary SHA256 MUST
come from the Sponsor independently of code/GitHub/Drive or this caller.
Production has NO configurable subprocess runner; tests patch it in unittest.
"""
from __future__ import annotations

import base64
import hashlib
import hmac
import os
from pathlib import Path
import re
import stat
import subprocess
import tempfile

KEYGEN = Path("/usr/bin/ssh-keygen")
PRINCIPAL = "qa525-ops"
NAMESPACE = "qa525-go-cut-v1@pmgm"
SHA256 = re.compile(r"sha256:[a-f0-9]{64}\Z")
FP = re.compile(r"SHA256:[A-Za-z0-9+/]{43}\Z")
SIGNER_LINE = re.compile(
    r'qa525-ops namespaces="qa525-go-cut-v1@pmgm" ssh-ed25519 ([A-Za-z0-9+/]{68})\n\Z')
PUB_PREFIX = b"\x00\x00\x00\x0bssh-ed25519\x00\x00\x00\x20"
SIG_BEGIN = b"-----BEGIN SSH SIGNATURE-----\n"
SIG_END = b"-----END SSH SIGNATURE-----\n"
MAX_BIN = 32 * 1024 * 1024
MAX_DOCUMENT = 65536
MAX_SIG = 4096
MAX_SIGNERS = 512


class Stop(Exception):
    """Only fixed diagnostic codes; no user data, paths or secrets."""


def _binary_sha():
    """Check trust chain, hash fixed root-owned binary from one stable FD."""
    for part in (Path("/"), Path("/usr"), Path("/usr/bin")):
        try:
            st = part.lstat()
        except OSError:
            raise Stop("GO_PORT_BINARY_UNAVAILABLE") from None
        if (not stat.S_ISDIR(st.st_mode) or stat.S_ISLNK(st.st_mode)
                or st.st_uid != 0 or st.st_mode & 0o022):
            raise Stop("GO_PORT_BINARY_PARENT_UNTRUSTED")
    try:
        old = KEYGEN.lstat()
        if (not stat.S_ISREG(old.st_mode) or old.st_uid != 0
                or old.st_nlink != 1 or old.st_mode & 0o022
                or old.st_mode & 0o111 == 0
                or not 0 < old.st_size <= MAX_BIN):
            raise Stop("GO_PORT_BINARY_UNTRUSTED")
        fd = os.open(KEYGEN, os.O_RDONLY | os.O_NOFOLLOW | os.O_CLOEXEC)
        with os.fdopen(fd, "rb") as reader:
            new = os.fstat(reader.fileno())
            ident = (old.st_dev, old.st_ino, old.st_size, old.st_mtime_ns, old.st_ctime_ns)
            if ident != (new.st_dev, new.st_ino, new.st_size, new.st_mtime_ns, new.st_ctime_ns):
                raise Stop("GO_PORT_BINARY_CHANGED")
            hash_ = hashlib.sha256()
            amount = 0
            while True:
                chunk = reader.read(1024 * 1024)
                if not chunk:
                    break
                amount += len(chunk)
                if amount > MAX_BIN:
                    raise Stop("GO_PORT_BINARY_UNTRUSTED")
                hash_.update(chunk)
            last = os.fstat(reader.fileno())
            if (amount != old.st_size or ident != (
                    last.st_dev, last.st_ino, last.st_size, last.st_mtime_ns,
                    last.st_ctime_ns)):
                raise Stop("GO_PORT_BINARY_CHANGED")
        return "sha256:" + hash_.hexdigest()
    except Stop:
        raise
    except OSError:
        raise Stop("GO_PORT_BINARY_UNAVAILABLE") from None


def _fingerprint(allowed):
    """Only one namespace-scoped Ed25519 signer line is accepted."""
    if type(allowed) is not bytes or not 0 < len(allowed) <= MAX_SIGNERS:
        raise Stop("GO_PORT_SIGNERS_INVALID")
    try:
        line = allowed.decode("ascii")
    except UnicodeError:
        raise Stop("GO_PORT_SIGNERS_INVALID") from None
    found = SIGNER_LINE.fullmatch(line)
    if found is None:
        raise Stop("GO_PORT_SIGNERS_INVALID")
    try:
        blob = base64.b64decode(found.group(1), validate=True)
    except ValueError:
        raise Stop("GO_PORT_SIGNERS_INVALID") from None
    if len(blob) != len(PUB_PREFIX) + 32 or not blob.startswith(PUB_PREFIX):
        raise Stop("GO_PORT_SIGNERS_INVALID")
    return "SHA256:" + base64.b64encode(hashlib.sha256(blob).digest()).decode().rstrip("=")


def _private(path, payload):
    try:
        fd = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL |
                     os.O_NOFOLLOW | os.O_CLOEXEC, 0o600)
        with os.fdopen(fd, "wb") as stream:
            stream.write(payload)
            stream.flush()
            os.fsync(stream.fileno())
    except OSError:
        raise Stop("GO_PORT_PRIVATE_FILE_DENIED") from None


def verifier_for(*, trusted_binary_sha256, allowed_signers):
    """Return immutable GO signature callback; **no runtime runner injection**.

    Caller must pin *both* external binary digest and GO signer fingerprint.
    This function does not claim to authenticate their independent origin.
    """
    if (type(trusted_binary_sha256) is not str
            or SHA256.fullmatch(trusted_binary_sha256) is None):
        raise Stop("GO_PORT_BINARY_PIN_INVALID")
    if os.geteuid() == 0:
        raise Stop("GO_PORT_ROOT_FORBIDDEN")
    fingerprint = _fingerprint(allowed_signers)
    if not hmac.compare_digest(_binary_sha(), trusted_binary_sha256):
        raise Stop("GO_PORT_BINARY_PIN_MISMATCH")

    def verify_go_signature(go_bytes, go_signature_bytes, go_fingerprint):
        if (type(go_bytes) is not bytes or not 0 < len(go_bytes) <= MAX_DOCUMENT
                or type(go_signature_bytes) is not bytes or
                not SIG_BEGIN.startswith(go_signature_bytes[:len(SIG_BEGIN)])
                or not go_signature_bytes.startswith(SIG_BEGIN)
                or not go_signature_bytes.endswith(SIG_END)
                or not 0 < len(go_signature_bytes) <= MAX_SIG
                or type(go_fingerprint) is not str
                or FP.fullmatch(go_fingerprint) is None
                or not hmac.compare_digest(fingerprint, go_fingerprint)):
            raise Stop("GO_PORT_INPUT_INVALID")
        if os.geteuid() == 0:
            raise Stop("GO_PORT_ROOT_FORBIDDEN")
        if not hmac.compare_digest(_binary_sha(), trusted_binary_sha256):
            raise Stop("GO_PORT_BINARY_PIN_MISMATCH")
        try:
            with tempfile.TemporaryDirectory(prefix="qa525-go-rootless-") as tmp:
                root = Path(tmp)
                _private(root / "allowed", allowed_signers)
                _private(root / "go.sig", go_signature_bytes)
                argv = [str(KEYGEN), "-Y", "verify", "-f", str(root / "allowed"),
                        "-I", PRINCIPAL, "-n", NAMESPACE, "-s", str(root / "go.sig")]
                try:
                    with tempfile.TemporaryFile(mode="w+b", dir=tmp) as output:
                        done = subprocess.run(
                            argv, input=go_bytes, stdout=output,
                            stderr=subprocess.DEVNULL, check=False, timeout=30,
                            cwd=tmp, env={"PATH": "/usr/bin:/bin", "LC_ALL": "C"})
                        output.seek(0, os.SEEK_END)
                        size = output.tell()
                        if size < 0 or size > 1024:
                            raise Stop("GO_PORT_CHILD_RESULT_INVALID")
                        output.seek(0)
                        printed = output.read()
                except (OSError, subprocess.TimeoutExpired, subprocess.SubprocessError):
                    raise Stop("GO_PORT_EXEC_DENIED") from None
        except Stop:
            raise
        except OSError:
            raise Stop("GO_PORT_TEMP_UNAVAILABLE") from None
        if not hmac.compare_digest(_binary_sha(), trusted_binary_sha256):
            raise Stop("GO_PORT_BINARY_CHANGED")
        expected = ('Good "%s" signature for %s with ED25519 key %s\n'
                    % (NAMESPACE, PRINCIPAL, fingerprint)).encode()
        return (type(done.returncode) is int and done.returncode == 0
                and hmac.compare_digest(printed, expected))

    return verify_go_signature


if __name__ == "__main__":
    raise SystemExit("QA525_GO_SIGNATURE_PORT_LIBRARY_ONLY_NO_ROOT_NO_GO")
