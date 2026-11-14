#!/usr/bin/env python3
"""QA525 auto-web: CI-side envelope producer (contract v1.3). Runs ONLY in GitHub Actions.

Called by `.github/workflows/qa-release-envelope.yml` after it has built and saved the WEB
image. Reads the local git history, the built image config and the saved archive; writes
`envelope.json` and `sbom.json`. It never talks to glmdch, never publishes, never signs (the
workflow attests the bytes with Sigstore) and grants nothing.

Inputs that describe the host (`--installed-json`) are EXPECTED values; the host re-measures
them and rejects any drift (contract §3).
"""
from __future__ import annotations

import argparse
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import re
import subprocess
import sys

HERE = Path(__file__).resolve().parent
SHA = re.compile(r"[0-9a-f]{40}\Z")
MODES_REMOVED = "000000"


def _load(name, filename):
    spec = importlib.util.spec_from_file_location(name, HERE / filename)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


CLASSIFIER = _load("ci_autoweb_classifier", "qa525-vnext-release-classifier.py")
ENVELOPE = _load("ci_autoweb_envelope", "qa525-vnext-release-envelope.py")
CLASSIFIER_FILE = HERE / "qa525-vnext-release-classifier.py"


class Fail(Exception):
    pass


def _git(repo, *args, binary=False):
    out = subprocess.run(["git", "-C", str(repo), *args], check=True, capture_output=True)
    return out.stdout if binary else out.stdout.decode()


def _file_sha(path):
    h = hashlib.sha256()
    with open(path, "rb") as fh:
        for chunk in iter(lambda: fh.read(1 << 20), b""):
            h.update(chunk)
    return "sha256:" + h.hexdigest()


def parse_raw_diff(raw: bytes):
    """Parse `git diff --raw --no-renames -z` output into (status, mode, blob, path) tuples."""
    parts = raw.split(b"\0")
    if parts and parts[-1] == b"":
        parts.pop()
    if len(parts) % 2:
        raise Fail("RAW_DIFF_MALFORMED")
    rows = []
    for meta, path in zip(parts[0::2], parts[1::2]):
        fields = meta.decode("ascii").lstrip(":").split(" ")
        if len(fields) != 5:
            raise Fail("RAW_DIFF_MALFORMED")
        old_mode, new_mode, _old_blob, new_blob, letter = fields
        status = {"A": "added", "M": "modified", "T": "modified", "D": "removed"}.get(letter[:1])
        if status is None:
            raise Fail("RAW_DIFF_STATUS_UNSUPPORTED")
        mode = old_mode if status == "removed" else new_mode
        rows.append((status, mode, new_blob, path.decode("utf-8")))
    return rows


def lineage(repo, installed_sha, candidate_sha):
    if SHA.fullmatch(installed_sha) is None or SHA.fullmatch(candidate_sha) is None:
        raise Fail("SHA_INVALID")
    merge_base = _git(repo, "merge-base", installed_sha, candidate_sha).strip()
    ahead = int(_git(repo, "rev-list", "--count", installed_sha + ".." + candidate_sha).strip())
    files = []
    for status, mode, blob, path in parse_raw_diff(
            _git(repo, "diff", "--raw", "--no-renames", "--no-abbrev", "-z", installed_sha, candidate_sha, binary=True)):
        if status == "removed":
            kind = "none"
        elif mode == "100644":
            kind = CLASSIFIER.sniff(_git(repo, "cat-file", "blob", blob, binary=True))
        else:
            kind = "unknown"
        files.append({"path": path, "status": status, "mode": mode, "content_kind": kind})
    files.sort(key=lambda e: e["path"])
    return {"merge_base": merge_base, "ahead_by": ahead, "total_changed_files": len(files),
            "changed_files_complete": True, "changed_files": files}


