#!/usr/bin/env bash
# QA525 G6 GLOBAL VALIDATION v1 — corte 164a07ec.
# Read-only closure of the completed backup/off-host/restore/review chain.
# No GO, PIN, project Python, state write, service change, API/WEB or identity action.
set -Eeuo pipefail
set +x
umask 077

SHA="164a07ecd36eb45428b6aeac4854a17e339ca525"
STAMP="20261113T165400Z"
ARCHIVE="centenario-qa-g6-v2-CIFRADO-${STAMP}.tar.gz"
ARCHIVE_SHA="b0c77c1c26ba48e1c6942abab3ea5d5b1a9aaa3228cbd65753bc274a775276fe"
REVIEW_SHA="f793a010911dd6de2a9c280b2a22c3b0066ee0369dc02d3afdf048a23e5e4370"
RECEIPT_SHA="f4c98b7ede5cda84603536a0b7e2cb032e3488f09e28a27c045214ad5a72fea9"
RESTORE_SHA="65307e7ae1aedaec8a0d820d4bea10c39171a0aed938f0ea66169efc2536a4a9"
STATE_ROOT="/var/lib/pmgm-qa525-g6-v2"
DOWNLOAD="$STATE_ROOT/downloads-v2/$ARCHIVE"
RECEIPT="$STATE_ROOT/state-v2/receipt-$STAMP.json"
RESTORE="$STATE_ROOT/state-v2/isolated-restore-v2-$STAMP.report.json"
REVIEW="$STATE_ROOT/state-v2/review-$STAMP.json"
WORKDIR="$STATE_ROOT/restores-v2/$STAMP"
BACKUP_INBOX="/var/lib/pmgm-qa525/inbox-g6-backup"
RESTORE_INBOX="/var/lib/pmgm-qa525/inbox-g6-restore"
REVIEW_INBOX="/var/lib/pmgm-qa525/inbox-g6-review"
VERIFY_CONTAINER="pmgm-g6v2-verify-20261113t165400z"
MINIO="centenario-qa-lab2-minio-1"
POSTGRES="centenario-qa-lab2-postgres-1"
PY="/usr/bin/python3.14"
PY_SHA="52e0a13e60a981d8c4b6478be2ba5176f69da07948a056bf49cf6f077e30cb41"
DOCKER="/usr/bin/docker"
DEADLINE_EPOCH="$(date -u -d '2026-11-15T16:54:00Z' +%s)"

stop() {
  echo "QA525_G6_GLOBAL_VALIDATION=STOP_$1"
  echo "G6_GLOBAL_VALIDATION_EXIT=1"
  exit 1
}

[ "$(hostname -s)" = "glmdch" ] && [ "$(id -un)" = "soporte" ] && [ "$(id -u)" = "1000" ] || stop HOST_USER
[ -t 0 ] || stop NEEDS_TERMINAL
[ "$(readlink -f "$PY")" = "$PY" ] && [ "$(sha256sum "$PY" | cut -d' ' -f1)" = "$PY_SHA" ] || stop INTERPRETER_PIN
[ "$(readlink -f "$DOCKER")" = "$DOCKER" ] || stop DOCKER_PATH
[ "$(date -u +%s)" -lt "$((DEADLINE_EPOCH - 300))" ] || stop G6_48H_WINDOW_LESS_THAN_5_MIN
sudo -v || stop SUDO

identity() {
  sudo "$DOCKER" inspect -f '{{index .Config.Labels "com.docker.compose.project"}}|{{index .Config.Labels "com.docker.compose.service"}}|{{.State.Running}}|{{if .State.Health}}{{.State.Health.Status}}{{else}}no-health{{end}}' "$1"
}

MINIO_BEFORE="$(identity "$MINIO")" || stop PRODUCTION_MINIO_READ_FAILED
POSTGRES_BEFORE="$(identity "$POSTGRES")" || stop PRODUCTION_POSTGRES_READ_FAILED
[ "$MINIO_BEFORE" = "centenario-qa-lab2|minio|true|healthy" ] || stop PRODUCTION_MINIO_NOT_HEALTHY
[ "$POSTGRES_BEFORE" = "centenario-qa-lab2|postgres|true|healthy" ] || stop PRODUCTION_POSTGRES_NOT_HEALTHY
sudo "$DOCKER" inspect "$VERIFY_CONTAINER" >/dev/null 2>&1 && stop VERIFIER_CONTAINER_PRESENT

