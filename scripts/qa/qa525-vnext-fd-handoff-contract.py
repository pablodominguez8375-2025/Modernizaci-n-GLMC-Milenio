#!/usr/bin/env python3
"""QA525 sealed APP FD transfer: Linux rootless transport ONLY, never privilege/GO.

An AF_UNIX SOCK_SEQPACKET message transfers exactly one already-sealed memfd via
SCM_RIGHTS. The receiver authenticates the *bytes* to an independently supplied
SHA256 pin and nonce. The wire, the sender, and the success result never grant
APP Sigstore, OPS/GO signatures, Sponsor consent, or permission to use root.
A future authenticated bootstrap must independently establish its executable,
caller identity, hashes and trust anchors *before* granting any privilege.

This library never opens artifact paths or runs commands/network/sudo/docker.
After SCM_RIGHTS receipt it reopens only /proc/self/fd/<received FD> read-only
for a private open-file description (the peer cannot alter its offset/flags).
This is only a rootless data transport; /proc and execution trust remain the
responsibility of a separately authenticated bootstrap.
"""
import array
import fcntl
import hashlib
import hmac
import os
import re
import socket
import stat

MAGIC = b"QA525-FD-V1:"
HEX64 = re.compile(r"[0-9a-f]{64}\Z")
SHA256 = re.compile(r"sha256:[0-9a-f]{64}\Z")
NONCE = re.compile(r"[A-Za-z0-9_-]{24,96}\Z")
MAX_INNER_BYTES = 2 * 1024 ** 3
SEALS = (fcntl.F_SEAL_WRITE | fcntl.F_SEAL_SHRINK |
         fcntl.F_SEAL_GROW | fcntl.F_SEAL_SEAL)
MSG_FLAGS = getattr(socket, "MSG_CMSG_CLOEXEC", 0)


class Stop(Exception):
    """Opaque STOP code; never include untrusted wire content."""


def _pin(value, code, *, digest=False, nonce=False):
    pattern = SHA256 if digest else NONCE if nonce else HEX64
    if type(value) is not str or pattern.fullmatch(value) is None:
        raise Stop(code)
    return value


def _socket(sock):
    if (not isinstance(sock, socket.socket)
            or sock.family != socket.AF_UNIX
            or (sock.type & 0xf) != socket.SOCK_SEQPACKET):
        raise Stop("FD_HANDOFF_SOCKET_INVALID")


def _fd_hash(fd, pin):
    """Re-check kernel seals and independently pinned bytes, without path re-open."""
    try:
        info = os.fstat(fd)
        if (not stat.S_ISREG(info.st_mode) or not 0 < info.st_size <= MAX_INNER_BYTES
                or fcntl.fcntl(fd, fcntl.F_GET_SEALS) & SEALS != SEALS):
            raise Stop("FD_HANDOFF_FD_UNSAFE")
        digest = hashlib.sha256()
        offset = 0
        while offset < info.st_size:
            block = os.pread(fd, min(1 << 20, info.st_size - offset), offset)
            if not block:
                raise Stop("FD_HANDOFF_FD_UNSAFE")
            digest.update(block)
            offset += len(block)
        if (os.fstat(fd).st_size != info.st_size
                or fcntl.fcntl(fd, fcntl.F_GET_SEALS) & SEALS != SEALS):
            raise Stop("FD_HANDOFF_FD_UNSAFE")
        if not hmac.compare_digest(digest.hexdigest(), pin[7:]):
            raise Stop("FD_HANDOFF_DIGEST_MISMATCH")
    except Stop:
        raise
    except (OSError, ValueError, OverflowError):
        raise Stop("FD_HANDOFF_FD_UNSAFE") from None


def send_verified_fd(sock, fd, *, nonce, sha256):
    """Send an already-sealed fd; never send a path or claim it authorizes GO."""
    _socket(sock)
    _pin(nonce, "FD_HANDOFF_NONCE_INVALID", nonce=True)
    _pin(sha256, "FD_HANDOFF_PIN_INVALID", digest=True)
    if type(fd) is not int or fd < 0:
        raise Stop("FD_HANDOFF_FD_UNSAFE")
    _fd_hash(fd, sha256)
    frame = MAGIC + nonce.encode("ascii") + b":" + sha256[7:].encode("ascii")
    try:
        count = sock.sendmsg([frame], [(socket.SOL_SOCKET, socket.SCM_RIGHTS,
                                         array.array("i", [fd]))])
        if count != len(frame):
            raise Stop("FD_HANDOFF_SEND_INCOMPLETE")
    except Stop:
        raise
    except OSError:
        raise Stop("FD_HANDOFF_SEND_FAILED") from None


