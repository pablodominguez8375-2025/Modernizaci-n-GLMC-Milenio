"""Offline tests for the physical HTTPS fetch; loopback sockets and fakes only."""
import ast
import importlib.util
from pathlib import Path
import socket
import ssl
import threading
import time
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


class Scripted:
    """Raw TCP server: each connection runs ``script(conn, request_bytes)``."""

    def __init__(self, script, read_request=True):
        self.script, self.read_request, self.requests = script, read_request, []
        self.listener = socket.socket()
        self.listener.bind(("127.0.0.1", 0))
        self.listener.listen(8)
        self.port = self.listener.getsockname()[1]
        self.stop = threading.Event()
        threading.Thread(target=self.serve, daemon=True).start()

    def serve(self):
        while not self.stop.is_set():
            try:
                conn, _ = self.listener.accept()
            except OSError:
                return
            threading.Thread(target=self.handle, args=(conn,), daemon=True).start()

    def handle(self, conn):
        try:
            data = b""
            if self.read_request:
                while b"\r\n\r\n" not in data:
                    chunk = conn.recv(4096)
                    if not chunk:
                        return
                    data += chunk
            self.requests.append(data)
            self.script(conn, data)
        except OSError:
            pass
        finally:
            conn.close()

    def close(self):
        self.stop.set()
        self.listener.close()


def reply(status=200, body=b"", headers=b"", length=True):
    head = b"HTTP/1.0 %d X\r\n" % status
    if length:
        head += b"Content-Length: %d\r\n" % len(body)
    return head + headers + b"\r\n" + body


class Base(unittest.TestCase):
    def serve(self, script, **kw):
        server = Scripted(script, **kw)
        self.addCleanup(server.close)
        return server

    def fetch(self, server, **kw):
        def connect(host, port, context, remaining):
            sock = socket.create_connection(("127.0.0.1", server.port), timeout=remaining())
            return sock
        return hf.build_fetch(connect=connect, **kw)

    def code(self, call):
        with self.assertRaises(hf.Stop) as c:
            call()
        self.assertIsNone(c.exception.__cause__)
        return str(c.exception)


