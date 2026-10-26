#!/usr/bin/env python3
"""#604: controlled, fail-closed Keycloak QA Master preparation on glmdch.

This DOES NOT provide global uniqueness for arbitrary Keycloak administrators.
All output is fixed diagnostic codes; never print subject, tokens, password or PII.
Staged, no automatic migration of synthetic qa.admin or other role holders.
"""
import argparse
import fcntl
import json
import os
from pathlib import Path
import re
import socket
import stat
import sys
import urllib.error
import urllib.parse
import urllib.request

ROLE = "platform_superadmin"
LIMIT = 10000
PAGE = 100
QA_LOCAL_BASE = "http://127.0.0.1:18180/identity"
QA_ISSUER = "https://sistema.granlogiamixta.cl/identity/realms/pmgm"
LEGACY_QA_SUBJECT = "aa820001-0000-4000-8000-000000000001"


class Blocked(Exception):
    pass


def private_file(path):
    p = Path(path)
    st = p.lstat()
    if (not stat.S_ISREG(st.st_mode) or st.st_uid != 0 or
            stat.S_IMODE(st.st_mode) & 0o077 or st.st_size > 16384):
        raise Blocked("HOST_FILE_UNSAFE")
    for parent in p.parents:
        s = parent.lstat()
        if (not stat.S_ISDIR(s.st_mode) or s.st_uid != 0 or
                stat.S_IMODE(s.st_mode) & 0o022):
            raise Blocked("HOST_PATH_UNSAFE")
    value = p.read_text(encoding="utf-8").strip()
    if not value:
        raise Blocked("HOST_FILE_EMPTY")
    return value


def save_subject(path, value):
    # Never overwrite an existing registration (including broken attempts).
    p = Path(path)
    if not re.fullmatch(r"[A-Za-z0-9_-]{8,320}", value):
        raise Blocked("MASTER_SUBJECT_INVALID")
    for parent in p.parents:
        st = parent.lstat()
        if (not stat.S_ISDIR(st.st_mode) or st.st_uid != 0 or
                stat.S_IMODE(st.st_mode) & 0o022):
            raise Blocked("HOST_PATH_UNSAFE")
    fd = os.open(p, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, 0o600)
    with os.fdopen(fd, "w", encoding="utf-8") as stream:
        stream.write(value + "\n")
        stream.flush()
        os.fsync(stream.fileno())


def validate_endpoint(base, realm, issuer):
    # Only a localhost Docker port opened by the QA-only overlay is allowed.
    # Public HTTPS /identity/admin is correctly blocked by Caddy.
    # The token never crosses external HTTP: this endpoint is 127.0.0.1 only.
    if base != QA_LOCAL_BASE or realm != "pmgm" or issuer != QA_ISSUER:
        raise Blocked("ENDPOINT_INVALID")
    return base


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None


class Keycloak:
    def __init__(self, base, realm, token):
        self.base = base
        self.realm = realm
        self.token = token
        self.opener = urllib.request.build_opener(NoRedirect)

    def call(self, method, path, payload=None):
        if not path.startswith("/") or path.startswith("//"):
            raise Blocked("API_PATH_INVALID")
        request = urllib.request.Request(
            self.base + path,
            data=None if payload is None else json.dumps(payload).encode("utf-8"),
            method=method,
            headers={
                "Authorization": "Bearer " + self.token,
                "Accept": "application/json",
                "Content-Type": "application/json",
            },
        )
        try:
            with self.opener.open(request, timeout=10) as response:
                if response.status not in (200, 201, 204):
                    raise Blocked("KEYCLOAK_UNAVAILABLE")
                if method == "GET":
                    buf = response.read(2_000_001)
                    if len(buf) > 2_000_000:
                        raise Blocked("INVENTORY_INCOMPLETE")
                    return json.loads(buf)
                return None
        except (urllib.error.URLError, OSError, ValueError, json.JSONDecodeError):
            raise Blocked("KEYCLOAK_UNAVAILABLE") from None

    def get(self, path):
        return self.call("GET", path)

    def post(self, path, body):
        return self.call("POST", path, body)

    def put(self, path, body):
        return self.call("PUT", path, body)

    def delete(self, path, body):
        return self.call("DELETE", path, body)


