"""Regression tests for isolated Keycloak database role in the pilot.

Real owner/privilege checks also run in PMGM CI, before and after backup/restore.
"""
import unittest
from pathlib import Path

# Ejecutar también la regresión estructural BD2 en el gate CI existente,
# sin tocar el workflow compartido reservado por PR #479.
from test_pilot_db2_isolation import PilotDb2IsolationTests  # noqa: F401
from test_pilot_db2_real_migrations import PilotDb2RealMigrationsTests  # noqa: F401

ROOT = Path(__file__).resolve().parents[1]


class PilotKeycloakDatabaseIsolationTests(unittest.TestCase):
    def read(self, path):
        return (ROOT / path).read_text(encoding="utf-8")

    def test_postgres_initialization_creates_non_superuser_owner(self):
        source = self.read("infrastructure/postgres/001-create-pilot-keycloak.sh")
        self.assertIn("CREATE ROLE pmgm_keycloak LOGIN", source)
        self.assertIn("NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION", source)
        self.assertIn("PASSWORD :'kc_password'", source)
        self.assertIn("--owner=pmgm_keycloak pmgm_keycloak", source)
        self.assertIn("REVOKE CONNECT, TEMPORARY ON DATABASE pmgm FROM PUBLIC", source)
        self.assertIn("REVOKE CONNECT, TEMPORARY ON DATABASE pmgm_keycloak FROM PUBLIC", source)
        self.assertFalse((ROOT / "infrastructure/postgres/001-create-pilot-keycloak.sql").exists())

    def test_keycloak_login_never_uses_application_database_superuser(self):
        compose = self.read("infrastructure/docker-compose.pilot.yml")
        postgres = compose.split("  postgres:\n", 1)[1].split("  minio:\n", 1)[0]
        keycloak = compose.split("  keycloak:\n", 1)[1].split("  api:\n", 1)[0]
        api = compose.split("  api:\n", 1)[1].split("  web:\n", 1)[0]
        self.assertIn("001-create-pilot-keycloak.sh:/docker-entrypoint-initdb.d/001-create-pilot-keycloak.sh:ro", postgres)
        self.assertIn("PMGM_PILOT_KC_DB_PASSWORD:", postgres)
        self.assertIn("KC_DB_USERNAME: pmgm_keycloak", keycloak)
        self.assertIn("KC_DB_PASSWORD: ${PMGM_PILOT_KC_DB_PASSWORD:", keycloak)
        self.assertNotIn("KC_DB_USERNAME: pmgm_app", keycloak)
        self.assertIn("Username=pmgm_app;", api)
        self.assertIn("Database__ApplyMigrationsOnStartup:", api)

    def test_pilot_preflight_requires_unique_keycloak_db_password(self):
        env = self.read("infrastructure/pilot.env.example")
        preflight = self.read("scripts/preflight-pilot.sh")
        self.assertIn("PMGM_PILOT_KC_DB_PASSWORD=", env)
        self.assertIn("PMGM_PILOT_KC_DB_PASSWORD", preflight.split("for name", 1)[0])
        self.assertIn('"PMGM_PILOT_KC_DB_PASSWORD",', preflight)
        self.assertIn("if len(set(secrets)) != len(secrets):", preflight)
        self.assertIn("if len(value) < 20:", preflight)

    def test_restore_remains_owned_by_keycloak_role(self):
        restore = self.read("scripts/restore-pilot.sh")
        self.assertIn('if [ "$database" = "pmgm_keycloak" ]; then', restore)
        self.assertIn("restore_role=(--role=pmgm_keycloak)", restore)
        self.assertIn('"\u0024{restore_role[@]}" -U pmgm_app', restore)
        check = self.read("scripts/check-pilot-keycloak-db-role.sh")
        self.assertIn("rolsuper, rolcreatedb, rolcreaterole, rolreplication", check)
        self.assertIn("has_database_privilege('pmgm_keycloak','pmgm','CONNECT')", check)
        self.assertIn("pg_get_userbyid(c.relowner)", check)

    def test_ci_checks_dedicated_role_before_and_after_recovery(self):
        workflow = self.read(".github/workflows/ci.yml")
        self.assertEqual(2, workflow.count("PMGM_PILOT_KC_DB_PASSWORD: ci-keycloak-db-secret-2026-010"))
        self.assertIn("PMGM_PILOT_KC_DB_PASSWORD=$PMGM_PILOT_KC_DB_PASSWORD", workflow)
        self.assertEqual(2, workflow.count("run: bash scripts/check-pilot-keycloak-db-role.sh"))


if __name__ == "__main__":
    unittest.main()
