"""Synthetic tests for the public QA external probe. No real network calls."""
import importlib.util
import json
from pathlib import Path
from unittest import TestCase, mock

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location(
    "qa_public_readiness", ROOT / "scripts/qa/check-public-readiness.py"
)
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


def oidc_body(issuer=None):
    issuer = issuer or module.ISSUER
    return json.dumps({
        "issuer": issuer,
        "authorization_endpoint": issuer + "/protocol/openid-connect/auth",
        "token_endpoint": issuer + "/protocol/openid-connect/token",
        "jwks_uri": issuer + "/protocol/openid-connect/certs",
    }).encode()


class QaPublicReadinessTests(TestCase):
    def test_offline_is_never_certified(self):
        state = module.inspect(False)
        self.assertEqual("NOT_CHECKED", state["decision"])
        self.assertEqual("NOT_VERIFIED", state["physical_deployment"])
        self.assertEqual("NOT_VERIFIED", state["smoke_regression_uat"])

    def test_nxdomain_blocks_without_http(self):
        with mock.patch.object(module.socket, "getaddrinfo", side_effect=OSError), \
             mock.patch.object(module, "request") as http:
            state = module.inspect(True)
        self.assertEqual("BLOCKED_DNS", state["decision"])
        http.assert_not_called()

    def test_untrusted_tls_blocks(self):
        with mock.patch.object(module.socket, "getaddrinfo", return_value=[(1,)]), \
             mock.patch.object(module, "request", side_effect=OSError):
            state = module.inspect(True)
        self.assertEqual("BLOCKED_TLS_OR_HTTPS", state["decision"])

    def test_downstream_unavailable_blocks(self):
        with mock.patch.object(module.socket, "getaddrinfo", return_value=[(1,)]), \
             mock.patch.object(module, "request", return_value=(503, {}, b"")):
            state = module.inspect(True)
        self.assertEqual("BLOCKED_PUBLIC_ROOT", state["decision"])
        self.assertEqual(503, state["public_root_http"])

    def test_wrong_issuer_blocks(self):
        with mock.patch.object(module.socket, "getaddrinfo", return_value=[(1,)]), \
             mock.patch.object(module, "request", side_effect=[
                 (200, {}, b""), (200, {}, oidc_body("https://evil.invalid"))
             ]):
            state = module.inspect(True)
        self.assertEqual("BLOCKED_OIDC", state["decision"])

    def test_master_route_exposed_blocks(self):
        with mock.patch.object(module.socket, "getaddrinfo", return_value=[(1,)]), \
             mock.patch.object(module, "request", side_effect=[
                 (200, {}, b""), (200, {}, oidc_body()),
                 (404, {}, b""), (200, {}, b""), (404, {}, b"")
             ]):
            state = module.inspect(True)
        self.assertEqual("BLOCKED_ADMIN_MASTER", state["decision"])

    def test_public_checks_never_imply_host_deployment(self):
        with mock.patch.object(module.socket, "getaddrinfo", return_value=[(1,)]), \
             mock.patch.object(module, "request", side_effect=[
                 (200, {}, b""), (200, {}, oidc_body()),
                 (404, {}, b""), (404, {}, b""), (404, {}, b""),
                (200, {}, b""), (200, {}, b""), (200, {}, b""),
                (200, {}, b""),
             ]):
            state = module.inspect(True)
        self.assertEqual("PUBLIC_ENTRY_CHECKS_PASS_ONLY", state["decision"])
        self.assertEqual("PASS", state["brand_assets"])
        self.assertEqual([200, 200, 200, 200], state["brand_http"])
        self.assertEqual("NOT_VERIFIED", state["physical_deployment"])
        self.assertEqual("NOT_VERIFIED", state["database_roles_on_glmdch"])

    def test_brand_403_blocks_even_when_root_oidc_and_admin_are_secure(self):
        with mock.patch.object(module.socket, "getaddrinfo", return_value=[(1,)]), \
             mock.patch.object(module, "request", side_effect=[
                (200, {}, b""), (200, {}, oidc_body()),
                (404, {}, b""), (404, {}, b""), (404, {}, b""),
                (200, {}, b""), (403, {}, b""), (200, {}, b""),
                (200, {}, b""),
             ]):
            state = module.inspect(True)
        self.assertEqual("BLOCKED_BRAND_ASSETS", state["decision"])
        self.assertEqual("BLOCKED", state["brand_assets"])
        self.assertEqual([200, 403, 200, 200], state["brand_http"])
        self.assertEqual("NOT_VERIFIED", state["smoke_regression_uat"])

    def test_brand_redirect_cannot_be_counted_as_ok(self):
        with mock.patch.object(module.socket, "getaddrinfo", return_value=[(1,)]), \
             mock.patch.object(module, "request", side_effect=[
                (200, {}, b""), (200, {}, oidc_body()),
                (404, {}, b""), (404, {}, b""), (404, {}, b""),
                (302, {"Location": "https://elsewhere.invalid/"}, b""),
                (200, {}, b""), (200, {}, b""), (200, {}, b""),
             ]):
            state = module.inspect(True)
        self.assertEqual("BLOCKED_BRAND_ASSETS", state["decision"])
        self.assertEqual([302, 200, 200, 200], state["brand_http"])


if __name__ == "__main__":
    import unittest
    unittest.main()