def receive_verified_fd(sock, *, expected_nonce, expected_sha256):
    """Receive one immutable fd, compare with independent pin, return NO authority.

    Caller must close result['fd']. Never derive the expected pin from the packet.
    The returned FD is a private read-only /proc/self/fd re-open; the SCM_RIGHTS
    FD sharing the sender's open-file description is always closed. The caller
    must trust its procfs environment independently. No authority is granted.
    """
    _socket(sock)
    _pin(expected_nonce, "FD_HANDOFF_NONCE_INVALID", nonce=True)
    _pin(expected_sha256, "FD_HANDOFF_PIN_INVALID", digest=True)
    received = []
    keep = None
    private_fd = None
    try:
        frame, ancillary, flags, _ = sock.recvmsg(
            256, socket.CMSG_SPACE(4 * array.array("i").itemsize), MSG_FLAGS)
        unexpected = False
        for level, kind, value in ancillary:
            if level != socket.SOL_SOCKET or kind != socket.SCM_RIGHTS:
                unexpected = True
                continue
            if len(value) == 0 or len(value) % array.array("i").itemsize:
                unexpected = True
                continue
            ints = array.array("i")
            ints.frombytes(value)
            received.extend(ints)
        if unexpected:
            raise Stop("FD_HANDOFF_CONTROL_INVALID")
        if flags & (socket.MSG_TRUNC | socket.MSG_CTRUNC):
            raise Stop("FD_HANDOFF_TRUNCATED")
        want = MAGIC + expected_nonce.encode("ascii") + b":" + expected_sha256[7:].encode("ascii")
        if not hmac.compare_digest(frame, want):
            raise Stop("FD_HANDOFF_FRAME_MISMATCH")
        if len(received) != 1:
            raise Stop("FD_HANDOFF_FD_COUNT_INVALID")
        fd = received[0]
        os.set_inheritable(fd, False)
        _fd_hash(fd, expected_sha256)
        original = os.fstat(fd)
        # SCM_RIGHTS duplicates the *open-file description*. Return a distinct
        # O_RDONLY description instead: peer F_SETFL and lseek must not affect it.
        # This is a kernel procfd for the already-verified received descriptor;
        # NEVER open an artifact path supplied by the peer or wire.
        private_fd = os.open("/proc/self/fd/" + str(fd),
                             os.O_RDONLY | os.O_CLOEXEC)
        private = os.fstat(private_fd)
        if ((original.st_dev, original.st_ino, original.st_size)
                != (private.st_dev, private.st_ino, private.st_size)
                or (fcntl.fcntl(private_fd, fcntl.F_GETFL)
                    & os.O_ACCMODE) != os.O_RDONLY):
            raise Stop("FD_HANDOFF_PRIVATE_REOPEN_INVALID")
        os.set_inheritable(private_fd, False)
        _fd_hash(private_fd, expected_sha256)
        keep = private_fd
        return {"fd": private_fd, "sha256": expected_sha256,
                "bytes_match_independent_pin": True,
                "private_readonly_description": True,
                "bootstrap_authenticated_here": False,
                "sponsor_authorized_here": False,
                "may_install": False, "may_apply": False}
    except Stop:
        raise
    except (OSError, ValueError, OverflowError):
        raise Stop("FD_HANDOFF_RECEIVE_FAILED") from None
    finally:
        if private_fd is not None and private_fd != keep:
            try:
                os.close(private_fd)
            except OSError:
                pass
        # Always retire the SCM_RIGHTS description, including on success.
        for fd in received:
            try:
                os.close(fd)
            except OSError:
                pass


if __name__ == "__main__":
    raise SystemExit("QA525_FD_HANDOFF_LIBRARY_ONLY_NO_EXECUTION")
