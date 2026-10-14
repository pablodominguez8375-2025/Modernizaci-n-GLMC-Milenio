"""Policy regression tests for MinIO documentary service account (Issue #470).

Checks intended IAM scope and Compose/preflight wiring. CI S3Mock doesn't enforce
MinIO IAM; real MinIO deny tests are an explicit operational acceptance gate.
"""
import json
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
POLICY = ROOT / "infrastructure/minio/pmgm-documents-policy.json"


class MinioPilotPolicyTests(unittest.TestCase):
    def test_documentary_policy_has_object_only_permissions(self):
        p = json.loads(POLICY.read_text(encoding="utf-8"))
        self.assertEqual("2012-10-17", p["Version"])
        self.assertEqual(1, len(p["Statement"]))
        statement = p["Statement"][0]
        self.assertEqual("Allow", statement["Effect"])
        self.assertEqual(
            {"s3:GetObject", "s3:PutObject", "s3:DeleteObject"},
            set(statement["Action"]),
        )
        self.assertEqual(
            ["arn:aws:s3:::pmgm-documents/*"], statement["Resource"]
        )
        self.assertNotIn("admin:*", statement["Action"])
        self.assertNotIn("s3:*", statement["Action"])

    def test_minio_root_stays_provisioning_only(self):
        compose = (ROOT / "infrastructure/docker-compose.pilot.yml").read_text()
        minio_init = compose.split("  minio-init:\n", 1)[1].split("  clamav:\n", 1)[0]
        api = compose.split("  api:\n", 1)[1].split("  web:\n", 1)[0]

        self.assertIn("mc admin policy create pmgm pmgm-documents-app", minio_init)
        self.assertIn("mc admin user add pmgm", minio_init)
        self.assertIn("mc admin policy attach pmgm pmgm-documents-app", minio_init)
        self.assertIn("pmgm-documents-policy.json:/etc/pmgm/documents-policy.json:ro", minio_init)
        self.assertIn("MINIO_APP_ACCESS_KEY:", minio_init)
        self.assertIn("MINIO_APP_SECRET_KEY:", minio_init)
        self.assertIn("DocumentStorage__AccessKey: ${PMGM_PILOT_MINIO_APP_USER:", api)
        self.assertIn("DocumentStorage__SecretKey: ${PMGM_PILOT_MINIO_APP_PASSWORD:", api)
        self.assertNotIn("DocumentStorage__AccessKey: ${PMGM_PILOT_MINIO_USER:", api)
        self.assertNotIn("DocumentStorage__SecretKey: ${PMGM_PILOT_MINIO_PASSWORD:", api)

    def test_pilot_preflight_fails_for_shared_credentials(self):
        script = (ROOT / "scripts/preflight-pilot.sh").read_text()
        template = (ROOT / "infrastructure/pilot.env.example").read_text()
        for name in ("PMGM_PILOT_MINIO_APP_USER", "PMGM_PILOT_MINIO_APP_PASSWORD"):
            self.assertIn(name, script)
            self.assertIn(name + "=", template)
        self.assertIn("if not app_user or app_user == root_user:", script)
        self.assertIn('"PMGM_PILOT_MINIO_APP_PASSWORD",', script)
        self.assertIn("if len(set(secrets)) != len(secrets):", script)

    def test_ci_has_synthetic_service_credentials_for_both_pilot_jobs(self):
        workflow = (ROOT / ".github/workflows/ci.yml").read_text()
        self.assertEqual(2, workflow.count("PMGM_PILOT_MINIO_APP_USER: ci-pilot-documents"))
        self.assertEqual(2, workflow.count("PMGM_PILOT_MINIO_APP_PASSWORD: CiPilotDocs2026SecureOnly!"))
        self.assertIn("PMGM_PILOT_MINIO_APP_USER=$PMGM_PILOT_MINIO_APP_USER", workflow)
        self.assertIn("PMGM_PILOT_MINIO_APP_PASSWORD=$PMGM_PILOT_MINIO_APP_PASSWORD", workflow)


if __name__ == "__main__":
    unittest.main()
