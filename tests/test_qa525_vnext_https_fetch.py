"""Offline tests for the physical HTTPS fetch; loopback servers and fakes only."""
import ast
import http.client
import http.server
import importlib.util
from pathlib import Path
import ssl
import threading
import unittest
from unittest import mock

ROOT = Path(__file__).resolve().parents[1]


def load(name, rel):
    spec = importlib.util.spec_from_file_location(name, ROOT / rel)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


hf = load("qa525_vnext_https_fetch", "scripts/qa/qa525-vnext-https-fetch.py")
hp = load("qa525_vnext_health_for_fetch", "scripts/qa/qa525-vnext-health.py")
URL = hf.ORIGIN + "/health/web"


class Handler(http.server.BaseHTTPRequestHandler):
    routes = {}
    seen = []

    def do_GET(self):
        Handler.seen.append((self.path, dict(self.headers)))
        status, headers, body = Handler.routes.get(self.path, (404, {}, b""))
        self.send_response(status)
        for key, value in headers.items():
            self.send_header(key, value)
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def log_message(self, *args):
        pass


class Loopback(unittest.TestCase):
    def setUp(self):
        Handler.routes, Handler.seen = {}, []
        self.server = http.server.ThreadingHTTPServer(("127.0.0.1", 0), Handler)
        threading.Thread(target=self.server.serve_forever, kwargs={"poll_interval": 0.01}, daemon=True).start()
        self.port = self.server.server_address[1]
        self.made = []

    def tearDown(self):
        self.server.shutdown()
        self.server.server_close()

    def fetch(self, **kw):
        def factory(host, port, timeout, context):
            self.made.append((host, port, timeout, context))
            return http.client.HTTPConnection("127.0.0.1", self.port, timeout=timeout)
        return hf.build_fetch(connection_factory=factory, **kw)

    def test_ok_response_and_fixed_request(self):
        Handler.routes["/health/web"] = (200, {}, b"ok")
        self.assertEqual(self.fetch()(URL), (200, URL, b"ok"))
        host, port, timeout, context = self.made[0]
        self.assertEqual((host, port), (hf.HOST, 443))
        self.assertEqual(timeout, hf.CONNECT_TIMEOUT)
        self.assertEqual(context.verify_mode, ssl.CERT_REQUIRED)
        self.assertTrue(context.check_hostname)
        self.assertGreaterEqual(context.minimum_version, ssl.TLSVersion.TLSv1_2)
        path, headers = Handler.seen[0]
        self.assertEqual(path, "/health/web")
        for forbidden in ("Cookie", "Authorization", "Proxy-Authorization"):
            self.assertNotIn(forbidden, headers)
        self.assertEqual(headers["User-Agent"], "qa525-vnext-health/1")
        self.assertEqual(headers["Connection"], "close")
        self.assertEqual(headers["Accept-Encoding"], "identity")

    def test_redirect_is_not_followed(self):
        Handler.routes["/health/web"] = (302, {"Location": "http://evil.example/x"}, b"")
        status, final, body = self.fetch()(URL)
        self.assertEqual((status, final, body), (302, URL, b""))
        self.assertEqual(len(Handler.seen), 1)
        self.assertEqual(len(self.made), 1)

    def test_error_status_is_returned_not_raised(self):
        Handler.routes["/health/web"] = (503, {}, b"down")
        self.assertEqual(self.fetch()(URL)[0], 503)

    def test_body_cap_is_exact(self):
        Handler.routes["/qa-version.json"] = (200, {}, b"a" * hf.MAX_BODY)
        self.assertEqual(len(self.fetch()(hf.ORIGIN + "/qa-version.json")[2]), hf.MAX_BODY)
        Handler.routes["/qa-version.json"] = (200, {}, b"a" * (hf.MAX_BODY + 1))
        with self.assertRaises(hf.Stop) as c:
            self.fetch()(hf.ORIGIN + "/qa-version.json")
        self.assertEqual(str(c.exception), "HTTPSFETCH_BODY_TOO_LARGE")

    def test_total_deadline_stops_slow_responses(self):
        Handler.routes["/health/web"] = (200, {}, b"x" * 50000)
        ticks = iter([0.0, 0.0, 100.0, 100.0, 100.0])
        with self.assertRaises(hf.Stop) as c:
            self.fetch(clock=lambda: next(ticks))(URL)
        self.assertEqual(str(c.exception), "HTTPSFETCH_DEADLINE")

    def test_connection_failure_is_fixed_code_without_leak(self):
        def factory(host, port, timeout, context):
            return http.client.HTTPConnection("127.0.0.1", 1, timeout=1)  # refused
        with self.assertRaises(hf.Stop) as c:
            hf.build_fetch(connection_factory=factory)(URL)
        self.assertEqual(str(c.exception), "HTTPSFETCH_FAILED")
        self.assertIsNone(c.exception.__cause__)

    def test_connection_always_closed(self):
        conn = mock.Mock()
        conn.getresponse.side_effect = OSError("secret https://x")
        fetch = hf.build_fetch(connection_factory=lambda *a: conn)
        with self.assertRaises(hf.Stop):
            fetch(URL)
        conn.close.assert_called_once()

    def test_tls_error_has_its_own_code(self):
        conn = mock.Mock()
        conn.request.side_effect = ssl.SSLCertVerificationError("secret host")
        with self.assertRaises(hf.Stop) as c:
            hf.build_fetch(connection_factory=lambda *a: conn)(URL)
        self.assertEqual(str(c.exception), "HTTPSFETCH_TLS_FAILED")

    def test_weak_tls_policy_is_refused(self):
        bad = ssl.create_default_context()
        bad.check_hostname = False
        bad.verify_mode = ssl.CERT_NONE
        with mock.patch.object(hf.ssl, "create_default_context", return_value=bad):
            with self.assertRaises(hf.Stop) as c:
                hf._context()
        self.assertEqual(str(c.exception), "HTTPSFETCH_TLS_POLICY_INVALID")

    def test_context_raises_weak_minimum_version(self):
        weak = ssl.create_default_context()
        weak.minimum_version = ssl.TLSVersion.MINIMUM_SUPPORTED
        with mock.patch.object(hf.ssl, "create_default_context", return_value=weak):
            context = hf._context()
        self.assertGreaterEqual(context.minimum_version, ssl.TLSVersion.TLSv1_2)

    def test_only_exact_health_urls_are_allowed(self):
        fetch = hf.build_fetch(connection_factory=lambda *a: self.fail("must not connect"))
        for bad in (hf.ORIGIN + "/health/web?x=1", hf.ORIGIN + "/health/web#a",
                    "http://sistema.granlogiamixta.cl/health/web",
                    "https://sistema.granlogiamixta.cl:444/health/web",
                    "https://user@sistema.granlogiamixta.cl/health/web",
                    "https://evil.example/health/web", hf.ORIGIN + "/etc/passwd",
                    hf.ORIGIN + "/health/web/", "", None, b"x"):
            with self.assertRaises(hf.Stop) as c:
                fetch(bad)
            self.assertEqual(str(c.exception), "HTTPSFETCH_URL_NOT_ALLOWED")

    def test_all_provider_urls_are_allowed_and_origin_matches(self):
        self.assertEqual(hf.ORIGIN, hp.ORIGIN)
        for path in (hp.HOME, hp.WEB, hp.READY, hp.OIDC, hp.VERSION):
            self.assertIn(hf.ORIGIN + path, hf.ALLOWED_URLS)
        self.assertEqual(len(hf.ALLOWED_URLS), 5)
        self.assertLessEqual(hp.MAX_HOME, hf.MAX_BODY)

    def test_works_with_health_provider_end_to_end(self):
        sha = "a" * 40
        Handler.routes.update({
            "/": (200, {}, b"<html></html>"),
            "/health/web": (200, {}, b"ok"),
            "/health/ready": (200, {}, b'{"status":"ready","database":"postgresql"}'),
            hp.OIDC: (200, {}, ('{"issuer":"%s%s","authorization_endpoint":"%s%s/a",'
                                '"token_endpoint":"%s%s/t","jwks_uri":"%s%s/j"}' % (
                                    (hf.ORIGIN, hp.ISSUER_PATH) * 4)).encode()),
            "/qa-version.json": (200, {}, ('{"environment":"QA_SYNTHETIC","source_sha":"%s"}'
                                           % sha).encode()),
        })
        check, revision = hp.build(self.fetch())
        self.assertTrue(check())
        self.assertEqual(revision(), sha)
        Handler.routes["/health/web"] = (302, {"Location": "/x"}, b"")
        self.assertFalse(check())

    def test_library_only_no_entrypoint_or_env(self):
        src = (ROOT / "scripts/qa/qa525-vnext-https-fetch.py").read_text()
        tree = ast.parse(src)
        mods = {a.name for n in ast.walk(tree) if isinstance(n, ast.Import) for a in n.names}
        self.assertEqual(mods, {"http.client", "ssl", "time"})
        self.assertFalse(any(isinstance(n, ast.ImportFrom) for n in ast.walk(tree)))
        attrs = {n.attr for n in ast.walk(tree) if isinstance(n, ast.Attribute)}
        for forbidden in ("environ", "getenv", "system", "Popen", "set_tunnel",
                          "load_cert_chain", "load_verify_locations", "HTTPConnection"):
            self.assertNotIn(forbidden, attrs)
        with self.assertRaises(SystemExit):
            exec(compile(src, "x", "exec"), {"__name__": "__main__"})


if __name__ == "__main__":
    unittest.main()
