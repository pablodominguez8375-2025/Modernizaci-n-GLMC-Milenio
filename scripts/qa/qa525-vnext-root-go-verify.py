#!/usr/bin/env python3
"""QA525 ROOT GO-signature verifier port (OpenSSH `ssh-keygen -Y verify`). INACTIVE library; no authority.

The rootless adapter refuses to run as root on purpose; this is the DISTINCT port the single root
launcher uses. It never re-implements Ed25519: it runs the pinned `ssh-keygen` and compares its exact
output. What it pins, all fixed BEFORE a byte is verified:
  * the verifier binary: opened once, judged on that FD (regular, OS-owned, not writable, one link),
    hashed from the FD, and EXECUTED THROUGH THAT SAME FD (/proc/self/fd/N) -- no path swap window;
  * the signer: exactly one `qa525-ops namespaces="qa525-go-cut-v1@pmgm" ssh-ed25519 KEY` line whose
    SHA256 fingerprint equals the externally pinned fingerprint (never derived-then-trusted);
  * principal, namespace, empty environment, fixed cwd, no shell/PATH, private 0700 temp dir, bounded output.
It returns a bool for go-cut-contract.check_cut; it is NOT a GO, a nonce lease or a bootstrap.
"""
import base64
import hashlib
import hmac
import os
import re
import shutil
import stat
import subprocess
import tempfile

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
MAX_OUTPUT = 1024
TIMEOUT = 30


class Stop(Exception):
    """Only fixed diagnostic codes; never paths, keys or child output."""


def _fingerprint(allowed):
    if type(allowed) is not bytes or not 0 < len(allowed) <= MAX_SIGNERS:
        raise Stop("GORV_SIGNERS_INVALID")
    try:
        line = allowed.decode("ascii")
    except UnicodeError:
        raise Stop("GORV_SIGNERS_INVALID") from None
    found = SIGNER_LINE.fullmatch(line)
    if found is None:
        raise Stop("GORV_SIGNERS_INVALID")
    try:
        blob = base64.b64decode(found.group(1), validate=True)
    except ValueError:
        raise Stop("GORV_SIGNERS_INVALID") from None
    if len(blob) != len(PUB_PREFIX) + 32 or not blob.startswith(PUB_PREFIX):
        raise Stop("GORV_SIGNERS_INVALID")
    return "SHA256:" + base64.b64encode(hashlib.sha256(blob).digest()).decode().rstrip("=")


def _ident(st):
    return (st.st_dev, st.st_ino, st.st_size, st.st_mtime_ns, st.st_ctime_ns)


def _open_binary(path, os_uid, want_sha):
    """Return (fd, identity) of a binary that matches the pin; the caller executes THIS fd."""
    if type(path) is not str or not os.path.isabs(path) or os.path.normpath(path) != path:
        raise Stop("GORV_BINARY_PATH_INVALID")
    fd = None
    try:
        fd = os.open(path, os.O_RDONLY | os.O_NOFOLLOW | os.O_CLOEXEC)
        st = os.fstat(fd)
        if (not stat.S_ISREG(st.st_mode) or st.st_uid != os_uid or st.st_nlink != 1
                or st.st_mode & 0o022 or not st.st_mode & 0o111 or not 0 < st.st_size <= MAX_BIN):
            raise Stop("GORV_BINARY_UNTRUSTED")
        digest, total = hashlib.sha256(), 0
        while True:
            block = os.read(fd, 1024 * 1024)
            if not block:
                break
            total += len(block)
            if total > MAX_BIN:
                raise Stop("GORV_BINARY_UNTRUSTED")
            digest.update(block)
        if total != st.st_size or _ident(os.fstat(fd)) != _ident(st):
            raise Stop("GORV_BINARY_CHANGED")
        if not hmac.compare_digest("sha256:" + digest.hexdigest(), want_sha):
            raise Stop("GORV_BINARY_PIN_MISMATCH")
        return fd, _ident(st)
    except BaseException as exc:
        if fd is not None:
            os.close(fd)
        if isinstance(exc, OSError):
            raise Stop("GORV_BINARY_UNAVAILABLE") from None
        raise


def _private(path, payload):
    try:
        fd = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW | os.O_CLOEXEC, 0o600)
        try:
            off = 0
            while off < len(payload):
                off += os.write(fd, payload[off:])
            os.fsync(fd)
        finally:
            os.close(fd)
    except OSError:
        raise Stop("GORV_PRIVATE_FILE_DENIED") from None


