#!/usr/bin/env python3
"""QA525 ROOT authenticity port: OPS v3 receipt + APP Sigstore attestation. INACTIVE library; no authority.

The launcher treats the OPS v3 receipt and the APP subject as CLAIMS until this port says `True`. The port
never trusts a `True`, a JSON, or a fingerprint produced by an unprivileged process: ROOT holds every input
(already pinned by digest in the Sponsor-signed release config) and ROOT judges the result of two PINNED
binaries it executes itself.

  OPS  `verify_ops_receipt` (strict v3) over the receipt bytes, using the pinned `ssh-keygen` port of
       `qa525-vnext-root-go-verify.py` in the DISTINCT OPS namespace; the receipt must bind the same
       `app_archive_sha256` and `ops_source_sha` the signed GO carries.
  APP  the pinned `gh attestation verify`, OFFLINE (`--bundle` + `--custom-trusted-root`, both pinned), run
       as an UNPRIVILEGED uid with an empty environment, over the exact archive bytes whose sha256 equals
       the GO's `app_archive_sha256`. The inputs reach the child only as sealed memfds (/proc/self/fd/N), so
       no path can be swapped and the child needs no filesystem access. Repository, signer workflow, OIDC
       issuer, source digest and predicate type are constants / the GO's pinned source, never the sender's.

It returns `True` only when BOTH halves pass and every identity matches the GO claims; any other outcome is
an exception (fixed code) that the launcher maps to `LAUNCH_AUTHENTICITY_DENIED`. It is not a GO, a nonce, a
bootstrap or an installer, and it does NOT prove where the pinned fingerprint or trusted root came from:
that anchor is the Sponsor's signature over the release config.
"""
import hashlib
import hmac
import json
import fcntl
import os
import re
import stat
import subprocess
import tempfile

REPO = "pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio"
WORKFLOW = REPO + "/.github/workflows/qa-silo-rebuild.yml"
ISSUER = "https://token.actions.githubusercontent.com"
PREDICATE = "https://slsa.dev/provenance/v1"
SHA256 = re.compile(r"sha256:[a-f0-9]{64}\Z")
SHA40 = re.compile(r"[a-f0-9]{40}\Z")
# `verify_ops_receipt` insists on a non-empty set of APP fingerprints distinct from the OPS key. Sigstore
# identities have no SSH fingerprint, so the set holds one inert sentinel that no real key can equal.
APP_FP_SENTINEL = "SHA256:" + "A" * 43
MAX_GH = 128 * 1024 * 1024
MAX_ZIP = 512 * 1024 * 1024
MAX_BUNDLE = 1_000_000
MAX_ROOT = 262144
MAX_OUTPUT = 4 * 1024 * 1024
TIMEOUT = 120
CHUNK = 1024 * 1024
# Linux ABI values (fcntl exposes them only from Python 3.13): F_ADD_SEALS and the four seals.
F_ADD_SEALS = getattr(fcntl, "F_ADD_SEALS", 1033)
SEAL_MASK = (getattr(fcntl, "F_SEAL_SEAL", 0x1) | getattr(fcntl, "F_SEAL_SHRINK", 0x2)
             | getattr(fcntl, "F_SEAL_GROW", 0x4) | getattr(fcntl, "F_SEAL_WRITE", 0x8))


class Stop(Exception):
    """Fixed codes only; never paths, digests, bundle text or child output."""


def _dup_free(pairs):
    out = {}
    for key, value in pairs:
        if key in out:
            raise ValueError
        out[key] = value
    return out


def _doc(raw, code):
    try:
        value = json.loads(raw.decode("utf-8"), object_pairs_hook=_dup_free)
    except (ValueError, UnicodeDecodeError, RecursionError):
        raise Stop(code) from None
    if type(value) is not dict:
        raise Stop(code)
    return value


def _digest_ok(value):
    return type(value) is str and SHA256.fullmatch(value) is not None


