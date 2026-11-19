#!/usr/bin/env python3
"""QA525 durable nonce SPEND STORE with trusted-root validation and authenticated GO binding (stdlib + ssh-keygen).

Off-host reference implementation for the gaps ChatGPT listed on #1041 (no host adapter, no glmdch, nothing here
mutates a real system):

  G1  Trusted root: the store directory is opened component by component from "/" with openat (dir_fd),
      O_NOFOLLOW|O_DIRECTORY; every component must be owned by root or the effective uid and NOT group/other
      writable; the final directory must be owned by the effective uid with mode 0700. Anything else fails closed.
  G2  Authentication BEFORE spend: `spend_authorized` verifies an ssh-ed25519 signature (ssh-keygen -Y verify,
      fixed namespace) over a strict, canonical GO message, then binds nonce/release/plan/purpose/expiry to what the
      executor computed. Any failure raises GoRejected and creates NO marker.
  G3/G4  Crash windows: the marker is created O_EXCL|O_NOFOLLOW under an exclusive flock. A marker that exists in ANY
      form (empty, partial, invalid, symlink, directory) means SPENT; `state()` reports SPENT_AMBIGUOUS until a human
      reconciles. The store never executes a nonce twice and never "repairs" a marker by itself.

It is NOT physical evidence and NOT certified for any host. Residual limits are listed in LIMITS.
"""
import fcntl
import json
import os
import re
import stat
import subprocess
import tempfile
import time

NAMESPACE = "qa525-go@glmch"
PURPOSE = "qa525-vnext-spend"
MAX_TTL_SECONDS = 24 * 3600
HEX64 = re.compile(r"^[0-9a-f]{64}$")
GO_KEYS = {"v", "nonce", "release_sha", "plan_sha256", "purpose", "not_after"}
LIMITS = ("no power-loss injection; depends on POSIX openat/flock/fsync semantics of the filesystem; "
          "a root attacker on the same host can defeat any on-disk store; ssh-keygen verification trusts the "
          "allowed_signers file, which must be provisioned and protected out of band")


class StoreError(Exception):
    """Trusted-root or filesystem validation failed (fail closed)."""


class GoRejected(Exception):
    """The GO could not be authenticated or does not match the plan (no nonce is spent)."""


def _check_owner_mode(st, euid, *, final, what):
    if st.st_uid not in (0, euid):
        raise StoreError(f"{what}: unexpected owner uid {st.st_uid}")
    if st.st_mode & 0o022:
        raise StoreError(f"{what}: group/other writable (mode {stat.S_IMODE(st.st_mode):o})")
    if final:
        if st.st_uid != euid:
            raise StoreError(f"{what}: final directory must be owned by the effective uid")
        if st.st_mode & 0o077:
            raise StoreError(f"{what}: final directory must be mode 0700 (is {stat.S_IMODE(st.st_mode):o})")


def open_trusted_dir(path):
    """Return an O_DIRECTORY fd for `path` after validating EVERY component. Caller closes it."""
    if not isinstance(path, str) or not path.startswith("/"):
        raise StoreError("path must be absolute")
    parts = [p for p in path.split("/") if p]
    if any(p in (".", "..") for p in parts):
        raise StoreError("path must not contain . or ..")
    euid = os.geteuid()
    cur = os.open("/", os.O_RDONLY | os.O_DIRECTORY)
    try:
        _check_owner_mode(os.fstat(cur), euid, final=not parts, what="/")
        for i, name in enumerate(parts):
            try:
                nxt = os.open(name, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW, dir_fd=cur)
            except OSError as exc:
                raise StoreError(f"cannot open component {name!r}: {exc.strerror}") from None
            os.close(cur)
            cur = nxt
            _check_owner_mode(os.fstat(cur), euid, final=(i == len(parts) - 1), what=name)
        return cur
    except BaseException:
        os.close(cur)
        raise


