#!/usr/bin/env python3
"""QA525 physical HTTPS ``fetch`` for the public-health provider. INACTIVE library.

Builds ``fetch(url) -> (status, final_url, body)`` for ``qa525-vnext-health.build``.
It performs a real TLS request only when a future reviewed entry point calls the
returned function; importing this file does nothing and there is no command line.

Rules, all enforced here and covered by tests:
* only the exact URLs the health provider asks for: the fixed origin plus one of
  its fixed paths, never a query, fragment, userinfo, port or other host;
* TLS with the system trust store, certificate AND hostname verification,
  TLS 1.2 or newer; no client certificates, no cookies, no credentials;
* no redirects are followed: a 3xx is returned as is and ``final_url`` is the
  requested URL, so the provider refuses it;
* one HARD wall-clock deadline for connect, TLS handshake, request, headers and
  body. Every network operation is a single bounded call whose timeout is the
  time left, so a server that drips bytes cannot extend it; no threads are used.
  RESIDUAL: name resolution is a blocking system call that cannot be bounded
  here; the future entry point must also run under an external process limit;
* the response is read by a small strict parser (HTTP/1.0 request, so no chunked
  encoding): bounded header size and body size, exact Content-Length, no
  Transfer-Encoding or Content-Encoding; any problem raises a fixed code and
  neither URLs, headers, bodies nor system messages are placed in the error.
"""
import re
import socket
import ssl
import time

HOST = "sistema.granlogiamixta.cl"
ORIGIN = "https://" + HOST
PATHS = ("/", "/health/web", "/health/ready",
         "/identity/realms/pmgm/.well-known/openid-configuration",
         "/qa-version.json")
ALLOWED_URLS = frozenset(ORIGIN + path for path in PATHS)
MAX_BODY = 262144
MAX_HEADER = 16384
CONNECT_TIMEOUT = 8.0
TOTAL_DEADLINE = 20.0
CHUNK = 8192
USER_AGENT = "qa525-vnext-health/1"
STATUS_LINE = re.compile(rb"HTTP/1\.[01] ([0-9]{3})(?: [^\r\n]*)?\Z")
DIGITS = re.compile(rb"[0-9]{1,9}\Z")


class Stop(Exception):
    """Fixed diagnostic code; never carries external content."""


def _context():
    context = ssl.create_default_context()  # system CAs, verify + hostname on
    context.minimum_version = ssl.TLSVersion.TLSv1_2
    if context.verify_mode != ssl.CERT_REQUIRED or not context.check_hostname:
        raise Stop("HTTPSFETCH_TLS_POLICY_INVALID")
    return context


def _tls_connect(host, port, context, remaining, connect_timeout=CONNECT_TIMEOUT):
    """TCP connect + TLS handshake, each bounded by the time left."""
    sock = socket.create_connection((host, port), timeout=min(connect_timeout, remaining()))
    try:
        sock.settimeout(remaining())
        tls = context.wrap_socket(sock, server_hostname=host, do_handshake_on_connect=False)
    except BaseException:
        sock.close()
        raise
    try:
        tls.settimeout(remaining())
        tls.do_handshake()  # one call, one overall timeout
    except BaseException:
        tls.close()
        raise
    return tls


def _parse_head(head):
    lines = head.split(b"\r\n")
    hit = STATUS_LINE.match(lines[0])
    if hit is None:
        raise Stop("HTTPSFETCH_RESPONSE_INVALID")
    length = None
    for line in lines[1:]:
        name, sep, value = line.partition(b":")
        if not sep or not name or name != name.strip():
            raise Stop("HTTPSFETCH_RESPONSE_INVALID")
        name, value = name.lower(), value.strip()
        if name == b"transfer-encoding":
            raise Stop("HTTPSFETCH_RESPONSE_INVALID")
        if name == b"content-encoding" and value.lower() != b"identity":
            raise Stop("HTTPSFETCH_RESPONSE_INVALID")
        if name == b"content-length":
            if length is not None or DIGITS.match(value) is None:
                raise Stop("HTTPSFETCH_RESPONSE_INVALID")
            length = int(value)
    return int(hit.group(1)), length


def build_fetch(*, connect=_tls_connect, context_factory=_context,
                clock=time.monotonic, total_deadline=TOTAL_DEADLINE,
                connect_timeout=CONNECT_TIMEOUT):
    """Return the ``fetch``; ``connect`` and clock are injectable for tests."""

    def fetch(url):
        if type(url) is not str or url not in ALLOWED_URLS:
            raise Stop("HTTPSFETCH_URL_NOT_ALLOWED")
        path = url[len(ORIGIN):]
        end = clock() + total_deadline

        def remaining():
            left = end - clock()
            if left <= 0:
                raise Stop("HTTPSFETCH_DEADLINE")
            return left

        sock = None
        try:
            context = context_factory()
            if connect is _tls_connect:
                sock = connect(HOST, 443, context, remaining, connect_timeout)
            else:
                sock = connect(HOST, 443, context, remaining)
            sock.settimeout(remaining())
            sock.sendall(("GET %s HTTP/1.0\r\nHost: %s\r\nAccept: */*\r\n"
                          "User-Agent: %s\r\nConnection: close\r\n\r\n"
                          % (path, HOST, USER_AGENT)).encode("ascii"))
            buffer = b""
            while b"\r\n\r\n" not in buffer:
                if len(buffer) > MAX_HEADER:
                    raise Stop("HTTPSFETCH_HEADER_TOO_LARGE")
                sock.settimeout(remaining())
                chunk = sock.recv(CHUNK)
                if not chunk:
                    raise Stop("HTTPSFETCH_RESPONSE_INVALID")
                buffer += chunk
            head, _, body = buffer.partition(b"\r\n\r\n")
            if len(head) > MAX_HEADER:
                raise Stop("HTTPSFETCH_HEADER_TOO_LARGE")
            status, length = _parse_head(head)
            if length is not None and length > MAX_BODY:
                raise Stop("HTTPSFETCH_BODY_TOO_LARGE")
            while (length is None or len(body) < length):
                if len(body) > MAX_BODY:
                    raise Stop("HTTPSFETCH_BODY_TOO_LARGE")
                sock.settimeout(remaining())
                chunk = sock.recv(CHUNK)
                if not chunk:
                    if length is not None:
                        raise Stop("HTTPSFETCH_RESPONSE_INVALID")
                    break
                body += chunk
            if len(body) > MAX_BODY or (length is not None and len(body) != length):
                raise Stop("HTTPSFETCH_BODY_TOO_LARGE" if len(body) > MAX_BODY
                           else "HTTPSFETCH_RESPONSE_INVALID")
            return status, url, bytes(body)
        except Stop:
            raise
        except TimeoutError:
            raise Stop("HTTPSFETCH_DEADLINE") from None
        except ssl.SSLError:
            raise Stop("HTTPSFETCH_TLS_FAILED") from None
        except Exception:
            raise Stop("HTTPSFETCH_FAILED") from None
        finally:
            if sock is not None:
                try:
                    sock.close()
                except Exception:
                    pass

    return fetch


if __name__ == "__main__":
    raise SystemExit("QA525_HTTPS_FETCH_LIBRARY_ONLY_NO_EXECUTION")
