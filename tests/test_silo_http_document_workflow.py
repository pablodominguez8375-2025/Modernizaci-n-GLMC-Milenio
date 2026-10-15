"""#470: real HTTP document workflow on isolated PostgreSQL/Silo/ClamAV.

The actual ASP.NET Minimal API routes execute through WebApplicationFactory
with a **synthetic test authentication handler**. This is NOT Keycloak/OIDC
acceptance. All resources and credentials are discarded, never deployed.
"""
import json
import os
from pathlib import Path
import re
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
from test_silo_dotnet_clamav import CLAMAV_IMAGE, _wait_scanner

ROOT = Path(__file__).resolve().parents[1]
PG_IMAGE = "postgres:17-alpine"
TARGET = "PMGM.Api.Tests.Integration.SiloDocumentHttpLifecycleRealProbe"
METHOD = "Authenticated_test_HTTP_enforces_scan_gate_audit_and_rejects_infected_content"


def _wait_postgres(container, username, database):
    for _ in range(60):
        ready = run(["docker", "exec", container, "pg_isready",
                     "-U", username, "-d", database],
                    check=False, timeout=8)
        if ready is not None and ready.returncode == 0:
            return
        time.sleep(2)
    raise AssertionError("PostgreSQL efímero no quedó listo.")


class SiloDocumentHttpRealWorkflowTests(unittest.TestCase):
    def test_api_http_auth_silo_clamav_postgres_and_audit_work_together(self):
        if not shutil.which("docker"):
            if os.getenv("GITHUB_ACTIONS"):
                self.fail("CI requiere Docker: HTTP integral no puede fingirse.")
            self.skipTest("Docker no disponible; test de laboratorio se ejecuta en CI.")

        marker = uuid.uuid4().hex[:9]
        network = "pmgm-http-ci-" + marker
        silo = "pmgm-http-silo-" + marker
        clamav = "pmgm-http-av-" + marker
        postgres = "pmgm-http-pg-" + marker
        dotnet = "pmgm-http-dotnet-" + marker
        pg_user = "pmgm"
        pg_db = "pmgm_ci"
        pg_pass = secrets.token_urlsafe(32)
        root = "root" + secrets.token_hex(8)
        root_pw = secrets.token_urlsafe(32)
        app = "docs" + secrets.token_hex(8)
        app_pw = secrets.token_urlsafe(32)

        try:
            with tempfile.TemporaryDirectory(prefix="pmgm-http-ci-") as tmp:
                policy = Path(tmp) / "documents-policy.json"
                policy.write_text(json.dumps(POLICY), encoding="utf-8")
                run(["docker", "network", "create", network])
                run(["docker", "pull", IMAGE], timeout=240)
                _start(silo, network, root, root_pw)
                _provision(silo, policy, root, root_pw, app, app_pw)

                run(["docker", "pull", CLAMAV_IMAGE], timeout=300)
                run(["docker", "run", "--detach", "--name", clamav,
                     "--network", network, CLAMAV_IMAGE], timeout=100)
                _wait_scanner(clamav, network)

                run(["docker", "pull", PG_IMAGE], timeout=300)
                run(["docker", "run", "--detach", "--name", postgres,
                     "--network", network,
                     "--env", "POSTGRES_USER=" + pg_user,
                     "--env", "POSTGRES_PASSWORD=" + pg_pass,
                     "--env", "POSTGRES_DB=" + pg_db,
                     "--env", "PGDATA=/var/lib/postgresql/data/pgci",
                     "--tmpfs", "/var/lib/postgresql/data",
                     PG_IMAGE], timeout=90)
                _wait_postgres(postgres, pg_user, pg_db)

                # The database, object store, clamd and SDK live solely within
                # an isolated Docker network. No 'docker -p', no host ports.
                run(["docker", "pull", SDK_IMAGE], timeout=420)
                dotnet_result = run([
                    "docker", "run", "--rm", "--name", dotnet,
                    "--network", network,
                    "--mount", "type=bind,source=" + str(ROOT) + ",target=/repo",
                    "--mount", "type=bind,source=" + str(Path(tmp)) + ",target=/evidence",
                    "--workdir", "/repo",
                    "--env", "DOTNET_CLI_TELEMETRY_OPTOUT=1",
                    "--env", "DOTNET_NOLOGO=1",
                    "--env", "PMGM_TEST_REQUIRE_REAL_HTTP_DOCS=1",
                    "--env", "PMGM_TEST_POSTGRES=Host=" + postgres +
                             ";Port=5432;Database=" + pg_db +
                             ";Username=" + pg_user + ";Password=" + pg_pass,
                    "--env", "PMGM_TEST_S3_URL=http://" + silo + ":9000",
                    "--env", "PMGM_TEST_S3_ACCESS_KEY=" + app,
                    "--env", "PMGM_TEST_S3_SECRET_KEY=" + app_pw,
                    "--env", "PMGM_TEST_S3_BUCKET=pmgm-documents",
                    "--env", "PMGM_TEST_CLAMAV_HOST=" + clamav,
                    "--env", "PMGM_TEST_CLAMAV_PORT=3310",
                    SDK_IMAGE,
                    "dotnet", "test",
                    "backend/tests/PMGM.Api.Tests/PMGM.Api.Tests.csproj",
                    "--configuration", "Release",
                    "--filter", "FullyQualifiedName~" + TARGET,
                    "--results-directory", "/evidence",
                    "--report-xunit-trx",
                    "--report-xunit-trx-filename", "real-http-documents.trx",
                    "--xunit-info"
                ], timeout=960, check=False)

                # Guard against false-green (zero tests) and failure concealed
                # by wrapper scripts. No text test logs or secrets are printed.
                trx = Path(tmp) / "real-http-documents.trx"
                if not trx.is_file():
                    # Keep stdout/stderr private; only report toolchain error
                    # codes, never connection strings or synthetic passwords.
                    combined = (
                        (dotnet_result.stdout or "") + "\n" + (dotnet_result.stderr or "")
                        if dotnet_result is not None else ""
                    )
                    codes = sorted(set(re.findall(
                        r"(?:error\s+(?:CS|NU)\d{4}|MSB\d{4})",
                        combined, flags=re.IGNORECASE
                    )))
                    exit_code = (
                        dotnet_result.returncode if dotnet_result is not None
                        else "timeout"
                    )
                    raise AssertionError(
                        f"Sin TRX HTTP: dotnet exit={exit_code}; compiler={codes[:4]}"
                    )
                results = ET.parse(trx).getroot()
                rows = [x for x in results.iter()
                        if x.tag.rsplit("}", 1)[-1] == "UnitTestResult"]
                matching = [
                    row for row in rows
                    if TARGET in row.attrib.get("testName", "")
                    or METHOD in row.attrib.get("testName", "")
                ]
                self.assertEqual(1, len(matching),
                                 "El flujo HTTP C# no se ejecutó exactamente una vez.")
                outcome = matching[0].attrib.get("outcome")
                if outcome != "Passed":
                    message = next(
                        (x.text or "" for x in matching[0].iter()
                         if x.tag.rsplit("}", 1)[-1] == "Message"),
                        "No test message"
                    )
                    for secret in (root_pw, app_pw, pg_pass, root, app):
                        message = message.replace(secret, "[synthetic-redacted]")
                    # Report the failing C# source line (not any raw stack,
                    # environment variable, connection string or process arg).
                    trace = next(
                        (x.text or "" for x in matching[0].iter()
                         if x.tag.rsplit("}", 1)[-1] == "StackTrace"),
                        ""
                    )
                    frame = re.search(
                        r"SiloDocumentHttpLifecycleRealProbe\.cs:line\s+(\d+)",
                        trace
                    )
                    csharp_line = frame.group(1) if frame else "unknown"
                    # Only report exception CLASS NAMES from SDK/test-host logs.
                    # The raw outputs are private and may contain lab passwords.
                    raw_logs = (
                        (dotnet_result.stdout or "") + "\n" +
                        (dotnet_result.stderr or "")
                    ) if dotnet_result is not None else ""
                    types = sorted(set(re.findall(
                        r"(?:Amazon|Npgsql|System|Microsoft)[A-Za-z0-9_.]*(?:Exception|Error)\b",
                        raw_logs
                    )))[:5]
                    raise AssertionError(
                        f"HTTP C# integration failed at C# line {csharp_line}, "
                        f"exception_types={types}: " +
                        message.replace("\r", " ").replace("\n", " ")[:400]
                    )
                self.assertIsNotNone(dotnet_result)
                self.assertEqual(0, dotnet_result.returncode)
        finally:
            for container in (dotnet, postgres, clamav, silo):
                run(["docker", "rm", "-f", container],
                    check=False, timeout=35)
            run(["docker", "network", "rm", network], check=False, timeout=35)


if __name__ == "__main__":
    unittest.main()