validate_chain() {
  sudo "$PY" -I -S - \
    0 "$DOWNLOAD" "$RECEIPT" "$RESTORE" "$REVIEW" "$WORKDIR" \
    "$BACKUP_INBOX" "$RESTORE_INBOX" "$REVIEW_INBOX" \
    "$ARCHIVE" "$ARCHIVE_SHA" "$RECEIPT_SHA" "$RESTORE_SHA" "$REVIEW_SHA" <<'PY_EVIDENCE'
import hashlib
import json
import os
import stat
import sys

(expected_uid_s, archive_path, receipt_path, restore_path, review_path, workdir,
 backup_inbox, restore_inbox, review_inbox, archive_name, archive_sha,
 receipt_sha, restore_sha, review_sha) = sys.argv[1:]
expected_uid = int(expected_uid_s)
repo = "b4df75c52a71050a4bbf493d25a7cd5dd5921d1fdb4ab8226eaf7b6b7ffb65ce"
epoch = "75870a7a926569c94e853a2a44f0a580"
drive = "1THaJaMm0sKRi1_VGp1eqOBuyzgQP54aT"
restore_result = "G6_SAME_HOST_ISOLATED_RESTORE_PASS_NOT_DISASTER_RECOVERY"

def unique(pairs):
    out = {}
    for key, value in pairs:
        if key in out:
            raise ValueError("duplicate key")
        out[key] = value
    return out

def open_regular(path, limit=None):
    before = os.lstat(path)
    fd = os.open(path, os.O_RDONLY | os.O_NOFOLLOW | os.O_CLOEXEC)
    try:
        current = os.fstat(fd)
        assert stat.S_ISREG(current.st_mode)
        assert current.st_uid == expected_uid and current.st_gid == expected_uid
        assert stat.S_IMODE(current.st_mode) == 0o600 and current.st_nlink == 1
        assert (before.st_dev, before.st_ino) == (current.st_dev, current.st_ino)
        if limit is not None:
            assert 0 < current.st_size <= limit
        with os.fdopen(fd, "rb", closefd=False) as stream:
            raw = stream.read(None if limit is None else limit + 1)
    finally:
        os.close(fd)
    if limit is not None:
        assert len(raw) <= limit
    return raw

def canonical_json(path, expected_sha):
    raw = open_regular(path, 65536)
    assert hashlib.sha256(raw).hexdigest() == expected_sha
    value = json.loads(raw, object_pairs_hook=unique)
    assert type(value) is dict
    encoded = (json.dumps(value, sort_keys=True, separators=(",", ":"),
                          ensure_ascii=True, allow_nan=False) + "\n").encode("ascii")
    assert encoded == raw
    return value

def validate_inbox(path, expected_command):
    info = os.lstat(path)
    assert stat.S_ISDIR(info.st_mode)
    assert info.st_uid == expected_uid and info.st_gid == expected_uid
    assert stat.S_IMODE(info.st_mode) == 0o700
    for active in ("go.json", "go.json.sig", ".go.json.new", ".go.json.sig.new"):
        assert not os.path.lexists(os.path.join(path, active))
    used = [name for name in os.listdir(path)
            if name.startswith(".used-go-") and name.endswith(".json")]
    assert used
    matched = 0
    for name in used:
        raw = open_regular(os.path.join(path, name), 65536)
        open_regular(os.path.join(path, name + ".sig"), 65536)
        value = json.loads(raw, object_pairs_hook=unique)
        assert type(value) is dict
        encoded = (json.dumps(value, sort_keys=True, separators=(",", ":"),
                              ensure_ascii=True, allow_nan=False) + "\n").encode("ascii")
        assert encoded == raw
        required = {
            "action", "admission_sha256", "app_archive_sha256", "app_source_sha",
            "environment", "expires_at", "g6_command", "g6_operation_sha256", "host",
            "issued_at", "nonce", "operator", "ops_source_sha", "plan_sha256",
            "policy_hash", "schema", "scope",
        }
        assert set(value) == required
        assert value["action"] == "g6-v2-%s-supervised" % expected_command
        assert value["g6_command"] == expected_command
        assert value["app_source_sha"] == value["ops_source_sha"] == "164a07ecd36eb45428b6aeac4854a17e339ca525"
        assert value["environment"] == "QA_GLMDCH_ONLY"
        assert value["host"] == "glmdch" and value["operator"] == "soporte@glmdch"
        assert value["schema"] == "qa525-go-g6-v1"
        assert value["scope"] == "QA525_G6_V2_ONLY_NO_LAUNCHER_NO_API_WEB"
        assert isinstance(value["nonce"], str) and len(value["nonce"]) == 64
        assert not set(value["nonce"]) - set("0123456789abcdef")
        for key in ("admission_sha256", "app_archive_sha256", "g6_operation_sha256",
                    "plan_sha256", "policy_hash"):
            digest = value[key]
            assert isinstance(digest, str) and len(digest) == 71 and digest.startswith("sha256:")
            assert not set(digest[7:]) - set("0123456789abcdef")
        matched += 1
    assert matched >= 1

try:
    archive_raw = open_regular(archive_path)
    assert hashlib.sha256(archive_raw).hexdigest() == archive_sha
    receipt = canonical_json(receipt_path, receipt_sha)
    restore = canonical_json(restore_path, restore_sha)
    review = canonical_json(review_path, review_sha)

    assert receipt == {
        "schema": "qa525-g6-v2-receipt",
        "archive_name": archive_name,
        "sha256": archive_sha,
        "status": "ENCRYPTED_LOCAL_ARCHIVE_READY_OFFHOST_PENDING",
        "restic_repo_id": repo,
        "key_epoch": epoch,
    }
    assert restore == {
        "schema": "qa525-g6-v2-restore",
        "result": restore_result,
        "archive_filename": archive_name,
        "archive_sha256": archive_sha,
        "restic_repo_id": repo,
        "key_epoch": epoch,
        "postgres_tables_by_database": {"pmgm": 120, "pmgm_keycloak": 100},
        "silo_objects_verified": 34,
        "production_touched": False,
    }
    assert review == {
        "schema": "qa525-g6-v2-review",
        "g6_archive_name": archive_name,
        "g6_archive_sha256": archive_sha,
        "g6_restore_report_name": os.path.basename(restore_path),
        "g6_offhost_sha256_verified": True,
        "g6_drive_file_id": drive,
        "restic_repo_id": repo,
        "key_epoch": epoch,
    }
    assert not os.path.lexists(workdir)
    for inbox, command in ((backup_inbox, "backup"), (restore_inbox, "restore"),
                           (review_inbox, "review")):
        validate_inbox(inbox, command)
except Exception:
    sys.exit(1)
PY_EVIDENCE
}
validate_chain || stop EVIDENCE_CHAIN_INVALID

