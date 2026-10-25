"""#604 synthetic-only tests for read-only Keycloak Master realm preflight."""
import importlib.util
from pathlib import Path
from tempfile import TemporaryDirectory
from unittest.mock import patch
import contextlib
import io
import json
import os
import urllib.parse
import unittest

SCRIPT = Path(__file__).resolve().parents[1] / "scripts/qa/check-keycloak-master-inventory.py"
spec = importlib.util.spec_from_file_location("master_realm_preflight", SCRIPT)
preflight = importlib.util.module_from_spec(spec)
spec.loader.exec_module(preflight)

ISSUER = "https://id.example.invalid/realms/pmgm-qa"
MASTER = "opaque-synthetic-master"
REALM = "pmgm-qa"
PREFIX = "/admin/realms/" + REALM


class FakeRealm:
    def __init__(self):
        self.users = [
            {"id": MASTER, "username": "admin", "enabled": True},
            {"id": "other-synthetic-sub", "username": "qa.member", "enabled": True},
        ]
        self.roles = {MASTER: [{"name": "platform_superadmin"}], "other-synthetic-sub": []}
        self.client_mappings = {}
        self.issuer = ISSUER
        self.count_override = None
        self.truncate = False
        self.page_extra = False
        self.calls = []

    def get(self, path):
        self.calls.append(path)
        if path == "/realms/" + REALM + "/.well-known/openid-configuration":
            return {"issuer": self.issuer}
        if path == PREFIX + "/users/count":
            return self.count_override if self.count_override is not None else len(self.users)
        if path.startswith(PREFIX + "/users?"):
            query = urllib.parse.parse_qs(urllib.parse.urlsplit(path).query)
            first = int(query["first"][0])
            limit = int(query["max"][0])
            if self.page_extra and first == len(self.users):
                return [{"id": "unexpected", "username": "unexpected", "enabled": True}]
            page = self.users[first:first + limit]
            return page[:-1] if self.truncate and page else page
        for user in self.users:
            enc = urllib.parse.quote(user["id"], safe="")
            if path == PREFIX + "/users/" + enc + "/role-mappings/realm/composite":
                return self.roles.get(user["id"], [])
            if path == PREFIX + "/users/" + enc + "/role-mappings":
                return {"clientMappings": self.client_mappings.get(user["id"], {})}
        raise AssertionError("Unexpected readonly Keycloak route: " + path)


class MasterRealmPreflightTests(unittest.TestCase):
    def check(self, state, subject=MASTER, issuer=ISSUER):
        return preflight.check_inventory(state.get, REALM, subject, issuer)

    def assert_blocked(self, state, code="MASTER_ACCOUNT_AMBIGUOUS", **args):
        with self.assertRaisesRegex(preflight.Blocked, code):
            self.check(state, **args)

    def test_single_registered_master_with_effective_realm_role_passes(self):
        state = FakeRealm()
        self.assertEqual(2, self.check(state))
        self.assertTrue(all("password" not in path for path in state.calls))
        self.assertTrue(all("/admin/realms/" in x or "openid-configuration" in x for x in state.calls))

    def test_second_role_holder_is_rejected_even_if_username_differs(self):
        s = FakeRealm()
        s.roles["other-synthetic-sub"] = [{"name": "platform_superadmin"}]
        self.assert_blocked(s)

    def test_master_alias_and_wrong_subject_are_rejected(self):
        s = FakeRealm()
        s.users[0]["username"] = "ADMIN"
        self.assert_blocked(s)
        s.users[0]["username"] = "admin"
        self.assert_blocked(s, subject="not-registered")

    def test_disabled_master_is_not_accepted(self):
        s = FakeRealm()
        s.users[0]["enabled"] = False
        self.assert_blocked(s)

    def test_no_master_or_duplicate_user_id_is_not_accepted(self):
        s = FakeRealm()
        s.roles[MASTER] = []
        self.assert_blocked(s)
        s = FakeRealm()
        s.users[1]["id"] = MASTER
        self.assert_blocked(s, code="MASTER_INVENTORY_INCOMPLETE")

    def test_stale_issuer_and_missing_registration_fail(self):
        s = FakeRealm()
        self.assert_blocked(s, code="MASTER_ISSUER_MISMATCH", issuer="https://other.invalid")
        self.assert_blocked(s, code="MASTER_REGISTRATION_UNAVAILABLE", subject=" ")

    def test_partial_pagination_or_changed_count_fails_closed(self):
        s = FakeRealm()
        s.truncate = True
        self.assert_blocked(s, code="MASTER_INVENTORY_INCOMPLETE")
        s = FakeRealm()
        s.count_override = 3
        self.assert_blocked(s, code="MASTER_INVENTORY_INCOMPLETE")
        s = FakeRealm()
        s.page_extra = True
        self.assert_blocked(s, code="MASTER_INVENTORY_INCOMPLETE")

    def test_missing_composite_roles_or_client_mappings_fails_closed(self):
        s = FakeRealm()
        s.roles[MASTER] = None
        self.assert_blocked(s, code="MASTER_ROLE_INVENTORY_INCOMPLETE")
        s = FakeRealm()
        s.client_mappings[MASTER] = {"client": {"mappings": [
            {"name": "platform_superadmin"}]}}
        self.assert_blocked(s)

    def test_token_files_must_be_private_and_regular(self):
        with TemporaryDirectory() as folder:
            path = Path(folder) / "token"
            path.write_text("synthetic-only-token", encoding="utf-8")
            os.chmod(path, 0o600)
            self.assertEqual("synthetic-only-token", preflight._read_protected_file(path))
            os.chmod(path, 0o644)
            with self.assertRaisesRegex(preflight.Blocked, "MASTER_CREDENTIAL_FILE_UNSAFE"):
                preflight._read_protected_file(path)

    def test_cli_rejects_http_before_reading_any_secrets(self):
        stream = io.StringIO()
        with contextlib.redirect_stdout(stream):
            ret = preflight.main([
                "--base-url", "http://insecure.example.invalid",
                "--realm", REALM,
                "--issuer", ISSUER,
                "--token-file", "/missing-private-token",
                "--subject-file", "/missing-private-subject",
            ])
        self.assertEqual(2, ret)
        self.assertEqual("BLOCKED MASTER_CONFIG_INVALID", stream.getvalue().strip())

    def test_no_private_data_is_rendered_on_blocked_cli(self):
        with TemporaryDirectory() as folder:
            token = Path(folder) / "token"
            sub = Path(folder) / "subject"
            token.write_text("synthetic-secret-token", encoding="utf-8")
            sub.write_text(MASTER, encoding="utf-8")
            os.chmod(token, 0o600)
            os.chmod(sub, 0o600)
            output = io.StringIO()
            with patch.object(preflight.urllib.request, "urlopen", side_effect=OSError("private-person-data")):
                with contextlib.redirect_stdout(output):
                    result = preflight.main([
                        "--base-url", "https://id.example.invalid",
                        "--realm", REALM, "--issuer", ISSUER,
                        "--token-file", str(token), "--subject-file", str(sub),
                    ])
            self.assertEqual(2, result)
            self.assertEqual("BLOCKED MASTER_REMOTE_INVENTORY_UNAVAILABLE", output.getvalue().strip())
            self.assertNotIn("synthetic-secret-token", output.getvalue())
            self.assertNotIn(MASTER, output.getvalue())


if __name__ == "__main__":
    unittest.main()
