"""Integration gate against disposable PostgreSQL 17 with synthetic credentials only.

Runs in GitHub Actions QA packaging, with no host ports, no persisted volume,
no institutional members or data. Also opt-in locally PMGM_QA_DB2_REAL_TEST=1.
"""
import os
from pathlib import Path
import shutil
import subprocess
import time
import uuid
import unittest

ROOT = Path(__file__).resolve().parents[1]
ENABLED = os.environ.get("GITHUB_ACTIONS") == "true" or os.environ.get("PMGM_QA_DB2_REAL_TEST") == "1"


@unittest.skipUnless(ENABLED and shutil.which("docker"), "Docker PostgreSQL runtime gate: sólo CI/opt-in")
class QaBd2PostgresRuntimeTests(unittest.TestCase):
    def test_new_volume_rejects_superuser_and_unowned_data(self):
        name = "pmgm-qa-g4-" + uuid.uuid4().hex[:10]
        network = name + "-net"
        pw = {
            "pmgm_app": "CiQAG4Bootstrap2026!Unique",
            "pmgm_keycloak": "CiQAG4Keycloak2026!Unique",
            "pmgm_runtime": "CiQAG4Runtime2026!Unique",
            "pmgm_migrator": "CiQAG4Migrator2026!Unique",
            "pmgm_reports": "CiQAG4Reports2026!Unique",
        }

        def run(args, *, check=True, timeout=120):
            out = subprocess.run(args, capture_output=True, text=True, timeout=timeout)
            if check and out.returncode:
                # Never log credentials or raw command invocation.
                raise AssertionError("Laboratorio PostgreSQL QA G4 no terminó correctamente")
            return out

        def sql(user, query, *, database="pmgm", check=True):
            return run([
                "docker", "exec", "-e", "PGPASSWORD=" + pw[user],
                name, "psql", "-X", "-h", "127.0.0.1", "-U", user, "-d", database,
                "-v", "ON_ERROR_STOP=1", "-tA", "-c", query,
            ], check=check)

        run(["docker", "network", "create", network])
        try:
            run([
                "docker", "run", "-d", "--name", name, "--network", network,
                "--network-alias", "postgres",
                "-e", "POSTGRES_DB=pmgm", "-e", "POSTGRES_USER=pmgm_app",
                "-e", "POSTGRES_PASSWORD=" + pw["pmgm_app"],
                "-e", "PMGM_QA_KC_DB_PASSWORD=" + pw["pmgm_keycloak"],
                "-e", "PMGM_QA_RUNTIME_DB_PASSWORD=" + pw["pmgm_runtime"],
                "-e", "PMGM_QA_MIGRATOR_DB_PASSWORD=" + pw["pmgm_migrator"],
                "-e", "PMGM_QA_REPORT_DB_PASSWORD=" + pw["pmgm_reports"],
                "-v", str(ROOT / "infrastructure/postgres/05-create-qa-db2.sh")
                      + ":/docker-entrypoint-initdb.d/05-create-qa-db2.sh:ro",
                "-v", str(ROOT / "infrastructure/postgres/init-srv01.sql")
                      + ":/docker-entrypoint-initdb.d/10-init-srv01.sql:ro",
                "-v", str(ROOT / "scripts/grant-pilot-db2.sh")
                      + ":/scripts/grant-pilot-db2.sh:ro",
                "postgres:17",
            ], timeout=180)

            for _ in range(50):
                ready = run(["docker", "exec", name, "pg_isready",
                             "-h", "127.0.0.1", "-U", "pmgm_app", "-d", "pmgm"],
                            check=False, timeout=8)
                if ready.returncode == 0:
                    break
                time.sleep(2)
            else:
                self.fail("El initdb QA G4 no quedó listo")

            self.assertEqual("pmgm_migrator", sql("pmgm_app",
                "SELECT pg_get_userbyid(datdba) FROM pg_database WHERE datname='pmgm';").stdout.strip())
            self.assertEqual("pmgm_keycloak", sql("pmgm_app",
                "SELECT pg_get_userbyid(datdba) FROM pg_database WHERE datname='pmgm_keycloak';").stdout.strip())

            sql("pmgm_migrator", "CREATE SCHEMA core;"
                "CREATE TABLE core.audit_events "
                "(id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY, payload text NOT NULL);"
                'CREATE TABLE public."__EFMigrationsHistory" (id text PRIMARY KEY);')

            run(["docker", "exec", "-e", "PGPASSWORD=" + pw["pmgm_app"],
                 name, "bash", "/scripts/grant-pilot-db2.sh"])
            sql("pmgm_runtime", "INSERT INTO core.audit_events(payload) VALUES ('synthetic-only');")
            self.assertEqual("1", sql("pmgm_runtime",
                "SELECT count(*) FROM core.audit_events;").stdout.strip())

            for statement in (
                "CREATE TABLE core.g4_forbidden (id int);",
                "ALTER TABLE core.audit_events DISABLE TRIGGER ALL;",
                "DELETE FROM core.audit_events WHERE FALSE;",
                "UPDATE core.audit_events SET payload='x' WHERE FALSE;",
                "TRUNCATE TABLE core.audit_events;",
            ):
                with self.subTest(operation=statement.split()[0]):
                    denied = sql("pmgm_runtime", "BEGIN;" + statement + " ROLLBACK;", check=False)
                    self.assertNotEqual(0, denied.returncode, "runtime obtuvo DDL o mutación auditoría")

            self.assertNotEqual(0, sql("pmgm_runtime",
                'SELECT * FROM public."__EFMigrationsHistory" LIMIT 0;', check=False).returncode)
            self.assertNotEqual(0, sql("pmgm_reports",
                "SELECT * FROM core.audit_events LIMIT 0;", check=False).returncode)
            self.assertNotEqual(0, sql("pmgm_keycloak",
                "SELECT 1;", database="pmgm", check=False).returncode)
            self.assertNotEqual(0, sql("pmgm_runtime",
                "SELECT 1;", database="pmgm_keycloak", check=False).returncode)
        finally:
            run(["docker", "rm", "-f", name], check=False, timeout=30)
            run(["docker", "network", "rm", network], check=False, timeout=30)


if __name__ == "__main__":
    unittest.main()
