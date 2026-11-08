#!/usr/bin/env python3
"""QA525 OPS Ed25519 verifier PORT for unprivileged PRE-root review only.

Call this from a separately trusted, pinned UNPRIVILEGED operator before any
Python/root bootstrap. No install, Docker, gh, network, sudo, roots or secrets.
Executable sha256 MUST come from an independent Sponsor/operator source,
never from this program, GitHub/Drive alone or a claimed release manifest.
Runtime execution uses subprocess.run directly, not an injectable runner.
The host OS/root and OpenSSH package are a residual trusted computing base.
"""
import hashlib
import os
from pathlib import Path
import re
import stat
import subprocess
import tempfile

SSH_KEYGEN = Path("/usr/bin/ssh-keygen")
DIGEST = re.compile(r"sha256:[a-f0-9]{64}\Z")
MAX_RECEIPT = 65536
MAX_SIGNATURE = 4096
MAX_SIGNERS = 1024
MAX_EXECUTABLE = 32 * 1024 * 1024


class Stop(Exception):
    """Fixed diagnostic codes; never include private bytes or raw stderr."""


def _trusted_parents():
    """Validate /usr and /usr/bin before trusting a pathname."""
    for parent in (SSH_KEYGEN.parent.parent, SSH_KEYGEN.parent):
        try:
            st = parent.lstat()
        except OSError:
            raise Stop("OPS_PORT_PARENT_UNAVAILABLE") from None
        if (not stat.S_ISDIR(st.st_mode) or stat.S_ISLNK(st.st_mode)
                or st.st_uid != 0 or st.st_mode & 0o022):
            raise Stop("OPS_PORT_PARENT_UNTRUSTED")


def _binary_digest():
    """Read a stable root-owned regular executable by fd, without symlinks."""
    _trusted_parents()
    try:
        st_path = SSH_KEYGEN.lstat()
        if (not stat.S_ISREG(st_path.st_mode) or st_path.st_uid != 0
                or st_path.st_nlink != 1 or st_path.st_mode & 0o022
                or not st_path.st_mode & 0o100
                or not 0 < st_path.st_size <= MAX_EXECUTABLE):
            raise Stop("OPS_PORT_BINARY_UNTRUSTED")
        fd = os.open(str(SSH_KEYGEN), os.O_RDONLY | os.O_NOFOLLOW | os.O_CLOEXEC)
        with os.fdopen(fd, "rb") as handle:
            st_fd = os.fstat(handle.fileno())
            if ((st_path.st_dev, st_path.st_ino, st_path.st_size,
                 st_path.st_mtime_ns, st_path.st_ctime_ns) !=
                (st_fd.st_dev, st_fd.st_ino, st_fd.st_size,
                 st_fd.st_mtime_ns, st_fd.st_ctime_ns)):
                raise Stop("OPS_PORT_BINARY_CHANGED")
            data = handle.read(MAX_EXECUTABLE + 1)
            after = os.fstat(handle.fileno())
            if (len(data) != st_fd.st_size or len(data) > MAX_EXECUTABLE
                    or (after.st_dev, after.st_ino, after.st_size,
                        after.st_mtime_ns, after.st_ctime_ns) !=
                       (st_fd.st_dev, st_fd.st_ino, st_fd.st_size,
                        st_fd.st_mtime_ns, st_fd.st_ctime_ns)):
                raise Stop("OPS_PORT_BINARY_CHANGED")
    except Stop:
        raise
    except OSError:
        raise Stop("OPS_PORT_BINARY_UNAVAILABLE") from None
    return "sha256:" + hashlib.sha256(data).hexdigest()


def _private_file(root, name, content):
    file = Path(root) / name
    fd = None
    try:
        fd = os.open(str(file), os.O_WRONLY | os.O_CREAT | os.O_EXCL |
                     os.O_NOFOLLOW | os.O_CLOEXEC, 0o600)
        os.fchmod(fd, 0o600)
        with os.fdopen(fd, "wb") as writer:
            fd = None
            writer.write(content)
            writer.flush()
            os.fsync(writer.fileno())
    except OSError:
        if fd is not None:
            os.close(fd)
        raise Stop("OPS_PORT_TEMP_WRITE_DENIED") from None
    return str(file)


