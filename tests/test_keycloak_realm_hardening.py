"""Regresión de endurecimiento de Keycloak (Issue #469).

Verifica exclusivamente los realms versionados para instalaciones nuevas; no
asegura que un realm ya existente haya sido actualizado en servidores reales.
"""
import json
import unittest
from pathlib import Path

# The existing realm-hardening CI job uses this named test module.
# Include the synthetic Master-inventory suite without editing a protected workflow.
from test_keycloak_master_inventory import MasterRealmPreflightTests  # noqa: F401

ROOT = Path(__file__).resolve().parents[1]
REALMS = (
    ROOT / "infrastructure/keycloak/pmgm-pilot-realm.json",
    ROOT / "infrastructure/keycloak/pmgm-realm.json",
)


class KeycloakRealmHardeningTests(unittest.TestCase):
    def test_both_realms_restrict_passwords_and_reuse(self):
        for path in REALMS:
            with self.subTest(realm=path.name):
                realm = json.loads(path.read_text(encoding="utf-8"))
                policies = realm["passwordPolicy"].split(" and ")
                for policy in (
                    "length(14)", "digits(1)", "lowerCase(1)",
                    "upperCase(1)", "specialChars(1)",
                    "notUsername", "notEmail", "passwordHistory(5)",
                ):
                    self.assertIn(policy, policies)

    def test_brute_force_protection_has_bounded_temporary_lockout(self):
        for path in REALMS:
            with self.subTest(realm=path.name):
                realm = json.loads(path.read_text(encoding="utf-8"))
                self.assertIs(realm["bruteForceProtected"], True)
                self.assertIs(realm["permanentLockout"], False)
                self.assertGreaterEqual(realm["failureFactor"], 3)
                self.assertLessEqual(realm["failureFactor"], 10)
                self.assertGreaterEqual(realm["waitIncrementSeconds"], 30)
                self.assertLessEqual(realm["maxFailureWaitSeconds"], 3600)
                self.assertGreaterEqual(realm["maxDeltaTimeSeconds"], realm["maxFailureWaitSeconds"])
                self.assertGreaterEqual(realm["minimumQuickLoginWaitSeconds"], 30)

    def test_login_and_admin_audit_have_retention(self):
        for path in REALMS:
            with self.subTest(realm=path.name):
                realm = json.loads(path.read_text(encoding="utf-8"))
                self.assertIs(realm["eventsEnabled"], True)
                self.assertIs(realm["adminEventsEnabled"], True)
                self.assertIs(realm["adminEventsDetailsEnabled"], False)
                self.assertGreaterEqual(realm["eventsExpiration"], 7 * 24 * 3600)
                self.assertLessEqual(realm["eventsExpiration"], 90 * 24 * 3600)

    def test_sessions_do_not_remain_unbounded(self):
        for path in REALMS:
            with self.subTest(realm=path.name):
                realm = json.loads(path.read_text(encoding="utf-8"))
                self.assertGreaterEqual(realm["ssoSessionIdleTimeout"], 600)
                self.assertLessEqual(realm["ssoSessionIdleTimeout"], 3600)
                self.assertLessEqual(realm["ssoSessionMaxLifespan"], 8 * 3600)

    def test_pilot_identity_grants_and_initial_password_remain_controlled(self):
        realm = json.loads(REALMS[0].read_text(encoding="utf-8"))
        client = next(c for c in realm["clients"] if c["clientId"] == "pmgm-web")
        self.assertFalse(client["directAccessGrantsEnabled"])
        self.assertTrue(client["standardFlowEnabled"])
        self.assertEqual(client["attributes"]["pkce.code.challenge.method"], "S256")
        users = realm["users"]
        self.assertEqual(len(users), 1)
        self.assertTrue(users[0]["credentials"][0]["temporary"])


if __name__ == "__main__":
    unittest.main()
