#!/usr/bin/env python3
"""QA525 exact signed root-owned launcher replacement, one-time supervised.

Never run raw/unreviewed code via sudo: this entrypoint MUST first be
delivered as part of an independently authenticated, immutable, root-owned
signed release. All runtime provenance checks here are additional barriers,
not a bootstrap for trusting this file. No Docker, Compose or EF commands.

If interrupted after intent creation, NO automatic retry or rollback is
attempted. The durable intent requires separately approved reconciliation.
"""
import argparse
from contextlib import contextmanager, ExitStack
import fcntl
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import re
import socket
import stat
import sys
import tempfile
import time

HERE = Path(__file__).resolve().parent
QA = HERE.parent
LAUNCHER = Path("/usr/local/sbin/pmgm-qa-update")
STORE = Path("/opt/centenario/qa525-signed-stage")
STATE = Path("/opt/centenario/qa525-launcher-replacement")
PREFLIGHT_STATE = Path("/opt/centenario/qa525-signed-prepare")
# Independently provisioned by a PRE-EXISTING trusted Sigstore bootstrap.
# This installer may check it, but MUST NOT create/refresh this admission.
ADMISSION = Path("/etc/pmgm/qa525-bootstrap-admission.json")
BOOTSTRAP_STATUS = "EXTERNAL_SIGSTORE_BOOTSTRAP_ADMISSION_ONLY"
SHA = re.compile(r"[a-f0-9]{40}")
NONCE = re.compile(r"[a-f0-9]{64}")
SCOPE = "QA525_SIGNED_ROOT_LAUNCHER_ONLY_EXPLICIT_SUPERVISION"
LOCKS = (
    Path("/opt/centenario/qa-api-refresh/operation.lock"),
    Path("/opt/centenario/qa-auto-lite/web-update.lock"),
    Path("/run/lock/pmgm-master-qa-604.lock"),
    Path("/run/lock/pmgm-qa754-web-hotfix.lock"),
    Path("/run/lock/pmgm-qa754-api-activation.lock"),
)
# Existing audited, root-owned modules. Verify ownership BEFORE importing.
SOURCES = (
    HERE / "qa525-launcher-supervised-root-install.py",
    HERE / "qa525-launcher-signed-preflight.py",
    HERE / "qa525-launcher-upgrade-inspect.py",
    HERE / "qa525-launcher-root-approval.py",
    HERE / "qa525-launcher-replacement-contract.py",
    HERE / "qa525-launcher-pinned-template.py",
    HERE / "qa525-launcher-retained-source-inspect.py",
    QA / "qa525-stage-current-release.py",
    QA / "qa525-current-release-gate.py",
    QA / "qa-agent-readiness.py",
    QA / "qa-public-release-download.py",
    QA / "qa-signed-staging.py",
)


class Blocked(Exception):
    """Static, no-secrets reason code."""


def require(test, code):
    if not test:
        raise Blocked(code)


def require_host():
    require(os.geteuid() == 0 and socket.gethostname() == "glmdch",
            "SIGNED_LAUNCHER_ROOT_GLMDCH_ONLY")


def root_chain(path, *, directory=False, exact_mode=None):
    path = Path(path)
    require(path.is_absolute(), "ROOT_PATH_NOT_ABSOLUTE")
    for i, part in enumerate((path, *path.parents)):
        try:
            s = part.lstat()
        except OSError:
            raise Blocked("ROOT_PATH_MISSING") from None
        require(s.st_uid == 0 and not stat.S_ISLNK(s.st_mode)
                and not (s.st_mode & 0o022), "ROOT_PATH_UNTRUSTED")
        require((stat.S_ISDIR(s.st_mode) if i == 0 and directory else
                 stat.S_ISREG(s.st_mode) and s.st_nlink == 1 if i == 0
                 else stat.S_ISDIR(s.st_mode)),
                "ROOT_PATH_TYPE_INVALID")
        if i == 0 and exact_mode is not None:
            require(stat.S_IMODE(s.st_mode) == exact_mode,
                    "ROOT_PATH_MODE_INVALID")
    return path


