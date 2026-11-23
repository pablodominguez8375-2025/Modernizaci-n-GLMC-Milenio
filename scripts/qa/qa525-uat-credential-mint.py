#!/usr/bin/env python3
"""Mint the QA525 UAT credential file on the host, interactively, as root.

Obtains two distinct Keycloak access tokens (initial-load Master and platform
operator) with the password grant of the public client pmgm-web and writes
/root/.qa525-uat-credential (root:root 0600) atomically, in exactly the shape
read by qa525-vnext-root-schema-uat.py: {"master_token", "operator_token"}.

Security properties:
- passwords are read only from the controlling terminal without echo; they are
  never accepted as arguments, environment or stdin, never written, logged or
  printed;
- tokens are never printed; output is fixed status lines and minutes of validity;
- TLS is always verified; HTTP error bodies are discarded;
- the target is created with O_EXCL|O_NOFOLLOW in a root-only directory and
  replaced by atomic rename only with --replace;
- both tokens are checked for issuer, expiry (> MIN_REMAINING seconds), claims
  and distinctness BEFORE anything is written; on any failure nothing is written.

Modes: default (mint), --check-only (same logins and checks, plus GET
/api/session/me with each token; writes nothing, needs no root, 60 s minimum
validity so it works with the current 300 s lifespan) and --remove (deletes the
credential file after the cut).

Run manually by the Sponsor in the host terminal. It is not part of the signed
cut and performs no GO, deployment or Keycloak change.
"""
import argparse
import base64
import datetime as dt
import getpass
import http.client
import json
import os
import re
import ssl
import stat
import sys
import urllib.parse

HOST = "sistema.granlogiamixta.cl"
TOKEN_PATH = "/identity/realms/pmgm/protocol/openid-connect/token"
ISSUER = "https://" + HOST + "/identity/realms/pmgm"
CLIENT_ID = "pmgm-web"
TARGET = "/root/.qa525-uat-credential"
MIN_REMAINING = 2400  # script rejects below 40 min; the UAT itself requires > 1800 s
CHECK_MIN_REMAINING = 60  # --check-only: only proves claims and API authorization
SESSION_PATH = "/api/session/me"
MAX_FILE = 4096
MAX_TOKEN = 2048
JWT = re.compile(r"[A-Za-z0-9_-]+[.][A-Za-z0-9_-]+[.][A-Za-z0-9_-]+\Z")
USERNAME = re.compile(r"[A-Za-z0-9._@+-]{1,128}\Z")


class Stop(Exception):
    """Fixed code only; never carries secrets."""


def claims_of(token):
    try:
        if type(token) is not str or len(token) > MAX_TOKEN or JWT.fullmatch(token) is None:
            raise ValueError()
        payload = token.split(".")[1]
        claims = json.loads(base64.urlsafe_b64decode(payload + "=" * (-len(payload) % 4)))
        if type(claims) is not dict:
            raise ValueError()
        return claims
    except Exception:
        raise Stop("UATMINT_TOKEN_MALFORMED") from None


def _as_list(value):
    return value if type(value) is list else [value]