class ResponseTests(Base):
    def test_ok_request_is_fixed_and_minimal(self):
        server = self.serve(lambda c, r: c.sendall(reply(body=b"ok")))
        self.assertEqual(self.fetch(server)(URL), (200, URL, b"ok"))
        request = server.requests[0].decode()
        self.assertTrue(request.startswith("GET /health/web HTTP/1.0\r\n"))
        self.assertIn("Host: sistema.granlogiamixta.cl\r\n", request)
        self.assertIn("Connection: close\r\n", request)
        for forbidden in ("Cookie", "Authorization", "Proxy-Authorization"):
            self.assertNotIn(forbidden, request)

    def test_body_until_eof_without_content_length(self):
        server = self.serve(lambda c, r: c.sendall(reply(body=b"hello", length=False)))
        self.assertEqual(self.fetch(server)(URL)[2], b"hello")

    def test_redirect_is_returned_not_followed(self):
        server = self.serve(lambda c, r: c.sendall(
            reply(302, headers=b"Location: http://evil.example/x\r\n")))
        self.assertEqual(self.fetch(server)(URL), (302, URL, b""))
        self.assertEqual(len(server.requests), 1)

    def test_error_status_is_returned_not_raised(self):
        server = self.serve(lambda c, r: c.sendall(reply(503, b"down")))
        self.assertEqual(self.fetch(server)(URL)[0], 503)

    def test_body_cap_with_and_without_content_length(self):
        big = b"a" * (hf.MAX_BODY + 1)
        exact = b"a" * hf.MAX_BODY
        server = self.serve(lambda c, r: c.sendall(reply(body=exact)))
        self.assertEqual(len(self.fetch(server)(URL)[2]), hf.MAX_BODY)
        server = self.serve(lambda c, r: c.sendall(reply(body=big)))
        self.assertEqual(self.code(lambda: self.fetch(server)(URL)), "HTTPSFETCH_BODY_TOO_LARGE")
        server = self.serve(lambda c, r: c.sendall(reply(body=big, length=False)))
        self.assertEqual(self.code(lambda: self.fetch(server)(URL)), "HTTPSFETCH_BODY_TOO_LARGE")

    def test_strict_response_parser(self):
        cases = {
            "transfer-encoding": reply(headers=b"Transfer-Encoding: chunked\r\n", body=b"0\r\n\r\n", length=False),
            "content-encoding": reply(headers=b"Content-Encoding: gzip\r\n", body=b"x"),
            "duplicate length": b"HTTP/1.0 200 X\r\nContent-Length: 1\r\nContent-Length: 1\r\n\r\nx",
            "bad length": b"HTTP/1.0 200 X\r\nContent-Length: +1\r\n\r\nx",
            "length longer": b"HTTP/1.0 200 X\r\nContent-Length: 5\r\n\r\nabc",
            "length shorter": b"HTTP/1.0 200 X\r\nContent-Length: 1\r\n\r\nabc",
            "bad status": b"FOO 200 X\r\n\r\n",
            "bad header": b"HTTP/1.0 200 X\r\nbroken\r\n\r\n",
            "no header end": b"HTTP/1.0 200 X\r\n",
        }
        for name, raw in cases.items():
            server = self.serve(lambda c, r, raw=raw: c.sendall(raw))
            self.assertEqual(self.code(lambda: self.fetch(server)(URL)),
                             "HTTPSFETCH_RESPONSE_INVALID", name)

    def test_header_too_large(self):
        raw = b"HTTP/1.0 200 X\r\n" + b"X-A: " + b"a" * (hf.MAX_HEADER + 100) + b"\r\n\r\n"
        server = self.serve(lambda c, r: c.sendall(raw))
        self.assertEqual(self.code(lambda: self.fetch(server)(URL)), "HTTPSFETCH_HEADER_TOO_LARGE")

    def test_connection_failure_is_fixed_code_without_leak(self):
        def connect(host, port, context, remaining):
            return socket.create_connection(("127.0.0.1", 1), timeout=1)  # refused
        self.assertEqual(self.code(lambda: hf.build_fetch(connect=connect)(URL)), "HTTPSFETCH_FAILED")

    def test_socket_always_closed(self):
        sock = mock.Mock()
        sock.recv.side_effect = OSError("secret https://x")
        fetch = hf.build_fetch(connect=lambda *a: sock)
        self.code(lambda: fetch(URL))
        sock.close.assert_called_once()

    def test_tls_error_has_its_own_code(self):
        def connect(*a):
            raise ssl.SSLCertVerificationError("secret host")
        self.assertEqual(self.code(lambda: hf.build_fetch(connect=connect)(URL)), "HTTPSFETCH_TLS_FAILED")