def bootstrap_trusted_sources():
    """Cross-check every privileged imported byte BEFORE helper imports.

    REQUIRED prior trust anchor: a separately authenticated immutable
    bootstrap must first verify Sigstore and install the 0600 admission.
    Nothing in this file establishes that independent bootstrap's integrity.
    """
    require_host()
    root_chain(ADMISSION, exact_mode=0o600)
    try:
        packet = json.loads(
            read_bound(ADMISSION, maximum=16_384, exact_mode=0o600),
            object_pairs_hook=unique_json_fields)
    except (UnicodeError, ValueError, TypeError):
        raise Blocked("ROOT_BOOTSTRAP_ADMISSION_JSON_INVALID") from None
    require(type(packet) is dict and set(packet) == {
                "status", "source_sha", "release_asset_sha256",
                "manifest_sha256", "trusted_verifier_sha256", "files"}
            and packet["status"] == BOOTSTRAP_STATUS
            and type(packet["source_sha"]) is str
            and SHA.fullmatch(packet["source_sha"]) is not None
            and type(packet["release_asset_sha256"]) is str
            and re.fullmatch(r"sha256:[0-9a-f]{64}",
                             packet["release_asset_sha256"]) is not None
            and all(type(packet[k]) is str
                    and NONCE.fullmatch(packet[k]) is not None
                    for k in ("manifest_sha256", "trusted_verifier_sha256"))
            and type(packet["files"]) is dict,
            "ROOT_BOOTSTRAP_ADMISSION_FIELDS_INVALID")
    expected = {str(source) for source in SOURCES}
    require(set(packet["files"]) == expected
            and all(type(name) is str and type(value) is str
                    and NONCE.fullmatch(value) is not None
                    for name, value in packet["files"].items()),
            "ROOT_BOOTSTRAP_SIGNED_FILE_SET_INVALID")
    for source in SOURCES:
        root_chain(source)
        require(digest(read_bound(source, maximum=2_000_000)) ==
                packet["files"][str(source)],
                "ROOT_BOOTSTRAP_SIGNED_SOURCE_HASH_MISMATCH")
    return packet


def verify_bootstrap_binding(plan):
    """Make even a previously checked admission stale if dev/release moved."""
    packet = bootstrap_trusted_sources()
    require(packet["source_sha"] == plan.get("source_sha")
            and packet["release_asset_sha256"] ==
                plan.get("release_asset_sha256"),
            "ROOT_BOOTSTRAP_SIGNED_RELEASE_IDENTITY_MISMATCH")
    return packet


def load(name, source):
    spec = importlib.util.spec_from_file_location(name, source)
    require(spec is not None and spec.loader is not None,
            "TRUSTED_IMPORT_SPEC_INVALID")
    module = importlib.util.module_from_spec(spec)
    sys.modules[name] = module
    spec.loader.exec_module(module)
    return module


def sync_dir(path):
    fd = os.open(path, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW)
    try:
        os.fsync(fd)
    finally:
        os.close(fd)


def read_bound(path, maximum=4_096, *, exact_mode=None):
    root_chain(path, exact_mode=exact_mode)
    before = path.lstat()
    require(0 < before.st_size <= maximum,
            "READ_BOUND_SIZE_INVALID")
    fd = os.open(path, os.O_RDONLY | os.O_NOFOLLOW | os.O_CLOEXEC)
    try:
        live = os.fstat(fd)
        require(stat.S_ISREG(live.st_mode) and live.st_uid == 0
                and live.st_nlink == 1 and not (live.st_mode & 0o022)
                and live.st_dev == before.st_dev and live.st_ino == before.st_ino
                and live.st_size == before.st_size
                and (exact_mode is None
                     or stat.S_IMODE(live.st_mode) == exact_mode),
                "READ_BOUND_RACE_OR_PERMISSIONS")
        with os.fdopen(fd, "rb") as stream:
            fd = -1
            result = stream.read(maximum + 1)
        require(len(result) == before.st_size and len(result) <= maximum,
                "READ_BOUND_CHANGED")
        return result
    finally:
        if fd >= 0:
            os.close(fd)


