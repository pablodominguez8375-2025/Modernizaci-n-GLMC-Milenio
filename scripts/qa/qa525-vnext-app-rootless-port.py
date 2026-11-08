#!/usr/bin/env python3
"""QA525 APP Sigstore verification PORT (UNPRIVILEGED, inactive, never GO).

This module wires a rootless gh attestation verify invocation to the vNext
dual-gate APP callback. It NEVER installs, authorizes root, or touches QA.
Its expected GH executable SHA, APP source SHA, and INNER ZIP SHA are
independent pins supplied by a trusted operator, NOT by this module.
The root-owned gh executable and host OS are a residual trust boundary.
The rootless GH CLI may need network trust-root lookup. Unit tests monkeypatch
subprocess.run. Runtime callers CANNOT supply a fake runner. On success,
take_verified_fd() transfers a one-use Linux F_SEAL_WRITE immutable memfd with
the exact verified bytes, not the original mutable ZIP path. This is not
authenticated root delivery, a bootstrap, or a GO authorization.
"""
from __future__ import annotations

import fcntl
import hashlib
import hmac
import os
from pathlib import Path
import re
import stat
import subprocess
import tempfile

GH = Path("/usr/bin/gh")
REPO = "pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio"
WORKFLOW = REPO + "/.github/workflows/qa-silo-rebuild.yml"
ISSUER = "https://token.actions.githubusercontent.com"
DIGEST = re.compile(r"sha256:[a-f0-9]{64}\Z")
SHA40 = re.compile(r"[a-f0-9]{40}\Z")
EXPECTED_FIELDS = frozenset({
    "repository", "signer_workflow", "oidc_issuer", "source_sha",
    "archive_sha256", "bundle_sha256",
})
MAX_BINARY = 128 * 1024 * 1024
MAX_ZIP = 2 * 1024 * 1024 * 1024
MAX_BUNDLE = 1_000_000
CHUNK = 1024 * 1024


class Stop(Exception):
    """Fixed public codes only; never echo path, data or command output."""


def _digest(raw):
    return "sha256:" + hashlib.sha256(raw).hexdigest()


def _check_digest(value):
    return type(value) is str and DIGEST.fullmatch(value) is not None


def _check_binary(pin):
    """Check root-owned, immovable CLI before invocation; root OS remains TCB."""
    if not _check_digest(pin):
        raise Stop("APP_PORT_BINARY_PIN_INVALID")
    for parent in (GH.parent.parent, GH.parent):
        try:
            st = parent.lstat()
        except OSError:
            raise Stop("APP_PORT_BINARY_UNAVAILABLE") from None
        if (not stat.S_ISDIR(st.st_mode) or stat.S_ISLNK(st.st_mode)
                or st.st_uid != 0 or st.st_mode & 0o022):
            raise Stop("APP_PORT_BINARY_PARENT_UNTRUSTED")
    try:
        before = GH.lstat()
        flags = os.O_RDONLY | os.O_NOFOLLOW | os.O_CLOEXEC
        fd = os.open(GH, flags)
    except OSError:
        raise Stop("APP_PORT_BINARY_UNAVAILABLE") from None
    try:
        after = os.fstat(fd)
        if (not stat.S_ISREG(after.st_mode)
                or (after.st_dev, after.st_ino, after.st_size, after.st_mtime_ns)
                != (before.st_dev, before.st_ino, before.st_size, before.st_mtime_ns)
                or after.st_uid != 0 or after.st_nlink != 1
                or after.st_mode & 0o022 or not after.st_mode & 0o111
                or not 0 < after.st_size <= MAX_BINARY):
            raise Stop("APP_PORT_BINARY_UNTRUSTED")
        h = hashlib.sha256()
        while True:
            blob = os.read(fd, CHUNK)
            if not blob:
                break
            h.update(blob)
        if not hmac.compare_digest("sha256:" + h.hexdigest(), pin):
            raise Stop("APP_PORT_BINARY_MISMATCH")
        end = os.fstat(fd)
        if (end.st_dev, end.st_ino, end.st_size, end.st_mtime_ns) != (
                after.st_dev, after.st_ino, after.st_size, after.st_mtime_ns):
            raise Stop("APP_PORT_BINARY_CHANGED")
    finally:
        os.close(fd)


