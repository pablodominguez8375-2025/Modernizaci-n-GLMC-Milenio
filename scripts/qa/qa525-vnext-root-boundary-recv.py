#!/usr/bin/env python3
"""QA525 boundary receiver: per-message SCM_CREDENTIALS + sealed-memfd SCM_RIGHTS + pin. Library only.

Authenticates the SENDING process of each message (kernel-stamped SCM_CREDENTIALS via
SO_PASSCRED, not the connect/socketpair-time SO_PEERCRED, which an inherited endpoint
can outlive), requires a mandatory timeout,
closes every received fd on any failure (including BaseException), verifies
seals and an independently supplied sha256 pin, and returns a PRIVATE read-only
reopen of the fd (own offset/flags; the sender cannot skew it).
It grants NO authority: no GO, no Sponsor/OPS/APP signature, no root use.
Never opens paths (except /proc/self/fd/N of its own fd), runs commands, or uses network.
"""
import array
import fcntl
import hashlib
import hmac
import os
import re
import socket
import stat
import struct

MAGIC = b"QA525-BND-V1:"
SHA256 = re.compile(r"sha256:[0-9a-f]{64}\Z")
NONCE = re.compile(r"[A-Za-z0-9_-]{24,96}\Z")
MAX_BYTES = 2 * 1024 ** 3
SEALS = (fcntl.F_SEAL_WRITE | fcntl.F_SEAL_SHRINK | fcntl.F_SEAL_GROW | fcntl.F_SEAL_SEAL)
MAX_FDS = 4


class Stop(Exception):
    """Opaque STOP code."""


def _check(sock, nonce, pin, uid, pid, timeout):
    if (not isinstance(sock, socket.socket) or sock.family != socket.AF_UNIX
            or (sock.type & 0xf) != socket.SOCK_SEQPACKET):
        raise Stop("BND_SOCKET_INVALID")
    if type(nonce) is not str or NONCE.fullmatch(nonce) is None:
        raise Stop("BND_NONCE_INVALID")
    if type(pin) is not str or SHA256.fullmatch(pin) is None:
        raise Stop("BND_PIN_INVALID")
    if type(uid) is not int or isinstance(uid, bool) or uid < 0:
        raise Stop("BND_UID_INVALID")
    if pid is not None and (type(pid) is not int or isinstance(pid, bool) or pid <= 0):
        raise Stop("BND_PID_INVALID")
    if type(timeout) not in (int, float) or isinstance(timeout, bool) or not 0 < timeout <= 60:
        raise Stop("BND_TIMEOUT_REQUIRED")


def _hash(fd, pin):
    info = os.fstat(fd)
    if (not stat.S_ISREG(info.st_mode) or not 0 < info.st_size <= MAX_BYTES
            or fcntl.fcntl(fd, fcntl.F_GET_SEALS) & SEALS != SEALS):
        raise Stop("BND_FD_UNSAFE")
    digest, off = hashlib.sha256(), 0
    while off < info.st_size:
        block = os.pread(fd, min(1 << 20, info.st_size - off), off)
        if not block:
            raise Stop("BND_FD_UNSAFE")
        digest.update(block)
        off += len(block)
    if os.fstat(fd).st_size != info.st_size or fcntl.fcntl(fd, fcntl.F_GET_SEALS) & SEALS != SEALS:
        raise Stop("BND_FD_UNSAFE")
    if not hmac.compare_digest(digest.hexdigest(), pin[7:]):
        raise Stop("BND_DIGEST_MISMATCH")


def prepare(sock):
    """Call BEFORE the peer can send: messages queued earlier carry no credentials."""
    try:
        sock.setsockopt(socket.SOL_SOCKET, socket.SO_PASSCRED, 1)
    except (OSError, AttributeError):
        raise Stop("BND_PEER_UNKNOWN") from None


def receive(sock, *, expected_nonce, expected_sha256, expected_peer_uid, timeout,
            expected_peer_pid=None):
    """Return {'fd': private RDONLY fd (caller closes), 'peer_pid', ...}; NO authority."""
    _check(sock, expected_nonce, expected_sha256, expected_peer_uid, expected_peer_pid, timeout)
    held = []
    prior = sock.gettimeout()
    try:
        try:
            if not sock.getsockopt(socket.SOL_SOCKET, socket.SO_PASSCRED):
                raise Stop("BND_PASSCRED_NOT_ENABLED")
        except OSError:
            raise Stop("BND_PEER_UNKNOWN") from None
        sock.settimeout(timeout)
        try:
            frame, anc, flags, _ = sock.recvmsg(
                256, socket.CMSG_SPACE(MAX_FDS * 4) + socket.CMSG_SPACE(12), getattr(socket, "MSG_CMSG_CLOEXEC", 0))
        except OSError:
            raise Stop("BND_RECEIVE_FAILED") from None
        bad = False
        creds = []
        for level, kind, value in anc:  # register fds immediately; BaseException-safe
            if level == socket.SOL_SOCKET and kind == socket.SCM_CREDENTIALS:
                if len(value) != 12:
                    bad = True
                else:
                    creds.append(struct.unpack("3i", value))
                continue
            if level != socket.SOL_SOCKET or kind != socket.SCM_RIGHTS or not value or len(value) % 4:
                bad = True
                continue
            ints = array.array("i")
            ints.frombytes(value)
            held.extend(ints)
        if bad:
            raise Stop("BND_CONTROL_INVALID")
        if len(creds) != 1:
            raise Stop("BND_CREDENTIALS_MISSING")
        pid, uid, _gid = creds[0]
        if pid <= 0:  # kernel stamps pid 0/overflow uid on messages queued before SO_PASSCRED
            raise Stop("BND_CREDENTIALS_MISSING")
        if uid != expected_peer_uid or (expected_peer_pid is not None and pid != expected_peer_pid):
            raise Stop("BND_PEER_REFUSED")
        if flags & (socket.MSG_TRUNC | socket.MSG_CTRUNC):
            raise Stop("BND_TRUNCATED")
        want = MAGIC + expected_nonce.encode() + b":" + expected_sha256[7:].encode()
        if not hmac.compare_digest(frame, want):
            raise Stop("BND_FRAME_MISMATCH")
        if len(held) != 1:
            raise Stop("BND_FD_COUNT_INVALID")
        _hash(held[0], expected_sha256)
        try:  # fresh open file description: own offset and flags
            private = os.open("/proc/self/fd/%d" % held[0], os.O_RDONLY | os.O_CLOEXEC)
        except OSError:
            raise Stop("BND_REOPEN_FAILED") from None
        try:
            _hash(private, expected_sha256)  # private fd is the one handed out
        except BaseException:
            os.close(private)
            raise
        return {"fd": private, "sender_pid": pid, "sender_uid": uid, "sha256": expected_sha256,
                "bootstrap_authenticated_here": False, "sponsor_authorized_here": False,
                "may_install": False, "may_apply": False}
    except (OSError, ValueError, OverflowError):
        raise Stop("BND_FD_UNSAFE") from None
    finally:
        for fd in held:
            try:
                os.close(fd)
            except OSError:
                pass
        try:
            sock.settimeout(prior)
        except OSError:
            pass


if __name__ == "__main__":
    raise SystemExit("QA525_BOUNDARY_RECV_LIBRARY_ONLY_NO_EXECUTION")
