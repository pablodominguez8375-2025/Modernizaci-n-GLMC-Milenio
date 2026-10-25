#!/usr/bin/env python3
"""Fetch an immutable QA release envelope WITHOUT any GitHub credential."""
import hashlib
from pathlib import Path
import re
from urllib.error import HTTPError
from urllib.parse import urlsplit
from urllib.request import Request, build_opener, HTTPRedirectHandler

REPO = "pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio"
LIMIT = 1024 ** 3


class Blocked(Exception):
    pass


class NoRedirect(HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None


def identity(selection):
    sha, digest = selection.get("source_sha"), selection.get("artifact_sha256")
    if (selection.get("status") != "READY_FOR_SIGNED_STAGING_ONLY"
            or not isinstance(sha, str) or not re.fullmatch(r"[a-f0-9]{40}", sha)
            or not isinstance(digest, str) or not re.fullmatch(r"sha256:[a-f0-9]{64}", digest)):
        raise Blocked("INVALID_RELEASE_SELECTION")
    name = "qa-signed-" + sha
    return sha, digest, "https://github.com/" + REPO + "/releases/download/" + name + "/" + name + ".zip"


def download(selection, destination, opener=None):
    sha, digest, url = identity(selection)
    destination = Path(destination)
    if destination.exists() or destination.is_symlink():
        raise Blocked("RELEASE_DESTINATION_EXISTS")
    opener = opener or build_opener(NoRedirect())
    try:
        response = opener.open(Request(url, headers={"User-Agent": "pmgm-qa-public-v1"}), timeout=30)
    except HTTPError as error:
        if error.code != 302:
            raise Blocked("SIGNED_PUBLIC_RELEASE_NOT_READY") from None
        location = error.headers.get("Location", "")
        parsed = urlsplit(location)
        if (parsed.scheme != "https" or parsed.hostname != "release-assets.githubusercontent.com"
                or parsed.port not in (None, 443) or parsed.username or parsed.password or parsed.fragment):
            raise Blocked("RELEASE_REDIRECT_NOT_ALLOWED") from None
    else:
        response.close()
        raise Blocked("RELEASE_EXPECTED_REDIRECT")
    # No token exists in this transport, including the cross-origin request.
    checksum = hashlib.sha256()
    total = 0
    try:
        with opener.open(Request(location, headers={"User-Agent": "pmgm-qa-public-v1"}), timeout=60) as response:
            if response.status != 200:
                raise Blocked("RELEASE_CDN_NOT_OK")
            with destination.open("xb") as stream:
                destination.chmod(0o600)
                while chunk := response.read(1024 * 1024):
                    total += len(chunk)
                    if total > LIMIT:
                        raise Blocked("RELEASE_SIZE_LIMIT")
                    checksum.update(chunk)
                    stream.write(chunk)
        if not total or "sha256:" + checksum.hexdigest() != digest:
            raise Blocked("RELEASE_DIGEST_MISMATCH")
    except Exception:
        destination.unlink(missing_ok=True)
        raise
    return {"source_sha": sha, "sha256": digest}
