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
PINNED_SILO_FIXED = "docker.io/pgsty/silo@sha256:aaac6bce13eb99316b3c88bd20143fc3c38ab607585256a411a095c44ee57f62"


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

    def test_silo_patched_release_exact_digest_only_allows_lab(self):
        result = module.evaluate(
            "silo", image=PINNED_SILO_FIXED, acknowledgment=module.ACK_TEXT
        )
        self.assertEqual("LAB_PRECHECK_ONLY", result["status"])
        self.assertEqual("NO_DEPLOYMENT", result["scope"])
        self.assertEqual("RELEASE.2026-10-15T00-00-00Z", result["release"])
        self.assertEqual("7a3ca0f82100cce06289a465f54139da2639f642",
                         result["source_commit"])
        self.assertIn("NO certifica origen remoto", result["warning"])
        self.assertGreaterEqual(len(result["requirements"]), 5)

    def test_silo_patched_image_needs_explicit_lab_consent(self):
        self.check_blocked("silo", image=PINNED_SILO_FIXED)
        self.check_blocked("silo", image=PINNED_SILO_FIXED,
                           acknowledgment="AUTORIZO_DESPLIEGUE")

    def test_silo_rejects_tags_old_version_and_other_digests(self):
        for image in (
            "",
            "docker.io/pgsty/silo:latest",
            "docker.io/pgsty/silo:RELEASE.2026-09-16T00-00-00Z",
            "docker.io/pgsty/silo:RELEASE.2026-10-15T00-00-00Z",
            "docker.io/pgsty/silo@sha256:635197cb9f36d01bee221d34d1c7d7960f6a95c48b0b6c01d99cd13bdae51a46",
            "docker.io/pgsty/silo@sha256:" + "b" * 64,
            "docker.io/unknown/silo@sha256:" + PINNED_SILO_FIXED.rsplit(":", 1)[1],
        ):
            with self.subTest(image=image):
                self.check_blocked("silo", image=image, acknowledgment=module.ACK_TEXT)

    def test_silo_cli_is_explicitly_only_a_lab_precheck(self):
        result = subprocess.run([
            sys.executable, str(SCRIPT), "--provider", "silo",
            "--image", PINNED_SILO_FIXED, "--ack-lab", module.ACK_TEXT
        ], capture_output=True, text=True, timeout=10)
        self.assertEqual(0, result.returncode)
        response = json.loads(result.stdout)
        self.assertEqual("LAB_PRECHECK_ONLY", response["status"])
        self.assertEqual("NO_DEPLOYMENT", response["scope"])
        self.assertNotIn("minio root", result.stdout.lower())

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
