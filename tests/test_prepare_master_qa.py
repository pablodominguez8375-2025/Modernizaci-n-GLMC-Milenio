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
            self.accounts.append({"username": body["username"], "enabled": body["enabled"], "id": self.next_id})
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

    def test_canonical_internal_qa_authority_and_legacy_path_denied(self):
        valid = ("InitialLoad__MasterSubject=" + SUBJECT + "\\n" +
                 "InitialLoad__MasterIssuer=" + ISSUER + "\\n" +
                 "Authentication__Authority=http://keycloak:8080/identity/realms/pmgm")
        mod.validate_config(valid, SUBJECT, ISSUER)
        legacy = valid.replace("keycloak:8080/identity/realms/", "keycloak:8080/realms/")
        with self.assertRaisesRegex(mod.Blocked, "RUNTIME_MASTER_CONFIG_MISMATCH"):
            mod.validate_config(legacy, SUBJECT, ISSUER)

    def test_registration_mismatch_never_enables_master(self):
        api = FakeKeycloak()
        api.accounts.append({"id": SUBJECT, "username": "admin", "enabled": False})
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
