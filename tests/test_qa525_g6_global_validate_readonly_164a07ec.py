"""Hermetic tests for the read-only QA525 G6 global chain validator."""
from __future__ import annotations

import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import tempfile
import unittest


ROOT = Path(__file__).resolve().parents[1]
SCRIPT = ROOT / "ops/qa525/164a07ec/global/qa525-g6-global-validate-readonly-164a07ec.sh"
SOURCE = SCRIPT.read_text()
MATCH = re.search(r"<<'PY_EVIDENCE'\n(.*?)\nPY_EVIDENCE", SOURCE, re.S)
assert MATCH
VERIFIER = MATCH.group(1)
REPO = "b4df75c52a71050a4bbf493d25a7cd5dd5921d1fdb4ab8226eaf7b6b7ffb65ce"
EPOCH = "75870a7a926569c94e853a2a44f0a580"
DRIVE = "1THaJaMm0sKRi1_VGp1eqOBuyzgQP54aT"
ARCHIVE_NAME = "centenario-qa-g6-v2-CIFRADO-20261113T165400Z.tar.gz"


def canonical(path: Path, value: dict) -> str:
    raw = (json.dumps(value, sort_keys=True, separators=(",", ":"), ensure_ascii=True) + "\n").encode()
    path.write_bytes(raw)
    path.chmod(0o600)
    return hashlib.sha256(raw).hexdigest()


class GlobalValidationTest(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        root = Path(self.tmp.name)
        self.archive = root / ARCHIVE_NAME
        self.archive.write_bytes(b"encrypted-fixture\n")
        self.archive.chmod(0o600)
        self.archive_sha = hashlib.sha256(self.archive.read_bytes()).hexdigest()
        self.receipt = root / "receipt-20261113T165400Z.json"
        self.restore = root / "isolated-restore-v2-20261113T165400Z.report.json"
        self.review = root / "review-20261113T165400Z.json"
        self.workdir = root / "restores-v2" / "20261113T165400Z"
        self.inboxes = [root / name for name in ("backup", "restore", "review-inbox")]
        for inbox, command in zip(self.inboxes, ("backup", "restore", "review")):
            inbox.mkdir(mode=0o700)
            used = inbox / ".used-go-1234567890abcdef-20261114T004100Z-1.json"
            canonical(used, {
                "action": f"g6-v2-{command}-supervised",
                "admission_sha256": "sha256:" + "a" * 64,
                "app_archive_sha256": "sha256:" + "b" * 64,
                "app_source_sha": "164a07ecd36eb45428b6aeac4854a17e339ca525",
                "environment": "QA_GLMDCH_ONLY",
                "expires_at": "2026-11-14T00:50:00Z",
                "g6_command": command,
                "g6_operation_sha256": "sha256:" + "c" * 64,
                "host": "glmdch",
                "issued_at": "2026-11-14T00:36:00Z",
                "nonce": "d" * 64,
                "operator": "soporte@glmdch",
                "ops_source_sha": "164a07ecd36eb45428b6aeac4854a17e339ca525",
                "plan_sha256": "sha256:" + "e" * 64,
                "policy_hash": "sha256:" + "f" * 64,
                "schema": "qa525-go-g6-v1",
                "scope": "QA525_G6_V2_ONLY_NO_LAUNCHER_NO_API_WEB",
            })
            sig = Path(str(used) + ".sig")
            sig.write_bytes(b"signature\n")
            sig.chmod(0o600)
        self.receipt_sha = canonical(self.receipt, {
            "schema": "qa525-g6-v2-receipt", "archive_name": ARCHIVE_NAME,
            "sha256": self.archive_sha, "status": "ENCRYPTED_LOCAL_ARCHIVE_READY_OFFHOST_PENDING",
            "restic_repo_id": REPO, "key_epoch": EPOCH,
        })
        self.restore_sha = canonical(self.restore, {
            "schema": "qa525-g6-v2-restore",
            "result": "G6_SAME_HOST_ISOLATED_RESTORE_PASS_NOT_DISASTER_RECOVERY",
            "archive_filename": ARCHIVE_NAME, "archive_sha256": self.archive_sha,
            "restic_repo_id": REPO, "key_epoch": EPOCH,
            "postgres_tables_by_database": {"pmgm": 120, "pmgm_keycloak": 100},
            "silo_objects_verified": 34, "production_touched": False,
        })
        self.review_sha = canonical(self.review, {
            "schema": "qa525-g6-v2-review", "g6_archive_name": ARCHIVE_NAME,
            "g6_archive_sha256": self.archive_sha,
            "g6_restore_report_name": self.restore.name,
            "g6_offhost_sha256_verified": True, "g6_drive_file_id": DRIVE,
            "restic_repo_id": REPO, "key_epoch": EPOCH,
        })

    def tearDown(self):
        self.tmp.cleanup()

    def run_verifier(self):
        verifier = Path(self.tmp.name) / "verifier.py"
        verifier.write_text(VERIFIER)
        args = [
            sys.executable, "-I", "-S", str(verifier), str(os.getuid()),
            str(self.archive), str(self.receipt), str(self.restore), str(self.review),
            str(self.workdir), *(str(item) for item in self.inboxes), ARCHIVE_NAME,
            self.archive_sha, self.receipt_sha, self.restore_sha, self.review_sha,
        ]
        return subprocess.run(args, text=True, capture_output=True, check=False)

    def test_valid_complete_chain_passes(self):
        self.assertEqual(self.run_verifier().returncode, 0)

    def test_tampered_review_stops(self):
        self.review.write_bytes(self.review.read_bytes() + b" ")
        self.assertNotEqual(self.run_verifier().returncode, 0)

    def test_active_go_stops(self):
        active = self.inboxes[1] / "go.json"
        active.write_bytes(b"{}\n")
        active.chmod(0o600)
        self.assertNotEqual(self.run_verifier().returncode, 0)

    def test_missing_used_signature_stops(self):
        next(self.inboxes[2].glob("*.json.sig")).unlink()
        self.assertNotEqual(self.run_verifier().returncode, 0)

    def test_used_go_from_wrong_command_stops(self):
        used = next(self.inboxes[0].glob("*.json"))
        value = json.loads(used.read_text())
        value["action"] = "g6-v2-restore-supervised"
        value["g6_command"] = "restore"
        canonical(used, value)
        self.assertNotEqual(self.run_verifier().returncode, 0)

    def test_hardlinked_evidence_stops(self):
        os.link(self.receipt, Path(self.tmp.name) / "receipt-link")
        self.assertNotEqual(self.run_verifier().returncode, 0)

    def test_noncanonical_json_stops(self):
        value = json.loads(self.restore.read_text())
        self.restore.write_text(json.dumps(value, indent=2) + "\n")
        self.restore.chmod(0o600)
        self.restore_sha = hashlib.sha256(self.restore.read_bytes()).hexdigest()
        self.assertNotEqual(self.run_verifier().returncode, 0)

    def test_shell_is_read_only_and_embedded_python_compiles(self):
        subprocess.run(["bash", "-n", str(SCRIPT)], check=True)
        compile(VERIFIER, "<embedded-evidence>", "exec")
        for forbidden in ("rm -", "mv ", "install ", "docker stop", "docker start",
                          "docker restart", "docker rm", "qa525-vnext-root-bootstrap.py"):
            self.assertNotIn(forbidden, SOURCE)
        self.assertNotIn("QA525GO1", SOURCE)
        self.assertNotIn("MANIFEST_PIN", SOURCE)


if __name__ == "__main__":
    unittest.main()

