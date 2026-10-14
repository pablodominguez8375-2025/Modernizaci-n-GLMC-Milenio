"""BD2 pilot least-privilege contract (structural checks, no real credentials).

Real PostgreSQL negative probes live in scripts/check-pilot-db2-roles.sh and must
pass against a NEW disposable volume before BD2 can leave DRAFT.
"""
import unittest
import json
import os
import shutil
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def read(path):
    return (ROOT / path).read_text(encoding="utf-8")


class PilotDb2IsolationTests(unittest.TestCase):
    def test_migrate_only_exits_before_http_listening_or_seed(self):
        api = read("backend/src/PMGM.Api/Program.cs")
        self.assertIn('GetValue<bool>("Database:RunMigrationsAndExit")', api)
        self.assertIn("if (migrationsOnly) return;", api)
        self.assertLess(api.index("if (migrationsOnly) return;"), api.index('GetValue<bool>("DemoData:Enabled")'))
        self.assertLess(api.index("if (migrationsOnly) return;"), api.index('app.Run();'))
        self.assertIn('GetValue<bool>("Database:ApplyMigrationsOnStartup")', api)

    def test_overlay_is_opt_in_and_does_not_change_original_compose(self):
        source = read("infrastructure/docker-compose.pilot-db2.yml")
        self.assertIn("Database__RunMigrationsAndExit: \"true\"", source)
        self.assertIn("Database__ApplyMigrationsOnStartup: \"false\"", source)
        self.assertIn("Username=pmgm_migrator;", source)
        self.assertIn("Username=pmgm_runtime;", source)
        self.assertIn("service_completed_successfully", source)
        self.assertIn("002-create-pilot-db2.sh", source)
        self.assertNotIn('command: ["--migrate-only"]', source)
        original = read("infrastructure/docker-compose.pilot.yml")
        self.assertIn("KC_DB_USERNAME: pmgm_keycloak", original)
        self.assertIn("Database__ApplyMigrationsOnStartup: \"true\"", original)

    def test_new_shell_scripts_parse(self):
        scripts = (
            "infrastructure/postgres/002-create-pilot-db2.sh",
            "scripts/grant-pilot-db2.sh",
            "scripts/preflight-pilot-db2.sh",
            "scripts/start-pilot-db2.sh",
            "scripts/check-pilot-db2-roles.sh",
        )
        for path in scripts:
            with self.subTest(path=path):
                subprocess.run(["bash", "-n", str(ROOT / path)], check=True,
                               stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)

    @unittest.skipUnless(shutil.which("docker"), "Docker Compose no disponible")
    def test_compose_overlay_resolves_least_privilege_credentials(self):
        env = dict(os.environ)
        env.update({
            "PMGM_PILOT_RUNTIME_DB_PASSWORD": "ci-runtime-bd2-value-unique-2026",
            "PMGM_PILOT_MIGRATOR_DB_PASSWORD": "ci-migrator-bd2-value-unique-2026",
            "PMGM_PILOT_REPORT_DB_PASSWORD": "ci-report-bd2-value-unique-2026",
        })
        result = subprocess.run([
            "docker", "compose",
            "-f", str(ROOT / "infrastructure/docker-compose.pilot.yml"),
            "-f", str(ROOT / "infrastructure/docker-compose.pilot-db2.yml"),
            "config", "--format", "json"
        ], env=env, check=True, stdout=subprocess.PIPE,
           stderr=subprocess.PIPE, text=True)
        services = json.loads(result.stdout)["services"]
        self.assertEqual(
            "false", str(services["api"]["environment"]["Database__ApplyMigrationsOnStartup"]).lower())
        self.assertIn("Username=pmgm_runtime;", services["api"]["environment"]["ConnectionStrings__MainDatabase"])
        self.assertIn("Username=pmgm_migrator;", services["migrator"]["environment"]["ConnectionStrings__MainDatabase"])
        self.assertEqual("service_completed_successfully",
                         services["api"]["depends_on"]["db-grants"]["condition"])
        self.assertEqual("service_completed_successfully",
                         services["db-grants"]["depends_on"]["migrator"]["condition"])
        self.assertNotIn("PGPASSWORD", services["api"]["environment"])

    def test_initdb_roles_are_distinct_non_superusers_without_secrets(self):
        script = read("infrastructure/postgres/002-create-pilot-db2.sh")
        for role in ("pmgm_runtime", "pmgm_migrator", "pmgm_reports"):
            self.assertIn("CREATE ROLE " + role + " LOGIN NOSUPERUSER", script)
        self.assertIn("ALTER DATABASE pmgm OWNER TO pmgm_migrator;", script)
        self.assertIn("REVOKE CONNECT, TEMPORARY ON DATABASE pmgm FROM PUBLIC", script)
        self.assertIn("CREATE SCHEMA IF NOT EXISTS reporting AUTHORIZATION pmgm_migrator;", script)
        self.assertNotIn("PASSWORD '", script)

    def test_grant_only_dml_and_never_updates_audit(self):
        script = read("scripts/grant-pilot-db2.sh")
        self.assertIn("GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES", script)
        self.assertIn("REVOKE UPDATE, DELETE, TRUNCATE, REFERENCES, TRIGGER ON core.audit_events", script)
        self.assertIn("GRANT SELECT, INSERT ON core.audit_events", script)
        self.assertIn("REVOKE ALL ON ALL TABLES", script)
        self.assertIn("FROM pmgm_reports", script)
        self.assertIn("ALTER DEFAULT PRIVILEGES FOR ROLE pmgm_migrator", script)

    def test_start_refuses_old_volume_and_checks_real_denials(self):
        start = read("scripts/start-pilot-db2.sh")
        preflight = read("scripts/preflight-pilot-db2.sh")
        assert_probe = read("scripts/check-pilot-db2-roles.sh")
        self.assertIn("bash \"$ROOT/scripts/preflight-pilot-db2.sh\"", start)
        self.assertIn("bash \"$ROOT/scripts/check-pilot-db2-roles.sh\"", start)
        self.assertIn("docker volume inspect", preflight)
        self.assertIn("CONFIRMO_VOLUMEN_NUEVO", preflight)
        for forbidden in (
            "CREATE TABLE core.pmgm_bd2_forbidden",
            "DISABLE TRIGGER ALL",
            "DELETE FROM core.audit_events WHERE FALSE",
            "TRUNCATE TABLE core.audit_events",
            "pmgm_reports \"SELECT * FROM core.audit_events LIMIT 0",
        ):
            self.assertIn(forbidden, assert_probe)


if __name__ == "__main__":
    unittest.main()