def ops_check_for(*, ops_signature, ops_port, signers, ops_fingerprint, signature, policy_hash):
    """`check(ops_bytes, app_archive_sha256) -> receipt dict` for the pinned OPS key and namespace.

    `ops_signature` is the loaded `qa525-vnext-ops-signature` module, `ops_port` the pinned verifier port
    from `go-verify.ops_port_for`. `signature` is the Sponsor-pinned detached signature file."""
    if type(signature) is not bytes or not 0 < len(signature) <= 4096 or not _digest_ok(policy_hash):
        raise Stop("AUTH_OPS_INPUT_INVALID")

    def check(ops_bytes, app_archive_sha256):
        if type(ops_bytes) is not bytes or not _digest_ok(app_archive_sha256):
            raise Stop("AUTH_OPS_INPUT_INVALID")
        try:
            info = ops_signature.verify_ops_receipt(
                receipt=ops_bytes, signature=signature, allowed_signers=signers,
                ops_fingerprint=ops_fingerprint, app_fingerprints=frozenset({APP_FP_SENTINEL}),
                policy_hash=policy_hash, verifier=ops_port, expected_app_archive_sha256=app_archive_sha256)
        except Exception:  # noqa: BLE001 - the contract's own Stop, a port failure: all the same verdict
            raise Stop("AUTH_OPS_DENIED") from None
        if (type(info) is not dict or info.get("signature_verified_here") is not True
                or info.get("app_archive_bound") is not True or info.get("may_apply") is not False
                or type(info.get("receipt")) is not dict):
            raise Stop("AUTH_OPS_DENIED")
        return info["receipt"]

    return check


def _memfd(name, payload_reader, limit):
    """Sealed memfd holding exactly the bytes `payload_reader()` yields (an iterator of chunks), size-capped."""
    required = ("memfd_create", "MFD_CLOEXEC", "MFD_ALLOW_SEALING")
    if any(not hasattr(os, n) for n in required):
        raise Stop("AUTH_MEMFD_UNAVAILABLE")
    fd = None
    try:
        fd = os.memfd_create(name, os.MFD_CLOEXEC | os.MFD_ALLOW_SEALING)
        digest, total = hashlib.sha256(), 0
        for chunk in payload_reader():
            total += len(chunk)
            if total > limit:
                raise Stop("AUTH_INPUT_TOO_LARGE")
            digest.update(chunk)
            view = memoryview(chunk)
            while view:
                written = os.write(fd, view)
                if written <= 0:
                    raise Stop("AUTH_MEMFD_UNAVAILABLE")
                view = view[written:]
        if not total:
            raise Stop("AUTH_INPUT_EMPTY")
        fcntl.fcntl(fd, F_ADD_SEALS, SEAL_MASK)
        result, fd = (fd, "sha256:" + digest.hexdigest()), None
        return result
    except OSError:
        raise Stop("AUTH_MEMFD_UNAVAILABLE") from None
    finally:
        if fd is not None:
            os.close(fd)


def _chunks_of_bytes(data):
    def gen():
        yield data
    return gen


def _chunks_of_fd(fd, expected_size):
    def gen():
        seen = 0
        while True:
            block = os.read(fd, CHUNK)
            if not block:
                break
            seen += len(block)
            yield block
        if seen != expected_size:
            raise Stop("AUTH_ARCHIVE_CHANGED")
    return gen


