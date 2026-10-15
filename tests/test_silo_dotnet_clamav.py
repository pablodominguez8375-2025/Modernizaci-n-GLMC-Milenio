"""Real Silo + ClamAV + product .NET component lifecycle acceptance (#470).

No institution data or credentials. No exposed TCP ports or shared volumes.
A missing/unreachable scanner or skipped C# test must fail CI.
"""
import json
import os
from pathlib import Path
import secrets
import shutil
import socket
import tempfile
import time
import unittest
import uuid
import xml.etree.ElementTree as ET

from test_silo_real_iam import IMAGE, POLICY, run
from test_silo_real_recovery import _provision, _start
from test_silo_dotnet_sdk import SDK_IMAGE

ROOT = Path(__file__).resolve().parents[1]
CLAMAV_IMAGE = "clamav/clamav:1.4"
TARGET = "PMGM.Api.Tests.Integration.SiloDocumentMalwareRealProbe"
METHOD = "Actual_S3_and_ClamAv_components_keep_infected_and_unscanned_content_unavailable"


def _wait_scanner(container, network):
    # The Docker bridge address is internal: no -p or host exposure.
    inspect = json.loads(run(["docker", "inspect", container]).stdout)[0]
    ip = inspect["NetworkSettings"]["Networks"][network]["IPAddress"]
    for _ in range(75):
        try:
            with socket.create_connection((ip, 3310), timeout=2):
                return
        except (OSError, TimeoutError):
            time.sleep(3)
    raise AssertionError("ClamAV no alcanzó readiness en red efímera.")


class SiloDotNetClamAvLifecycleTests(unittest.TestCase):
    def test_actual_silo_storage_and_real_clamav_are_fail_closed(self):
        if not shutil.which("docker"):
            if os.getenv("GITHUB_ACTIONS"):
                self.fail("Docker obligatorio para Silo/ClamAV real en CI.")
            self.skipTest("Sin Docker: prueba real solo en runner compatible.")

        suffix = uuid.uuid4().hex[:10]
        network = "pmgm-silo-av-ci-" + suffix
        silo = "pmgm-silo-av-" + suffix
        clam = "pmgm-clamav-ci-" + suffix
        sdk = "pmgm-dotnet-av-ci-" + suffix
        root = "root" + secrets.token_hex(8)
        app = "docs" + secrets.token_hex(8)
        root_pw = secrets.token_urlsafe(32)
        app_pw = secrets.token_urlsafe(32)

        try:
            with tempfile.TemporaryDirectory(prefix="pmgm-av-silo-") as tmp:
                policy = Path(tmp) / "policy.json"
                policy.write_text(json.dumps(POLICY), encoding="utf-8")

                run(["docker", "network", "create", network])
                run(["docker", "pull", IMAGE], timeout=240)
                _start(silo, network, root, root_pw)
                _provision(silo, policy, root, root_pw, app, app_pw)

                run(["docker", "pull", CLAMAV_IMAGE], timeout=240)
                run(["docker", "run", "-d", "--name", clam,
                     "--network", network, CLAMAV_IMAGE], timeout=100)
                _wait_scanner(clam, network)

                run(["docker", "pull", SDK_IMAGE], timeout=420)
                outcome = run([
                    "docker", "run", "--rm", "--name", sdk,
                    "--network", network,
                    "--mount", "type=bind,source=" + str(ROOT) + ",target=/repo",
                    "--mount", "type=bind,source=" + str(Path(tmp)) + ",target=/evidence",
                    "--workdir", "/repo",
                    "--env", "DOTNET_CLI_TELEMETRY_OPTOUT=1",
                    "--env", "DOTNET_NOLOGO=1",
                    "--env", "PMGM_TEST_REQUIRE_REAL_SILO_CLAMAV=1",
                    "--env", "PMGM_TEST_S3_URL=http://" + silo + ":9000",
                    "--env", "PMGM_TEST_S3_ACCESS_KEY=" + app,
                    "--env", "PMGM_TEST_S3_SECRET_KEY=" + app_pw,
                    "--env", "PMGM_TEST_S3_BUCKET=pmgm-documents",
                    "--env", "PMGM_TEST_CLAMAV_HOST=" + clam,
                    "--env", "PMGM_TEST_CLAMAV_PORT=3310",
                    SDK_IMAGE,
                    "dotnet", "test",
                    "backend/tests/PMGM.Api.Tests/PMGM.Api.Tests.csproj",
                    "--configuration", "Release",
                    "--filter", "FullyQualifiedName~" + TARGET,
                    "--results-directory", "/evidence",
                    "--report-xunit-trx",
                    "--report-xunit-trx-filename", "silo-clamav-dotnet.trx",
                    "--xunit-info"
                ], timeout=800)

                # Explicit proof a single product C# test actually ran and passed.
                trx = Path(tmp) / "silo-clamav-dotnet.trx"
                self.assertTrue(trx.is_file(), "TRX de Silo/ClamAV real no generado.")
                xml = ET.parse(trx).getroot()
                tests = [x for x in xml.iter()
                         if x.tag.rsplit("}", 1)[-1] == "UnitTestResult"]
                selected = [x for x in tests
                            if TARGET in x.attrib.get("testName", "")
                            or METHOD in x.attrib.get("testName", "")]
                self.assertEqual(1, len(selected),
                                 "Filtro no ejecutó EXACTAMENTE un test C# de malware.")
                self.assertEqual("Passed", selected[0].attrib.get("outcome"))
                self.assertEqual(0, outcome.returncode)
        finally:
            for container in (sdk, clam, silo):
                run(["docker", "rm", "-f", container], check=False, timeout=35)
            run(["docker", "network", "rm", network], check=False, timeout=35)


if __name__ == "__main__":
    unittest.main()