def digest(data):
    return hashlib.sha256(data).hexdigest()


def write_once(path, content, mode=0o600):
    """Durable creation only. Never overwrite an existing intent or backup."""
    fd = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL |
                 os.O_NOFOLLOW | os.O_CLOEXEC, mode)
    try:
        os.fchmod(fd, mode)
        with os.fdopen(fd, "wb") as stream:
            fd = -1
            stream.write(content)
            stream.flush()
            os.fsync(stream.fileno())
    finally:
        if fd >= 0:
            os.close(fd)
    sync_dir(path.parent)


def json_bytes(record):
    return (json.dumps(record, sort_keys=True, separators=(",", ":")) +
            "\n").encode("utf-8")


def fsync_retained_package(package):
    """Persist every attested source file before making its SHA path executable.

    A directory rename alone is not sufficient durability proof for the data
    blocks produced by the signed zip extractor. Reject links, hardlinks,
    misplaced ownership, writable members and partial directory traversal.
    """
    package = Path(package)
    uid = os.geteuid()
    require(package.is_dir() and not package.is_symlink(),
            "RETAINED_PACKAGE_ROOT_INVALID")
    total_files = 0
    total_bytes = 0
    observed_dirs = 0

    def traversal_error(error):
        raise Blocked("RETAINED_PACKAGE_TRAVERSAL_FAILED") from None

    for current, dirs, filenames in os.walk(
            package, topdown=False, followlinks=False,
            onerror=traversal_error):
        current = Path(current)
        for child_name in dirs:
            child = current / child_name
            st = child.lstat()
            require(stat.S_ISDIR(st.st_mode) and st.st_uid == uid
                    and not stat.S_ISLNK(st.st_mode)
                    and stat.S_IMODE(st.st_mode) == 0o700,
                    "RETAINED_PACKAGE_DIRECTORY_UNTRUSTED")
        directory = current.lstat()
        require(stat.S_ISDIR(directory.st_mode)
                and directory.st_uid == uid
                and stat.S_IMODE(directory.st_mode) == 0o700
                and not stat.S_ISLNK(directory.st_mode),
                "RETAINED_PACKAGE_DIRECTORY_UNTRUSTED")
        observed_dirs += 1
        require(observed_dirs <= 20_000,
                "RETAINED_PACKAGE_TOO_MANY_DIRECTORIES")
        for child_name in filenames:
            child = current / child_name
            before = child.lstat()
            require(stat.S_ISREG(before.st_mode)
                    and before.st_uid == uid and before.st_nlink == 1
                    and not (before.st_mode & 0o022),
                    "RETAINED_PACKAGE_FILE_UNTRUSTED")
            total_files += 1
            total_bytes += before.st_size
            require(total_files <= 100_000 and total_bytes <= 2 * 1024**3,
                    "RETAINED_PACKAGE_SIZE_LIMIT")
            fd = os.open(child, os.O_RDONLY | os.O_NOFOLLOW | os.O_CLOEXEC)
            try:
                live = os.fstat(fd)
                require(live.st_uid == uid and stat.S_ISREG(live.st_mode)
                        and live.st_nlink == 1
                        and live.st_dev == before.st_dev
                        and live.st_ino == before.st_ino
                        and live.st_size == before.st_size,
                        "RETAINED_PACKAGE_FILE_CHANGED_DURING_FSYNC")
                os.fsync(fd)
                after = os.fstat(fd)
                require(after.st_size == live.st_size and
                        after.st_mtime_ns == live.st_mtime_ns,
                        "RETAINED_PACKAGE_FILE_CHANGED_DURING_FSYNC")
            finally:
                os.close(fd)
        sync_dir(current)
    require(total_files > 0 and observed_dirs > 0,
            "RETAINED_PACKAGE_EMPTY")


def lock_parent(path):
    if path == Path("/run/lock"):
        root_chain(path.parent, directory=True)
        st = path.lstat()
        require(stat.S_ISDIR(st.st_mode) and st.st_uid == 0
                and not stat.S_ISLNK(st.st_mode)
                and stat.S_IMODE(st.st_mode) in (0o700, 0o755, 0o1777),
                "ROOT_LOCK_DIRECTORY_UNTRUSTED")
    else:
        root_chain(path, directory=True)


