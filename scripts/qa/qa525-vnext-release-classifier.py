#!/usr/bin/env python3
"""QA525 vNext release classifier. PURE and INACTIVE: no I/O, no host, no network.

Decides only whether an installed->candidate file interval may be applied by the future
`auto-web` path (WEB container only) or must stay MANUAL. It grants nothing: a Sponsor
policy and an attested envelope are still required by the host. Unknown = manual.
"""
from __future__ import annotations

import re

VERSION = "qa525-release-classifier-v1"
MAX_FILES = 300
AUTO = "auto-web"
MANUAL = "manual"
STATUSES = frozenset({"added", "modified", "removed", "renamed"})
ALLOWED_ROOTS = ("frontend/src/", "frontend/public/")
ALLOWED_EXT = frozenset({".ts", ".tsx", ".css", ".svg", ".png", ".jpg", ".jpeg", ".webp", ".ico"})
DENY_DIRS = frozenset({"api", "auth", "oidc", "keycloak", "accounts", "config", "runtime", "env"})
DENY_TOKENS = frozenset({"auth", "oidc", "keycloak", "token", "secret", "password",
                         "credential", "session", "env", "runtime", "sw", "vite", "manifest"})
PATH_OK = re.compile(r"[A-Za-z0-9._/@+-]{1,200}\Z")


class Stop(Exception):
    """Static code; never echoes external content."""


def _tokens(stem):
    spaced = re.sub(r"([a-z0-9])([A-Z])", r"\1 \2", stem)
    return {t for t in re.split(r"[^A-Za-z0-9]+", spaced.lower()) if t}


def _path_reason(path):
    if type(path) is not str or PATH_OK.fullmatch(path) is None or ".." in path.split("/") \
            or path.startswith("/") or "//" in path or "/./" in path or path.startswith("./"):
        return "PATH_INVALID"
    if not path.startswith(ALLOWED_ROOTS):
        return "OUTSIDE_AUTO_WEB_ROOTS"
    parts = path.split("/")
    name = parts[-1]
    if any(p.lower() in DENY_DIRS for p in parts[2:-1]):
        return "DENIED_DIRECTORY"
    stem, dot, ext = name.rpartition(".")
    if not dot or ("." + ext.lower()) not in ALLOWED_EXT:
        return "EXTENSION_NOT_ALLOWED"
    base = stem[:-5] if stem.endswith(".test") else stem
    if base.lower() == "main" or _tokens(base) & DENY_TOKENS or name.lower().endswith(".d.ts"):
        return "DENIED_NAME"
    return None


def classify(files, *, complete):
    """files: list of {path,status[,previous_filename]}; complete: comparison not truncated."""
    reasons = []
    if complete is not True:
        reasons.append("COMPARISON_INCOMPLETE")
    if type(files) is not list or not files:
        reasons.append("EMPTY_OR_INVALID_INTERVAL")
    elif len(files) > MAX_FILES:
        reasons.append("TOO_MANY_FILES")
    else:
        for entry in files:
            ok = (type(entry) is dict and set(entry) <= {"path", "status", "previous_filename"}
                  and entry.get("status") in STATUSES
                  and (entry["status"] == "renamed") == ("previous_filename" in entry))
            if not ok:
                reasons.append("ENTRY_INVALID")
                continue
            for p in (entry["path"], entry.get("previous_filename", entry["path"])):
                why = _path_reason(p)
                if why:
                    reasons.append(why)
    reasons = sorted(set(reasons))
    return {"classifier_version": VERSION, "category": MANUAL if reasons else AUTO,
            "reasons": reasons}


if __name__ == "__main__":
    raise SystemExit("QA525_RELEASE_CLASSIFIER_LIBRARY_ONLY_NO_EXECUTION")