def inventory(get, realm, issuer, allow_exact_legacy=False):
    prefix = "/admin/realms/" + realm
    disc = get("/realms/" + realm + "/.well-known/openid-configuration")
    if not isinstance(disc, dict) or disc.get("issuer") != issuer:
        raise Blocked("ISSUER_MISMATCH")
    total = get(prefix + "/users/count")
    if type(total) is not int or total < 0 or total > LIMIT:
        raise Blocked("INVENTORY_INCOMPLETE")
    accounts, master = [], []
    seen = set()
    for first in range(0, total, PAGE):
        users = get(prefix + "/users?first=" + str(first) + "&max=" + str(PAGE))
        if not isinstance(users, list) or len(users) != min(PAGE, total - first):
            raise Blocked("INVENTORY_INCOMPLETE")
        for user in users:
            if not isinstance(user, dict) or not isinstance(user.get("id"), str) or not isinstance(user.get("username"), str):
                raise Blocked("INVENTORY_INCOMPLETE")
            ident = user["id"]
            if not re.fullmatch(r"[A-Za-z0-9_-]{8,320}", ident) or ident in seen:
                raise Blocked("INVENTORY_INCOMPLETE")
            seen.add(ident)
            name = user["username"]
            if name.casefold() == "admin":
                if name != "admin":
                    raise Blocked("MASTER_ALIAS")
                accounts.append(user)
            encoded = urllib.parse.quote(ident, safe="")
            roles = get(prefix + "/users/" + encoded + "/role-mappings/realm/composite")
            if not isinstance(roles, list) or any(
                not isinstance(r, dict) or not isinstance(r.get("name"), str) for r in roles
            ):
                raise Blocked("ROLE_INVENTORY_INCOMPLETE")
            if any(r["name"] == ROLE for r in roles):
                master.append(user)
            mapping = get(prefix + "/users/" + encoded + "/role-mappings")
            if not isinstance(mapping, dict) or not isinstance(mapping.get("clientMappings") or {}, dict):
                raise Blocked("ROLE_INVENTORY_INCOMPLETE")
            for client in (mapping.get("clientMappings") or {}).values():
                if (not isinstance(client, dict) or not isinstance(client.get("mappings"), list) or
                    any(not isinstance(r, dict) or not isinstance(r.get("name"), str) for r in client["mappings"])):
                    raise Blocked("ROLE_INVENTORY_INCOMPLETE")
                if any(r["name"] == ROLE for r in client["mappings"]):
                    raise Blocked("CLIENT_ROLE_ALIAS")
    if (len(seen) != total or get(prefix + "/users/count") != total or
            get(prefix + "/users?first=" + str(total) + "&max=1") != []):
        raise Blocked("INVENTORY_INCOMPLETE")
    if len(accounts) > 1 or len(master) > 1:
        raise Blocked("MASTER_AMBIGUOUS")
    if master and (not accounts or master[0]["id"] != accounts[0]["id"]):
        # Allow this one exact fixture ONLY inside the explicit role retirement path.
        legacy = (allow_exact_legacy and len(master) == 1 and not accounts and
                  master[0]["id"] == LEGACY_QA_SUBJECT and
                  master[0]["username"] == "qa.admin")
        if not legacy:
            raise Blocked("FOREIGN_MASTER_ROLE")
    return accounts[0] if accounts else None, master[0] if master else None


def retire_exact_legacy_master_role(api, realm, issuer):
    """Narrow, backed-up QA-only deprivileging, never delete synthetic user."""
    prefix = "/admin/realms/" + realm
    account, master = inventory(api.get, realm, issuer, allow_exact_legacy=True)
    if account is not None or master is None or master["id"] != LEGACY_QA_SUBJECT:
        raise Blocked("LEGACY_IDENTITY_NOT_EXACT")
    enc = urllib.parse.quote(LEGACY_QA_SUBJECT, safe="")
    user = api.get(prefix + "/users/" + enc)
    if (not isinstance(user, dict) or user.get("id") != LEGACY_QA_SUBJECT or
            user.get("username") != "qa.admin" or user.get("enabled") is not True or
            user.get("email") != "qa.admin@pmgm.invalid" or
            not isinstance(user.get("attributes"), dict) or
            user["attributes"].get("pmgm_scope") != ["order"]):
        raise Blocked("LEGACY_FIXTURE_MISMATCH")
    role = api.get(prefix + "/roles/" + ROLE)
    direct = api.get(prefix + "/users/" + enc + "/role-mappings/realm")
    if (not isinstance(role, dict) or role.get("name") != ROLE or
            not isinstance(role.get("id"), str) or
            not isinstance(direct, list) or
            len([r for r in direct if isinstance(r, dict) and r.get("name") == ROLE]) != 1 or
            not any(isinstance(r, dict) and r.get("name") == ROLE and
                    r.get("id") == role["id"] for r in direct)):
        raise Blocked("LEGACY_DIRECT_ROLE_MISMATCH")
    # No wildcard: delete only this one realm Master assignment, not the user
    # and not any other existing role. Keycloak admin events provide remote audit.
    api.delete(prefix + "/users/" + enc + "/role-mappings/realm", [
        {"id": role["id"], "name": ROLE},
    ])
    account, master = inventory(api.get, realm, issuer)
    if account or master:
        raise Blocked("LEGACY_ROLE_RETIREMENT_UNVERIFIED")
    return "LEGACY_QA_MASTER_ROLE_RETIRED_ACCOUNT_PRESERVED"


