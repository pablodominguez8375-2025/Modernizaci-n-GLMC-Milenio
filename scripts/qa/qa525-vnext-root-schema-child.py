#!/usr/bin/env python3
"""Signed schema-cut child. Only PRE-4's pinned launcher may call this after consuming the GO.

The host port supplies reviewed effect callables. Missing host methods, mismatched
archive, lost plane, or malformed steps STOP; no arbitrary command or path is read
from the GO. The sequencer and durable journal come from the verified release.
"""
import hashlib
import os
import re
import stat

SHA = re.compile(r"sha256:[0-9a-f]{64}\\Z")
SOURCE = re.compile(r"[0-9a-f]{40}\\Z")
NONCE = re.compile(r"[0-9a-f]{64}\\Z")
CLAIMS = frozenset({"purpose", "nonce", "app_source_sha", "app_archive_sha256",
                    "go_sha256", "plan_sha256", "expires_at"})
MAX_ARCHIVE = 512 * 1024 * 1024


class Stop(Exception):
    """Opaque fixed diagnostic."""


def _archive(fd, expected):
    try:
        st = os.fstat(fd)
        if not stat.S_ISREG(st.st_mode) or st.st_size <= 0 or st.st_size > MAX_ARCHIVE:
            raise Stop("SCHEMA_CHILD_FD_INVALID")
        os.lseek(fd, 0, os.SEEK_SET)
        chunks, remaining = [], st.st_size
        while remaining:
            block = os.read(fd, min(remaining, 1 << 20))
            if not block:
                raise Stop("SCHEMA_CHILD_FD_INVALID")
            chunks.append(block)
            remaining -= len(block)
        if os.read(fd, 1):
            raise Stop("SCHEMA_CHILD_FD_INVALID")
        payload = b"".join(chunks)
    except OSError:
        raise Stop("SCHEMA_CHILD_FD_INVALID") from None
    if "sha256:" + hashlib.sha256(payload).hexdigest() != expected:
        raise Stop("SCHEMA_CHILD_ARCHIVE_MISMATCH")
    return payload


def act_for(*, claims, fd, plane, journal_root, uid, host, executor, store,
            contract, nonce_journal, plan_bytes):
    if (type(claims) is not dict or set(claims) != CLAIMS
            or claims["purpose"] != "SCHEMA_API_WEB_QA_ONLY"
            or type(claims["nonce"]) is not str or NONCE.fullmatch(claims["nonce"]) is None
            or type(claims["app_source_sha"]) is not str or SOURCE.fullmatch(claims["app_source_sha"]) is None
            or any(type(claims[k]) is not str or SHA.fullmatch(claims[k]) is None
                   for k in ("app_archive_sha256", "go_sha256", "plan_sha256"))
            or type(claims["expires_at"]) is not int or claims["expires_at"] <= 0
            or type(plan_bytes) is not bytes or not 0 < len(plan_bytes) <= 65536
            or type(uid) is not int or isinstance(uid, bool) or uid < 0
            or type(journal_root) is not str or not os.path.isabs(journal_root)
            or os.path.normpath(journal_root) != journal_root
            or not callable(getattr(plane, "assert_held", None))
            or not callable(getattr(host, "build_steps", None))
            or not callable(getattr(executor, "run", None))
            or not callable(getattr(store, "append", None))
            or not callable(getattr(contract, "next_event", None))):
        raise Stop("SCHEMA_CHILD_BINDING_INVALID")
    try:
        plane.assert_held()
        payload = _archive(fd, claims["app_archive_sha256"])
        binding = {"nonce": claims["nonce"], "go_sha256": claims["go_sha256"],
                   "plan_sha256": claims["plan_sha256"]}
        steps = host.build_steps(archive_bytes=payload, plan_bytes=plan_bytes,
                                 claims=dict(claims), plane=plane)
        if type(steps) is not dict or set(steps) != executor.STEPS or any(
                not callable(steps[k]) for k in executor.STEPS):
            raise Stop("SCHEMA_CHILD_STEPS_INVALID")

        def journal(phase):
            return store.append(journal_root, binding, phase, uid=uid, plane=plane,
                                contract=contract, nonce_journal=nonce_journal)
        return executor.run(journal, plane, steps)
    except Stop:
        raise
    except Exception:
        raise Stop("SCHEMA_CHILD_STOP") from None


if __name__ == "__main__":
    raise SystemExit("QA525_SCHEMA_CHILD_LIBRARY_ONLY_NO_ROOT_NO_GO")
