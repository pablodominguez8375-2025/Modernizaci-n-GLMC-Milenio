#!/usr/bin/env python3
"""Comment the signed QA cut (SHA + digest + ready command) on Issue #525.

Informational only: it never writes the technical review (that must be done by
a reviewer after reading the delta) and never starts or approves an update.
"""
import json
import os
import re
import subprocess
import sys

REPO = "pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio"
ISSUE = 525
MARKER = "<!-- qa-corte-candidato:{sha} -->"


def api(path, *extra):
    out = subprocess.run(["gh", "api", *extra, "repos/" + REPO + path], check=True,
                         stdout=subprocess.PIPE, stderr=subprocess.DEVNULL, timeout=60)
    return json.loads(out.stdout)


def candidate(sha, release):
    """Return the digest of the signed archive for exactly this SHA, or raise."""
    if not re.fullmatch(r"[a-f0-9]{40}", sha or ""):
        raise SystemExit("CANDIDATE_SHA_INVALID")
    name = "qa-signed-" + sha
    matches = [a for a in release.get("assets", []) if a.get("name") == name + ".zip"]
    digest = matches[0].get("digest") if len(matches) == 1 else None
    if release.get("tag_name") != name or not re.fullmatch(r"sha256:[a-f0-9]{64}", digest or ""):
        raise SystemExit("CANDIDATE_RELEASE_NOT_FOUND")
    return digest


def body(sha, digest):
    return "\n".join([
        MARKER.format(sha=sha),
        "## Corte candidato QA — `" + sha[:12] + "`",
        "",
        "Publicado automáticamente tras CI exact-head y release firmada. **No es una revisión ni una autorización**: "
        "la revisión técnica del delta y la autorización del Sponsor siguen siendo pasos aparte.",
        "",
        "- SHA: `" + sha + "`",
        "- Digest: `" + digest + "`",
        "- Release: https://github.com/" + REPO + "/releases/tag/qa-signed-" + sha,
        "",
        "Comando del operador (tras registrar la revisión de este SHA en un comentario):",
        "```text",
        "sudo pmgm-qa-update --review-and-run --source " + sha + " --digest " + digest + " \\",
        "  --technical-review-reference <URL-comentario-de-revision> \\",
        "  --g6-review /etc/pmgm/qa525-g6-reviewed.json --operator-name \"<operador>\"",
        "```",
    ])


def main():
    sha = os.environ.get("GITHUB_SHA", "")
    digest = candidate(sha, api("/releases/tags/qa-signed-" + sha))
    marker = MARKER.format(sha=sha)
    for page in (1, 2, 3):
        if any(marker in (c.get("body") or "") for c in api("/issues/%d/comments?per_page=100&page=%d" % (ISSUE, page))):
            print("QA_CANDIDATE_COMMENT_ALREADY_PRESENT")
            return 0
    subprocess.run(["gh", "api", "-X", "POST", "repos/%s/issues/%d/comments" % (REPO, ISSUE), "-f", "body=" + body(sha, digest)],
                   check=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=60)
    print("QA_CANDIDATE_COMMENT_POSTED " + sha)
    return 0


if __name__ == "__main__":
    sys.exit(main())