class HardDeadlineTests(Base):
    """Real wall-clock behaviour: no read may outlive the total deadline."""

    def timed(self, call, limit):
        started = time.monotonic()
        code = self.code(call)
        elapsed = time.monotonic() - started
        self.assertLess(elapsed, limit)
        return code

    def test_slow_drip_body_cannot_extend_deadline(self):
        def script(conn, request):
            conn.sendall(b"HTTP/1.0 200 X\r\nContent-Length: 100\r\n\r\n")
            for _ in range(100):
                time.sleep(0.1)
                conn.sendall(b"a")
        server = self.serve(script)
        fetch = self.fetch(server, total_deadline=0.6)
        self.assertEqual(self.timed(lambda: fetch(URL), 1.8), "HTTPSFETCH_DEADLINE")

    def test_slow_drip_headers_cannot_extend_deadline(self):
        def script(conn, request):
            for byte in b"HTTP/1.0 200 X\r\nX-A: " + b"a" * 200:
                time.sleep(0.1)
                conn.sendall(bytes([byte]))
        server = self.serve(script)
        fetch = self.fetch(server, total_deadline=0.6)
        self.assertEqual(self.timed(lambda: fetch(URL), 1.8), "HTTPSFETCH_DEADLINE")

    def test_silent_server_after_request(self):
        server = self.serve(lambda c, r: time.sleep(5))
        fetch = self.fetch(server, total_deadline=0.5)
        self.assertEqual(self.timed(lambda: fetch(URL), 1.8), "HTTPSFETCH_DEADLINE")

    def test_tls_handshake_against_silent_server_is_bounded(self):
        server = self.serve(lambda c, r: time.sleep(5), read_request=False)
        context = hf._context()
        deadline = time.monotonic() + 0.5
        def remaining():
            left = deadline - time.monotonic()
            if left <= 0:
                raise hf.Stop("HTTPSFETCH_DEADLINE")
            return left
        started = time.monotonic()
        with self.assertRaises((TimeoutError, hf.Stop)):
            hf._tls_connect("127.0.0.1", server.port, context, remaining)
        self.assertLess(time.monotonic() - started, 1.8)

    def test_handshake_drip_cannot_extend_deadline(self):
        def script(conn, request):
            for _ in range(100):
                time.sleep(0.1)
                conn.sendall(b"\x16")  # TLS-looking bytes, never a full record
        server = self.serve(script, read_request=False)
        context = hf._context()
        deadline = time.monotonic() + 0.6
        def remaining():
            left = deadline - time.monotonic()
            if left <= 0:
                raise hf.Stop("HTTPSFETCH_DEADLINE")
            return left
        started = time.monotonic()
        with self.assertRaises((TimeoutError, hf.Stop, ssl.SSLError)):
            hf._tls_connect("127.0.0.1", server.port, context, remaining)
        self.assertLess(time.monotonic() - started, 1.8)

    def test_no_threads_left_behind(self):
        before = threading.active_count()
        server = self.serve(lambda c, r: time.sleep(3))
        fetch = self.fetch(server, total_deadline=0.3)
        self.code(lambda: fetch(URL))
        # only the test server's own threads exist; the fetch added none
        started = threading.active_count()
        self.code(lambda: fetch(URL))
        self.assertLessEqual(threading.active_count(), started + 1)  # server handler only
        self.assertGreaterEqual(threading.active_count(), before)


