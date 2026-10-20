#!/usr/bin/env python3
"""Real signed candidate verification with no host token or gh login.

An ephemeral CI token downloads the fixture first. It is NOT provided to the
signature verifier; stage(tokenless=True) also uses a fresh empty gh config.
"""
import importlib.util
import json
import os
from pathlib import Path
import sys
import tempfile
from urllib.request import Request, urlopen

ROOT = Path(__file__).resolve().parents[1] / "scripts/qa"


def load(name, filename):
    spec = importlib.util.spec_from_file_location(name, ROOT / filename)
    value = importlib.util.module_from_spec(spec)
    sys.modules[name] = value
    spec.loader.exec_module(value)
    return value


def main():
    readiness = load("signature_fixture_ready", "qa-agent-readiness.py")
    fetcher = load("signature_fixture_download", "qa-artifact-download.py")
    staging = load("signature_no_token_stage", "qa-signed-staging.py")
    def api(path):
        request = Request(readiness.API + path, headers={"Authorization": "Bearer " + os.environ["GH_TOKEN"],
                          "Accept": "application/vnd.github+json", "User-Agent": "pmgm-qa-signature-fixture"})
        with urlopen(request, timeout=30) as response:
            return json.load(response)
    selection = readiness.evaluate_latest_validated(api)
    with tempfile.TemporaryDirectory(prefix="qa-tokenless-signature-") as tmp:
        tmp = Path(tmp)
        archive = tmp / "signed.zip"
        fetcher.download(selection["artifact_id"], selection["artifact_sha256"], os.environ["GH_TOKEN"], archive)
        staging.stage(archive, selection["artifact_sha256"], selection["source_sha"], tmp / "package", tokenless=True)
        print("PASS:REAL_SIGNATURE_AND_MANIFEST_WITHOUT_HOST_TOKEN_OR_GH_LOGIN")


if __name__ == "__main__":
    main()
