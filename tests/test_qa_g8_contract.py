"""G8 account-grade fixture contract (synthetic only, canonical HTTPS)."""
import json
import re
from pathlib import Path
import unittest
import uuid

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / "infrastructure/keycloak/pmgm-realm.json"
PUBLIC = ROOT / "infrastructure/keycloak/pmgm-qa-public-realm.json"
SEED = ROOT / "backend/src/PMGM.Api/Infrastructure/QaIdentitySeedService.cs"
COMPOSE = ROOT / "infrastructure/docker-compose.srv01-db2.yml"
PREFLIGHT = ROOT / "scripts/preflight-srv01-qa.sh"


class QaG8Contracts(unittest.TestCase):
    def test_exact_subject_and_names_for_all_fake_profiles(self):
        users = json.loads(BASE.read_text())["users"]
        self.assertEqual(21, len(users))
        self.assertEqual(len(users), len({u["username"] for u in users}))
        self.assertEqual(len(users), len({u["id"] for u in users}))
        for index, u in enumerate(users, 1):
            with self.subTest(user=u["username"]):
                self.assertTrue(u["username"].startswith("qa."))
                self.assertEqual(
                    f"aa820001-0000-4000-8000-{index:012d}",
                    str(uuid.UUID(u["id"])))
                self.assertTrue(u["email"].endswith("@pmgm.invalid"))
                self.assertTrue(u["enabled"])
                self.assertFalse(u["credentials"][0]["temporary"])
                self.assertEqual("${PMGM_QA_USER_PASSWORD}",
                                 u["credentials"][0]["value"])

        profile = {u["username"]: u for u in users}
        # The CI fixture deliberately uses qa.admin to test the isolated
        # synthetic technical Master. glmdch must import PUBLIC (no Master).
        self.assertIn("platform_superadmin", profile["qa.admin"]["realmRoles"])
        self.assertTrue(all("platform_superadmin" not in u["realmRoles"]
                            for u in users if u["username"] != "qa.admin"))
        for grade in (1, 2, 3):
            user = profile[f"qa.hermano{grade}"]
            self.assertEqual([], user["realmRoles"], "Un hermano sin cargo no recibe roles elevados")
            self.assertEqual(["organization"], user["attributes"]["pmgm_scope"])
        for name, role in (
            ("qa.gransecretaria", "grand_secretariat"),
            ("qa.regimeninterior", "internal_affairs"),
            ("qa.granarchivero", "grand_archivist"),
            ("qa.bibliotecaeditor", "document_manager"),
            ("qa.bibliotecarevisor", "document_manager"),
            ("qa.bibliotecario", "librarian"),
            ("qa.comunicaciones", "communications"),
        ):
            self.assertEqual([role], profile[name]["realmRoles"])

    def test_backend_uses_same_subject_order_and_degree(self):
        source = SEED.read_text()
        users = json.loads(BASE.read_text())["users"]
        fixtures = re.findall(
            r'new\((\d+), "([^"]+)", "(apprentice|fellowcraft|master)"'
            r'(?:, "([^"]+)")?(?:, (true))?\)', source
        )
        self.assertEqual(20, len(fixtures))
        for offset, (index, name, degree, office, expired) in enumerate(fixtures, 2):
            self.assertEqual(offset, int(index))
            self.assertEqual(users[offset-1]["username"], name)
        self.assertEqual({
            "qa.hermano1": "apprentice", "qa.hermano2": "fellowcraft",
            "qa.hermano3": "master", "qa.cargovencido23": "master",
        }, {name: degree for _, name, degree, *_ in fixtures if
            name in ("qa.hermano1", "qa.hermano2",
                     "qa.hermano3", "qa.cargovencido23")})
        self.assertTrue(any(name == "qa.cargovencido23" and expired == "true"
                            for _, name, _, _, expired in fixtures))
        self.assertTrue(any(name == "qa.venerable23" and office == "venerable_master"
                            and not expired for _, name, _, office, expired in fixtures))
        self.assertIn("core.member_identity_links", source)
        self.assertIn('AND "RevokedAtUtc" IS NULL', source)
        self.assertIn('linked.Any(id => id != memberId)', source)
        self.assertIn("https://sistema.granlogiamixta.cl/identity/realms/pmgm", source)
        self.assertIn('Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") != "Staging"', source)
        self.assertIn('Environment.GetEnvironmentVariable("PMGM_QA_HTTPS") != "true"', source)
        self.assertIn('Environment.GetEnvironmentVariable("PMGM_QA_DB2") != "true"', source)

    def test_public_realm_equal_users_and_install_guards(self):
        src, dst = json.loads(BASE.read_text()), json.loads(PUBLIC.read_text())
        self.assertEqual(len(src["users"]), len(dst["users"]))
        for lab_user, qa_user in zip(src["users"], dst["users"]):
            self.assertEqual(lab_user["id"], qa_user["id"])
            self.assertEqual(lab_user["username"], qa_user["username"])
            lab_copy, qa_copy = dict(lab_user), dict(qa_user)
            lab_roles = lab_copy.pop("realmRoles")
            qa_roles = qa_copy.pop("realmRoles")
            self.assertEqual(lab_copy, qa_copy)
            if qa_user["username"] == "qa.admin":
                self.assertEqual(["platform_superadmin"] + qa_roles, lab_roles)
                self.assertNotIn("platform_superadmin", qa_roles)
            else:
                self.assertEqual(lab_roles, qa_roles)
        self.assertTrue(all("platform_superadmin" not in u["realmRoles"]
                            for u in dst["users"]))
        # Master QA is provisioned only by the restricted host bootstrap.
        overlay = (ROOT / "infrastructure/docker-compose.qa-master.yml").read_text()
        self.assertIn("pmgm-qa-public-realm.json", overlay)
        self.assertIn("PMGM_QA_MASTER_APP_ENV_FILE", overlay)
        qa_compose = (ROOT / "infrastructure/docker-compose.srv01.yml").read_text()
        ci_compose = (ROOT / "infrastructure/docker-compose.first.yml").read_text()
        self.assertIn("./keycloak/pmgm-qa-public-realm.json:", qa_compose)
        self.assertIn("./keycloak/pmgm-realm.json:", ci_compose)
        self.assertEqual("external", dst["sslRequired"])
        self.assertEqual("false", COMPOSE.read_text().split("PMGM_QA_G8_ENABLED: ${PMGM_QA_G8_ENABLED:-")[1].split("}")[0])
        self.assertIn('[ "${PMGM_QA_G8_ENABLED:-false}" != true ]', PREFLIGHT.read_text())
        self.assertNotIn("PMGM_QA_USER_PASSWORD=Actual", (ROOT / "infrastructure/srv01.env.example").read_text())
        self.assertIn("check-g8-identities.sh", (ROOT / "scripts/install-srv01-qa.sh").read_text())
        checker = (ROOT / "scripts/qa/check-g8-identities.sh").read_text()
        self.assertIn("core.member_identity_links", checker)
        self.assertIn("public.user_entity", checker)
        self.assertIn('[ "$members" = 20 ] && [ "$kc_users" = 21 ]', checker)


if __name__ == "__main__":
    unittest.main()
