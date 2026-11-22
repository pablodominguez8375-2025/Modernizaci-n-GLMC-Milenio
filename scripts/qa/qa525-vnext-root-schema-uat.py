#!/usr/bin/env python3
"""Authenticated read-only QA525 UAT probes. Verified PRE-4 library; no CLI or GO.

Two distinct JWTs are read from the root 0600 JSON credential by the effects
module: registered initial-load Master and platform operator. The API validates
the signatures and permissions. This module sends only fixed GET requests over
verified TLS, never prints tokens, identity or response bodies, and only POSTs the nonpersistent\nsynthetic CSV preview with writesPerformed=0.
"""
import base64
import datetime as dt
import http.client
import json
import re
import ssl

HOST = "sistema.granlogiamixta.cl"
ISSUER = "https://" + HOST + "/identity/realms/pmgm"
PATHS = ("/api/session/me", "/api/system/workshops",
         "/api/platform/bootstrap/initial-load/status")
MAX_BODY = 2_500_000
BASE = "/api/platform/bootstrap/initial-load"
GET_PATHS = frozenset(PATHS + ("/health/ready", "/qa-version.json", BASE + "/contract", BASE + "/template-csv"))
POST_PATHS = frozenset((BASE + "/preview",))
SIBLING_MODULES = {"synthetic": "qa525-vnext-synthetic-e2e.py"}
_DEPS = {}


def bind_dependencies(mods):
    if type(mods) is not dict or set(mods) != set(SIBLING_MODULES):
        raise Stop("SCHEMA_UAT_BINDING_INVALID")
    _DEPS.clear()
    _DEPS.update(mods)
TOKEN = re.compile(r"[A-Za-z0-9_-]+[.][A-Za-z0-9_-]+[.][A-Za-z0-9_-]+\Z")


class Stop(Exception):
    """Fixed code only."""


def _tokens(read_private, clock):
    try:
        raw = read_private()
        if type(raw) is not bytes or not 0 < len(raw) <= 4096:
            raise ValueError()
        data = json.loads(raw)
        if type(data) is not dict or set(data) != {"master_token", "operator_token"}:
            raise ValueError()
        now = clock()
        if not isinstance(now, dt.datetime) or now.tzinfo is None:
            raise ValueError()
        for token in data.values():
            if type(token) is not str or len(token) > 2048 or TOKEN.fullmatch(token) is None:
                raise ValueError()
            payload = token.split(".")[1]
            claims = json.loads(base64.urlsafe_b64decode(payload + "=" * (-len(payload) % 4)))
            if (type(claims) is not dict or claims.get("iss") != ISSUER
                    or type(claims.get("exp")) is not int
                    or claims["exp"] <= now.timestamp() + 1800):
                raise ValueError()
        if data["master_token"] == data["operator_token"]:
            raise ValueError()
        return data
    except Exception:
        raise Stop("SCHEMA_UAT_CREDENTIAL_INVALID") from None


def _get(path, token):
    conn = http.client.HTTPSConnection(HOST, 443, timeout=12, context=ssl.create_default_context())
    try:
        conn.request("GET", path, headers={"Authorization": "Bearer " + token,
                     "Accept": "application/json", "Cache-Control": "no-store"})
        resp = conn.getresponse()
        body = resp.read(MAX_BODY + 1)
        if resp.status != 200 or len(body) > MAX_BODY:
            raise Stop("SCHEMA_UAT_HTTP_REJECTED")
        data = json.loads(body)
        if type(data) is not dict:
            raise Stop("SCHEMA_UAT_RESPONSE_INVALID")
        return data
    except Stop:
        raise
    except Exception:
        raise Stop("SCHEMA_UAT_HTTP_REJECTED") from None
    finally:
        conn.close()