def app_check_for(*, open_binary, gh_path, gh_sha256, trusted_root, bundle, open_archive, drop_uid, drop_gid,
                  required_euid=0, os_uid=0):
    """`check(app_archive_sha256, app_source_sha) -> True` running the pinned `gh attestation verify` offline.

    open_binary   `go-verify.open_pinned_binary` (judged and hashed on the fd that is then executed);
    trusted_root  the Sponsor-pinned `trusted_root.jsonl` bytes; bundle the pinned Sigstore bundle bytes;
    open_archive  `() -> (fd, size)` of the root-owned archive file, opened by the launcher without links;
    drop_uid/gid  the unprivileged identity the verifier runs as (never 0)."""
    for value in (drop_uid, drop_gid):
        if type(value) is not int or isinstance(value, bool) or value <= 0:
            raise Stop("AUTH_DROP_IDENTITY_INVALID")
    if (type(trusted_root) is not bytes or not 0 < len(trusted_root) <= MAX_ROOT
            or type(bundle) is not bytes or not 0 < len(bundle) <= MAX_BUNDLE
            or not callable(open_binary) or not callable(open_archive) or not _digest_ok(gh_sha256)):
        raise Stop("AUTH_APP_INPUT_INVALID")
    if os.geteuid() != required_euid:
        raise Stop("AUTH_ROOT_REQUIRED")

    def check(app_archive_sha256, app_source_sha):
        if not _digest_ok(app_archive_sha256) or type(app_source_sha) is not str or SHA40.fullmatch(app_source_sha) is None:
            raise Stop("AUTH_APP_INPUT_INVALID")
        if os.geteuid() != required_euid:
            raise Stop("AUTH_ROOT_REQUIRED")
        held = []
        gh_fd = None
        try:
            try:
                archive_fd, size = open_archive()
            except Stop:
                raise
            except Exception:  # noqa: BLE001
                raise Stop("AUTH_ARCHIVE_UNAVAILABLE") from None
            try:
                zip_fd, got = _memfd("qa525-app-archive", _chunks_of_fd(archive_fd, size), MAX_ZIP)
            finally:
                os.close(archive_fd)
            held.append(zip_fd)
            if not hmac.compare_digest(got, app_archive_sha256):
                raise Stop("AUTH_ARCHIVE_DIGEST_MISMATCH")  # the bytes are not the release the GO signed
            bundle_fd, _ = _memfd("qa525-app-bundle", _chunks_of_bytes(bundle), MAX_BUNDLE)
            held.append(bundle_fd)
            root_fd, _ = _memfd("qa525-trusted-root", _chunks_of_bytes(trusted_root), MAX_ROOT)
            held.append(root_fd)
            gh_fd, before = open_binary(gh_path, os_uid, gh_sha256, MAX_GH)
            argv = ["gh", "attestation", "verify", "/proc/self/fd/%d" % zip_fd,
                    "--repo", REPO, "--bundle", "/proc/self/fd/%d" % bundle_fd,
                    "--custom-trusted-root", "/proc/self/fd/%d" % root_fd,
                    "--signer-workflow", WORKFLOW, "--source-digest", app_source_sha,
                    "--cert-oidc-issuer", ISSUER, "--predicate-type", PREDICATE,
                    "--deny-self-hosted-runners", "--format", "json"]
            env = {"PATH": "/nonexistent", "HOME": "/nonexistent", "GH_CONFIG_DIR": "/nonexistent",
                   "GH_PROMPT_DISABLED": "1", "GH_NO_UPDATE_NOTIFIER": "1", "NO_COLOR": "1", "LANG": "C"}
            # Production runs as root (required_euid == 0) and drop_uid > 0, so the child ALWAYS drops. Only when
            # the caller already is that exact unprivileged identity (tests as a normal user) there is nothing to drop.
            same = (drop_uid, drop_gid) == (os.geteuid(), os.getegid())
            drop = {} if same else {"user": drop_uid, "group": drop_gid, "extra_groups": []}
            try:
                with tempfile.TemporaryFile(mode="w+b") as out:
                    done = subprocess.run(
                        argv, executable="/proc/self/fd/%d" % gh_fd,
                        pass_fds=(gh_fd, zip_fd, bundle_fd, root_fd), env=env, cwd="/",
                        stdin=subprocess.DEVNULL, stdout=out, stderr=subprocess.DEVNULL, shell=False,
                        close_fds=True, timeout=TIMEOUT, check=False, start_new_session=True, **drop)
                    out.seek(0, os.SEEK_END)
                    if out.tell() > MAX_OUTPUT:
                        raise Stop("AUTH_APP_DENIED")
                    out.seek(0)
                    printed = out.read()
            except (OSError, ValueError, subprocess.SubprocessError):
                raise Stop("AUTH_APP_DENIED") from None
            end = os.fstat(gh_fd)
            if (end.st_dev, end.st_ino, end.st_size, end.st_mtime_ns, end.st_ctime_ns) != before:
                raise Stop("AUTH_APP_DENIED")
            if done.returncode != 0:
                raise Stop("AUTH_APP_DENIED")
            try:
                verified = json.loads(printed.decode("utf-8"))
            except (ValueError, UnicodeDecodeError, RecursionError):
                raise Stop("AUTH_APP_DENIED") from None
            if type(verified) is not list or not verified:
                raise Stop("AUTH_APP_DENIED")
            return True
        finally:
            for fd in held:
                try:
                    os.close(fd)
                except OSError:
                    pass
            if gh_fd is not None:
                try:
                    os.close(gh_fd)
                except OSError:
                    pass

    return check


