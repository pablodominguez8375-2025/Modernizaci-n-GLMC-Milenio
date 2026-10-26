"""Real EF Core migration + runtime least-privilege smoke on ephemeral PostgreSQL.

Runs only in CI infrastructure tests with Docker available; no exposed ports,
persisted volumes, institutional credentials, MinIO or access to srv01.
"""
import json
import os
from pathlib import Path
import shutil
import subprocess
import time
import unittest
import urllib.error
import urllib.request
import uuid

ROOT = Path(__file__).resolve().parents[1]


class PilotDb2RealMigrationsTests(unittest.TestCase):
    @unittest.skipUnless(shutil.which("docker"), "Docker Engine no disponible")
    def test_nine_ef_contexts_and_restricted_runtime_over_real_postgres(self):
        suffix = uuid.uuid4().hex[:10]
        network = "pmgm-bd2-ef-" + suffix
        db = "pmgm-bd2-ef-pg-" + suffix
        api = "pmgm-bd2-ef-api-" + suffix
        image = "pmgm-bd2-ef:" + suffix
        pw = {
            "admin": "CiDb2Bootstrap2026!" + suffix,
            "keycloak": "CiDb2Keycloak2026!" + suffix,
            "migrator": "CiDb2Migrator2026!" + suffix,
            "runtime": "CiDb2Runtime2026!" + suffix,
            "reports": "CiDb2Reports2026!" + suffix,
        }

        def run(args, check=True, timeout=180):
            r = subprocess.run(args, text=True, stdout=subprocess.PIPE,
                               stderr=subprocess.PIPE, timeout=timeout)
            if check and r.returncode != 0:
                # Only synthetic test outputs. Do not dump environment/passwords.
                raise AssertionError(
                    "Falla en entorno efímero BD2: " + (r.stderr or r.stdout)[-2500:])
            return r

        def psql(username, password, sql, check=True):
            return run([
                "docker", "exec", "-e", "PGPASSWORD=" + password,
                db, "psql", "-X", "-h", "127.0.0.1", "-U", username,
                "-d", "pmgm", "-v", "ON_ERROR_STOP=1", "-tA", "-c", sql
            ], check=check, timeout=40)

        def credentials(role):
            return (
                "Host=postgres;Port=5432;Database=pmgm;"
                "Username=pmgm_" + role + ";Password=" + pw[role]
            )

        run(["docker", "network", "create", network])
        try:
            init = ROOT / "infrastructure/postgres"
            run([
                "docker", "run", "--detach", "--name", db,
                "--network", network, "--network-alias", "postgres",
                "-e", "POSTGRES_DB=pmgm",
                "-e", "POSTGRES_USER=pmgm_app",
                "-e", "POSTGRES_PASSWORD=" + pw["admin"],
                "-e", "PMGM_PILOT_KC_DB_PASSWORD=" + pw["keycloak"],
                "-e", "PMGM_PILOT_RUNTIME_DB_PASSWORD=" + pw["runtime"],
                "-e", "PMGM_PILOT_MIGRATOR_DB_PASSWORD=" + pw["migrator"],
                "-e", "PMGM_PILOT_REPORT_DB_PASSWORD=" + pw["reports"],
                "-v", str(init / "001-create-pilot-keycloak.sh")
                      + ":/docker-entrypoint-initdb.d/001-create-pilot-keycloak.sh:ro",
                "-v", str(init / "002-create-pilot-db2.sh")
                      + ":/docker-entrypoint-initdb.d/002-create-pilot-db2.sh:ro",
                "-v", str(ROOT / "scripts/grant-pilot-db2.sh")
                      + ":/scripts/grant-pilot-db2.sh:ro",
                "postgres:17",
            ])
            for _ in range(75):
                r = run(["docker", "exec", db, "pg_isready",
                         "-h", "127.0.0.1", "-U", "pmgm_app", "-d", "pmgm"],
                        check=False, timeout=10)
                if r.returncode == 0:
                    break
                time.sleep(2)
            else:
                self.fail("initdb PostgreSQL efímero no completó arranque")

            # Build the *actual* backend image, not a synthetic EF migrator.
            # Shell arguments do not contain credentials.
            run(["docker", "build", "-f", str(ROOT / "backend/Dockerfile"),
                 "-t", image, str(ROOT / "backend")], timeout=720)
            common_env = [
                "-e", "ASPNETCORE_ENVIRONMENT=Staging",
                "-e", "DemoData__Enabled=false",
                "-e", "ConnectionStrings__MainDatabase=" + credentials("migrator"),
            ]
            migrated = run([
                "docker", "run", "--rm", "--network", network,
                *common_env,
                "-e", "Database__ApplyMigrationsOnStartup=true",
                "-e", "Database__RunMigrationsAndExit=true",
                image,
            ], check=False, timeout=360)
            self.assertEqual(0, migrated.returncode,
                "Fallo migración EF Core con rol restringido: "
                + (migrated.stderr or migrated.stdout)[-2400:])

            # Verify a genuine EF table and the main module's migration history.
            self.assertEqual("t", psql("pmgm_migrator", pw["migrator"],
                "SELECT to_regclass('core.audit_events') IS NOT NULL;").stdout.strip())
            # #752: el migrador REAL de la imagen debe aplicar también las
            # migraciones propias de LodgeManagementDbContext (antes omitidas).
            self.assertEqual("t", psql("pmgm_migrator", pw["migrator"],
                "SELECT to_regclass('core.lodge_anonymous_ballots') IS NOT NULL;").stdout.strip())
            self.assertEqual("t", psql("pmgm_migrator", pw["migrator"],
                "SELECT EXISTS (SELECT 1 FROM information_schema.columns "
                "WHERE table_schema='core' AND table_name='lodge_anonymous_ballots' "
                "AND column_name='ProcedureNumber');").stdout.strip())
            self.assertGreater(int(psql("pmgm_migrator", pw["migrator"],
                'SELECT count(*) FROM public."__EFMigrationsHistory";'
            ).stdout.strip()), 20)

            run(["docker", "exec", "-e", "PGPASSWORD=" + pw["admin"],
                 db, "bash", "/scripts/grant-pilot-db2.sh"], timeout=90)
            # El rol de API puede leer la tabla, pero no migrarla.
            self.assertEqual("t", psql("pmgm_app", pw["admin"],
                "SELECT has_table_privilege('pmgm_runtime', "
                "'core.lodge_anonymous_ballots', 'SELECT');").stdout.strip())
            self.assertEqual(0, psql("pmgm_runtime", pw["runtime"],
                'SELECT "ProcedureNumber" FROM core.lodge_anonymous_ballots LIMIT 0;',
                check=False).returncode)

            # Historial EF migrator-only: no SELECT ni DML para runtime/reportes.
            # Comprobar tanto ACL como intentos SQL con identidades auténticas.
            for role in ("runtime", "reports"):
                for privilege in ("SELECT", "INSERT", "UPDATE", "DELETE", "TRUNCATE"):
                    query = (
                        "SELECT has_table_privilege('pmgm_" + role + "', "
                        "'public.\"__EFMigrationsHistory\"', '" + privilege + "');"
                    )
                    with self.subTest(role=role, privilege=privilege):
                        self.assertEqual("f", psql("pmgm_app", pw["admin"],
                             query).stdout.strip())
                self.assertNotEqual(0, psql("pmgm_" + role, pw[role],
                    'SELECT count(*) FROM public."__EFMigrationsHistory";',
                    check=False).returncode)
            self.assertGreater(int(psql("pmgm_migrator", pw["migrator"],
                'SELECT count(*) FROM public."__EFMigrationsHistory";'
            ).stdout.strip()), 20)


            # API must run under DML-only, not bootstrap or migrator.
            run([
                "docker", "run", "--detach", "--name", api,
                "--network", network,
                "-e", "ASPNETCORE_ENVIRONMENT=Staging",
                "-e", "ASPNETCORE_URLS=http://+:8080",
                "-e", "DemoData__Enabled=false",
                "-e", "Database__ApplyMigrationsOnStartup=false",
                "-e", "Database__RunMigrationsAndExit=false",
                "-e", "ConnectionStrings__MainDatabase=" + credentials("runtime"),
                image,
            ])
            ip = json.loads(run(["docker", "inspect", api]).stdout)[0][
                "NetworkSettings"]["Networks"][network]["IPAddress"]
            self.assertTrue(ip, "Falta IP del contenedor API efímero")
            healthy = False
            for _ in range(45):
                try:
                    with urllib.request.urlopen(
                        "http://" + ip + ":8080/health/ready", timeout=2
                    ) as resp:
                        if resp.status == 200:
                            healthy = True
                            break
                except (urllib.error.URLError, TimeoutError, OSError):
                    pass
                time.sleep(2)
            self.assertTrue(healthy, "API runtime no responde /health/ready")

            # Negative permissions on the complete migrated table.
            for sql in (
                "CREATE TABLE core.pmgm_bd2_ddl_denied(id integer);",
                "ALTER TABLE core.audit_events DISABLE TRIGGER ALL;",
                "UPDATE core.audit_events SET id=id WHERE FALSE;",
                "DELETE FROM core.audit_events WHERE FALSE;",
                "TRUNCATE TABLE core.audit_events;",
            ):
                with self.subTest(sql=sql):
                    self.assertNotEqual(0, psql("pmgm_runtime", pw["runtime"],
                         sql, check=False).returncode)
            self.assertNotEqual(0, psql("pmgm_reports", pw["reports"],
                 "SELECT * FROM core.audit_events LIMIT 0;",
                 check=False).returncode)
            self.assertEqual("f", psql("pmgm_app", pw["admin"],
                 "SELECT has_database_privilege('pmgm_runtime','pmgm_keycloak','CONNECT');"
            ).stdout.strip())

            # Backup / restore PostgreSQL-only with real EF schema and no persisted data.
            # MinIO recovery remains a separate blocked, non-certified integration.
            run(["docker", "stop", api], timeout=45)
            backup_path = "/tmp/pmgm_bd2_ci_" + suffix + ".dump"
            run(["docker", "exec", db, "pg_dump", "-U", "pmgm_app",
                 "-d", "pmgm", "--format=custom", "--no-owner",
                 "--no-privileges", "--file=" + backup_path], timeout=90)
            run(["docker", "exec", db, "pg_restore", "-U", "pmgm_app",
                 "-d", "pmgm", "--clean", "--if-exists", "--no-owner",
                 "--no-privileges", "--exit-on-error",
                 "--role=pmgm_migrator", backup_path], timeout=120)
            run(["docker", "exec", "-e", "PGPASSWORD=" + pw["admin"],
                 db, "bash", "/scripts/grant-pilot-db2.sh"], timeout=45)
            restored_owner = psql("pmgm_app", pw["admin"],
                 "SELECT pg_get_userbyid(relowner) FROM pg_class "
                 "WHERE oid='core.audit_events'::regclass;").stdout.strip()
            self.assertEqual("pmgm_migrator", restored_owner)
            self.assertEqual("f", psql("pmgm_app", pw["admin"],
                 "SELECT has_table_privilege('pmgm_runtime', "
                 "'core.audit_events', 'UPDATE');").stdout.strip())
            self.assertEqual("t", psql("pmgm_app", pw["admin"],
                 "SELECT has_table_privilege('pmgm_runtime', "
                 "'core.audit_events', 'INSERT');").stdout.strip())
            self.assertEqual("f", psql("pmgm_app", pw["admin"],
                 "SELECT has_table_privilege('pmgm_reports', "
                 "'core.audit_events', 'SELECT');").stdout.strip())
            self.assertGreater(int(psql("pmgm_migrator", pw["migrator"],
                'SELECT count(*) FROM public."__EFMigrationsHistory";'
            ).stdout.strip()), 20)
            for role in ("runtime", "reports"):
                self.assertNotEqual(0, psql("pmgm_" + role, pw[role],
                    'SELECT * FROM public."__EFMigrationsHistory" LIMIT 0;',
                    check=False).returncode)
            run(["docker", "start", api], timeout=45)
            for attempt in range(45):
                try:
                    with urllib.request.urlopen(
                        "http://" + ip + ":8080/health/ready", timeout=2
                    ) as response:
                        if response.status == 200:
                            break
                except (urllib.error.URLError, TimeoutError, OSError):
                    pass
                time.sleep(2)
            else:
                self.fail("API runtime no recuperó disponibilidad tras restore PostgreSQL BD2")
        finally:
            run(["docker", "rm", "-f", api], check=False, timeout=45)
            run(["docker", "rm", "-f", db], check=False, timeout=45)
            run(["docker", "network", "rm", network], check=False, timeout=45)
            run(["docker", "image", "rm", "-f", image], check=False, timeout=45)


if __name__ == "__main__":
    unittest.main()