def _open_inner(path):
    """Source must already be staged in a user-owned, mode-0700 private dir."""
    if not isinstance(path, Path) or not path.is_absolute():
        raise Stop("APP_PORT_SOURCE_INVALID")
    # O_NOFOLLOW applies to the final component only. Refuse symlinked
    # ancestors too; the private parent remains an authenticated boundary.
    if os.path.realpath(path) != os.fspath(path):
        raise Stop("APP_PORT_SOURCE_UNTRUSTED")
    try:
        parent = path.parent.lstat()
        source = path.lstat()
        fd = os.open(path, os.O_RDONLY | os.O_NOFOLLOW | os.O_CLOEXEC)
    except OSError:
        raise Stop("APP_PORT_SOURCE_UNTRUSTED") from None
    try:
        current = os.fstat(fd)
        me = os.geteuid()
        if (not stat.S_ISDIR(parent.st_mode) or parent.st_uid != me
                or parent.st_mode & 0o077 or stat.S_ISLNK(parent.st_mode)
                or not stat.S_ISREG(current.st_mode) or current.st_uid != me
                or current.st_nlink != 1 or current.st_mode & 0o077
                or not 0 < current.st_size <= MAX_ZIP
                or (source.st_dev, source.st_ino, source.st_size, source.st_mtime_ns)
                != (current.st_dev, current.st_ino, current.st_size, current.st_mtime_ns)):
            raise Stop("APP_PORT_SOURCE_UNTRUSTED")
        return fd, current
    except BaseException:
        os.close(fd)
        raise


def _seal_zip(source, output, identity, expected_sha):
    """Copy fd-once to own private file and bind hash + identity, no path reread."""
    h = hashlib.sha256()
    total = 0
    with open(output, "xb", buffering=0) as dst:
        os.chmod(output, 0o600)
        while True:
            part = os.read(source, CHUNK)
            if not part:
                break
            total += len(part)
            if total > MAX_ZIP:
                raise Stop("APP_PORT_SOURCE_TOO_LARGE")
            h.update(part)
            dst.write(part)
        os.fsync(dst.fileno())
    end = os.fstat(source)
    if (not total or total != identity.st_size
            or (end.st_dev, end.st_ino, end.st_size, end.st_mtime_ns)
            != (identity.st_dev, identity.st_ino, identity.st_size, identity.st_mtime_ns)
            or not hmac.compare_digest("sha256:" + h.hexdigest(), expected_sha)):
        raise Stop("APP_PORT_SOURCE_HASH_MISMATCH")


def _hash_private_file(path, limit):
    """Stream and verify a private regular file without following a link."""
    try:
        before = path.lstat()
        fd = os.open(path, os.O_RDONLY | os.O_NOFOLLOW | os.O_CLOEXEC)
    except OSError:
        raise Stop("APP_PORT_SEALED_FILE_UNTRUSTED") from None
    try:
        st = os.fstat(fd)
        if (not stat.S_ISREG(st.st_mode) or st.st_uid != os.geteuid()
                or st.st_nlink != 1 or st.st_mode & 0o077
                or not 0 < st.st_size <= limit
                or (before.st_dev, before.st_ino, before.st_size, before.st_mtime_ns)
                != (st.st_dev, st.st_ino, st.st_size, st.st_mtime_ns)):
            raise Stop("APP_PORT_SEALED_FILE_UNTRUSTED")
        total = 0
        h = hashlib.sha256()
        while True:
            b = os.read(fd, CHUNK)
            if not b:
                break
            total += len(b)
            if total > limit:
                raise Stop("APP_PORT_SEALED_FILE_UNTRUSTED")
            h.update(b)
        final = os.fstat(fd)
        if (total != st.st_size
                or (final.st_dev, final.st_ino, final.st_size, final.st_mtime_ns)
                != (st.st_dev, st.st_ino, st.st_size, st.st_mtime_ns)):
            raise Stop("APP_PORT_SEALED_FILE_CHANGED")
        return "sha256:" + h.hexdigest()
    finally:
        os.close(fd)