@contextmanager
def all_five_locks():
    """Exact order of existing QA525 operators; nonblocking exclusive flock."""
    require_host()
    with ExitStack() as stack:
        for path in LOCKS:
            lock_parent(path.parent)
            fd = os.open(path, os.O_RDWR | os.O_CREAT | os.O_NOFOLLOW |
                         os.O_CLOEXEC, 0o600)
            stack.callback(os.close, fd)
            st = os.fstat(fd)
            require(stat.S_ISREG(st.st_mode) and st.st_uid == 0
                    and st.st_nlink == 1
                    and stat.S_IMODE(st.st_mode) == 0o600,
                    "ROOT_LOCK_FILE_UNTRUSTED")
            # Reject inode substitution between open and the exclusive
            # advisory lock, even in an otherwise root-owned directory.
            named = path.lstat()
            require(stat.S_ISREG(named.st_mode)
                    and not stat.S_ISLNK(named.st_mode)
                    and named.st_uid == 0 and named.st_nlink == 1
                    and named.st_dev == st.st_dev
                    and named.st_ino == st.st_ino,
                    "ROOT_LOCK_PATH_CHANGED_DURING_OPEN")
            try:
                fcntl.flock(fd, fcntl.LOCK_EX | fcntl.LOCK_NB)
            except BlockingIOError:
                raise Blocked("ANOTHER_QA_OPERATOR_RUNNING") from None
        yield


def unique_json_fields(pairs):
    result = {}
    for name, value in pairs:
        require(name not in result, "ROOT_JOURNAL_DUPLICATE_JSON_FIELD")
        result[name] = value
    return result


def scan_history():
    """Fail closed on any unresolved transaction, unknown file or nonce."""
    root_chain(STATE, directory=True, exact_mode=0o700)
    consumed = set()
    listing = sorted(STATE.iterdir(), key=lambda item: item.name)
    require(len(listing) <= 1000, "ROOT_LAUNCHER_JOURNAL_TOO_LARGE")
    names = {item.name for item in listing}
    for entry in listing:
        name = entry.name
        require(not entry.is_symlink(), "ROOT_LAUNCHER_JOURNAL_SYMLINK")
        require(not name.startswith("intent-"),
                "ROOT_LAUNCHER_PENDING_MANUAL_RECONCILIATION")
        require(name.startswith(("receipt-", "old-")),
                "ROOT_LAUNCHER_UNKNOWN_JOURNAL_ENTRY")
        suffix = name.split("-", 1)[1]
        suffix = suffix.removesuffix(".json").removesuffix(".bin")
        require(NONCE.fullmatch(suffix) is not None,
                "ROOT_LAUNCHER_JOURNAL_NONCE_INVALID")
        require(("receipt-" + suffix + ".json") in names and
                ("old-" + suffix + ".bin") in names,
                "ROOT_LAUNCHER_INCOMPLETE_JOURNAL")
        if name.startswith("receipt-"):
            try:
                record = json.loads(
                    read_bound(entry, maximum=4096, exact_mode=0o600),
                    object_pairs_hook=unique_json_fields)
            except (ValueError, UnicodeError, TypeError):
                raise Blocked("ROOT_LAUNCHER_RECEIPT_JSON_INVALID") from None
            require(type(record) is dict
                    and set(record) == {
                        "status", "source_sha", "release_asset_sha256",
                        "old_launcher_sha256", "new_launcher_sha256",
                        "candidate_operator_sha256", "nonce"}
                    and record["status"] == "LAUNCHER_ATOMIC_SWAP_COMMITTED"
                    and record["nonce"] == suffix
                    and type(record["source_sha"]) is str
                    and SHA.fullmatch(record["source_sha"]) is not None
                    and type(record["release_asset_sha256"]) is str
                    and re.fullmatch(r"sha256:[0-9a-f]{64}",
                                     record["release_asset_sha256"]) is not None
                    and all(type(record[key]) is str
                            and NONCE.fullmatch(record[key]) is not None
                            for key in ("old_launcher_sha256",
                                        "new_launcher_sha256",
                                        "candidate_operator_sha256"))
                    and record["old_launcher_sha256"] !=
                        record["new_launcher_sha256"],
                    "ROOT_LAUNCHER_RECEIPT_UNTRUSTED")
            old_backup = read_bound(
                STATE / ("old-" + suffix + ".bin"),
                maximum=4096, exact_mode=0o700)
            require(digest(old_backup) == record["old_launcher_sha256"],
                    "ROOT_LAUNCHER_RECEIPT_BACKUP_HASH_MISMATCH")
            consumed.add(suffix)
        else:
            read_bound(entry, maximum=4096, exact_mode=0o700)
    return consumed


