"""G4 QA synthetic PostgreSQL isolation, structural/offline regression.

These checks never contact glmdch, use real institutional data, or expose secrets.
The real-negative acceptance script is scripts/check-qa-db2-roles.sh and must
also pass AFTER a first install/restore using a new disposable QA volume.
"""
import os
from pathlib import Path
import shutil
import subprocess
import unittest

ROOT = Path(__file__).resolve().parents[1]


def read(name):
    return (ROOT / name).read_text(encoding="utf-8")


class QaBd2Contracts(unittest.TestCase):
    def test_shell_syntax(self):
        for name in (
            "infrastructure/postgres/05-create-qa-db2.sh",
            "scripts/check-qa-db2-roles.sh",
            "scripts/install-srv01-qa.sh",
            "scripts/preflight-srv01-qa.sh",
            "scripts/restore-srv01-qa.sh",
            "scripts/qa/public-ingress-guards.sh",
        ):
            with self.subTest(name=name):
                subprocess.run(["bash", "-n", str(ROOT / name)], check=True)

    def test_qa_db2_isolated_from_pilot_and_does_not_embed_secrets(self):
        qa = read("infrastructure/docker-compose.srv01-db2.yml")
        base = read("infrastructure/docker-compose.srv01.yml")
        init = read("infrastructure/postgres/05-create-qa-db2.sh")
        self.assertNotIn("PMGM_PILOT_", qa + init)
        self.assertIn("Username=pmgm_migrator", qa)
        self.assertIn("Username=pmgm_runtime", qa)
        self.assertIn("KC_DB_USERNAME: pmgm_keycloak", qa)
        self.assertIn('Database__ApplyMigrationsOnStartup: "false"', qa)
        self.assertIn('Database__RunMigrationsAndExit: "true"', qa)
        self.assertIn("service_completed_successfully", qa)
        self.assertIn("05-create-qa-db2.sh", qa)
        self.assertIn("10-init-srv01.sql", base)
        self.assertIn("REVOKE CONNECT, TEMPORARY ON DATABASE pmgm FROM PUBLIC", init)
        self.assertIn("REVOKE CONNECT, TEMPORARY ON DATABASE pmgm_keycloak FROM PUBLIC", init)
        self.assertIn("CREATE ROLE pmgm_keycloak LOGIN NOSUPERUSER", init)
        self.assertIn("CREATE ROLE pmgm_runtime LOGIN NOSUPERUSER", init)
        self.assertNotIn("PASSWORD '", init)
        self.assertNotIn("quay.io/minio/minio:latest", qa)

    def test_signed_bundle_is_required_before_overlay(self):
        prepare = read("scripts/qa/prepare-silo-archive.py")
        install = read("scripts/install-srv01-qa.sh")
        self.assertIn("docker-compose.srv01-db2.yml", prepare)
        self.assertIn("PMGM_QA_DB2", prepare)
        self.assertIn("qa_silo_require_bundle", install)
        self.assertIn("qa_db2_require_empty_volume", install)
        self.assertIn("QA_DB2", install)
        self.assertLess(install.index("qa_db2_require_empty_volume"), install.index('"${compose[@]}" pull postgres'))
        self.assertLess(install.index('bash "$ROOT/scripts/check-qa-db2-roles.sh"'), install.index('bash "$ROOT/scripts/smoke-srv01-qa.sh"'))

    def test_credential_and_volume_guard_fail_closed(self):
        env = read("infrastructure/srv01.env.example")
        preflight = read("scripts/preflight-srv01-qa.sh")
        guard = read("scripts/qa/public-ingress-guards.sh")
        for name in ("PMGM_QA_RUNTIME_DB_PASSWORD", "PMGM_QA_KC_DB_PASSWORD",
                     "PMGM_QA_REPORT_DB_PASSWORD", "PMGM_QA_MIGRATOR_DB_PASSWORD"):
            self.assertIn(name, env)
            self.assertIn(name, preflight)
        self.assertIn("len(values) != len(set(values))", preflight)
        self.assertIn("PMGM_QA_DB2_NEW_VOLUME_ACK", guard)
        self.assertIn("docker volume ls", guard)
        self.assertIn("PMGM_QA_HTTPS", preflight)
        self.assertIn("PMGM_QA_DB2", preflight)

    def test_restore_regrants_but_preserves_data_only_review(self):
        restore = read("scripts/restore-srv01-qa.sh")
        self.assertIn("qa_https_require_data_only_restore", restore)
        self.assertIn("restore_role=(--role=pmgm_migrator)", restore)
        self.assertIn("restore_role=(--role=pmgm_keycloak)", restore)
        self.assertIn('run --rm -T --no-deps db-grants', restore)
        self.assertIn('check-qa-db2-roles.sh', restore)
        self.assertLess(restore.index("run --rm -T --no-deps db-grants"),
                        restore.index('echo "Restaurando objetos Silo'))

    def test_real_role_probe_has_negative_controls(self):
        source = read("scripts/check-qa-db2-roles.sh")
        for term in ("core.audit_events", "pmgm_keycloak", "__EFMigrationsHistory",
                     "CREATE TABLE core.g4_forbidden", "DELETE FROM core.audit_events",
                     "DISABLE TRIGGER ALL", "pmgm_reports", "has_database_privilege"):
            self.assertIn(term, source)

    @unittest.skipUnless(shutil.which("docker"), "Docker Compose no instalado")
    def test_overlay_compose_resolves_separate_users(self):
        env = dict(os.environ)
        env.update(dict(
            PMGM_QA_DB_PASSWORD="ci-qa-bootstrap-fake-00001",
            PMGM_QA_KC_DB_PASSWORD="ci-qa-keycloak-fake-00002",
            PMGM_QA_RUNTIME_DB_PASSWORD="ci-qa-runtime-fake-00003",
            PMGM_QA_MIGRATOR_DB_PASSWORD="ci-qa-migrator-fake-00004",
            PMGM_QA_REPORT_DB_PASSWORD="ci-qa-report-fake-00005",
            PMGM_QA_STORAGE_ADMIN_USER="ciqaadmin",
            PMGM_QA_STORAGE_ADMIN_PASSWORD="ci-qa-storage-admin-fake-00006",
            PMGM_QA_DOCUMENT_ACCESS_KEY="ciqadocuments",
            PMGM_QA_DOCUMENT_SECRET_KEY="ci-qa-document-fake-00007",
            PMGM_QA_BACKUP_ACCESS_KEY="ciqabackup",
            PMGM_QA_BACKUP_SECRET_KEY="ci-qa-backup-fake-00008",
            PMGM_QA_KEYCLOAK_ADMIN_PASSWORD="ci-qa-admin-fake-00009",
            PMGM_QA_USER_PASSWORD="ci-qa-users-fake-00010",
            PMGM_QA_WEB_PUBLIC_URL="https://sistema.granlogiamixta.cl",
            PMGM_QA_OIDC_PUBLIC_URL="https://sistema.granlogiamixta.cl/identity",
        ))
        cmd = ["docker", "compose", "-f", str(ROOT / "infrastructure/docker-compose.srv01.yml"),
               "-f", str(ROOT / "infrastructure/docker-compose.srv01-db2.yml"),
               "config", "--format", "json"]
        result = subprocess.run(cmd, env=env, capture_output=True, text=True, check=True)
        import json
        s = json.loads(result.stdout)["services"]
        self.assertEqual("pmgm_keycloak", s["keycloak"]["environment"]["KC_DB_USERNAME"])
        self.assertIn("Username=pmgm_runtime", s["api"]["environment"]["ConnectionStrings__MainDatabase"])
        self.assertIn("Username=pmgm_migrator", s["migrator"]["environment"]["ConnectionStrings__MainDatabase"])
        self.assertEqual("service_completed_successfully", s["api"]["depends_on"]["db-grants"]["condition"])


if __name__ == "__main__":
    unittest.main()
