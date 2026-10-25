#!/usr/bin/env python3
"""Publish only a fully gated, signed synthetic QA envelope from push-to-dev."""
import importlib.util
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import time

REPO = "pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio"
ROOT = Path(__file__).resolve().parent


def module(name, filename):
    spec = importlib.util.spec_from_file_location(name, ROOT / filename)
    result = importlib.util.module_from_spec(spec)
    sys.modules[name] = result
    spec.loader.exec_module(result)
    return result


def allowed_event(event_name, payload):
    return (event_name == "push" and payload.get("repository", {}).get("full_name") == REPO
            and payload.get("ref") == "refs/heads/dev" and payload.get("deleted") is not True)


def api(path):
    data = subprocess.run(["gh", "api", "repos/" + REPO + path], check=True,
                          stdout=subprocess.PIPE, stderr=subprocess.DEVNULL, timeout=60)
    return json.loads(data.stdout)


def publish_selected(selection, readiness):
    sha = selection["source_sha"]
    fetcher = module("publisher_download", "qa-artifact-download.py")
    staging = module("publisher_staging", "qa-signed-staging.py")
    with tempfile.TemporaryDirectory(prefix="pmgm-qa-publication-") as tmp:
        tmp = Path(tmp)
        name = "qa-signed-" + sha
        archive = tmp / (name + ".zip")
        fetcher.download(selection["artifact_id"], selection["artifact_sha256"], os.environ["GH_TOKEN"], archive)
        staging.stage(archive, selection["artifact_sha256"], sha, tmp / "verified")
        def pinned(path):
            return {"commit": {"sha": sha}} if path == "/branches/dev" else api(path)
        if readiness.evaluate(pinned) != selection:
            raise SystemExit("CANDIDATE_GATES_CHANGED_BEFORE_PUBLICATION")
        tip = api("/branches/dev").get("commit", {}).get("sha")
        if tip != sha:
            relation = api("/compare/" + sha + "..." + tip)
            if relation.get("status") != "ahead" or relation.get("merge_base_commit", {}).get("sha") != sha:
                raise SystemExit("PUBLISHED_CANDIDATE_NOT_ON_DEV")
        existing = subprocess.run(["gh", "release", "view", name, "--repo", REPO],
                                  stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=30)
        if existing.returncode == 0:
            release = api("/releases/tags/" + name)
            matches = [asset for asset in release.get("assets", []) if asset.get("name") == archive.name]
            if len(matches) != 1 or matches[0].get("digest") != selection["artifact_sha256"]:
                raise SystemExit("EXISTING_RELEASE_ASSET_MISMATCH_NO_OVERWRITE")
            print("QA_SIGNED_RELEASE_ALREADY_PUBLISHED")
            return 0
        subprocess.run(["gh", "release", "create", name, str(archive), "--repo", REPO,
                        "--target", sha, "--prerelease", "--title", "QA sintético " + sha[:12],
                        "--notes", "Paquete QA sintético firmado del SHA indicado. No es producción ni UAT."],
                       check=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=180)
        print("QA_SIGNED_PUBLIC_RELEASE_PUBLISHED " + sha)
    return 0


def main():
    event = json.loads(Path(os.environ["GITHUB_EVENT_PATH"]).read_text())
    if not allowed_event(os.environ.get("GITHUB_EVENT_NAME"), event):
        raise SystemExit("NOT_A_TRUSTED_DEV_PUSH")
    readiness = module("publisher_readiness", "qa-agent-readiness.py")
    published = set()
    # Publish the last green cut promptly, then also publish this push when its
    # remaining pipelines finish. Otherwise a quiet final push could be missed.
    for _ in range(50):
        if api("/branches/dev").get("commit", {}).get("sha") != event.get("after"):
            print("QA_PUBLICATION_IGNORES_SUPERSEDED_PUSH")
            return 0
        try:
            selection = readiness.evaluate_latest_validated(api)
        except readiness.GateFailure:
            time.sleep(30)
            continue
        sha = selection["source_sha"]
        if sha not in published:
            publish_selected(selection, readiness)
            published.add(sha)
        if sha == event.get("after"):
            return 0
        time.sleep(30)
    if published:
        # An older validated release is useful, but is not proof that this
        # push has been published. Do not report a false-green workflow.
        print("QA_VALIDATED_CUT_PUBLISHED_NEWER_PUSH_NOT_YET_GREEN")
        raise SystemExit("QA_TARGET_SHA_NOT_PUBLISHED")
    raise SystemExit("QA_PUBLICATION_GATES_NOT_READY")


if __name__ == "__main__":
    sys.exit(main())
