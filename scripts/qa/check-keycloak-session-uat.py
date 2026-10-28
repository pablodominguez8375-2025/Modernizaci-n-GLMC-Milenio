#!/usr/bin/env python3
"""Prueba UAT de SOLO LECTURA: JWT previo a reactivación vs sesión nueva (#768).

Debe ejecutarse SOLO tras el procedimiento institucional autorizado de
desactivar/reactivar una cuenta sintética que NO sea qa.admin.
No cierra sesiones, no habilita cuentas ni lee contraseñas o secretos de cliente.
No imprime ni conserva tokens, identificadores ni respuestas HTTP.
"""
import argparse
import base64
import http.client
import json
import os
import re
import ssl
import stat
import time
import uuid

HOST = "sistema.granlogiamixta.cl"
ISSUER = "https://sistema.granlogiamixta.cl/identity/realms/pmgm"
PATH = "/api/session/me"
MAX_TOKEN_BYTES = 32768
MAX_RESPONSE_BYTES = 65536


class Blocked(Exception):
    """Fallo preventivo con código fijo, sin detalles privados."""


def require(condition, code):
    if not condition:
        raise Blocked(code)


def read_private_token(path):
    """Protege contra symlinks, archivos de otros usuarios y lectura sin límite."""
    flags = os.O_RDONLY | getattr(os, "O_NOFOLLOW", 0)
    try:
        fd = os.open(path, flags)
    except OSError:
        raise Blocked("PRIVATE_TOKEN_FILE_UNAVAILABLE") from None
    try:
        metadata = os.fstat(fd)
        require(stat.S_ISREG(metadata.st_mode) and metadata.st_uid == 0
                and stat.S_IMODE(metadata.st_mode) == 0o600,
                "PRIVATE_TOKEN_FILE_PERMISSIONS")
        data = os.read(fd, MAX_TOKEN_BYTES + 1)
        require(0 < len(data) <= MAX_TOKEN_BYTES, "TOKEN_FILE_SIZE")
        try:
            token = data.decode("ascii").strip()
        except UnicodeDecodeError:
            raise Blocked("TOKEN_NOT_ASCII") from None
        require(token and not any(ch.isspace() for ch in token), "TOKEN_FORMAT")
        return token
    finally:
        os.close(fd)


def parse_claims(token, now):
    """Inspección local NO criptográfica; la firma la valida la API real."""
    parts = token.split(".")
    require(len(parts) == 3 and all(parts), "TOKEN_FORMAT")
    try:
        payload = parts[1] + "=" * (-len(parts[1]) % 4)
        raw = base64.urlsafe_b64decode(payload.encode("ascii"))
        require(len(raw) <= 16384, "TOKEN_CLAIMS_SIZE")
        data = json.loads(raw)
    except (ValueError, UnicodeError, json.JSONDecodeError):
        raise Blocked("TOKEN_CLAIMS_INVALID") from None
    require(isinstance(data, dict), "TOKEN_CLAIMS_INVALID")
    username = data.get("preferred_username")
    subject = data.get("sub")
    require(isinstance(username, str) and
            re.fullmatch(r"qa\.[a-z0-9_.-]{2,60}", username) is not None and
            username != "qa.admin", "ONLY_SYNTHETIC_NONPRIVILEGED_IDENTITY")
    try:
        uid = uuid.UUID(subject)
    except (ValueError, TypeError, AttributeError):
        raise Blocked("SUBJECT_INVALID") from None
    require(data.get("iss") == ISSUER, "OIDC_ISSUER_MISMATCH")
    issued, expires = data.get("iat"), data.get("exp")
    require(type(issued) is int and type(expires) is int and
            issued <= now + 30 and expires > now + 45,
            "TOKEN_NOT_FRESH_ENOUGH_FOR_UAT")
    sid = data.get("sid")
    session_state = data.get("session_state")
    require(all(x is None or (isinstance(x, str) and 0 < len(x) <= 256)
                for x in (sid, session_state)), "SESSION_ID_INVALID")
    require(sid is not None or session_state is not None, "SESSION_ID_MISSING")
    require(sid is None or session_state is None or sid == session_state,
            "SESSION_ID_CONFLICT")
    return {"subject": str(uid), "username": username,
            "sid": sid or session_state, "iat": issued}


def api_session_status(token):
    """Solo GET fijo, TLS verificado, sin redirects y sin proxy HTTP."""
    conn = http.client.HTTPSConnection(
        HOST, 443, timeout=12, context=ssl.create_default_context())
    try:
        conn.request("GET", PATH, headers={
            "Authorization": "Bearer " + token,
            "Cache-Control": "no-store",
            "Accept": "application/json"
        })
        response = conn.getresponse()
        content = response.read(MAX_RESPONSE_BYTES + 1)
        require(len(content) <= MAX_RESPONSE_BYTES, "RESPONSE_TOO_LARGE")
        return response.status
    except (OSError, ssl.SSLError, http.client.HTTPException):
        raise Blocked("HTTPS_PROBE_UNAVAILABLE") from None
    finally:
        conn.close()


def verify(old_token, fresh_token, status_probe, now):
    """Debe observar 401 para JWT viejo aún NO expirado y 200 para sesión nueva."""
    require(old_token != fresh_token, "TOKENS_ARE_IDENTICAL")
    old = parse_claims(old_token, now)
    fresh = parse_claims(fresh_token, now)
    require(old["subject"] == fresh["subject"] and
            old["username"] == fresh["username"], "DIFFERENT_TEST_IDENTITIES")
    require(old["sid"] != fresh["sid"], "OLD_SESSION_NOT_REPLACED")
    require(old["iat"] <= fresh["iat"], "TOKEN_ORDER_INVALID")
    # Siempre probar sesión nueva primero; evita confundir un fallo general de API
    # con una supuesta revocación del token anterior.
    require(status_probe(fresh_token) == 200, "FRESH_SESSION_NOT_AUTHENTICATED")
    require(status_probe(old_token) == 401, "OLD_SESSION_STILL_AUTHENTICATED")
    return True


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--old-token-file", required=True)
    parser.add_argument("--fresh-token-file", required=True)
    parser.add_argument("--confirm-synthetic-only", action="store_true",
                        help="Confirmar que el procedimiento previo se hizo SOLO en QA sintético")
    args = parser.parse_args(argv)
    try:
        require(os.geteuid() == 0, "ROOT_REQUIRED_FOR_PRIVATE_TOKEN_FILES")
        require(args.confirm_synthetic_only, "SYNTHETIC_ONLY_CONFIRMATION_REQUIRED")
        old = read_private_token(args.old_token_file)
        new = read_private_token(args.fresh_token_file)
        verify(old, new, api_session_status, int(time.time()))
        print("QA_KEYCLOAK_REACTIVATION_READONLY: PASS")
        return 0
    except Blocked as e:
        print("QA_KEYCLOAK_REACTIVATION_READONLY: BLOCKED " + str(e))
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
