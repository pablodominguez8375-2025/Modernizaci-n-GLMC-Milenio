"""Synthetic QA glmdch Silo candidate checks; NOT authorization to deploy."""
import importlib.util
import json
import os
from pathlib import Path
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[1]
PIN = "docker.io/pgsty/silo@sha256:aaac6bce13eb99316b3c88bd20143fc3c38ab607585256a411a095c44ee57f62"


class QaGlmdchSiloCandidateTests(unittest.TestCase):
    def read(self, p):
        return (ROOT / p).read_text(encoding="utf-8")

    def test_compose_pins_silo_and_separates_document_account(self):
        y = self.read("infrastructure/docker-compose.srv01.yml")
        self.assertEqual(3, y.count("image: " + PIN))
        self.assertNotIn("quay.io/minio/minio:latest", y)
        self.assertNotIn("quay.io/minio/mc:latest", y)
        api = y.split("  api:\n", 1)[1].split("  web:\n", 1)[0]
        self.assertIn("DocumentStorage__AccessKey: ${PMGM_QA_DOCUMENT_ACCESS_KEY", api)
        self.assertIn("DocumentStorage__SecretKey: ${PMGM_QA_DOCUMENT_SECRET_KEY", api)
        self.assertNotIn("PMGM_QA_STORAGE_ADMIN_", api)
        self.assertNotIn("PMGM_QA_BACKUP_", api)
        self.assertIn("service_healthy", y)
        storage = y.split("  minio:\n", 1)[1].split("  minio-init:\n", 1)[0]
        self.assertNotIn("    ports:", storage)
        self.assertIn("silo", storage)

    def test_provisioning_is_external_file_and_uses_minimum_policy(self):
        y = self.read("infrastructure/docker-compose.srv01.yml")
        self.assertIn("qa-provision-silo.sh:/scripts/qa-provision-silo.sh:ro", y)
        self.assertIn("pmgm-qa-documents-policy.json:/policies/documents.json:ro", y)
        p = json.loads(self.read("infrastructure/silo/pmgm-qa-documents-policy.json"))
        grants = {(act, resource) for st in p["Statement"] for act in st["Action"] for resource in st["Resource"]}
        self.assertEqual({("s3:ListBucket", "arn:aws:s3:::pmgm-documents"),
                          ("s3:GetObject", "arn:aws:s3:::pmgm-documents/*"),
                          ("s3:PutObject", "arn:aws:s3:::pmgm-documents/*"),
                          ("s3:DeleteObject", "arn:aws:s3:::pmgm-documents/*")}, grants)
        self.assertTrue(all(st["Effect"] == "Allow" for st in p["Statement"]))

    def test_provisioning_rejects_root_reuse_before_network_access(self):
        script = ROOT / "scripts/qa/qa-provision-silo.sh"
        env = dict(os.environ, MINIO_ROOT_USER="syntheticadmin",
                   MINIO_ROOT_PASSWORD="synthetic-admin-password",
                   DOCUMENT_ACCESS_KEY="syntheticadmin",
                   DOCUMENT_SECRET_KEY="synthetic-doc-password",
                   BACKUP_ACCESS_KEY="backupuser",
                   BACKUP_SECRET_KEY="synthetic-backup-password")
        proc = subprocess.run(["sh", str(script)], env=env, capture_output=True, text=True, timeout=4)
        self.assertNotEqual(0, proc.returncode)
        self.assertIn("ADMIN_REUSE", proc.stderr)
        self.assertNotIn("synthetic-admin-password", proc.stderr)

    def test_backup_restore_never_use_admin_secrets(self):
        backup = self.read("scripts/backup-srv01-qa.sh")
        restore = self.read("scripts/restore-srv01-qa.sh")
        for s in (backup, restore):
            self.assertIn("storage-backup-client", s)
            self.assertIn("BACKUP_ACCESS_KEY", s)
            self.assertNotIn("MINIO_ACCESS_KEY", s)
            self.assertNotIn("MINIO_ROOT_PASSWORD", s)
            self.assertIn("check-qa-storage-config.py", s)
        self.assertNotIn("mirror --overwrite --remove", restore)
        self.assertIn("bucket inexistente, inaccesible o no vacío", restore)

    def test_templates_require_separate_qa_accounts(self):
        env = self.read("infrastructure/srv01.env.example")
        pre = self.read("scripts/preflight-srv01-qa.sh")
        for key in ("STORAGE_ADMIN_USER", "STORAGE_ADMIN_PASSWORD",
                    "DOCUMENT_ACCESS_KEY", "DOCUMENT_SECRET_KEY",
                    "BACKUP_ACCESS_KEY", "BACKUP_SECRET_KEY"):
            self.assertIn("PMGM_QA_" + key, env)
            self.assertIn("PMGM_QA_" + key, pre)
        script = ROOT / "scripts/qa/qa-provision-silo.sh"
        self.assertEqual(0, subprocess.run(["sh", "-n", str(script)], capture_output=True).returncode)


if __name__ == "__main__":
    unittest.main()
