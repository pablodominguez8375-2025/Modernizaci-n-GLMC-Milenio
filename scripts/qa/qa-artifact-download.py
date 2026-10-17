#!/usr/bin/env python3
"""Download one GitHub Actions QA artifact using an Actions:read-only token.

Never installs or executes files; caller must separately verify outer digest,
installable provenance, complete manifest and Silo evidence before staging.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import stat
import tempfile
from urllib.error import HTTPError, URLError
from urllib.parse import urlsplit
from urllib.request import Request, build_opener, HTTPRedirectHandler

REPO = "pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio"
SHA = re.compile(r"^[0-9a-f]{40}$")
DIGEST = re.compile(r"^sha256:[0-9a-f]{64}$")
MAX_ARTIFACT_BYTES = 1024 * 1024 * 1024


class Blocked(Exception):
    pass


class NoRedirect(HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None


def read_token(path):
    path = Path(path)
    st = path.lstat()
    if (not stat.S_ISREG(st.st_mode) or
            st.st_uid != os.geteuid() or
            stat.S_IMODE(st.st_mode) & 0o077):
        raise Blocked("TOKEN_FILE_OWNER_OR_PERMISSIONS")
    fd = os.open(path, os.O_RDONLY | os.O_NOFOLLOW)
    with os.fdopen(fd, "rb") as stream:
        raw = stream.read(512)
    token = raw.strip()
    if not token or len(token) > 256 or any(byte <= 32 or byte >= 127 for byte in token):
        raise Blocked("TOKEN_FILE_INVALID")
    return token.decode("ascii")


def read_selection(path):
    selection = json.loads(Path(path).read_text(encoding="utf-8"))
    sha = selection.get("source_sha")
    ident = selection.get("artifact_id")
    digest = selection.get("artifact_sha256")
    if (selection.get("status") != "READY_FOR_SIGNED_STAGING_ONLY" or
            selection.get("scope") != "NO_HOST_ACCESS_NO_DEPLOYMENT_NO_UAT" or
            not isinstance(sha, str) or SHA.fullmatch(sha) is None or
            not isinstance(ident, int) or isinstance(ident, bool) or ident <= 0 or
            not isinstance(digest, str) or DIGEST.fullmatch(digest) is None):
        raise Blocked("INVALID_SIGNED_ARTIFACT_SELECTION")
    return ident, digest, sha


def allowed_redirect(url):
    parsed = urlsplit(url)
    host = (parsed.hostname or "").lower()
    if (parsed.scheme != "https" or parsed.username or parsed.password or
            parsed.port not in (None, 443) or parsed.fragment):
        return False
    return (host.endswith(".actions.githubusercontent.com") or
            host.endswith(".blob.core.windows.net"))


def download(ident, expected_digest, token, destination, opener=None):
    if not isinstance(ident, int) or isinstance(ident, bool) or ident <= 0:
        raise Blocked("INVALID_ARTIFACT_ID")
    if not isinstance(expected_digest, str) or not DIGEST.fullmatch(expected_digest):
        raise Blocked("INVALID_EXPECTED_DIGEST")
    destination = Path(destination)
    if destination.exists() or destination.is_symlink() or not destination.parent.is_dir():
        raise Blocked("DESTINATION_NOT_NEW")
    os.umask(0o077)
    opener = opener or build_opener(NoRedirect())
    api = "https://api.github.com/repos/" + REPO + "/actions/artifacts/" + str(ident) + "/zip"
    request = Request(api, headers={
        "Authorization": "Bearer " + token,
        "Accept": "application/vnd.github+json",
        "X-GitHub-Api-Version": "2022-11-28",
        "User-Agent": "centenario-qa-download-v1",
    })
    # Suppress automatic cross-origin redirects to guarantee that no token
    # is forwarded to the cloud artifact CDN.
    try:
        response = opener.open(request, timeout=25)
    except HTTPError as e:
        if e.code != 302:
            raise Blocked("GITHUB_ARTIFACT_API_FAILED") from None
        url = e.headers.get("Location", "")
        if not allowed_redirect(url):
            raise Blocked("UNTRUSTED_ARTIFACT_REDIRECT")
    else:
        response.close()
        raise Blocked("EXPECTED_GITHUB_ARTIFACT_REDIRECT")

    # No Authorization header or token is sent to the CDN.
    cdn_request = Request(url, headers={"User-Agent": "centenario-qa-download-v1"})
    try:
        cdn_response = opener.open(cdn_request, timeout=45)
    except HTTPError:
        raise Blocked("ARTIFACT_CDN_FAILED") from None
    temp = None
    try:
        if getattr(cdn_response, "status", 200) != 200:
            raise Blocked("ARTIFACT_CDN_NOT_OK")
        digest = hashlib.sha256()
        size = 0
        with tempfile.NamedTemporaryFile(mode="wb", dir=destination.parent,
                                         prefix=".pmgm-qa-download-", delete=False) as stream:
            temp = Path(stream.name)
            os.chmod(temp, 0o600)
            while True:
                data = cdn_response.read(1024 * 1024)
                if not data:
                    break
                size += len(data)
                if size > MAX_ARTIFACT_BYTES:
                    raise Blocked("ARTIFACT_SIZE_LIMIT")
                digest.update(data)
                stream.write(data)
            stream.flush()
            os.fsync(stream.fileno())
        if not size or "sha256:" + digest.hexdigest() != expected_digest:
            raise Blocked("ARTIFACT_DIGEST_MISMATCH")
        if destination.exists() or destination.is_symlink():
            raise Blocked("DESTINATION_RACE")
        os.replace(temp, destination)
        temp = None
    finally:
        cdn_response.close()
        if temp is not None:
            temp.unlink(missing_ok=True)
    return {"status": "DOWNLOADED_UNVERIFIED_NOT_INSTALLED",
            "artifact_id": ident,
            "sha256": expected_digest,
            "scope": "NO_EXECUTION_NO_HOST_DEPLOYMENT"}


def main():
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--selection", required=True, type=Path)
    ap.add_argument("--token-file", required=True, type=Path)
    ap.add_argument("--output", required=True, type=Path)
    args = ap.parse_args()
    try:
        ident, digest, sha = read_selection(args.selection)
        token = read_token(args.token_file)
        result = download(ident, digest, token, args.output)
        result["source_sha"] = sha
        print(json.dumps(result, sort_keys=True))
        return 0
    except (Blocked, OSError, ValueError, json.JSONDecodeError, URLError) as e:
        code = str(e) if isinstance(e, Blocked) else "ARTIFACT_DOWNLOAD_UNAVAILABLE"
        print(json.dumps({"status": "BLOCKED_QA_ARTIFACT_DOWNLOAD",
                          "scope": "NO_HOST_DEPLOYMENT",
                          "code": code}, sort_keys=True))
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
