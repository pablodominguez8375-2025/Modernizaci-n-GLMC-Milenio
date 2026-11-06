"""QA525 vNext public-health provider: offline tests against a fake fetch."""
import ast
import importlib.util
import json
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]
SRC = ROOT / "scripts" / "qa" / "qa525-vnext-health.py"
RUNTIME = ROOT / "scripts" / "qa" / "qa525-vnext-runtime.py"


def load(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


health = load("qa525_vnext_health_test", SRC)
runtime = load("qa525_vnext_runtime_for_health_test", RUNTIME)
O = health.ORIGIN
SHA = "a" * 40


def good_pages():
    return {
        O + "/": b"<html>ok</html>",
        O + "/health/web": b"ok\n",
        O + "/health/ready": json.dumps({"status": "ready", "database": "postgresql"}).encode(),
        O + health.OIDC: json.dumps({
            "issuer": O + "/identity/realms/pmgm",
            "authorization_endpoint": O + "/identity/realms/pmgm/protocol/openid-connect/auth",
            "token_endpoint": O + "/identity/realms/pmgm/protocol/openid-connect/token",
            "jwks_uri": O + "/identity/realms/pmgm/protocol/openid-connect/certs"}).encode(),
        O + "/qa-version.json": json.dumps(
            {"environment": "QA_SYNTHETIC", "source_sha": SHA}).encode(),
    }


class Fetch:
    def __init__(self, pages=None):
        self.pages = good_pages() if pages is None else pages
        self.override = {}
        self.calls = []

    def __call__(self, url):
        self.calls.append(url)
        if url in self.override:
            value = self.override[url]
            if isinstance(value, Exception):
                raise value
            return value
        return (200, url, self.pages[url])


class HealthTests(unittest.TestCase):
    def setUp(self):
        self.fetch = Fetch()
        self.ok, self.served = health.build(self.fetch)

    def test_all_checks_pass_and_revision_is_returned(self):
        self.assertIs(self.ok(), True)
        self.assertEqual(self.served(), SHA)
        self.assertTrue(all(u.startswith(O) for u in self.fetch.calls))

    def test_each_page_failure_turns_health_false(self):
        for path in ("/", "/health/web", "/health/ready", health.OIDC, "/qa-version.json"):
            for label, answer in (
                    ("status", (500, O + path, b"x")),
                    ("redirect", (200, O + path + "?x", b"x")),
                    ("body type", (200, O + path, "text")),
                    ("status bool", (True, O + path, b"x")),
                    ("shape", (200, O + path)),
                    ("raises", RuntimeError("secret detail"))):
                with self.subTest(path=path, case=label):
                    fetch = Fetch()
                    fetch.override[O + path] = answer
                    ok, _ = health.build(fetch)
                    self.assertIs(ok(), False)

    def test_content_must_match_exactly(self):
        mutations = {
            "/health/web": b"not ok",
            "/health/ready": json.dumps({"status": "ready", "database": "mysql"}).encode(),
            "/qa-version.json": json.dumps(
                {"environment": "PROD", "source_sha": SHA}).encode(),
        }
        for path, body in mutations.items():
            with self.subTest(path=path):
                pages = good_pages()
                pages[O + path] = body
                ok, _ = health.build(Fetch(pages))
                self.assertIs(ok(), False)

    def test_version_marker_is_strict(self):
        bad = [
            {"environment": "QA_SYNTHETIC", "source_sha": "A" * 40},
            {"environment": "QA_SYNTHETIC", "source_sha": "a" * 39},
            {"environment": "QA_SYNTHETIC", "source_sha": SHA, "extra": 1},
            {"environment": "QA_SYNTHETIC", "source_sha": 5},
        ]
        for marker in bad:
            with self.subTest(marker=marker):
                pages = good_pages()
                pages[O + "/qa-version.json"] = json.dumps(marker).encode()
                ok, served = health.build(Fetch(pages))
                self.assertIs(ok(), False)
                with self.assertRaisesRegex(health.Stop, "^HEALTH_VERSION_MARKER_INVALID$"):
                    served()

    def test_oidc_issuer_and_endpoints_must_stay_on_origin(self):
        doc = json.loads(good_pages()[O + health.OIDC])
        variants = {
            "issuer": dict(doc, issuer=O + "/identity/realms/other"),
            "foreign endpoint": dict(doc, token_endpoint="https://evil.example/identity/t"),
            "outside identity": dict(doc, jwks_uri=O + "/other/certs"),
            "missing": {k: v for k, v in doc.items() if k != "jwks_uri"},
        }
        for label, variant in variants.items():
            with self.subTest(case=label):
                pages = good_pages()
                pages[O + health.OIDC] = json.dumps(variant).encode()
                ok, _ = health.build(Fetch(pages))
                self.assertIs(ok(), False)

    def test_json_duplicates_nan_and_oversize_are_refused(self):
        cases = {
            "/health/ready": b'{"status": "ready", "status": "ready", "database": "postgresql"}',
            "/qa-version.json": b'{"environment": "QA_SYNTHETIC", "source_sha": NaN}',
        }
        for path, body in cases.items():
            with self.subTest(path=path):
                pages = good_pages()
                pages[O + path] = body
                ok, _ = health.build(Fetch(pages))
                self.assertIs(ok(), False)
        pages = good_pages()
        pages[O + "/qa-version.json"] = b" " * (health.MAX_SMALL + 1)
        ok, served = health.build(Fetch(pages))
        self.assertIs(ok(), False)
        with self.assertRaisesRegex(health.Stop, "^HEALTH_BODY_TOO_LARGE$"):
            served()

    def test_invalid_construction_is_refused(self):
        with self.assertRaisesRegex(health.Stop, "^HEALTH_FETCH_INVALID$"):
            health.build(object())
        for origin in ("http://x.example", "https://x.example/path", "https://X.example",
                       "https://x.example:8443", "", 5):
            with self.subTest(origin=origin), \
                    self.assertRaisesRegex(health.Stop, "^HEALTH_ORIGIN_INVALID$"):
                health.build(self.fetch, origin)

    def test_errors_never_echo_external_text(self):
        fetch = Fetch()
        fetch.override[O + "/qa-version.json"] = RuntimeError("TOP-SECRET-TEXT")
        _, served = health.build(fetch)
        with self.assertRaises(health.Stop) as caught:
            served()
        self.assertNotIn("TOP-SECRET", str(caught.exception))
        self.assertEqual(str(caught.exception), "HEALTH_FETCH_FAILED")

    def test_plugs_into_the_runtime_reader_contract(self):
        class Compose:
            @staticmethod
            def metadata(run):
                return {}
        reader = runtime.build_reader(Compose, lambda argv: "", self.ok, self.served)
        result = reader._health()
        self.assertEqual(result, {"ok": True, "web_sha": SHA})
        fetch = Fetch()
        fetch.override[O + "/health/web"] = (503, O + "/health/web", b"down")
        ok, served = health.build(fetch)
        failing = runtime.build_reader(Compose, lambda argv: "", ok, served)
        self.assertEqual(failing._health()["ok"], False)

    def test_library_has_no_host_effects(self):
        tree = ast.parse(SRC.read_text(encoding="utf-8"))
        imported = set()
        for node in ast.walk(tree):
            if isinstance(node, ast.Import):
                imported.update(a.name.split(".")[0] for a in node.names)
            elif isinstance(node, ast.ImportFrom):
                imported.add((node.module or "").split(".")[0])
        self.assertEqual(imported, {"json", "re"})
        raw = SRC.read_text(encoding="utf-8")
        for token in ("urlopen", "socket", "subprocess", "os.environ", "getenv", "open("):
            self.assertNotIn(token, raw)


if __name__ == "__main__":
    unittest.main()