MINIO_AFTER="$(identity "$MINIO")" || stop PRODUCTION_MINIO_READ_FAILED_AFTER
POSTGRES_AFTER="$(identity "$POSTGRES")" || stop PRODUCTION_POSTGRES_READ_FAILED_AFTER
[ "$MINIO_AFTER" = "$MINIO_BEFORE" ] || stop PRODUCTION_MINIO_CHANGED
[ "$POSTGRES_AFTER" = "$POSTGRES_BEFORE" ] || stop PRODUCTION_POSTGRES_CHANGED
sudo "$DOCKER" inspect "$VERIFY_CONTAINER" >/dev/null 2>&1 && stop VERIFIER_CONTAINER_PRESENT_AFTER
validate_chain || stop EVIDENCE_CHAIN_CHANGED_DURING_VALIDATION

echo "G6_CHAIN_ARCHIVE_ROOT_0600_HASH_OK=si"
echo "G6_CHAIN_RECEIPT_RESTORE_REVIEW_CANONICAL_HASHED=si"
echo "G6_CHAIN_COHERENT_REPO_EPOCH_DRIVE=si"
echo "G6_CHAIN_OFFHOST_RESTORE_REVIEW_COMPLETE=si"
echo "G6_USED_GO_ARCHIVES_PRESENT_ACTIVE_GO_ABSENT=si"
echo "G6_PLAINTEXT_AND_VERIFIER_ABSENT=si"
echo "PRODUCTION_CONTAINERS_UNCHANGED_HEALTHY=si"
echo "ROOT_PROJECT_CODE_EXECUTED=no"
echo "QA525_G6_GLOBAL_VALIDATION=PASS_READ_ONLY_COMPLETE_CHAIN_PRODUCTION_UNTOUCHED"
echo "G6_GLOBAL_VALIDATION_EXIT=0"

