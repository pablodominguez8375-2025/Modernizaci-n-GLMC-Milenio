#!/usr/bin/env python3
"""Read-only, fail-closed Keycloak Master inventory preflight for #604.

No identity changes, passwords or PII in output. The token and expected subject
must come from protected host credential files, never from Git or arguments.
This is a preflight snapshot, NOT atomic uniqueness or approval to bootstrap.
"""
import argparse
import json
import re
import stat
import sys
import urllib.parse
import urllib.request
from pathlib import Path

ROLE = "platform_superadmin"
LIMIT = 10000
PAGE = 100


class Blocked(Exception):
    """Machine-readable, non-sensitive denial code."""


def _valid_id(value):
    return isinstance(value, str) and 0 < len(value) <= 320 and not any(
        c.isspace() or ord(c) < 32 for c in value)


def check_inventory(get, realm, expected_subject, expected_issuer):
    """get(path) returns parsed JSON. Never emits or mutates realm records."""
    if not _valid_id(expected_subject) or not (
        isinstance(expected_issuer, str) and
        expected_issuer.startswith("https://") and
        len(expected_issuer) <= 2048):
        raise Blocked("MASTER_REGISTRATION_UNAVAILABLE")
    prefix = "/admin/realms/" + realm
    discovery = get("/realms/" + realm + "/.well-known/openid-configuration")
    if not isinstance(discovery, dict) or discovery.get("issuer") != expected_issuer:
        raise Blocked("MASTER_ISSUER_MISMATCH")
    total = get(prefix + "/users/count")
    if type(total) is not int or total < 1 or total > LIMIT:
        raise Blocked("MASTER_INVENTORY_INCOMPLETE")

    seen = set()
    master_holders = []
    admin_accounts = []
    for first in range(0, total, PAGE):
        batch = get(prefix + "/users?first=" + str(first) + "&max=" + str(PAGE))
        if not isinstance(batch, list) or len(batch) != min(PAGE, total - first):
            raise Blocked("MASTER_INVENTORY_INCOMPLETE")
        for user in batch:
            if not isinstance(user, dict) or not _valid_id(user.get("id")) or not isinstance(user.get("username"), str):
                raise Blocked("MASTER_INVENTORY_INCOMPLETE")
            subject = user["id"]
            if subject in seen:
                raise Blocked("MASTER_INVENTORY_INCOMPLETE")
            seen.add(subject)
            name = user["username"]
            if name.casefold() == "admin":
                admin_accounts.append(user)
                if name != "admin":
                    raise Blocked("MASTER_ACCOUNT_AMBIGUOUS")

            # Composite realm role resolution includes inherited realm roles.
            roles = get(prefix + "/users/" + urllib.parse.quote(subject, safe="") +
                        "/role-mappings/realm/composite")
            if not isinstance(roles, list) or any(
                not isinstance(role, dict) or not isinstance(role.get("name"), str)
                for role in roles):
                raise Blocked("MASTER_ROLE_INVENTORY_INCOMPLETE")
            if any(role["name"] == ROLE for role in roles):
                master_holders.append(user)

            # A client-scoped role with the same name must not be confused
            # with the realm Master role by downstream token mappers.
            mappings = get(prefix + "/users/" + urllib.parse.quote(subject, safe="") +
                           "/role-mappings")
            if not isinstance(mappings, dict):
                raise Blocked("MASTER_ROLE_INVENTORY_INCOMPLETE")
            clients = mappings.get("clientMappings") or {}
            if not isinstance(clients, dict):
                raise Blocked("MASTER_ROLE_INVENTORY_INCOMPLETE")
            for client in clients.values():
                if not isinstance(client, dict) or not isinstance(client.get("mappings"), list):
                    raise Blocked("MASTER_ROLE_INVENTORY_INCOMPLETE")
                if any(isinstance(r, dict) and r.get("name") == ROLE
                       for r in client["mappings"]):
                    raise Blocked("MASTER_ACCOUNT_AMBIGUOUS")

    # Detect partial pagination and changes to the number of users while
    # iterating. This does NOT replace an exclusive provisioning lock.
    if len(seen) != total or get(prefix + "/users/count") != total or get(
            prefix + "/users?first=" + str(total) + "&max=1") != []:
        raise Blocked("MASTER_INVENTORY_INCOMPLETE")
    if len(admin_accounts) != 1 or len(master_holders) != 1:
        raise Blocked("MASTER_ACCOUNT_AMBIGUOUS")
    master = master_holders[0]
    if (master["id"] != expected_subject or master["username"] != "admin" or
            master.get("enabled") is not True or
            admin_accounts[0]["id"] != expected_subject):
        raise Blocked("MASTER_ACCOUNT_AMBIGUOUS")
    return total


def _read_protected_file(path):
    p = Path(path)
    if p.is_symlink() or not p.is_file():
        raise Blocked("MASTER_CREDENTIAL_FILE_UNSAFE")
    if stat.S_IMODE(p.stat().st_mode) & 0o077 or p.stat().st_size > 16384:
        raise Blocked("MASTER_CREDENTIAL_FILE_UNSAFE")
    value = p.read_text(encoding="utf-8").strip()
    if not value:
        raise Blocked("MASTER_CREDENTIAL_FILE_UNSAFE")
    return value


def main(argv=None):
    parser = argparse.ArgumentParser(description="Preflight Keycloak Máster único (solo lectura)")
    parser.add_argument("--base-url", required=True)
    parser.add_argument("--realm", required=True)
    parser.add_argument("--issuer", required=True)
    parser.add_argument("--token-file", required=True)
    parser.add_argument("--subject-file", required=True)
    args = parser.parse_args(argv)
    try:
        parsed = urllib.parse.urlsplit(args.base_url)
        if (parsed.scheme != "https" or not parsed.hostname or parsed.username or
                parsed.password or parsed.query or parsed.fragment or
                not re.fullmatch(r"[A-Za-z0-9_-]{1,80}", args.realm) or
                not re.fullmatch(r"/(?:[A-Za-z0-9_-]+/)*[A-Za-z0-9_-]*", parsed.path)):
            raise Blocked("MASTER_CONFIG_INVALID")
        base = args.base_url.rstrip("/")
        token = _read_protected_file(args.token_file)
        subject = _read_protected_file(args.subject_file)

        def get(path):
            url = base + path
            request = urllib.request.Request(url, headers={
                "Authorization": "Bearer " + token,
                "Accept": "application/json",
            }, method="GET")
            with urllib.request.urlopen(request, timeout=10) as response:
                if response.status != 200:
                    raise Blocked("MASTER_REMOTE_INVENTORY_UNAVAILABLE")
                data = response.read(2_000_001)
                if len(data) > 2_000_000:
                    raise Blocked("MASTER_INVENTORY_INCOMPLETE")
                return json.loads(data)

        total = check_inventory(get, args.realm, subject, args.issuer)
        print("PASS MASTER_UNIQUE_REALM_SNAPSHOT users_checked=" + str(total))
        return 0
    except (Blocked, ValueError, OSError, UnicodeError, urllib.error.URLError,
            json.JSONDecodeError) as exc:
        code = str(exc) if isinstance(exc, Blocked) else "MASTER_REMOTE_INVENTORY_UNAVAILABLE"
        print("BLOCKED " + code)
        return 2


if __name__ == "__main__":
    sys.exit(main())
