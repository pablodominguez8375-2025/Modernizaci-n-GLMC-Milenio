"""#604: synthetic-only Keycloak Master QA provisioning, never live credentials."""
import importlib.util
import io
import contextlib
from pathlib import Path
from tempfile import TemporaryDirectory
import unittest
from unittest.mock import patch
import urllib.parse

SCRIPT = Path(__file__).resolve().parents[1] / "scripts/qa/prepare-master-qa.py"
spec = importlib.util.spec_from_file_location("pmgm_qa_master_bootstrap", SCRIPT)
mod = importlib.util.module_from_spec(spec)
spec.loader.exec_module(mod)
ISSUER = "https://id.example.invalid/identity/realms/pmgm"
REALM = "pmgm"
PREFIX = "/admin/realms/" + REALM
SUBJECT = "synthetic-subject-master-uuid"


class FakeKeycloak:
    def __init__(self):
        self.accounts = [{"id": "synthetic-member-uuid", "username": "qa.member", "enabled": True}]
        self.realm_roles = {}
        self.client_roles = {}
        self.operations = []
        self.next_id = SUBJECT
        self.issuer = ISSUER
        self.password = None
        self.truncate = False

    def get(self, path):
        self.operations.append(("GET", path))
        if path == "/realms/" + REALM + "/.well-known/openid-configuration":
            return {"issuer": self.issuer}
        if path == PREFIX + "/users/count":
            return len(self.accounts)
        if path.startswith(PREFIX + "/users?"):
            qs = urllib.parse.parse_qs(urllib.parse.urlsplit(path).query)
            first = int(qs["first"][0])
            maximum = int(qs["max"][0])
            users = self.accounts[first:first + maximum]
            return users[:-1] if self.truncate and users else users
        if path == PREFIX + "/roles/platform_superadmin":
            return {"name": "platform_superadmin", "id": "synthetic-role-uuid"}
        for u in self.accounts:
            if path == PREFIX + "/users/" + u["id"]:
                return u
            if path == PREFIX + "/users/" + u["id"] + "/role-mappings/realm":
                return [{"name": name, "id": "synthetic-role-uuid"}
                        for name in self.realm_roles.get(u["id"], [])]
        for user in self.accounts:
            enc = urllib.parse.quote(user["id"], safe="")
            if path == PREFIX + "/users/" + enc + "/role-mappings/realm/composite":
                return [{"name": n} for n in self.realm_roles.get(user["id"], [])]
            if path == PREFIX + "/users/" + enc + "/role-mappings":
                if user["id"] in self.client_roles:
                    return {"clientMappings": {"web": {"mappings": [
                        {"name": n} for n in self.client_roles[user["id"]]]}}}
                return {"clientMappings": {}}
        raise AssertionError("unhandled GET")

    def post(self, path, body):
        self.operations.append(("POST", path))
        if path == PREFIX + "/users":
            if any(x["username"] == body["username"] for x in self.accounts):
                raise AssertionError("duplicate account")
            self.accounts.append({
                "username": body["username"], "enabled": body["enabled"],
                "id": self.next_id, "attributes": body.get("attributes", {}),
            })
            return
        if path == PREFIX + "/users/" + SUBJECT + "/role-mappings/realm":
            self.realm_roles[SUBJECT] = [body[0]["name"]]
            return
        raise AssertionError("unexpected POST")

    def delete(self, path, body):
        self.operations.append(("DELETE", path))
        legacy = mod.LEGACY_QA_SUBJECT
        if path != PREFIX + "/users/" + legacy + "/role-mappings/realm":
            raise AssertionError("unexpected DELETE")
        if body != [{"id": "synthetic-role-uuid", "name": "platform_superadmin"}]:
            raise AssertionError("unexpected role removal")
        self.realm_roles[legacy] = [x for x in self.realm_roles[legacy]
                                    if x != "platform_superadmin"]

    def put(self, path, body):
        self.operations.append(("PUT", path))
        if path == PREFIX + "/users/" + SUBJECT + "/reset-password":
            self.password = body
            return
        if path == PREFIX + "/users/" + SUBJECT:
            self.accounts[-1]["enabled"] = body["enabled"]
            return
        raise AssertionError("unexpected PUT")