def sbom(lock_path):
    lock = json.loads(Path(lock_path).read_text(encoding="utf-8"))
    packages = sorted("%s@%s" % (name.split("node_modules/")[-1], meta.get("version", ""))
                      for name, meta in lock.get("packages", {}).items() if name)
    record = {"schema": "pmgm-qa-auto-web-sbom-min-v1",
              "npm_lock_sha256": _file_sha(lock_path), "packages": packages}
    return ENVELOPE.canonical(record)


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--repo", required=True)
    ap.add_argument("--installed-json", required=True, help="expected installed state (6 fields)")
    ap.add_argument("--candidate-sha", required=True)
    ap.add_argument("--sequence", type=int, required=True)
    ap.add_argument("--issued-at", required=True)
    ap.add_argument("--not-before", required=True)
    ap.add_argument("--expires-at", required=True)
    ap.add_argument("--image-inspect", required=True, help="`docker image inspect` JSON of the built image")
    ap.add_argument("--image-archive", required=True, help="`docker save` tar of the built image")
    ap.add_argument("--base-digests-json", required=True)
    ap.add_argument("--build-args-json", required=True)
    ap.add_argument("--content-layer-count", type=int, required=True)
    ap.add_argument("--out-dir", required=True)
    a = ap.parse_args(argv)

    env = os.environ
    repo_name, ref = env["GITHUB_REPOSITORY"], env["GITHUB_REF"]
    if ref != "refs/heads/dev" or env.get("GITHUB_SHA") != a.candidate_sha:
        raise Fail("CI_REF_OR_SHA_NOT_DEV_HEAD")
    inspect = json.loads(Path(a.image_inspect).read_text(encoding="utf-8"))
    if type(inspect) is not list or len(inspect) != 1:
        raise Fail("IMAGE_INSPECT_INVALID")
    image = inspect[0]
    binding = ENVELOPE.image_binding(image, a.content_layer_count)
    out = Path(a.out_dir)
    out.mkdir(parents=True, exist_ok=True)
    sbom_bytes = sbom(Path(a.repo) / "frontend/package-lock.json")
    (out / "sbom.json").write_bytes(sbom_bytes)
    candidate = {"sha": a.candidate_sha, "web_image_digest": image["Id"],
                 "web_image_archive_sha256": _file_sha(a.image_archive),
                 "web_sbom_sha256": "sha256:" + hashlib.sha256(sbom_bytes).hexdigest(),
                 "base_image_digests": json.loads(Path(a.base_digests_json).read_text(encoding="utf-8")),
                 "build_args_sha256": ENVELOPE.build_args_sha256(
                     json.loads(Path(a.build_args_json).read_text(encoding="utf-8"))),
                 **binding}
    raw = ENVELOPE.build(
        repository=repo_name, source_repository_id=int(env["GITHUB_REPOSITORY_ID"]),
        sequence=a.sequence, installed=json.loads(a.installed_json), candidate=candidate,
        lineage=lineage(a.repo, json.loads(a.installed_json)["sha"], a.candidate_sha),
        classifier_module=CLASSIFIER, classifier_sha256=_file_sha(CLASSIFIER_FILE),
        ci={"workflow": ".github/workflows/qa-release-envelope.yml", "workflow_ref": ref,
            "run_id": int(env["GITHUB_RUN_ID"]), "run_attempt": int(env["GITHUB_RUN_ATTEMPT"]),
            "issuer": ENVELOPE.ISSUER, "subject": "repo:%s:ref:%s" % (repo_name, ref),
            "source_sha": a.candidate_sha},
        issued_at=a.issued_at, not_before=a.not_before, expires_at=a.expires_at)
    (out / "envelope.json").write_bytes(raw)
    verdict = json.loads(raw)["classifier"]
    print(json.dumps({"status": "ENVELOPE_PRODUCED_NOT_PUBLISHED", "result": verdict["result"],
                      "reasons": verdict["reasons"], "release_id": json.loads(raw)["release_id"]}))
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (Fail, ENVELOPE.Stop) as exc:
        print(json.dumps({"status": "ENVELOPE_NOT_PRODUCED", "code": str(exc)}))
        sys.exit(1)