def read_trusted_file(path, max_bytes=65536):
    """Read a regular file whose parent chain is trusted (no symlinks; owner root/euid; not group/other writable)."""
    parent, _, name = path.rpartition("/")
    if not name or not parent:
        raise StoreError("bad file path")
    euid = os.geteuid()
    parts = [p for p in parent.split("/") if p]
    if not path.startswith("/") or any(p in (".", "..") for p in parts):
        raise StoreError("path must be absolute without . or ..")
    cur = os.open("/", os.O_RDONLY | os.O_DIRECTORY)
    try:
        _check_owner_mode(os.fstat(cur), euid, final=False, what="/")
        for p in parts:
            nxt = os.open(p, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW, dir_fd=cur)
            os.close(cur); cur = nxt
            _check_owner_mode(os.fstat(cur), euid, final=False, what=p)
        fd = os.open(name, os.O_RDONLY | os.O_NOFOLLOW | os.O_NONBLOCK, dir_fd=cur)   # NONBLOCK: a FIFO must not hang us
    except OSError as exc:
        raise StoreError(f"cannot open trusted file: {exc.strerror}") from None
    finally:
        os.close(cur)   # the directory fd is closed on EVERY path (success, error, exception)
    try:   # the descriptor is closed on EVERY path below, including a failing os.fdopen (e.g. a directory)
        st = os.fstat(fd)
        if not stat.S_ISREG(st.st_mode):
            raise StoreError("not a regular file")
        _check_owner_mode(st, euid, final=False, what=name)
        handle = os.fdopen(fd, "rb")
    except BaseException:
        os.close(fd)
        raise
    with handle:
        data = handle.read(max_bytes + 1)
    if len(data) > max_bytes:
        raise StoreError("file too large")
    return data


