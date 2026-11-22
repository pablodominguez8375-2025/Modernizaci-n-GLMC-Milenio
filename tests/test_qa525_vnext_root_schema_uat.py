"""Authenticated read-only schema UAT: fixed paths, distinct credentials, rejection paths."""
import base64
import datetime as dt
import importlib.util
import json
from pathlib import Path
import unittest

spec = importlib.util.spec_from_file_location(
    "schema_uat", Path(__file__).resolve().parents[1] / "scripts/qa/qa525-vnext-root-schema-uat.py")
uat = importlib.util.module_from_spec(spec)
spec.loader.exec_module(uat)
NOW = dt.datetime(2026, 11, 22, 10, tzinfo=dt.timezone.utc)


def token(sub):
    payload = {"iss": uat.ISSUER, "exp": int(NOW.timestamp()) + 3600, "sub": sub}
    part = base64.urlsafe_b64encode(json.dumps(payload).encode()).decode().rstrip("=")
    return "a." + part + ".b"


def credentials():
    return json.dumps({"master_token": token("master"), "operator_token": token("operator")}).encode()


class UATTests(unittest.TestCase):
    def test_fixed_get_contract_and_separate_roles(self):
        calls = []
        def probe(path, jwt):
            calls.append((path, jwt))
            return ({"/api/session/me": {"ok": True},
                     "/api/system/workshops": {"items": [], "canManage": True},
                     "/api/platform/bootstrap/initial-load/status": {
                         "code": "OK", "status": "not_opened", "writesPerformed": 0}})[path]
        checks = uat.make(credentials, lambda: NOW, probe)
        self.assertEqual(len(checks), 3)
        self.assertTrue(all(check() is True for check in checks))
        self.assertEqual([x[0] for x in calls], list(uat.PATHS))
        self.assertEqual(calls[0][1], calls[1][1])
        self.assertNotEqual(calls[1][1], calls[2][1])

    def test_synthetic_preview_routes_master_and_registry_operator(self):
        calls = []
        class Synthetic:
            @staticmethod
            def uat_checks(http, expected_sha, unauth):
                self.assertEqual(expected_sha, "a" * 40)
                def check():
                    body = {"Talleres.csv": "x", "Hermanos.csv": "y", "CargosVigentes.csv": "z"}
                    http("POST", uat.BASE + "/preview", body)
                    http("GET", "/api/system/workshops")
                    unauth("GET", uat.BASE + "/contract")
                    return True
                return (check,)
        uat.bind_dependencies({"synthetic": Synthetic})
        def probe(method, path, jwt, body):
            calls.append((method, path, jwt, body))
            return 200, {}
        checks = uat.make(credentials, lambda: NOW, expected_sha="a" * 40, synthetic_probe=probe)
        self.assertEqual(len(checks), 4)
        self.assertIs(checks[-1](), True)
        self.assertEqual(calls[0][2], token("master"))
        self.assertEqual(calls[1][2], token("operator"))
        self.assertIsNone(calls[2][2])
        self.assertEqual(calls[0][3], {"Talleres.csv": "x", "Hermanos.csv": "y", "CargosVigentes.csv": "z"})

    def test_bad_credentials_rejected_before_any_probe(self):
        calls = []
        for raw in (b"", b"{}", b"[]", b"not-json", json.dumps({
                "master_token": token("master"), "operator_token": token("master")}).encode(),
                json.dumps({"master_token": token("master"), "operator_token": token("operator"),
                            "extra": "x"}).encode()):
            with self.assertRaises(uat.Stop):
                uat.make(lambda raw=raw: raw, lambda: NOW, lambda p, t: calls.append(p))
        self.assertEqual(calls, [])

    def test_denied_or_malformed_response_fails_closed(self):
        for response in (None, [], {}, {"status": "unavailable", "code": "WINDOW_STATUS_UNAVAILABLE"},
                         {"status": "open", "code": "OK", "writesPerformed": True}):
            checks = uat.make(credentials, lambda: NOW, lambda p, t, r=response: r)
            with self.assertRaises(uat.Stop):
                checks[2]()
        checks = uat.make(credentials, lambda: NOW, lambda p, t: {"items": "bad", "canManage": True})
        with self.assertRaises(uat.Stop):
            checks[1]()


if __name__ == "__main__":
    unittest.main()