class PolicyTests(Base):
    def test_limits_are_pinned(self):
        self.assertEqual((hf.MAX_BODY, hf.MAX_HEADER), (262144, 16384))

    def test_weak_tls_policy_is_refused(self):
        bad = ssl.create_default_context()
        bad.check_hostname = False
        bad.verify_mode = ssl.CERT_NONE
        with mock.patch.object(hf.ssl, "create_default_context", return_value=bad):
            self.assertEqual(self.code(hf._context), "HTTPSFETCH_TLS_POLICY_INVALID")

    def test_context_raises_weak_minimum_version(self):
        weak = ssl.create_default_context()
        weak.minimum_version = ssl.TLSVersion.MINIMUM_SUPPORTED
        with mock.patch.object(hf.ssl, "create_default_context", return_value=weak):
            context = hf._context()
        self.assertGreaterEqual(context.minimum_version, ssl.TLSVersion.TLSv1_2)
        self.assertEqual(context.verify_mode, ssl.CERT_REQUIRED)
        self.assertTrue(context.check_hostname)

    def test_production_connect_uses_verified_context_and_hostname(self):
        calls = {}
        class FakeTls:
            def settimeout(self, value): calls["tls_timeout"] = value
            def do_handshake(self): calls["handshake"] = True
            def close(self): pass
        class FakeContext:
            def wrap_socket(self, sock, server_hostname, do_handshake_on_connect):
                calls.update(host=server_hostname, lazy=do_handshake_on_connect)
                return FakeTls()
        with mock.patch.object(hf.socket, "create_connection") as cc:
            cc.return_value = mock.Mock()
            hf._tls_connect(hf.HOST, 443, FakeContext(), lambda: 5.0, 3.0)
        cc.assert_called_once_with((hf.HOST, 443), timeout=3.0)
        self.assertEqual(calls["host"], hf.HOST)
        self.assertIs(calls["lazy"], False)
        self.assertTrue(calls["handshake"])

    def test_only_exact_health_urls_are_allowed(self):
        fetch = hf.build_fetch(connect=lambda *a: self.fail("must not connect"))
        for bad in (hf.ORIGIN + "/health/web?x=1", hf.ORIGIN + "/health/web#a",
                    "http://sistema.granlogiamixta.cl/health/web",
                    "https://sistema.granlogiamixta.cl:444/health/web",
                    "https://user@sistema.granlogiamixta.cl/health/web",
                    "https://evil.example/health/web", hf.ORIGIN + "/etc/passwd",
                    hf.ORIGIN + "/health/web/", "", None, b"x"):
            self.assertEqual(self.code(lambda: fetch(bad)), "HTTPSFETCH_URL_NOT_ALLOWED")

    def test_all_provider_urls_are_allowed_and_origin_matches(self):
        self.assertEqual(hf.ORIGIN, hp.ORIGIN)
        for path in (hp.HOME, hp.WEB, hp.READY, hp.OIDC, hp.VERSION):
            self.assertIn(hf.ORIGIN + path, hf.ALLOWED_URLS)
        self.assertEqual(len(hf.ALLOWED_URLS), 5)
        self.assertLessEqual(hp.MAX_HOME, hf.MAX_BODY)

    def test_works_with_health_provider_end_to_end(self):
        sha = "a" * 40
        issuer = hf.ORIGIN + hp.ISSUER_PATH
        routes = {
            "/": b"<html></html>", "/health/web": b"ok",
            "/health/ready": b'{"status":"ready","database":"postgresql"}',
            hp.OIDC: ('{"issuer":"%s","authorization_endpoint":"%s/a","token_endpoint":"%s/t",'
                      '"jwks_uri":"%s/j"}' % ((issuer,) * 4)).encode(),
            "/qa-version.json": ('{"environment":"QA_SYNTHETIC","source_sha":"%s"}' % sha).encode(),
        }
        state = {"redirect": False}
        def script(conn, request):
            path = request.split(b" ")[1].decode()
            if state["redirect"] and path == "/health/web":
                conn.sendall(reply(302, headers=b"Location: /x\r\n"))
            else:
                conn.sendall(reply(body=routes[path]))
        check, revision = hp.build(self.fetch(self.serve(script)))
        self.assertTrue(check())
        self.assertEqual(revision(), sha)
        state["redirect"] = True
        self.assertFalse(check())

    def test_library_only_no_entrypoint_env_or_threads(self):
        src = (ROOT / "scripts/qa/qa525-vnext-https-fetch.py").read_text()
        tree = ast.parse(src)
        mods = {a.name for n in ast.walk(tree) if isinstance(n, ast.Import) for a in n.names}
        self.assertEqual(mods, {"re", "socket", "ssl", "time"})
        self.assertFalse(any(isinstance(n, ast.ImportFrom) for n in ast.walk(tree)))
        attrs = {n.attr for n in ast.walk(tree) if isinstance(n, ast.Attribute)}
        for forbidden in ("environ", "getenv", "system", "Popen", "Thread", "Timer", "alarm",
                          "setitimer", "load_cert_chain", "load_verify_locations",
                          "set_tunnel", "getproxies"):
            self.assertNotIn(forbidden, attrs)
        with self.assertRaises(SystemExit):
            exec(compile(src, "x", "exec"), {"__name__": "__main__"})


if __name__ == "__main__":
    unittest.main()