class MasterQaBootstrapTests(unittest.TestCase):
    def assert_denied(self, api, code):
        with self.assertRaisesRegex(mod.Blocked, code):
            mod.inventory(api.get, REALM, ISSUER)

    def test_clean_inventory_preparation_and_activation(self):
        api = FakeKeycloak()
        output = []
        with TemporaryDirectory() as folder:
            with patch.object(mod, "save_subject", side_effect=lambda p, v: output.append(v)):
                result = mod.prepare(api, REALM, ISSUER, "SyntheticPass12345!", Path(folder) / "subject")
        self.assertEqual("PREPARED_DISABLED_FOR_QA_CONFIG", result)
        self.assertEqual([SUBJECT], output)
        self.assertEqual(False, api.accounts[-1]["enabled"])
        self.assertEqual(True, api.password["temporary"])
        self.assertEqual(["platform_superadmin"], api.realm_roles[SUBJECT])
        config = ("InitialLoad__MasterSubject=" + SUBJECT + "\n" +
                  "InitialLoad__MasterIssuer=" + ISSUER + "\n" +
                  "Authentication__Authority=" + ISSUER + "\n")
        self.assertEqual("ENABLED_FOR_AUTHENTICATED_UAT_NOT_CERTIFIED",
                         mod.activate(api, REALM, ISSUER, SUBJECT, config))
        self.assertEqual("REALM_SNAPSHOT_OK_UAT_REQUIRED",
                         mod.status(api, REALM, ISSUER, SUBJECT, config))

    def test_master_scope_is_part_of_new_account_registration(self):
        api = FakeKeycloak()
        with TemporaryDirectory() as folder:
            with patch.object(mod, "save_subject"):
                result = mod.prepare(api, REALM, ISSUER, "SyntheticPass12345!",
                                     Path(folder) / "subject")
        self.assertEqual("PREPARED_DISABLED_FOR_QA_CONFIG", result)
        attrs = api.accounts[-1]["attributes"]
        self.assertEqual(["order"], attrs["pmgm_scope"])
        self.assertEqual(["issue-604"], attrs["pmgm_master_qa_bootstrap"])

    def test_missing_scope_fails_closed_before_master_activation(self):
        api = FakeKeycloak()
        with TemporaryDirectory() as folder:
            with patch.object(mod, "save_subject"):
                mod.prepare(api, REALM, ISSUER, "SyntheticPass12345!",
                            Path(folder) / "subject")
        api.accounts[-1]["attributes"].pop("pmgm_scope")
        config = ("InitialLoad__MasterSubject=" + SUBJECT + "\n" +
                  "InitialLoad__MasterIssuer=" + ISSUER + "\n" +
                  "Authentication__Authority=" + ISSUER + "\n")
        count = len(api.operations)
        with self.assertRaisesRegex(mod.Blocked, "MASTER_SCOPE_NOT_PROVISIONED"):
            mod.activate(api, REALM, ISSUER, SUBJECT, config)
        self.assertFalse(any(op == "PUT" for op, _ in api.operations[count:]))
        self.assertFalse(api.accounts[-1]["enabled"])

    def test_tampered_scope_or_bootstrap_marker_blocks_status(self):
        api = FakeKeycloak()
        with TemporaryDirectory() as folder:
            with patch.object(mod, "save_subject"):
                mod.prepare(api, REALM, ISSUER, "SyntheticPass12345!",
                            Path(folder) / "subject")
        api.accounts[-1]["enabled"] = True
        config = ("InitialLoad__MasterSubject=" + SUBJECT + "\n" +
                  "InitialLoad__MasterIssuer=" + ISSUER + "\n" +
                  "Authentication__Authority=" + ISSUER + "\n")
        for attrs in (
            {"pmgm_scope": ["workshop"], "pmgm_master_qa_bootstrap": ["issue-604"]},
            {"pmgm_scope": ["order"], "pmgm_master_qa_bootstrap": ["other"]},
        ):
            api.accounts[-1]["attributes"] = attrs
            with self.assertRaisesRegex(mod.Blocked, "MASTER_SCOPE_NOT_PROVISIONED"):
                mod.status(api, REALM, ISSUER, SUBJECT, config)

    def test_missing_scope_during_prepare_does_not_assign_role(self):
        api = FakeKeycloak()
        # Simulate an identity provider that silently drops the scope attribute.
        original_post = api.post

        def strip_scope(path, body):
            if path == PREFIX + "/users":
                body = {**body, "attributes": {
                    "pmgm_master_qa_bootstrap": ["issue-604"]}}
            return original_post(path, body)

        api.post = strip_scope
        with TemporaryDirectory() as folder:
            with patch.object(mod, "save_subject") as save:
                with self.assertRaisesRegex(mod.Blocked, "MASTER_SCOPE_NOT_PROVISIONED"):
                    mod.prepare(api, REALM, ISSUER, "SyntheticPass12345!",
                                Path(folder) / "subject")
                save.assert_not_called()
        self.assertFalse(any(path.endswith("/role-mappings/realm") and op == "POST"
                             for op, path in api.operations))
        self.assertFalse(api.accounts[-1]["enabled"])

    def test_retire_only_exact_legacy_fixture_master_role(self):
        api = FakeKeycloak()
        legacy = mod.LEGACY_QA_SUBJECT
        api.accounts.append({
            "id": legacy, "username": "qa.admin", "enabled": True,
            "email": "qa.admin@pmgm.invalid",
            "attributes": {"pmgm_scope": ["order"]},
        })
        api.realm_roles[legacy] = ["platform_superadmin", "grand_lodge_admin"]
        self.assertEqual("LEGACY_QA_MASTER_ROLE_RETIRED_ACCOUNT_PRESERVED",
                         mod.retire_exact_legacy_master_role(api, REALM, ISSUER))
        self.assertEqual("qa.admin", api.accounts[-1]["username"])
        self.assertTrue(api.accounts[-1]["enabled"])
        self.assertEqual(["grand_lodge_admin"], api.realm_roles[legacy])
        self.assertEqual(1, sum(op == "DELETE" for op, _ in api.operations))

    def test_legacy_retirement_rejects_lookalike_without_mutation(self):
        api = FakeKeycloak()
        legacy = mod.LEGACY_QA_SUBJECT
        api.accounts.append({
            "id": legacy, "username": "qa.admin", "enabled": True,
            "email": "anomalous@example.invalid",
            "attributes": {"pmgm_scope": ["order"]},
        })
        api.realm_roles[legacy] = ["platform_superadmin"]
        with self.assertRaisesRegex(mod.Blocked, "LEGACY_FIXTURE_MISMATCH"):
            mod.retire_exact_legacy_master_role(api, REALM, ISSUER)
        self.assertFalse(any(op == "DELETE" for op, _ in api.operations))

    def test_block_existing_synthetic_superadmin_before_any_write(self):
        api = FakeKeycloak()
        api.realm_roles["synthetic-member-uuid"] = ["platform_superadmin"]
        with TemporaryDirectory() as folder:
            with self.assertRaisesRegex(mod.Blocked, "FOREIGN_MASTER_ROLE"):
                mod.prepare(api, REALM, ISSUER, "SyntheticPass12345!", Path(folder) / "subject")
        self.assertFalse(any(op != "GET" for op, _ in api.operations))

    def test_block_existing_admin_even_without_role(self):
        api = FakeKeycloak()
        api.accounts.append({"id": SUBJECT, "username": "admin", "enabled": False})
        with TemporaryDirectory() as folder:
            with self.assertRaisesRegex(mod.Blocked, "MASTER_ALREADY_PRESENT_OR_PARTIAL"):
                mod.prepare(api, REALM, ISSUER, "SyntheticPass12345!", Path(folder) / "subject")
        self.assertFalse(any(op != "GET" for op, _ in api.operations))

    def test_block_alias_uppercase(self):
        api = FakeKeycloak()
        api.accounts.append({"id": SUBJECT, "username": "ADMIN", "enabled": False})
        self.assert_denied(api, "MASTER_ALIAS")

    def test_block_second_holder_even_with_valid_admin(self):
        api = FakeKeycloak()
        api.accounts.append({"id": SUBJECT, "username": "admin", "enabled": True})
        api.realm_roles[SUBJECT] = ["platform_superadmin"]
        api.realm_roles["synthetic-member-uuid"] = ["platform_superadmin"]
        self.assert_denied(api, "MASTER_AMBIGUOUS")

    def test_block_client_scope_same_named_role(self):
        api = FakeKeycloak()
        api.client_roles["synthetic-member-uuid"] = ["platform_superadmin"]
        self.assert_denied(api, "CLIENT_ROLE_ALIAS")

    def test_inventory_truncation_blocks(self):
        api = FakeKeycloak()
        api.truncate = True
        self.assert_denied(api, "INVENTORY_INCOMPLETE")

    def test_issuer_mismatch_blocks(self):
        api = FakeKeycloak()
        api.issuer = "https://attacker.invalid/realms/pmgm"
        self.assert_denied(api, "ISSUER_MISMATCH")

    def test_qa_compose_explicit_authority_overrides_legacy_base(self):
        # Compose environment overrides env_file, even when the private file
        # contains another Authority. Never trust the file check alone.
        root = SCRIPT.parents[2]
        base = (root / "infrastructure/docker-compose.srv01.yml").read_text()
        overlay = (root / "infrastructure/docker-compose.qa-master.yml").read_text()
        expected = 'Authentication__Authority: "http://keycloak:8080/identity/realms/pmgm"'
        self.assertIn("Authentication__Authority: http://keycloak:8080/realms/pmgm", base)
        self.assertIn("\n  api:\n", overlay)
        api = overlay.split("\n  api:\n", 1)[1]
        self.assertIn("\n    env_file:\n", "\n" + api)
        self.assertIn("PMGM_QA_MASTER_APP_ENV_FILE", api)
        self.assertIn("\n    environment:\n", "\n" + api)
        self.assertIn("      " + expected, api)
        self.assertNotIn("InitialLoad__MasterSubject:", api)
        self.assertNotIn("InitialLoad__MasterIssuer:", api)
        # Keycloak administrative API must remain loopback-only.
        self.assertIn('"127.0.0.1:18180:8080"', overlay)
        self.assertIn("ports: !override", overlay)
        # Without !override the inherited base 8180 mapping survives.
        self.assertIn("PMGM_QA_OIDC_PORT:-8180", base)
        self.assertEqual(1, api.count("Authentication__Authority:"))

    def test_canonical_internal_qa_authority_and_legacy_path_denied(self):
        valid = chr(10).join([
            "InitialLoad__MasterSubject=" + SUBJECT,
            "InitialLoad__MasterIssuer=" + ISSUER,
            "Authentication__Authority=http://keycloak:8080/identity/realms/pmgm",
        ])
        mod.validate_config(valid, SUBJECT, ISSUER)
        legacy = valid.replace("keycloak:8080/identity/realms/", "keycloak:8080/realms/")
        with self.assertRaisesRegex(mod.Blocked, "RUNTIME_MASTER_CONFIG_MISMATCH"):
            mod.validate_config(legacy, SUBJECT, ISSUER)

    def test_registration_mismatch_never_enables_master(self):
        api = FakeKeycloak()
        api.accounts.append({
            "id": SUBJECT, "username": "admin", "enabled": False,
            "attributes": {
                "pmgm_scope": ["order"], "pmgm_master_qa_bootstrap": ["issue-604"],
            },
        })
        api.realm_roles[SUBJECT] = ["platform_superadmin"]
        with self.assertRaisesRegex(mod.Blocked, "RUNTIME_MASTER_CONFIG_MISMATCH"):
            mod.activate(api, REALM, ISSUER, SUBJECT,
                         "InitialLoad__MasterSubject=wrong\nInitialLoad__MasterIssuer=" + ISSUER +
                         "\nAuthentication__Authority=" + ISSUER)
        self.assertFalse(any(op == "PUT" for op, _ in api.operations))

    def test_wrong_subject_cannot_activate(self):
        api = FakeKeycloak()
        api.accounts.append({"id": SUBJECT, "username": "admin", "enabled": False})
        api.realm_roles[SUBJECT] = ["platform_superadmin"]
        with self.assertRaisesRegex(mod.Blocked, "MASTER_NOT_PREPARED"):
            mod.activate(api, REALM, ISSUER, "synthetic-other-uuid", "")
        self.assertFalse(any(op == "PUT" for op, _ in api.operations))

    def test_duplicate_runtime_configuration_blocks(self):
        with self.assertRaisesRegex(mod.Blocked, "RUNTIME_CONFIG_AMBIGUOUS"):
            mod.validate_config("InitialLoad__MasterSubject=x\nInitialLoad__MasterSubject=y", SUBJECT, ISSUER)

    def test_http_is_rejected(self):
        with self.assertRaisesRegex(mod.Blocked, "ENDPOINT_INVALID"):
            mod.validate_endpoint("http://id.example.invalid/identity", REALM, ISSUER)

    def test_bad_host_rejects_before_opening_token_file(self):
        out = io.StringIO()
        with patch.object(mod.os, "geteuid", return_value=1000):
            with contextlib.redirect_stdout(out):
                result = mod.main([
                    "--base-url", "https://id.example.invalid/identity",
                    "--realm", REALM, "--issuer", ISSUER,
                    "--token-file", "/nonexistent-token", "--subject-file", "/nonexistent-subject",
                ])
        self.assertEqual(2, result)
        self.assertEqual("BLOCKED QA_HOST_NOT_AUTHORIZED", out.getvalue().strip())


if __name__ == "__main__":
    unittest.main()
