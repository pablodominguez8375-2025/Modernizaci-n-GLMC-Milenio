#!/usr/bin/env python3
"""QA525 vNext public-health provider -- INACTIVE library, NO network code.

Builds the two callables that ``qa525-vnext-runtime.build_reader`` expects:
``health_check()`` (True only after HTTPS entry, WEB, API readiness and OIDC
discovery pass) and ``served_web_revision()`` (the 40-hex source SHA the public
WEB actually serves). The only way to reach a host is the injected ``fetch``
callable ``fetch(url) -> (status, final_url, body)``; this file imports only
json and re (no network, process, environment or Docker client). Passing these tests proves
the contract against fakes, never a physical result or permission to APPLY.

Contract mirrored from the existing operators (same paths and strictness):
* origin ``https://sistema.granlogiamixta.cl``; every URL is built here from a
  fixed path list, never from external text;
* status exactly 200 and ``final_url == url`` (a redirect is a failure);
* ``/health/web`` body is ``ok``; ``/health/ready`` is exactly
  ``{"status": "ready", "database": "postgresql"}``;
* OIDC discovery for realm ``pmgm`` has the exact issuer and every endpoint
  under the same origin ``/identity/``;
* ``/qa-version.json`` is exactly ``{"environment": "QA_SYNTHETIC",
  "source_sha": <40 lowercase hex>}``.
Any exception, wrong type, oversize body, duplicate JSON key or NaN yields a
failure with a fixed code; external text is never echoed.
"""
import json
import re

STATUS = "HEALTH_PROVIDER_NOT_AUTHENTICATED_NOT_AUTHORIZED_FOR_PHYSICAL_APPLY"
ORIGIN = "https://sistema.granlogiamixta.cl"
ORIGIN_PATTERN = re.compile(r"https://[a-z0-9]+(?:[.-][a-z0-9]+)*\Z")
SHA40 = re.compile(r"[a-f0-9]{40}\Z")
HOME, WEB, READY = "/", "/health/web", "/health/ready"
OIDC = "/identity/realms/pmgm/.well-known/openid-configuration"
ISSUER_PATH = "/identity/realms/pmgm"
VERSION = "/qa-version.json"
MAX_HOME = 262144
MAX_SMALL = 2048
OIDC_ENDPOINTS = ("authorization_endpoint", "token_endpoint", "jwks_uri")


class Stop(Exception):
    """Fixed diagnostic code; never carries external content."""


def _unique(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise Stop("HEALTH_JSON_INVALID")
        result[key] = value
    return result


def _json(body):
    try:
        value = json.loads(body.decode("utf-8"), object_pairs_hook=_unique,
                           parse_constant=lambda _: (_ for _ in ()).throw(
                               Stop("HEALTH_JSON_INVALID")))
    except Stop:
        raise
    except (ValueError, UnicodeDecodeError, RecursionError, TypeError):
        raise Stop("HEALTH_JSON_INVALID") from None
    if type(value) is not dict:
        raise Stop("HEALTH_JSON_INVALID")
    return value


def build(fetch, origin=ORIGIN):
    """Return ``(health_check, served_web_revision)`` over the injected fetch."""
    if not callable(fetch):
        raise Stop("HEALTH_FETCH_INVALID")
    if type(origin) is not str or ORIGIN_PATTERN.fullmatch(origin) is None:
        raise Stop("HEALTH_ORIGIN_INVALID")

    def get(path, limit):
        url = origin + path
        try:
            answer = fetch(url)
        except Exception:
            raise Stop("HEALTH_FETCH_FAILED") from None
        if type(answer) is not tuple or len(answer) != 3:
            raise Stop("HEALTH_RESPONSE_INVALID")
        status, final_url, body = answer
        if (type(status) is not int or type(final_url) is not str
                or type(body) is not bytes):
            raise Stop("HEALTH_RESPONSE_INVALID")
        if status != 200 or final_url != url:
            raise Stop("HEALTH_STATUS_OR_REDIRECT_REFUSED")
        if len(body) > limit:
            raise Stop("HEALTH_BODY_TOO_LARGE")
        return body

    def version():
        marker = _json(get(VERSION, MAX_SMALL))
        sha = marker.get("source_sha")
        if (set(marker) != {"environment", "source_sha"}
                or marker["environment"] != "QA_SYNTHETIC"
                or type(sha) is not str or SHA40.fullmatch(sha) is None):
            raise Stop("HEALTH_VERSION_MARKER_INVALID")
        return sha

    def oidc():
        doc = _json(get(OIDC, MAX_SMALL * 8))
        if doc.get("issuer") != origin + ISSUER_PATH:
            raise Stop("HEALTH_OIDC_ISSUER_INVALID")
        for key in OIDC_ENDPOINTS:
            value = doc.get(key)
            if type(value) is not str or not value.startswith(origin + "/identity/"):
                raise Stop("HEALTH_OIDC_ENDPOINT_INVALID")

    def checks():
        get(HOME, MAX_HOME)
        if get(WEB, MAX_SMALL).strip() != b"ok":
            raise Stop("HEALTH_WEB_NOT_OK")
        if _json(get(READY, MAX_SMALL)) != {"status": "ready",
                                            "database": "postgresql"}:
            raise Stop("HEALTH_API_NOT_READY")
        oidc()
        version()

    def health_check():
        try:
            checks()
        except Exception:
            return False
        return True

    def served_web_revision():
        try:
            return version()
        except Stop:
            raise
        except Exception:
            raise Stop("HEALTH_VERSION_MARKER_INVALID") from None

    return health_check, served_web_revision


if __name__ == "__main__":
    raise SystemExit("QA525_HEALTH_LIBRARY_ONLY_NO_EXECUTION")
