"""Regresión de aceptación SOLO de prefiltro S3 #470 (sin conexiones externas)."""
import importlib.util
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[1]
SCRIPT = ROOT / "scripts/check-pilot-s3-supplier.py"
spec = importlib.util.spec_from_file_location("s3_supplier_guard", SCRIPT)
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)
PINNED_AISTOR = "quay.io/minio/aistor/minio@sha256:" + "f" * 64


class S3SupplierSupplyGateTests(unittest.TestCase):
    def check_blocked(self, provider, **kw):
        value = module.evaluate(provider, **kw)
        self.assertEqual("BLOCKED", value["status"])
        self.assertEqual("NO_DEPLOYMENT", value["scope"])

    def test_refuses_old_minio_community(self):
        self.check_blocked(
            "minio-community",
            image="minio/minio:RELEASE.2025-09-07T16-13-09Z",
        )

    def test_refuses_unfixed_silo_release_even_when_digest_supplied(self):
        self.check_blocked(
            "silo",
            image="docker.io/pgsty/silo@sha256:" + "a" * 64,
        )

    def test_refuses_unreviewed_provider(self):
        self.check_blocked("other", image=PINNED_AISTOR)

    def test_aistor_requires_digest_not_latest_or_mutable_tag(self):
        for image in (
            "", "quay.io/minio/aistor/minio:latest",
            "quay.io/minio/aistor/minio:RELEASE.2026-09-19T17-05-25Z",
            "quay.io/minio/aistor/minio@sha256:" + "123",
            "docker.io/unknown/aistor@sha256:" + "f" * 64,
        ):
            with self.subTest(image=image):
                self.check_blocked("aistor", image=image)

    def test_aistor_requires_license_and_explicit_lab_ack(self):
        with tempfile.TemporaryDirectory() as tmp:
            license_path = Path(tmp) / "license"
            license_path.write_bytes(b"solo ficticia para pruebas; no valida licencias")
            self.check_blocked("aistor", image=PINNED_AISTOR,
                               license_file=str(license_path))
            self.check_blocked("aistor", image=PINNED_AISTOR,
                               acknowledgment=module.ACK_TEXT)
            self.check_blocked("aistor", image=PINNED_AISTOR,
                               license_file=str(license_path),
                               acknowledgment="AUTORIZO_PRODUCCION")

    def test_aistor_refuses_license_in_repo_even_when_nonempty(self):
        with tempfile.TemporaryDirectory(dir=str(ROOT), prefix=".ci-s3-") as tmp:
            private = Path(tmp) / "license"
            private.write_bytes(b"no tiene valor legal")
            self.check_blocked("aistor", image=PINNED_AISTOR,
                               license_file=str(private),
                               acknowledgment=module.ACK_TEXT)

    def test_aistor_lab_precheck_is_not_deployment_authorization(self):
        with tempfile.TemporaryDirectory() as tmp:
            private = Path(tmp) / "synthetic-nonlicense"
            private.write_bytes(b"fake sample for unit testing")
            value = module.evaluate(
                "aistor", PINNED_AISTOR, str(private), module.ACK_TEXT
            )
            self.assertEqual("LAB_PRECHECK_ONLY", value["status"])
            self.assertEqual("NO_DEPLOYMENT", value["scope"])
            self.assertIn("NO certifica IAM", value["warning"])
            self.assertNotIn(str(private), json.dumps(value))
            self.assertGreaterEqual(len(value["requirements"]), 4)

    def test_cli_fail_closed_without_secrets_in_output(self):
        denied = subprocess.run(
            [sys.executable, str(SCRIPT), "--provider", "silo"],
            capture_output=True, text=True, timeout=10
        )
        self.assertNotEqual(0, denied.returncode)
        self.assertEqual("BLOCKED", json.loads(denied.stdout)["status"])
        self.assertNotIn("Traceback", denied.stderr)

    def test_script_never_changes_compose_pilot_or_secrets(self):
        src = SCRIPT.read_text(encoding="utf-8")
        self.assertNotIn("subprocess.", src)
        self.assertNotIn("docker compose", src)
        self.assertNotIn("MINIO_ROOT_PASSWORD", src)
        self.assertIn("NO_DEPLOYMENT", src)


if __name__ == "__main__":
    unittest.main()
