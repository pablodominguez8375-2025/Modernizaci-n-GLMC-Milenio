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
        "> **NO EJECUTAR todavía en glmdch.** Mientras el lanzador firmado (#822/#834) y su instalación supervisada "
        "estén pendientes, el comando de abajo es solo una plantilla de referencia, no una instrucción lista para usar.",
        "> \"Release publicada\" significa solo que el archivo existe en GitHub; **no** significa que su firma Sigstore "
        "haya sido verificada en el host (eso lo hace el operador al ejecutar).",
        "",
        "- SHA: `" + sha + "`",
        "- Digest: `" + digest + "`",
        "- Release: https://github.com/" + REPO + "/releases/tag/qa-signed-" + sha,
        "",
        "Plantilla de referencia del comando del operador (solo cuando el lanzador firmado esté instalado y exista la revisión de este SHA):",
        "```text",
        "sudo pmgm-qa-update --review-and-run --source " + sha + " --digest " + digest + " \\",
        "  --technical-review-reference <URL-comentario-de-revision> \\",
        "  --g6-review /etc/pmgm/qa525-g6-reviewed.json --operator-name \"<operador>\"",
        "```",
    ])


MAX_PAGES = 200  # hard stop (20 000 comments); reaching it fails closed instead of risking a duplicate


def marker_present(fetch_page, marker):
    """Scan ALL comment pages (no fixed page cap); fail closed if the safety bound is hit."""
    for page in range(1, MAX_PAGES + 1):
        comments = fetch_page(page)
        if any(marker in (c.get("body") or "") for c in comments):
            return True
        if len(comments) < 100:
            return False
    raise SystemExit("CANDIDATE_COMMENT_SCAN_BOUND_REACHED")


def post_comment(text):
    subprocess.run(["gh", "api", "-X", "POST", "repos/%s/issues/%d/comments" % (REPO, ISSUE), "-f", "body=" + text],
                   check=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=60)


def run(sha, fetch_release, fetch_page, post):
    digest = candidate(sha, fetch_release(sha))
    if marker_present(fetch_page, MARKER.format(sha=sha)):
        print("QA_CANDIDATE_COMMENT_ALREADY_PRESENT")
        return 0
    post(body(sha, digest))
    print("QA_CANDIDATE_COMMENT_POSTED " + sha)
    return 0


def main():
    return run(os.environ.get("GITHUB_SHA", ""),
               lambda sha: api("/releases/tags/qa-signed-" + sha),
               lambda page: api("/issues/%d/comments?per_page=100&page=%d" % (ISSUE, page)),
               post_comment)


if __name__ == "__main__":
    sys.exit(main())