def _immutable_fd_from_verified_zip(path, expected_sha):
    """Rebind verified ZIP *bytes* to an immutable Linux memfd (never a path).

    The FD remains open after the temporary gh verification path vanishes.
    This is a pre-root handoff primitive, not a bootstrap or physical GO.
    Fail closed on kernels without F_SEAL_WRITE support.
    """
    required = ("MFD_CLOEXEC", "MFD_ALLOW_SEALING")
    if (not hasattr(os, "memfd_create")
            or any(not hasattr(os, name) for name in required)
            or any(not hasattr(fcntl, name) for name in (
                "F_ADD_SEALS", "F_GET_SEALS", "F_SEAL_WRITE", "F_SEAL_SEAL",
                "F_SEAL_SHRINK", "F_SEAL_GROW"))):
        raise Stop("APP_PORT_IMMUTABLE_FD_UNAVAILABLE")
    source, identity = _open_inner(path)
    frozen = None
    try:
        frozen = os.memfd_create("qa525-verified-inner-zip",
                                 os.MFD_CLOEXEC | os.MFD_ALLOW_SEALING)
        sha = hashlib.sha256()
        size = 0
        while True:
            chunk = os.read(source, CHUNK)
            if not chunk:
                break
            size += len(chunk)
            if size > MAX_ZIP:
                raise Stop("APP_PORT_IMMUTABLE_FD_INVALID")
            sha.update(chunk)
            view = memoryview(chunk)
            while view:
                written = os.write(frozen, view)
                if written <= 0:
                    raise Stop("APP_PORT_IMMUTABLE_FD_INVALID")
                view = view[written:]
        end = os.fstat(source)
        if (not 0 < size == identity.st_size
                or (identity.st_dev, identity.st_ino, identity.st_size,
                    identity.st_mtime_ns) !=
                   (end.st_dev, end.st_ino, end.st_size, end.st_mtime_ns)
                or not hmac.compare_digest("sha256:" + sha.hexdigest(), expected_sha)):
            raise Stop("APP_PORT_IMMUTABLE_FD_INVALID")
        os.fsync(frozen)
        seals = (fcntl.F_SEAL_WRITE | fcntl.F_SEAL_SEAL |
                 fcntl.F_SEAL_SHRINK | fcntl.F_SEAL_GROW)
        fcntl.fcntl(frozen, fcntl.F_ADD_SEALS, seals)
        if fcntl.fcntl(frozen, fcntl.F_GET_SEALS) & seals != seals:
            raise Stop("APP_PORT_IMMUTABLE_FD_INVALID")
        os.lseek(frozen, 0, os.SEEK_SET)
        result = frozen
        frozen = None
        return result
    except Stop:
        raise
    except OSError:
        raise Stop("APP_PORT_IMMUTABLE_FD_UNAVAILABLE") from None
    finally:
        os.close(source)
        if frozen is not None:
            os.close(frozen)


def _recheck_immutable_fd(fd, expected_sha):
    """Check sealed, immutable memfd content without changing its file offset."""
    try:
        st = os.fstat(fd)
        seals = (fcntl.F_SEAL_WRITE | fcntl.F_SEAL_SEAL |
                 fcntl.F_SEAL_SHRINK | fcntl.F_SEAL_GROW)
        if (not stat.S_ISREG(st.st_mode) or not 0 < st.st_size <= MAX_ZIP
                or fcntl.fcntl(fd, fcntl.F_GET_SEALS) & seals != seals):
            raise Stop("APP_PORT_IMMUTABLE_FD_INVALID")
        h = hashlib.sha256()
        offset = 0
        while offset < st.st_size:
            data = os.pread(fd, min(CHUNK, st.st_size - offset), offset)
            if not data:
                raise Stop("APP_PORT_IMMUTABLE_FD_INVALID")
            h.update(data)
            offset += len(data)
        if not hmac.compare_digest("sha256:" + h.hexdigest(), expected_sha):
            raise Stop("APP_PORT_IMMUTABLE_FD_INVALID")
    except OSError:
        raise Stop("APP_PORT_IMMUTABLE_FD_INVALID") from None