def inspect_pending_reconciliation():
    """Classify one unfinished durable journal WITHOUT authorizing any action.

    This is a read-only diagnostic to be run by a separately authenticated,
    root-owned code path on glmdch. It never executes a staged launcher,
    downloads a release, consumes an approval, rolls back, or deploys.
    """
    require_host()
    root_chain(STATE, directory=True, exact_mode=0o700)
    root_chain(LAUNCHER, exact_mode=0o700)
    with all_five_locks():
        names = {p.name for p in STATE.iterdir()}
        require(len(names) <= 1000, "ROOT_RECONCILIATION_JOURNAL_TOO_LARGE")
        intents = [s for s in names if s.startswith("intent-")]
        require(len(intents) == 1, "ROOT_RECONCILIATION_ONE_PENDING_REQUIRED")
        name = intents[0]
        require(name.endswith(".json")
                and NONCE.fullmatch(name[len("intent-"):-len(".json")]) is not None,
                "ROOT_RECONCILIATION_NONCE_INVALID")
        nonce = name[len("intent-"):-len(".json")]
        intent = STATE / name
        backup = STATE / ("old-" + nonce + ".bin")
        receipt = STATE / ("receipt-" + nonce + ".json")
        require(backup.name in names, "ROOT_RECONCILIATION_BACKUP_MISSING")
        for other in names - {name, backup.name, receipt.name}:
            # Historical receipts/backups may coexist with one pending cut,
            # but unrecognized journal artifacts must never be ignored.
            kind, sep, suffix = other.partition("-")
            require(sep == "-" and kind in ("receipt", "old")
                    and suffix.endswith(".json" if kind == "receipt" else ".bin")
                    and NONCE.fullmatch(
                        suffix[:-5] if kind == "receipt" else suffix[:-4])
                        is not None,
                    "ROOT_RECONCILIATION_UNKNOWN_JOURNAL_ENTRY")
        try:
            act = json.loads(read_bound(intent, exact_mode=0o600),
                             object_pairs_hook=unique_json_fields)
        except (ValueError, UnicodeError, TypeError):
            raise Blocked("ROOT_RECONCILIATION_INTENT_JSON_INVALID") from None
        require(type(act) is dict and set(act) == {
                    "status", "source_sha", "release_asset_sha256",
                    "old_launcher_sha256", "new_launcher_sha256", "nonce"}
                and act["status"] == "SWAP_INTENT_NO_DEPLOYMENT"
                and act["nonce"] == nonce
                and type(act["source_sha"]) is str
                and SHA.fullmatch(act["source_sha"]) is not None
                and type(act["release_asset_sha256"]) is str
                and re.fullmatch(r"sha256:[0-9a-f]{64}",
                                 act["release_asset_sha256"]) is not None
                and all(type(act[k]) is str
                        and NONCE.fullmatch(act[k]) is not None
                        for k in ("old_launcher_sha256", "new_launcher_sha256"))
                and act["old_launcher_sha256"] != act["new_launcher_sha256"],
                "ROOT_RECONCILIATION_INTENT_INVALID")
        old_bytes = read_bound(backup, exact_mode=0o700)
        require(digest(old_bytes) == act["old_launcher_sha256"],
                "ROOT_RECONCILIATION_BACKUP_HASH_MISMATCH")
        current_hash = digest(read_bound(LAUNCHER, exact_mode=0o700))
        require(current_hash in (
                    act["old_launcher_sha256"], act["new_launcher_sha256"]),
                "ROOT_RECONCILIATION_LIVE_UNKNOWN")
        receipt_present = receipt.name in names
        if receipt_present:
            try:
                commit = json.loads(read_bound(receipt, exact_mode=0o600),
                                    object_pairs_hook=unique_json_fields)
            except (ValueError, UnicodeError, TypeError):
                raise Blocked("ROOT_RECONCILIATION_RECEIPT_JSON_INVALID") from None
            require(type(commit) is dict
                    and set(commit) == set(act) | {"candidate_operator_sha256"}
                    and commit.get("status") == "LAUNCHER_ATOMIC_SWAP_COMMITTED"
                    and type(commit.get("candidate_operator_sha256")) is str
                    and NONCE.fullmatch(commit["candidate_operator_sha256"])
                        is not None
                    and all(commit.get(k) == act[k] for k in act if k != "status")
                    and current_hash == act["new_launcher_sha256"],
                    "ROOT_RECONCILIATION_RECEIPT_MISMATCH")
        return {
            "status": "MANUAL_RECONCILIATION_REQUIRED_NO_AUTOREPAIR",
            "source_sha": act["source_sha"],
            "nonce": nonce,
            "observed_phase": ("POST_ATOMIC_SWAP" if current_hash ==
                               act["new_launcher_sha256"]
                               else "PRE_ATOMIC_SWAP"),
            "durable_receipt_present": receipt_present,
            "backup_hash_matches_intent": True,
            "signature_verified_by_inspection": False,
            "rollback_authorized": False,
            "may_resume": False,
            "may_apply": False,
        }