def require_master_scope(api, realm, subject):
    """The API requires the Keycloak pmgm_scope mapper alongside Master role.

    Inventory returns abbreviated user representations; read the full user
    to verify immutable bootstrap marker and exact institutional scope.
    Fail closed before any activation or a purported successful prepare.
    """
    full = api.get("/admin/realms/" + realm + "/users/" +
                   urllib.parse.quote(subject, safe=""))
    attrs = full.get("attributes") if isinstance(full, dict) else None
    if (not isinstance(full, dict) or full.get("id") != subject or
            full.get("username") != "admin" or not isinstance(attrs, dict) or
            attrs.get("pmgm_scope") != ["order"] or
            attrs.get("pmgm_master_qa_bootstrap") != ["issue-604"]):
        raise Blocked("MASTER_SCOPE_NOT_PROVISIONED")
    return True


def prepare(api, realm, issuer, password, subject_path):
    prefix = "/admin/realms/" + realm
    account, master = inventory(api.get, realm, issuer)
    if account or master or Path(subject_path).exists():
        raise Blocked("MASTER_ALREADY_PRESENT_OR_PARTIAL")
    role = api.get(prefix + "/roles/" + ROLE)
    if (not isinstance(role, dict) or role.get("name") != ROLE or
            not isinstance(role.get("id"), str)):
        raise Blocked("MASTER_ROLE_UNAVAILABLE")
    if len(password) < 14 or len(password) > 256 or "\n" in password:
        raise Blocked("PASSWORD_POLICY_INVALID")
    # A previous global backup and exclusive host writer lock are required by CLI.
    api.post(prefix + "/users", {
        "username": "admin", "enabled": False, "emailVerified": False,
        "requiredActions": ["UPDATE_PASSWORD"],
        "attributes": {
            "pmgm_master_qa_bootstrap": ["issue-604"],
            "pmgm_scope": ["order"],
        },
    })
    account, master = inventory(api.get, realm, issuer)
    if not account or master or account.get("enabled") is not False:
        raise Blocked("MASTER_CREATION_AMBIGUOUS")
    ident = account["id"]
    require_master_scope(api, realm, ident)
    save_subject(subject_path, ident)
    enc = urllib.parse.quote(ident, safe="")
    api.put(prefix + "/users/" + enc + "/reset-password", {
        "type": "password", "temporary": True, "value": password,
    })
    api.post(prefix + "/users/" + enc + "/role-mappings/realm", [
        {"id": role["id"], "name": ROLE},
    ])
    account, master = inventory(api.get, realm, issuer)
    if (not account or not master or account["id"] != ident or
            master["id"] != ident or account.get("enabled") is not False):
        raise Blocked("MASTER_PREPARATION_INCOMPLETE")
    require_master_scope(api, realm, ident)
    return "PREPARED_DISABLED_FOR_QA_CONFIG"


def validate_config(text, ident, issuer):
    values = {}
    for line in text.splitlines():
        line = line.strip()
        if not line or line.startswith("#"):
            continue
        if "=" in line:
            k, v = line.split("=", 1)
            if k in ("InitialLoad__MasterSubject", "InitialLoad__MasterIssuer", "Authentication__Authority"):
                if k in values:
                    raise Blocked("RUNTIME_CONFIG_AMBIGUOUS")
                values[k] = v.strip().strip('"')
    # Docker QA uses internal Keycloak metadata transport. OIDC issuer
    # remains canonical HTTPS; no forced rewrite of a healthy authority.
    internal_authority = "http://keycloak:8080/identity/realms/" + issuer.rsplit("/", 1)[-1]
    if (values.get("InitialLoad__MasterSubject") != ident or
            values.get("InitialLoad__MasterIssuer") != issuer or
            values.get("Authentication__Authority") not in (issuer, internal_authority) or
            len(values) != 3):
        raise Blocked("RUNTIME_MASTER_CONFIG_MISMATCH")


def activate(api, realm, issuer, subject, config):
    account, master = inventory(api.get, realm, issuer)
    if (not account or not master or account["id"] != subject or
            master["id"] != subject or account.get("enabled") is not False):
        raise Blocked("MASTER_NOT_PREPARED")
    require_master_scope(api, realm, subject)
    validate_config(config, subject, issuer)
    api.put("/admin/realms/" + realm + "/users/" + urllib.parse.quote(subject, safe=""),
            {"enabled": True})
    account, master = inventory(api.get, realm, issuer)
    if (not account or not master or account["id"] != subject or
            master["id"] != subject or account.get("enabled") is not True):
        raise Blocked("MASTER_ACTIVATION_UNVERIFIED")
    return "ENABLED_FOR_AUTHENTICATED_UAT_NOT_CERTIFIED"