def verifier_for(*, signed_inner_zip: Path, trusted_gh_sha256: str,
                 expected_source_sha: str, expected_inner_sha256: str,
                 expected_bundle_sha256: str):
    """Return verify_app(bundle_bytes,challenge) compatible with dual.verify_dual.

    The caller must supply authentic, independently obtained source/ZIP/bundle
    and GH binary pins, and keep the rootless staged ZIP in a private folder.
    Tests may monkeypatch subprocess.run in their own process, but a caller
    cannot request an injected fake verifier through this public API.
    """
    if os.geteuid() == 0:
        raise Stop("APP_PORT_ROOT_FORBIDDEN")
    if (not isinstance(signed_inner_zip, Path)
            or type(expected_source_sha) is not str
            or SHA40.fullmatch(expected_source_sha) is None
            or not _check_digest(expected_inner_sha256)
            or not _check_digest(expected_bundle_sha256)
            or not _check_digest(trusted_gh_sha256)):
        raise Stop("APP_PORT_PINS_INVALID")

    held = {"fd": None, "verified_once": False}

    def take_verified_fd():
        """Transfer the exact immutable bytes once; CALLER must close the fd.

        A trusted consumer must bind these bytes/digest to its separately
        authenticated root bootstrap. This library NEVER performs that step.
        """
        fd = held["fd"]
        if fd is None:
            raise Stop("APP_PORT_VERIFIED_FD_UNAVAILABLE")
        _recheck_immutable_fd(fd, expected_inner_sha256)
        held["fd"] = None
        return fd, expected_inner_sha256

    def close_verified_fd():
        fd = held["fd"]
        held["fd"] = None
        if fd is not None:
            os.close(fd)

    def verify_app(bundle_bytes, challenge):
        if held["verified_once"]:
            raise Stop("APP_PORT_ALREADY_VERIFIED")
        if (type(bundle_bytes) is not bytes or not 0 < len(bundle_bytes) <= MAX_BUNDLE
                or type(challenge) is not dict or set(challenge) != EXPECTED_FIELDS):
            raise Stop("APP_PORT_INPUT_INVALID")
        wanted = {
            "repository": REPO, "signer_workflow": WORKFLOW,
            "oidc_issuer": ISSUER, "source_sha": expected_source_sha,
            "archive_sha256": expected_inner_sha256,
            "bundle_sha256": expected_bundle_sha256,
        }
        if (challenge != wanted
                or not hmac.compare_digest(_digest(bundle_bytes), expected_bundle_sha256)):
            raise Stop("APP_PORT_CHALLENGE_MISMATCH")

        # GH and source are checked on every invocation. No GH_TOKEN or
        # privileged process; namespace and workflow are fixed.
        _check_binary(trusted_gh_sha256)
        fd, source_identity = _open_inner(signed_inner_zip)
        try:
            with tempfile.TemporaryDirectory(prefix="qa525-app-rootless-") as tmp:
                temp = Path(tmp)
                signed = temp / "signed-inner.zip"
                _seal_zip(fd, signed, source_identity, expected_inner_sha256)
                bundle = temp / "attestation.sigstore.json"
                with open(bundle, "xb") as stream:
                    os.chmod(bundle, 0o600)
                    stream.write(bundle_bytes)
                    stream.flush()
                    os.fsync(stream.fileno())
                safe_env = {
                    "PATH": "/usr/bin:/bin", "LANG": "C",
                    "HOME": str(temp),
                    "GH_CONFIG_DIR": str(temp / "gh-config"),
                    "GH_PROMPT_DISABLED": "1", "NO_COLOR": "1",
                }
                (temp / "gh-config").mkdir(mode=0o700)
                argv = [
                    str(GH), "attestation", "verify", str(signed),
                    "--repo", REPO, "--bundle", str(bundle),
                    "--signer-workflow", WORKFLOW,
                    "--source-digest", expected_source_sha,
                    "--cert-oidc-issuer", ISSUER,
                    "--predicate-type", "https://slsa.dev/provenance/v1",
                    "--deny-self-hosted-runners", "--format", "json",
                ]
                try:
                    done = subprocess.run(argv, env=safe_env, cwd=str(temp),
                                  stdout=subprocess.DEVNULL,
                                  stderr=subprocess.DEVNULL, timeout=120,
                                  check=False)
                except (OSError, subprocess.TimeoutExpired, subprocess.CalledProcessError):
                    raise Stop("APP_PORT_SIGSTORE_DENIED") from None
                if (getattr(done, "returncode", None) != 0
                        or _hash_private_file(signed, MAX_ZIP) != expected_inner_sha256
                        or _hash_private_file(bundle, MAX_BUNDLE) != expected_bundle_sha256):
                    raise Stop("APP_PORT_SIGSTORE_DENIED")
                _check_binary(trusted_gh_sha256)
                # The result may never be used via the old source pathname.
                # Keep immutable content in a sealed memfd, not a temp path
                # that disappears or could be replaced by the same user.
                kept_fd = _immutable_fd_from_verified_zip(signed, expected_inner_sha256)
                held["fd"] = kept_fd
                held["verified_once"] = True
                return True
        finally:
            os.close(fd)
    verify_app.take_verified_fd = take_verified_fd
    verify_app.close_verified_fd = close_verified_fd
    return verify_app


if __name__ == "__main__":
    raise SystemExit("QA525_ROOTLESS_APP_PORT_LIBRARY_ONLY_NO_ROOT_NO_GO")