def _temp_root_ok(path, os_uid):
    try:
        st = os.lstat(path)
    except OSError:
        raise Stop("GORV_TEMP_ROOT_UNSAFE") from None
    if (not stat.S_ISDIR(st.st_mode) or st.st_uid != os_uid or st.st_mode & 0o077):
        raise Stop("GORV_TEMP_ROOT_UNSAFE")  # a private (0700) directory of the OS owner only


def verifier_for(*, keygen_path, trusted_binary_sha256, allowed_signers, expected_fingerprint,
                 temp_root, required_euid=0, os_uid=0):
    """Return the immutable `verify_go_signature(go_bytes, sig_bytes, fingerprint) -> bool` callback.

    keygen_path / trusted_binary_sha256 / expected_fingerprint must come from the Sponsor-anchored
    manifest (bootstrap `helpers` + pinned config), never from the sender, the GO or the package tree.
    There is NO runner injection point in production code; unit tests patch subprocess.run.
    """
    if type(trusted_binary_sha256) is not str or SHA256.fullmatch(trusted_binary_sha256) is None:
        raise Stop("GORV_BINARY_PIN_INVALID")
    if type(expected_fingerprint) is not str or FP.fullmatch(expected_fingerprint) is None:
        raise Stop("GORV_FINGERPRINT_PIN_INVALID")
    if not hmac.compare_digest(_fingerprint(allowed_signers), expected_fingerprint):
        raise Stop("GORV_FINGERPRINT_PIN_MISMATCH")
    if os.geteuid() != required_euid:
        raise Stop("GORV_ROOT_REQUIRED")
    _temp_root_ok(temp_root, os_uid)
    signers, binary_path = bytes(allowed_signers), keygen_path

    def verify_go_signature(go_bytes, go_signature_bytes, go_fingerprint):
        if (type(go_bytes) is not bytes or not 0 < len(go_bytes) <= MAX_DOCUMENT
                or type(go_signature_bytes) is not bytes
                or not go_signature_bytes.startswith(SIG_BEGIN)
                or not go_signature_bytes.endswith(SIG_END)
                or not 0 < len(go_signature_bytes) <= MAX_SIG
                or type(go_fingerprint) is not str or FP.fullmatch(go_fingerprint) is None
                or not hmac.compare_digest(expected_fingerprint, go_fingerprint)):
            raise Stop("GORV_INPUT_INVALID")
        if os.geteuid() != required_euid:
            raise Stop("GORV_ROOT_REQUIRED")
        _temp_root_ok(temp_root, os_uid)
        fd, before = _open_binary(binary_path, os_uid, trusted_binary_sha256)
        tmp = None
        try:
            try:
                tmp = tempfile.mkdtemp(prefix="gorv-", dir=temp_root)  # 0700 by construction
                os.chmod(tmp, 0o700)
                _private(os.path.join(tmp, "allowed"), signers)
                _private(os.path.join(tmp, "go.sig"), go_signature_bytes)
                argv = ["ssh-keygen", "-Y", "verify", "-f", os.path.join(tmp, "allowed"),
                        "-I", PRINCIPAL, "-n", NAMESPACE, "-s", os.path.join(tmp, "go.sig")]
                with tempfile.TemporaryFile(mode="w+b", dir=tmp) as output:
                    done = subprocess.run(  # executes the VERIFIED fd: no path to swap
                        argv, executable="/proc/self/fd/%d" % fd, pass_fds=(fd,), input=go_bytes,
                        stdout=output, stderr=subprocess.DEVNULL, check=False, timeout=TIMEOUT,
                        cwd=tmp, env={}, shell=False, close_fds=True)
                    output.seek(0, os.SEEK_END)
                    if output.tell() > MAX_OUTPUT:
                        raise Stop("GORV_CHILD_RESULT_INVALID")
                    output.seek(0)
                    printed = output.read()
            except (OSError, subprocess.TimeoutExpired, subprocess.SubprocessError):
                raise Stop("GORV_EXEC_DENIED") from None
            if _ident(os.fstat(fd)) != before:
                raise Stop("GORV_BINARY_CHANGED")
        finally:
            os.close(fd)
            if tmp is not None:
                shutil.rmtree(tmp, ignore_errors=True)
        expected = ('Good "%s" signature for %s with ED25519 key %s\n'
                    % (NAMESPACE, PRINCIPAL, expected_fingerprint)).encode()
        return (type(done.returncode) is int and done.returncode == 0
                and hmac.compare_digest(printed, expected))

    return verify_go_signature


if __name__ == "__main__":
    raise SystemExit("QA525_ROOT_GO_VERIFY_LIBRARY_ONLY_NO_ROOT_NO_GO")