def port_for(*, ops_check, app_check):
    """The launcher's `port(ops_bytes=, app_subject_bytes=, claims=) -> True` (anything else raises)."""
    if not callable(ops_check) or not callable(app_check):
        raise Stop("AUTH_PORT_INVALID")

    def verify(*, ops_bytes, app_subject_bytes, claims):
        if (type(ops_bytes) is not bytes or type(app_subject_bytes) is not bytes or type(claims) is not dict):
            raise Stop("AUTH_INPUT_INVALID")
        archive, ops_src, app_src = (claims.get("app_archive_sha256"), claims.get("ops_source_sha"),
                                     claims.get("app_source_sha"))
        if (not _digest_ok(archive) or type(ops_src) is not str or SHA40.fullmatch(ops_src) is None
                or type(app_src) is not str or SHA40.fullmatch(app_src) is None):
            raise Stop("AUTH_CLAIMS_INVALID")
        receipt = ops_check(ops_bytes, archive)
        if receipt.get("ops_source_sha") != ops_src or receipt.get("app_archive_sha256") != archive:
            raise Stop("AUTH_OPS_CLAIMS_MISMATCH")
        subject = _doc(app_subject_bytes, "AUTH_SUBJECT_INVALID")
        if (subject.get("source_sha") != app_src or subject.get("subject_sha256") != archive
                or subject.get("signer_workflow") != WORKFLOW or subject.get("oidc_issuer") != ISSUER):
            raise Stop("AUTH_APP_CLAIMS_MISMATCH")
        if app_check(archive, app_src) is not True:
            raise Stop("AUTH_APP_DENIED")
        return True

    return verify


def port_from_release(*, ops_signature, go_verify, keygen, gh, policy_hash, authenticity, ops_signature_bytes,
                      app_bundle, trusted_root, open_archive, temp_root, go_fingerprint, required_euid=0,
                      os_uid=0):
    """Assemble the launcher's port from the Sponsor-pinned release material (everything already digest-checked).

    keygen / gh   `(path, "sha256:<hex>")` pairs from the bootstrap's verified `helpers`;
    authenticity  the config's `authenticity` section (OPS signers for the OPS namespace, drop identity);
    go_fingerprint the Sponsor key fingerprint, which also pins the OPS-namespace signers line.
    Nothing here is read from the host or the sender."""
    if type(authenticity) is not dict or type(keygen) is not tuple or type(gh) is not tuple:
        raise Stop("AUTH_RELEASE_INVALID")
    try:
        ops_port = go_verify.ops_port_for(
            keygen_path=keygen[0], trusted_binary_sha256=keygen[1],
            allowed_signers=authenticity["ops_allowed_signers"], expected_fingerprint=go_fingerprint,
            temp_root=temp_root, required_euid=required_euid, os_uid=os_uid)
    except KeyError:
        raise Stop("AUTH_RELEASE_INVALID") from None
    ops_check = ops_check_for(
        ops_signature=ops_signature, ops_port=ops_port, signers=authenticity["ops_allowed_signers"],
        ops_fingerprint=go_fingerprint, signature=ops_signature_bytes, policy_hash=policy_hash)
    app_check = app_check_for(
        open_binary=go_verify.open_pinned_binary, gh_path=gh[0], gh_sha256=gh[1], trusted_root=trusted_root,
        bundle=app_bundle, open_archive=open_archive, drop_uid=authenticity.get("drop_uid"),
        drop_gid=authenticity.get("drop_gid"), required_euid=required_euid, os_uid=os_uid)
    return port_for(ops_check=ops_check, app_check=app_check)


if __name__ == "__main__":
    raise SystemExit("QA525_ROOT_AUTHENTICITY_LIBRARY_ONLY_NO_ROOT_NO_GO")