def status(api, realm, issuer, subject, config):
    account, master = inventory(api.get, realm, issuer)
    if not account or not master or account["id"] != subject or master["id"] != subject:
        raise Blocked("MASTER_NOT_UNIQUE")
    require_master_scope(api, realm, subject)
    validate_config(config, subject, issuer)
    if account.get("enabled") is not True:
        raise Blocked("MASTER_DISABLED")
    return "REALM_SNAPSHOT_OK_UAT_REQUIRED"


def lock_file(path="/run/lock/pmgm-master-qa-604.lock"):
    p = Path(path)
    if p.is_symlink():
        raise Blocked("LOCK_UNSAFE")
    fd = os.open(p, os.O_RDWR | os.O_CREAT | os.O_NOFOLLOW, 0o600)
    if os.fstat(fd).st_uid != 0 or stat.S_IMODE(os.fstat(fd).st_mode) & 0o077:
        os.close(fd)
        raise Blocked("LOCK_UNSAFE")
    try:
        fcntl.flock(fd, fcntl.LOCK_EX | fcntl.LOCK_NB)
    except BlockingIOError:
        os.close(fd)
        raise Blocked("BOOTSTRAP_CONCURRENT")
    return fd


def main(argv=None):
    parser = argparse.ArgumentParser(description="Máster QA único: staged, no secret output")
    parser.add_argument("--mode", choices=("plan", "retire-legacy", "prepare", "activate", "status"), default="plan")
    parser.add_argument("--base-url", required=True)
    parser.add_argument("--realm", required=True)
    parser.add_argument("--issuer", required=True)
    parser.add_argument("--token-file", required=True)
    parser.add_argument("--subject-file", required=True)
    parser.add_argument("--password-file")
    parser.add_argument("--backup-receipt-file")
    parser.add_argument("--runtime-config-file")
    args = parser.parse_args(argv)
    try:
        if os.geteuid() != 0 or socket.gethostname() != "glmdch":
            raise Blocked("QA_HOST_NOT_AUTHORIZED")
        base = validate_endpoint(args.base_url, args.realm, args.issuer)
        token = private_file(args.token_file)
        fd = lock_file()
        try:
            api = Keycloak(base, args.realm, token)
            if args.mode == "plan":
                account, master = inventory(api.get, args.realm, args.issuer)
                if master or account:
                    raise Blocked("MASTER_OR_LEGACY_IDENTITY_PRESENT")
                result = "EMPTY_REALM_MASTER_SLOT_READY_FOR_STAGED_PREPARATION"
            elif args.mode == "retire-legacy":
                if not args.backup_receipt_file or not re.fullmatch(
                        r"sha256:[a-f0-9]{64}", private_file(args.backup_receipt_file)):
                    raise Blocked("BACKUP_RECEIPT_INVALID")
                result = retire_exact_legacy_master_role(api, args.realm, args.issuer)
            elif args.mode == "prepare":
                if not args.password_file or not args.backup_receipt_file:
                    raise Blocked("PROVISIONING_INPUT_MISSING")
                receipt = private_file(args.backup_receipt_file)
                if not re.fullmatch(r"sha256:[a-f0-9]{64}", receipt):
                    raise Blocked("BACKUP_RECEIPT_INVALID")
                password = private_file(args.password_file)
                result = prepare(api, args.realm, args.issuer, password, args.subject_file)
            else:
                if not args.runtime_config_file:
                    raise Blocked("RUNTIME_CONFIG_MISSING")
                subject = private_file(args.subject_file)
                config = private_file(args.runtime_config_file)
                if args.mode == "activate":
                    if not args.backup_receipt_file or not re.fullmatch(
                            r"sha256:[a-f0-9]{64}", private_file(args.backup_receipt_file)):
                        raise Blocked("BACKUP_RECEIPT_INVALID")
                    result = activate(api, args.realm, args.issuer, subject, config)
                else:
                    result = status(api, args.realm, args.issuer, subject, config)
            print("PASS " + result)
            return 0
        finally:
            os.close(fd)
    except (Blocked, FileNotFoundError, FileExistsError, PermissionError, OSError,
            ValueError, UnicodeError, json.JSONDecodeError) as exc:
        code = str(exc) if isinstance(exc, Blocked) else "HOST_OR_REMOTE_UNAVAILABLE"
        print("BLOCKED " + code)
        return 2


if __name__ == "__main__":
    sys.exit(main())