def verifier_for(*, trusted_binary_sha256):
    """Produce a port for qa525-vnext-ops-signature.verify_ops_receipt.

    Returned callable accepts (signers, signature, principal, namespace,
    receipt) and returns (exitcode, stdout), which #902 checks EXACTLY.
    Real ssh-keygen verify uses stdin receipt and a private signers/sig path.
    Production callers CANNOT inject or replace subprocess.run. Tests may
    patch the child boundary in their isolated test process only.
    """
    if (type(trusted_binary_sha256) is not str or
            DIGEST.fullmatch(trusted_binary_sha256) is None):
        raise Stop("OPS_PORT_INPUT_INVALID")
    if os.geteuid() == 0:
        raise Stop("OPS_PORT_ROOT_FORBIDDEN")
    if _binary_digest() != trusted_binary_sha256:
        raise Stop("OPS_PORT_BINARY_PIN_MISMATCH")

    def verify(signers, signature, principal, namespace, receipt):
        # Values from fixed #902 contract, not free-form shell/CLI text.
        if (type(signers) is not bytes or not 0 < len(signers) <= MAX_SIGNERS
                or type(signature) is not bytes or
                not 0 < len(signature) <= MAX_SIGNATURE
                or type(receipt) is not bytes or
                not 0 < len(receipt) <= MAX_RECEIPT
                or principal != "qa525-ops"
                or namespace != "qa525-ops-v2@pmgm"):
            raise Stop("OPS_PORT_PAYLOAD_INVALID")
        if os.geteuid() == 0:
            raise Stop("OPS_PORT_ROOT_FORBIDDEN")
        if _binary_digest() != trusted_binary_sha256:
            raise Stop("OPS_PORT_BINARY_PIN_MISMATCH")
        try:
            with tempfile.TemporaryDirectory(prefix="qa525-ops-rootless-") as temp:
                allowed = _private_file(temp, "allowed_signers", signers)
                sig = _private_file(temp, "ops.sig", signature)
                argv = [str(SSH_KEYGEN), "-Y", "verify", "-f", allowed,
                        "-I", "qa525-ops", "-n", "qa525-ops-v2@pmgm",
                        "-s", sig]
                # Capture into a private on-disk file, not an unbounded PIPE:
                # caller reads at most 1024 bytes after child completion.
                try:
                    with tempfile.TemporaryFile(mode="w+b", dir=temp) as output:
                        try:
                            response = subprocess.run(
                                argv, input=receipt, check=False,
                                stdout=output, stderr=subprocess.DEVNULL,
                                timeout=30, cwd=temp,
                                env={"PATH": "/usr/bin:/bin", "LC_ALL": "C"})
                        except (OSError, subprocess.TimeoutExpired, subprocess.SubprocessError):
                            raise Stop("OPS_PORT_CHILD_FAILED") from None
                        if type(response.returncode) is not int:
                            raise Stop("OPS_PORT_CHILD_RESULT_INVALID")
                        output.flush()
                        output.seek(0, os.SEEK_END)
                        size = output.tell()
                        if not 0 <= size <= 1024:
                            raise Stop("OPS_PORT_CHILD_RESULT_INVALID")
                        output.seek(0)
                        answer = output.read(size)
                except OSError:
                    raise Stop("OPS_PORT_CHILD_FAILED") from None
                if _binary_digest() != trusted_binary_sha256:
                    raise Stop("OPS_PORT_BINARY_CHANGED")
                return response.returncode, answer
        except Stop:
            raise
        except OSError:
            raise Stop("OPS_PORT_PRIVATE_DIRECTORY_DENIED") from None
    return verify


if __name__ == "__main__":
    raise SystemExit("QA525_OPS_ROOTLESS_PORT_LIBRARY_ONLY_NO_INSTALL")
