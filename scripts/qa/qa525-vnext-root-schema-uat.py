#!/usr/bin/env python3
"""Authenticated read-only QA525 UAT probes. Verified PRE-4 library; no CLI or GO.

Two distinct JWTs are read from the root 0600 JSON credential by the effects
module: registered initial-load Master and platform operator. The API validates
the signatures and permissions. This module sends only fixed GET requests over
verified TLS, never prints tokens, identity or response bodies, and never writes.
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
MAX_BODY = 65536
TOKEN = re.compile(r"[A-Za-z0-9_-]+[.][A-Za-z0-9_-]+[.][A-Za-z0-9_-]+\\Z")


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


def make(read_private, clock, probe=None):
    """Validate credential before nonce; return checks run after application."""
    if not callable(read_private) or not callable(clock) or (probe is not None and not callable(probe)):
        raise Stop("SCHEMA_UAT_INPUT_INVALID")
    _tokens(read_private, clock)
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

    return session, workshops, initial_load


if __name__ == "__main__":
    raise SystemExit("QA525_SCHEMA_UAT_LIBRARY_ONLY_NO_GO")