def verify_inputs():
    """Build independent evidence with trusted local verifier code only."""
    bootstrap_trusted_sources()
    inspector = load("qa525_root_inspector", SOURCES[2])
    preflight = load("qa525_root_signed_preflight", SOURCES[1])
    signed = load("qa525_root_signed_stage", SOURCES[7])
    approval_reader = load("qa525_root_launcher_approval", SOURCES[3])
    contract = load("qa525_root_cut_policy", SOURCES[4])
    template = load("qa525_root_pinned_template", SOURCES[5])
    retained = load("qa525_root_retained_inspector", SOURCES[6])
    return inspector, preflight, signed, approval_reader, contract, template, retained


def replace_approved():
    require_host()
    modules = verify_inputs()
    inspector, preflight, signed, approval_reader, contract, template, retained = modules
    # All state and release roots MUST already exist as root-owned mode0700.
    root_chain(STORE, directory=True, exact_mode=0o700)
    root_chain(STATE, directory=True, exact_mode=0o700)
    root_chain(PREFLIGHT_STATE, directory=True, exact_mode=0o700)
    root_chain(LAUNCHER, exact_mode=0o700)
    with all_five_locks():
        consumed = scan_history()
        plan = preflight.signed_candidate_preflight(inspector, signed,
                                                    stage_parent=PREFLIGHT_STATE)
        # Independent admission is required BEFORE import, and bound again
        # to the actual verified live release after the signed preflight.
        bootstrap_packet = verify_bootstrap_binding(plan)
        approval = approval_reader.read_root_approval()
        assessment = contract.assess_review(
            plan, approval, now=int(time.time()), pending=False,
            consumed_nonces=tuple(sorted(consumed)))
        require(assessment["status"] == "REVIEWED_SIGNED_LAUNCHER_PLAN_NO_INSTALL"
                and assessment["may_install"] is False,
                "ROOT_SIGNED_CUT_REVIEW_NOT_COMPLETE")
        sha, nonce = plan["source_sha"], assessment["nonce"]
        target = STORE / sha
        require(SHA.fullmatch(sha) is not None
                and NONCE.fullmatch(nonce) is not None
                and not target.exists() and not target.is_symlink(),
                "ROOT_RELEASE_ALREADY_RETAINED_OR_INVALID")
        existing = inspector.inspect()
        require(existing.get("status") == "LAUNCHER_OBSERVED_UPGRADE_NOT_AUTHORIZED"
                and existing.get("current_launcher_sha256") == plan["old_launcher_sha256"],
                "ROOT_OLD_LAUNCHER_DRIFT")
        old_bytes = read_bound(LAUNCHER, exact_mode=0o700)
        require(digest(old_bytes) == plan["old_launcher_sha256"],
                "ROOT_OLD_LAUNCHER_HASH_CHANGED")
        new_bytes = template.render(sha)
        checked = template.inspect_template(sha, new_bytes)
        require(checked.get("launcher_sha256") == digest(new_bytes)
                and checked.get("may_install") is False,
                "ROOT_NEW_LAUNCHER_TEMPLATE_INVALID")
        intent = STATE / ("intent-" + nonce + ".json")
        backup = STATE / ("old-" + nonce + ".bin")
        receipt = STATE / ("receipt-" + nonce + ".json")
        staged = LAUNCHER.parent / (".pmgm-qa-update-" + nonce + ".tmp")
        require(all(not x.exists() and not x.is_symlink()
                    for x in (intent, backup, receipt, staged)),
                "ROOT_LAUNCHER_REPLAY_OR_PARTIAL_STATE")
        # Download, independently attest and verify signed source AGAIN.
        with tempfile.TemporaryDirectory(prefix=".qa525-release-", dir=STORE) as tmp:
            package = Path(tmp) / "verified-package"
            evidence = signed.verify_current(package)
            require(evidence.get("source_sha") == sha
                    and evidence.get("release_asset_sha256") ==
                        plan["release_asset_sha256"]
                    and evidence.get("status") ==
                        "SIGNATURE_VERIFIED_STAGED_NOT_DEPLOYED",
                    "ROOT_SIGNED_RELEASE_CHANGED_DURING_CUT")
            signed.staging.verify_manifest(package, sha)
            # Tie the externally authenticated helper admission to the
            # actual Sigstore-verified signed manifest before any journal IO.
            require(digest(preflight.stable_hash(
                    package / "MANIFEST.sha256", maximum=5_000_000)) ==
                    bootstrap_packet["manifest_sha256"],
                    "ROOT_BOOTSTRAP_MANIFEST_BINDING_MISMATCH")
            operator = preflight.stable_hash(package /
                    "scripts/qa/operators/qa525-update.py")
            require(digest(operator) == plan["candidate_operator_sha256"],
                    "ROOT_CANDIDATE_BYTES_DRIFT")
            # Approval may expire during the download. Recheck right now.
            renewed_approval = approval_reader.read_root_approval()
            require(renewed_approval == approval
                    and renewed_approval.get("nonce") == nonce,
                    "ROOT_APPROVAL_CHANGED_DURING_RELEASE_DOWNLOAD")
            renewed_cut = contract.assess_review(
                plan, renewed_approval, now=int(time.time()),
                pending=False, consumed_nonces=tuple(sorted(scan_history())))
            require(renewed_cut.get("nonce") == nonce,
                    "ROOT_REVIEW_NONCE_CHANGED_DURING_DOWNLOAD")
            require(digest(read_bound(LAUNCHER, exact_mode=0o700)) ==
                    plan["old_launcher_sha256"],
                    "ROOT_LAUNCHER_CHANGED_BEFORE_JOURNAL")
            record = {
                "status": "SWAP_INTENT_NO_DEPLOYMENT",
                "source_sha": sha,
                "release_asset_sha256": plan["release_asset_sha256"],
                "old_launcher_sha256": digest(old_bytes),
                "new_launcher_sha256": digest(new_bytes),
                "nonce": nonce,
            }
            # No filesystem mutation of launcher before durable intent/backup.
            write_once(intent, json_bytes(record))
            write_once(backup, old_bytes, mode=0o700)
            require(digest(read_bound(backup, exact_mode=0o700)) ==
                    record["old_launcher_sha256"],
                    "ROOT_OLD_LAUNCHER_BACKUP_UNVERIFIED")
            require(not target.exists() and not target.is_symlink(),
                    "ROOT_TARGET_STAGE_ALREADY_EXISTS")
            # Manifest and signatures were verified before staging. Guarantee
            # the ZIP-extracted bytes are durable before publishing the path.
            fsync_retained_package(package)
            # Rehash ALL attested members after durability barriers, then
            # reaffirm the exact operator's digest before exposing a path.
            signed.staging.verify_manifest(package, sha)
            require(digest(preflight.stable_hash(package /
                    "scripts/qa/operators/qa525-update.py")) ==
                    plan["candidate_operator_sha256"],
                    "ROOT_SIGNED_OPERATOR_CHANGED_DURING_FSYNC")
            os.rename(package, target)
            sync_dir(STORE)
            retained_root = retained.inspect_retained(
                sha, plan["candidate_operator_sha256"])
            require(retained_root["status"] ==
                    "ROOT_OWNED_PINNED_SOURCE_HASH_MATCH_NOT_SIGNED",
                    "ROOT_RETAINED_SOURCE_INVALID")
            write_once(staged, new_bytes, mode=0o700)
            require(digest(read_bound(staged, exact_mode=0o700)) ==
                    digest(new_bytes), "ROOT_STAGED_LAUNCHER_CHANGED")
            require(digest(read_bound(LAUNCHER, exact_mode=0o700)) ==
                    digest(old_bytes), "ROOT_LAUNCHER_DRIFT_BEFORE_SWAP")
            # The second download and on-disk fsync may take time. Reject
            # expiry or approval substitution at the LAST pre-swap boundary.
            last_approval = approval_reader.read_root_approval()
            require(last_approval == approval
                    and last_approval.get("nonce") == nonce
                    and int(time.time()) < approval["expires_at"],
                    "ROOT_APPROVAL_CHANGED_OR_EXPIRED_BEFORE_ATOMIC_SWAP")
            os.replace(staged, LAUNCHER)
            sync_dir(LAUNCHER.parent)
            require(digest(read_bound(LAUNCHER, exact_mode=0o700)) ==
                    digest(new_bytes), "ROOT_LAUNCHER_DRIFT_AFTER_SWAP")
            retained.inspect_retained(sha, plan["candidate_operator_sha256"])
            write_once(receipt, json_bytes({
                **record, "status": "LAUNCHER_ATOMIC_SWAP_COMMITTED",
                "candidate_operator_sha256": plan["candidate_operator_sha256"],
            }))
            intent.unlink()
            sync_dir(STATE)
        return {
            "status": "QA525_LAUNCHER_REPLACED_NO_API_WEB_DEPLOY",
            "scope": SCOPE,
            "source_sha": sha,
            "release_asset_sha256": plan["release_asset_sha256"],
            "launcher_sha256": digest(new_bytes),
            "nonce": nonce,
            "requires_new_api_web_approval": True,
            "api_web_changed": False,
            "may_apply": False,
        }


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    choice = parser.add_mutually_exclusive_group(required=True)
    choice.add_argument("--replace-approved", action="store_true")
    choice.add_argument("--inspect-pending", action="store_true")
    args = parser.parse_args(argv)
    try:
        if args.inspect_pending:
            # An inspection is not permission to execute unverified Python:
            # require the same externally provisioned source admission first.
            bootstrap_trusted_sources()
            print(json.dumps(inspect_pending_reconciliation(), sort_keys=True))
        else:
            print(json.dumps(replace_approved(), sort_keys=True))
        return 0
    except Exception as exc:
        # All failures after durable intent require manually audited
        # reconciliation; never automatically replay or alter Docker/identity.
        code = str(exc) if isinstance(exc, Blocked) else "ROOT_SIGNED_LAUNCHER_ABORTED"
        print(json.dumps({"status": ("BLOCKED_QA525_LAUNCHER_INSPECTION"
                                     if args.inspect_pending else
                                     "BLOCKED_QA525_LAUNCHER_REPLACE"),
                          "code": code, "scope": SCOPE,
                          "may_install": False, "may_apply": False,
                          "requires_manual_reconciliation_if_intent_exists": True},
                         sort_keys=True))
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
