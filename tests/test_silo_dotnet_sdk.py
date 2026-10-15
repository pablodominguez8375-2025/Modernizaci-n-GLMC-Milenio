"""#470 — real production AWS S3 .NET SDK integration with patched Silo.

Invoked by the existing infrastructure job. All credentials and data are
synthetic, Docker has no published ports, and no real deployment is made.
"""
import os
from pathlib import Path
import secrets
import shutil
import tempfile
import unittest
import uuid
import xml.etree.ElementTree as ET

from test_silo_real_iam import BUCKET, FORBIDDEN_BUCKET, IMAGE, POLICY, run
from test_silo_real_recovery import _provision, _start

ROOT = Path(__file__).resolve().parents[1]
SDK_IMAGE = "mcr.microsoft.com/dotnet/sdk:10.0"
TARGET = ("PMGM.Api.Tests.Integration."
          "SiloDocumentObjectStoreRealIntegrationTests")


class SiloDotNetSdkRealIntegrationTests(unittest.TestCase):
    def test_actual_api_s3_document_store_uses_restricted_silo_account(self):
        if not shutil.which("docker"):
            if os.getenv("GITHUB_ACTIONS"):
                self.fail("Docker es requisito: no fingir compatibilidad SDK .NET.")
            self.skipTest("Sin Docker local; test real se exige en GitHub Actions.")

        suffix = uuid.uuid4().hex[:11]
        network = "pmgm-sdk-lab-" + suffix
        server = "pmgm-sdk-silo-" + suffix
        sdk = "pmgm-sdk-dotnet-" + suffix
        admin = "root" + secrets.token_hex(8)
        app_user = "docs" + secrets.token_hex(8)
        admin_pw = secrets.token_urlsafe(32)
        app_pw = secrets.token_urlsafe(32)

        try:
            with tempfile.TemporaryDirectory(prefix="pmgm-sdk-silo-") as tmp:
                policy = Path(tmp) / "policy.json"
                policy.write_text(
                    __import__("json").dumps(POLICY), encoding="utf-8"
                )
                run(["docker", "network", "create", network])
                run(["docker", "pull", IMAGE], timeout=240)
                _start(server, network, admin, admin_pw)
                _provision(server, policy, admin, admin_pw, app_user, app_pw)

                # Real .NET 10 + AmazonS3Client + production S3DocumentObjectStore.
                # Separate SDK container connects through Docker DNS to Silo.
                run(["docker", "pull", SDK_IMAGE], timeout=420)
                test = run([
                    "docker", "run", "--rm", "--name", sdk,
                    "--network", network,
                    "--mount", "type=bind,source=" + str(ROOT) +
                               ",target=/repo",
                    "--mount", "type=bind,source=" + str(Path(tmp)) +
                               ",target=/evidence",
                    "--workdir", "/repo",
                    "--env", "DOTNET_CLI_TELEMETRY_OPTOUT=1",
                    "--env", "DOTNET_NOLOGO=1",
                    "--env", "PMGM_TEST_REQUIRE_REAL_SILO=1",
                    "--env", "PMGM_TEST_S3_URL=http://" + server + ":9000",
                    "--env", "PMGM_TEST_S3_ACCESS_KEY=" + app_user,
                    "--env", "PMGM_TEST_S3_SECRET_KEY=" + app_pw,
                    "--env", "PMGM_TEST_S3_BUCKET=" + BUCKET,
                    "--env", "PMGM_TEST_S3_FORBIDDEN_BUCKET=" + FORBIDDEN_BUCKET,
                    SDK_IMAGE,
                    "dotnet", "test",
                    "backend/tests/PMGM.Api.Tests/PMGM.Api.Tests.csproj",
                    "--configuration", "Release",
                    "--filter", "FullyQualifiedName~" + TARGET,
                    "--results-directory", "/evidence",
                    "--report-xunit-trx",
                    "--report-xunit-trx-filename", "silo-real-dotnet.trx",
                    "--xunit-info",
                ], timeout=780)

                # Prevent false-green if the test runner silently filters out
                # every test, or if the environment-sensitive test did not run.
                trx = Path(tmp) / "silo-real-dotnet.trx"
                self.assertTrue(trx.is_file(), "No hay resultado .NET TRX.")
                xml = ET.parse(trx).getroot()
                rows = [item for item in xml.iter()
                        if item.tag.rsplit("}", 1)[-1] == "UnitTestResult"]
                matching = [item for item in rows
                            if TARGET in item.attrib.get("testName", "")
                            or "Restricted_Silo_identity_operates_document_store"
                               in item.attrib.get("testName", "")]
                self.assertEqual(
                    1, len(matching),
                    "El test real SDK no se ejecutó exactamente una vez."
                )
                self.assertEqual("Passed", matching[0].attrib.get("outcome"))
                self.assertEqual(0, test.returncode)
        finally:
            run(["docker", "rm", "-f", sdk], check=False, timeout=30)
            run(["docker", "rm", "-f", server], check=False, timeout=30)
            run(["docker", "network", "rm", network], check=False, timeout=30)


if __name__ == "__main__":
    unittest.main()