def check_token(token, role, now, min_remaining=MIN_REMAINING):
    """role: 'master' or 'operator'. Mirrors the UAT reader plus the API's role claims."""
    claims = claims_of(token)
    if claims.get("iss") != ISSUER:
        raise Stop("UATMINT_ISSUER_INVALID")
    exp = claims.get("exp")
    if type(exp) is not int or exp <= now.timestamp() + min_remaining:
        raise Stop("UATMINT_LIFESPAN_TOO_SHORT")
    aud = claims.get("aud")
    if "pmgm-api" not in (aud if type(aud) is list else [aud]):
        raise Stop("UATMINT_AUDIENCE_INVALID")
    if claims.get("azp") != CLIENT_ID:
        raise Stop("UATMINT_CLIENT_INVALID")
    nbf = claims.get("nbf")
    if nbf is not None and (type(nbf) is not int or nbf > now.timestamp() + 60):
        raise Stop("UATMINT_NOT_YET_VALID")
    if claims.get("pmgm_scope") != "order":
        raise Stop("UATMINT_CLAIM_SCOPE_MISSING")
    roles = [str(x).lower() for x in _as_list(claims.get("pmgm_role", []))]
    master = "platform_superadmin" in roles
    if role == "master":
        if not master:
            raise Stop("UATMINT_NOT_MASTER")
    else:
        kind = str(claims.get("pmgm_account_kind", "")).lower()
        if master or kind != "superuser":
            raise Stop("UATMINT_NOT_OPERATOR")
    return int((exp - now.timestamp()) // 60)


def request_token(username, password, post):
    form = urllib.parse.urlencode({"grant_type": "password", "client_id": CLIENT_ID,
                                   "username": username, "password": password,
                                   "scope": "openid"})
    status, body = post(form.encode("ascii"))
    try:
        if status != 200:
            raise Stop("UATMINT_AUTH_REJECTED")
        data = json.loads(body)
        token = data["access_token"]
    except Stop:
        raise
    except Exception:
        raise Stop("UATMINT_AUTH_RESPONSE_INVALID") from None
    return token


def http_post(body):
    conn = http.client.HTTPSConnection(HOST, 443, timeout=15, context=ssl.create_default_context())
    try:
        conn.request("POST", TOKEN_PATH, body=body, headers={
            "Content-Type": "application/x-www-form-urlencoded", "Accept": "application/json",
            "Cache-Control": "no-store"})
        resp = conn.getresponse()
        return resp.status, resp.read(65536)
    except Exception:
        raise Stop("UATMINT_NETWORK_FAILED") from None
    finally:
        conn.close()


def http_get(path, token):
    conn = http.client.HTTPSConnection(HOST, 443, timeout=15, context=ssl.create_default_context())
    try:
        conn.request("GET", path, headers={"Authorization": "Bearer " + token,
                     "Accept": "application/json", "Cache-Control": "no-store"})
        resp = conn.getresponse()
        resp.read(65536)
        return resp.status
    except Exception:
        raise Stop("UATMINT_NETWORK_FAILED") from None
    finally:
        conn.close()


def _require_safe_directory(directory, uid):
    """The parent must be a real directory owned by root and not writable by group/other."""
    try:
        st = os.lstat(directory)
    except OSError:
        raise Stop("UATMINT_DIRECTORY_UNSAFE") from None
    if not stat.S_ISDIR(st.st_mode) or st.st_uid != uid or (st.st_mode & 0o022):
        raise Stop("UATMINT_DIRECTORY_UNSAFE")


def _require_plain_file(target, uid):
    """--replace and --remove act only on a regular, non-linked, root-owned 0600 file in a safe directory."""
    _require_safe_directory(os.path.dirname(target), uid)
    try:
        st = os.lstat(target)
    except OSError:
        raise Stop("UATMINT_TARGET_UNSAFE") from None
    if (not stat.S_ISREG(st.st_mode) or st.st_nlink != 1 or st.st_uid != uid
            or (st.st_mode & 0o777) != 0o600):
        raise Stop("UATMINT_TARGET_UNSAFE")


def remove_credential(target=TARGET, uid=0, euid=None, out=print):
    if (os.geteuid() if euid is None else euid) != uid:
        raise Stop("UATMINT_ROOT_REQUIRED")
    if not os.path.lexists(target):
        out("OK no existe %s; nada que borrar." % target)
        return
    _require_plain_file(target, uid)
    try:
        os.unlink(target)
    except OSError:
        raise Stop("UATMINT_REMOVE_FAILED") from None
    out("OK credencial borrada: %s" % target)
    out("AVISO: borrar el archivo NO revoca los tokens ya emitidos; siguen validos hasta su expiracion. "
        "Revertir el lifespan no los invalida; la invalidacion de sesiones requiere autorizacion separada.")


def write_atomic(data, target, replace, uid=0):
    """Create target root-only; never follows links; atomic rename; never leaves a temp."""
    directory = os.path.dirname(target)
    _require_safe_directory(directory, uid)
    if os.path.lexists(target):
        if not replace:
            raise Stop("UATMINT_TARGET_EXISTS")
        _require_plain_file(target, uid)
    tmp = os.path.join(directory, ".qa525-uat-credential.tmp-" + str(os.getpid()))
    fd = None
    try:
        fd = os.open(tmp, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, 0o600)
        st = os.fstat(fd)
        if st.st_uid != uid or (st.st_mode & 0o777) != 0o600:
            raise Stop("UATMINT_FILE_MODE_INVALID")
        view = memoryview(data)
        while view:
            view = view[os.write(fd, view):]
        os.fsync(fd)
        os.close(fd)
        fd = None
        if replace:
            os.replace(tmp, target)
        else:
            os.link(tmp, target)  # fails if target appeared meanwhile: no overwrite
            try:
                os.unlink(tmp)
            except OSError:
                os.unlink(target)  # never leave a second name for the credential
                raise
        tmp = None
        dfd = os.open(directory, os.O_RDONLY | os.O_DIRECTORY)
        try:
            os.fsync(dfd)
        finally:
            os.close(dfd)
    except Stop:
        raise
    except FileExistsError:
        raise Stop("UATMINT_TARGET_EXISTS") from None
    except OSError:
        raise Stop("UATMINT_WRITE_FAILED") from None
    finally:
        if fd is not None:
            os.close(fd)
        if tmp is not None:
            try:
                os.unlink(tmp)
            except OSError:
                pass


def run(master_user, operator_user, *, ask, post, clock, target=TARGET, replace=False,
        uid=0, euid=None, out=print, check_only=False, get=None):
    if not check_only and (os.geteuid() if euid is None else euid) != uid:
        raise Stop("UATMINT_ROOT_REQUIRED")
    if get is None or (check_only and replace):
        raise Stop("UATMINT_CHECK_ONLY_INVALID")
    for name in (master_user, operator_user):
        if type(name) is not str or USERNAME.fullmatch(name) is None:
            raise Stop("UATMINT_USERNAME_INVALID")
    if master_user == operator_user:
        out("AVISO: Master y operador son la misma cuenta; el UAT lo rechazara si no es Superusuario.")
    if not check_only and os.path.lexists(target) and not replace:
        raise Stop("UATMINT_TARGET_EXISTS")
    minimum = CHECK_MIN_REMAINING if check_only else MIN_REMAINING
    tokens = {}
    for key, user, role in (("master_token", master_user, "master"),
                            ("operator_token", operator_user, "operator")):
        password = ask("Clave de %s (no se muestra): " % user)
        if type(password) is not str or not password:
            raise Stop("UATMINT_PASSWORD_EMPTY")
        token = request_token(user, password, post)
        password = None
        minutes = check_token(token, role, clock(), minimum)
        out("OK %s: claims correctas, vigencia %d min" % (role, minutes))
        # The API verifies signature and authorization; the credential is never
        # written from claims alone (the script only decodes the payload).
        status = get(SESSION_PATH, token)
        if status != 200:
            raise Stop("UATMINT_API_REJECTED_%d" % status if type(status) is int and 100 <= status <= 599
                       else "UATMINT_API_REJECTED")
        out("OK %s: la API acepta el token (GET %s = 200)" % (role, SESSION_PATH))
        tokens[key] = token
    if tokens["master_token"] == tokens["operator_token"]:
        raise Stop("UATMINT_TOKENS_EQUAL")
    if check_only:
        out("OK verificacion completa. No se escribio ningun archivo ni se cambio nada.")
        return
    data = json.dumps(tokens, sort_keys=True, separators=(",", ":")).encode("ascii")
    if len(data) > MAX_FILE:
        raise Stop("UATMINT_FILE_TOO_LARGE")
    write_atomic(data, target, replace, uid)
    out("OK credencial escrita en %s (root 0600). No se imprimio ningun token." % target)


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--master-user")
    parser.add_argument("--operator-user")
    parser.add_argument("--replace", action="store_true")
    parser.add_argument("--check-only", action="store_true")
    parser.add_argument("--remove", action="store_true")
    args = parser.parse_args(argv)
    try:
        os.umask(0o077)
        if args.remove:
            if args.check_only or args.replace or args.master_user or args.operator_user:
                raise Stop("UATMINT_ARGUMENTS_INVALID")
            remove_credential()
            return 0
        if not args.master_user or not args.operator_user:
            raise Stop("UATMINT_ARGUMENTS_INVALID")
        if not sys.stdin.isatty() or not sys.stdout.isatty():
            raise Stop("UATMINT_TTY_REQUIRED")
        run(args.master_user, args.operator_user, ask=getpass.getpass, post=http_post,
            clock=lambda: dt.datetime.now(dt.timezone.utc), replace=args.replace,
            check_only=args.check_only, get=http_get)
    except Stop as e:
        print("ERROR " + str(e), file=sys.stderr)
        return 2
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