def _synthetic_request(method, path, token, body=None):
    if method not in ("GET", "POST") or (method == "GET" and path not in GET_PATHS) or (method == "POST" and path not in POST_PATHS):
        raise Stop("SCHEMA_UAT_PATH_INVALID")
    if method == "POST":
        if type(body) is not dict or set(body) != {"Talleres.csv", "Hermanos.csv", "CargosVigentes.csv"}:
            raise Stop("SCHEMA_UAT_PAYLOAD_INVALID")
        payload = json.dumps(body, sort_keys=True, separators=(",", ":")).encode("utf-8")
        if len(payload) > MAX_BODY:
            raise Stop("SCHEMA_UAT_PAYLOAD_INVALID")
    else:
        if body is not None:
            raise Stop("SCHEMA_UAT_PAYLOAD_INVALID")
        payload = None
    conn = http.client.HTTPSConnection(HOST, 443, timeout=12, context=ssl.create_default_context())
    try:
        headers = {"Accept": "application/json", "Cache-Control": "no-store"}
        if token is not None:
            headers["Authorization"] = "Bearer " + token
        if payload is not None:
            headers["Content-Type"] = "application/json"
        conn.request(method, path, body=payload, headers=headers)
        resp = conn.getresponse()
        raw = resp.read(MAX_BODY + 1)
        if len(raw) > MAX_BODY:
            raise Stop("SCHEMA_UAT_HTTP_REJECTED")
        if path == BASE + "/template-csv" and resp.status == 200:
            return resp.status, raw
        try:
            return resp.status, json.loads(raw)
        except (ValueError, UnicodeError):
            return resp.status, None
    except Stop:
        raise
    except Exception:
        raise Stop("SCHEMA_UAT_HTTP_REJECTED") from None
    finally:
        conn.close()


def make(read_private, clock, probe=None, expected_sha=None, synthetic_probe=None):
    """Validate credential before nonce; return checks run after application."""
    if not callable(read_private) or not callable(clock) or (probe is not None and not callable(probe)):
        raise Stop("SCHEMA_UAT_INPUT_INVALID")
    _tokens(read_private, clock)
    if expected_sha is not None and (type(expected_sha) is not str or re.fullmatch(r"[0-9a-f]{40}", expected_sha) is None
                                     or set(_DEPS) != set(SIBLING_MODULES)
                                     or not callable(getattr(_DEPS["synthetic"], "uat_checks", None))):
        raise Stop("SCHEMA_UAT_BINDING_INVALID")
    request = probe or _get

    def session():
        tokens = _tokens(read_private, clock)
        result = request(PATHS[0], tokens["operator_token"])
        if type(result) is not dict:
            raise Stop("SCHEMA_UAT_SESSION_INVALID")
        return True

    def workshops():
        tokens = _tokens(read_private, clock)
        result = request(PATHS[1], tokens["operator_token"])
        if (type(result) is not dict or type(result.get("items")) is not list
                or type(result.get("canManage")) is not bool):
            raise Stop("SCHEMA_UAT_WORKSHOPS_INVALID")
        return True

    def initial_load():
        tokens = _tokens(read_private, clock)
        result = request(PATHS[2], tokens["master_token"])
        if (type(result) is not dict or result.get("code") != "OK"
                or result.get("status") not in ("not_opened", "open", "closed")
                or type(result.get("writesPerformed")) is not int):
            raise Stop("SCHEMA_UAT_LOAD_INVALID")
        return True

    checks = (session, workshops, initial_load)
    if expected_sha is None:
        return checks

    def http(method, path, body=None):
        tokens = _tokens(read_private, clock)
        token = tokens["operator_token"] if path == "/api/system/workshops" else tokens["master_token"]
        return (synthetic_probe or _synthetic_request)(method, path, token, body)

    def unauth(method, path):
        return (synthetic_probe or _synthetic_request)(method, path, None, None)

    return checks + _DEPS["synthetic"].uat_checks(http, expected_sha, unauth=unauth)


if __name__ == "__main__":
    raise SystemExit("QA525_SCHEMA_UAT_LIBRARY_ONLY_NO_GO")