class SpendStore:
    SPENT, REPLAY = "SPENT", "REPLAY"
    UNSPENT, COMPLETE, AMBIGUOUS = "UNSPENT", "SPENT_COMPLETE", "SPENT_AMBIGUOUS"

    def __init__(self, directory):
        self.fd = open_trusted_dir(directory)

    def close(self):
        os.close(self.fd)

    def __enter__(self):
        return self

    def __exit__(self, *exc):
        self.close()

    @staticmethod
    def _check_ids(*ids):
        if not all(isinstance(x, str) and HEX64.match(x) for x in ids):
            raise ValueError("expected lowercase sha256 identifiers")

    def _lock(self):
        fd = os.open(".lock", os.O_RDWR | os.O_CREAT | os.O_NOFOLLOW, 0o600, dir_fd=self.fd)
        st = os.fstat(fd)
        if not stat.S_ISREG(st.st_mode) or st.st_uid != os.geteuid():
            os.close(fd)
            raise StoreError("lock file is not a regular file owned by the effective uid")
        fcntl.flock(fd, fcntl.LOCK_EX)
        return fd

    def spend(self, nonce, release, plan, _fault=None):
        """Atomic across processes, durable before return. Returns SPENT once, REPLAY forever after (any marker form)."""
        self._check_ids(nonce, release, plan)
        lock = self._lock()
        try:
            name = "spent-" + nonce
            try:
                os.stat(name, dir_fd=self.fd, follow_symlinks=False)
                return self.REPLAY
            except FileNotFoundError:
                pass
            mfd = os.open(name, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, 0o600, dir_fd=self.fd)
            if _fault == "after_create":
                os._exit(75)
            with os.fdopen(mfd, "wb") as out:
                data = json.dumps({"nonce": nonce, "release_sha": release, "plan_sha256": plan}, sort_keys=True).encode() + b"\n"
                out.write(data[: len(data) // 2] if _fault == "partial_write" else data)
                out.flush()
                if _fault in ("partial_write", "after_write"):
                    os._exit(75)
                os.fsync(out.fileno())
                if _fault == "after_fsync":
                    os._exit(75)
            os.fsync(self.fd)
            if _fault == "after_dirsync":
                os._exit(75)
            return self.SPENT
        finally:
            os.close(lock)

    def state(self, nonce, release=None, plan=None):
        self._check_ids(nonce)
        name = "spent-" + nonce
        try:
            st = os.stat(name, dir_fd=self.fd, follow_symlinks=False)
        except FileNotFoundError:
            return self.UNSPENT
        if not stat.S_ISREG(st.st_mode):
            return self.AMBIGUOUS
        try:
            fd = os.open(name, os.O_RDONLY | os.O_NOFOLLOW, dir_fd=self.fd)
            with os.fdopen(fd, "rb") as handle:
                raw = handle.read(4096)
            rec = json.loads(raw)
        except (OSError, ValueError):
            return self.AMBIGUOUS
        ok = (isinstance(rec, dict) and set(rec) == {"nonce", "release_sha", "plan_sha256"} and rec["nonce"] == nonce
              and raw.endswith(b"\n") and all(isinstance(rec[k], str) and HEX64.match(rec[k]) for k in rec))
        if ok and release is not None and rec["release_sha"] != release:
            ok = False
        if ok and plan is not None and rec["plan_sha256"] != plan:
            ok = False
        return self.COMPLETE if ok else self.AMBIGUOUS


def canonical_go(go):
    """THE canonical GO encoding: UTF-8 JSON, keys sorted, separators (",", ":"), ASCII-only escapes, no trailing newline."""
    return json.dumps(go, sort_keys=True, separators=(",", ":"), ensure_ascii=True, allow_nan=False).encode("ascii")


def _no_duplicate_keys(pairs):
    seen = {}
    for key, value in pairs:
        if key in seen:
            raise GoRejected(f"GO has a duplicate key: {key!r}")
        seen[key] = value
    return seen


def _reject_constant(name):
    raise GoRejected(f"GO contains the non-standard JSON constant {name}")


def parse_go(message):
    """Strict canonical GO: a JSON object with exactly GO_KEYS, validated types, NO duplicate keys, and bytes that are
    EXACTLY canonical_go(parsed) (so a signature can only ever cover one representation); returns the dict or raises GoRejected."""
    try:
        text = message.decode("utf-8")
        go = json.loads(text, object_pairs_hook=_no_duplicate_keys, parse_constant=_reject_constant)
    except GoRejected:
        raise
    except (ValueError, UnicodeDecodeError, RecursionError):
        raise GoRejected("GO is not valid UTF-8 JSON") from None
    if not isinstance(go, dict) or set(go) != GO_KEYS:
        raise GoRejected("GO keys are not exactly the expected set")
    if go["v"] != 1 or go["purpose"] != PURPOSE:
        raise GoRejected("GO version/purpose mismatch")
    if not all(isinstance(go[k], str) and HEX64.match(go[k]) for k in ("nonce", "release_sha", "plan_sha256")):
        raise GoRejected("GO identifiers are not lowercase sha256")
    if not isinstance(go["not_after"], int) or isinstance(go["not_after"], bool):
        raise GoRejected("GO not_after must be an integer epoch")
    if canonical_go(go) != message:
        raise GoRejected("GO is not in the canonical representation")
    return go


def verify_signature(message, signature, allowed_signers_path, principal):
    """ssh-keygen -Y verify over `message` with a fixed namespace; allowed_signers must come from a trusted path."""
    signers = read_trusted_file(allowed_signers_path)
    if not re.fullmatch(r"[A-Za-z0-9._@-]{1,64}", principal or ""):
        raise GoRejected("bad principal")
    with tempfile.TemporaryDirectory(prefix="qa525-verify-") as tmp:   # mkdtemp is 0700, private to this uid
        sp, gp = os.path.join(tmp, "allowed_signers"), os.path.join(tmp, "go.sig")
        for path, data in ((sp, signers), (gp, signature)):
            fd = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, 0o600)
            with os.fdopen(fd, "wb") as handle:
                handle.write(data)
        proc = subprocess.run(["ssh-keygen", "-Y", "verify", "-f", sp, "-I", principal, "-n", NAMESPACE, "-s", gp],
                              input=message, capture_output=True, timeout=20)
    if proc.returncode != 0:
        raise GoRejected("signature verification failed")


def spend_authorized(store, go_message, go_signature, allowed_signers_path, principal, release, plan, now=None, _fault=None):
    """Order is fixed: authenticate -> parse -> bind to the executor's release/plan -> check expiry -> spend."""
    verify_signature(go_message, go_signature, allowed_signers_path, principal)
    go = parse_go(go_message)
    now = int(time.time()) if now is None else now
    if go["release_sha"] != release:
        raise GoRejected("GO release does not match the release to deploy")
    if go["plan_sha256"] != plan:
        raise GoRejected("GO plan does not match the computed plan")
    if not (now < go["not_after"] <= now + MAX_TTL_SECONDS):
        raise GoRejected("GO expired or TTL too long")
    return store.spend(go["nonce"], go["release_sha"], go["plan_sha256"], _fault=_fault)
