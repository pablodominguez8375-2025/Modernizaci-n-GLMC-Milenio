#!/usr/bin/env python3
"""Pruebas herméticas del contrato UAT #768: sin claves ni tráfico de red."""
import base64
import importlib.util
import json
from pathlib import Path
import unittest
import uuid

SRC = Path(__file__).resolve().parents[1] / "scripts" / "qa" / "check-keycloak-session-uat.py"
SPEC = importlib.util.spec_from_file_location("keycloak_readonly_uat", SRC)
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)
NOW = 1800000000
SUBJECT = str(uuid.UUID("12345678-1234-4234-8234-123456789abc"))


def token(sid, username="qa.hermano1", subject=SUBJECT, issued=NOW-180,
          expires=NOW+600, issuer=MODULE.ISSUER, claim="sid"):
    header = base64.urlsafe_b64encode(b'{"alg":"none","typ":"JWT"}').rstrip(b"=").decode()
    payload = {
        "sub": subject, "preferred_username": username, "iat": issued,
        "exp": expires, "iss": issuer, claim: sid
    }
    data = base64.urlsafe_b64encode(json.dumps(payload).encode()).rstrip(b"=").decode()
    # Ningún token sintético puede utilizarse para autenticarse realmente.
    return header + "." + data + ".synthetic-signature"


class KeycloakReadonlyUatTests(unittest.TestCase):
    def setUp(self):
        self.old = token("session-original", issued=NOW-180)
        self.new = token("session-renewed", issued=NOW-30)

    def test_valid_pair_accepts_only_old_401_and_fresh_200(self):
        called = []
        def probe(value):
            called.append(value)
            return 200 if value == self.new else 401
        self.assertTrue(MODULE.verify(self.old, self.new, probe, NOW))
        self.assertEqual([self.new, self.old], called)

    def test_stale_token_still_accepted_fails(self):
        with self.assertRaisesRegex(MODULE.Blocked, "OLD_SESSION_STILL_AUTHENTICATED"):
            MODULE.verify(self.old, self.new, lambda _: 200, NOW)

    def test_fresh_session_not_available_must_not_test_stale(self):
        calls = []
        def probe(value):
            calls.append(value)
            return 503
        with self.assertRaisesRegex(MODULE.Blocked, "FRESH_SESSION_NOT_AUTHENTICATED"):
            MODULE.verify(self.old, self.new, probe, NOW)
        self.assertEqual([self.new], calls)

    def test_redirect_or_forbidden_is_not_success(self):
        for status in (301, 302, 403, 429, 500):
            with self.subTest(status=status):
                with self.assertRaises(MODULE.Blocked):
                    MODULE.verify(self.old, self.new, lambda _: status, NOW)

    def test_old_token_expired_is_not_revocation_evidence(self):
        old = token("old", expires=NOW-1)
        with self.assertRaisesRegex(MODULE.Blocked, "TOKEN_NOT_FRESH_ENOUGH_FOR_UAT"):
            MODULE.verify(old, self.new, lambda _: 200, NOW)

    def test_session_same_or_tokens_same_is_rejected(self):
        same_sid = token("session-original", issued=NOW-30)
        with self.assertRaisesRegex(MODULE.Blocked, "OLD_SESSION_NOT_REPLACED"):
            MODULE.verify(self.old, same_sid, lambda _: 200, NOW)
        with self.assertRaisesRegex(MODULE.Blocked, "TOKENS_ARE_IDENTICAL"):
            MODULE.verify(self.old, self.old, lambda _: 200, NOW)

    def test_privileged_or_nonsynthetic_identity_rejected(self):
        for username in ("qa.admin", "admin", "usuario", "qa.", "qa.ADMIN"):
            with self.subTest(username=username):
                with self.assertRaisesRegex(MODULE.Blocked, "ONLY_SYNTHETIC_NONPRIVILEGED_IDENTITY"):
                    MODULE.parse_claims(token("old", username=username), NOW)

    def test_issuer_subject_user_order_mismatch(self):
        cases = (
            (token("old", issuer="https://evil.example.invalid"), "OIDC_ISSUER_MISMATCH"),
            (token("old", subject=str(uuid.uuid4())), "DIFFERENT_TEST_IDENTITIES"),
        )
        for value, code in cases:
            with self.subTest(code=code):
                with self.assertRaisesRegex(MODULE.Blocked, code):
                    MODULE.verify(value, self.new, lambda _: 200, NOW)
        with self.assertRaisesRegex(MODULE.Blocked, "TOKEN_ORDER_INVALID"):
            MODULE.verify(token("old", issued=NOW-10), token("new", issued=NOW-90),
                          lambda _: 200, NOW)

    def test_session_state_alternative_supported(self):
        old = token("old-session", claim="session_state")
        new = token("new-session", claim="session_state")
        self.assertTrue(MODULE.verify(old, new, lambda value: 200 if value == new else 401, NOW))

    def test_absent_conflicting_or_invalid_sid_is_denied(self):
        no_sid = token("old", claim="unexpected")
        with self.assertRaisesRegex(MODULE.Blocked, "SESSION_ID_MISSING"):
            MODULE.parse_claims(no_sid, NOW)
        data = json.loads(base64.urlsafe_b64decode(self.old.split(".")[1] + "=" * (-len(self.old.split(".")[1]) % 4)))
        data["session_state"] = "different-session"
        payload = base64.urlsafe_b64encode(json.dumps(data).encode()).rstrip(b"=").decode()
        conflicting = self.old.split(".")[0] + "." + payload + ".synthetic-signature"
        with self.assertRaisesRegex(MODULE.Blocked, "SESSION_ID_CONFLICT"):
            MODULE.parse_claims(conflicting, NOW)

    def test_malformed_jwt_is_rejected_without_network(self):
        for value in ("", "abc", "x.!.z", "x.@@@@.z", "x.e30=.z"):
            with self.subTest(value=value):
                with self.assertRaises(MODULE.Blocked):
                    MODULE.parse_claims(value, NOW)


if __name__ == "__main__":
    unittest.main()
