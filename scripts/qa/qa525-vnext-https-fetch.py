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
* a hard cap on body bytes and a total deadline for the whole request, also
  against slow drip responses; any problem raises a fixed code, and neither
  URLs, headers, bodies nor system messages are ever placed in the error.
"""
import http.client
import ssl
import time

HOST = "sistema.granlogiamixta.cl"
ORIGIN = "https://" + HOST
PATHS = ("/", "/health/web", "/health/ready",
         "/identity/realms/pmgm/.well-known/openid-configuration",
         "/qa-version.json")
ALLOWED_URLS = frozenset(ORIGIN + path for path in PATHS)
MAX_BODY = 262144
CONNECT_TIMEOUT = 8.0
TOTAL_DEADLINE = 20.0
CHUNK = 8192
HEADERS = {"Accept": "*/*", "Accept-Encoding": "identity",
           "Connection": "close", "User-Agent": "qa525-vnext-health/1"}


class Stop(Exception):
    """Fixed diagnostic code; never carries external content."""


def _context():
    context = ssl.create_default_context()  # system CAs, verify + hostname on
    context.minimum_version = ssl.TLSVersion.TLSv1_2
    if context.verify_mode != ssl.CERT_REQUIRED or not context.check_hostname:
        raise Stop("HTTPSFETCH_TLS_POLICY_INVALID")
    return context


def _connection(host, port, timeout, context):
    return http.client.HTTPSConnection(host, port, timeout=timeout, context=context)


def build_fetch(*, connection_factory=_connection, context_factory=_context,
                clock=time.monotonic):
    """Return the injected-effect ``fetch``; factories exist only for tests."""

    def fetch(url):
        if type(url) is not str or url not in ALLOWED_URLS:
            raise Stop("HTTPSFETCH_URL_NOT_ALLOWED")
        path = url[len(ORIGIN):]
        started = clock()
        connection = None
        try:
            context = context_factory()
            connection = connection_factory(HOST, 443, CONNECT_TIMEOUT, context)
            connection.request("GET", path, headers=dict(HEADERS))
            response = connection.getresponse()
            status = response.status
            body = bytearray()
            while True:
                if clock() - started > TOTAL_DEADLINE:
                    raise Stop("HTTPSFETCH_DEADLINE")
                chunk = response.read(min(CHUNK, MAX_BODY + 1 - len(body)))
                if not chunk:
                    break
                body += chunk
                if len(body) > MAX_BODY:
                    raise Stop("HTTPSFETCH_BODY_TOO_LARGE")
            if clock() - started > TOTAL_DEADLINE:
                raise Stop("HTTPSFETCH_DEADLINE")
            return status, url, bytes(body)
        except Stop:
            raise
        except ssl.SSLError:
            raise Stop("HTTPSFETCH_TLS_FAILED") from None
        except Exception:
            raise Stop("HTTPSFETCH_FAILED") from None
        finally:
            if connection is not None:
                try:
                    connection.close()
                except Exception:
                    pass

    return fetch


if __name__ == "__main__":
    raise SystemExit("QA525_HTTPS_FETCH_LIBRARY_ONLY_NO_EXECUTION")
