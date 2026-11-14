#!/usr/bin/env python3
"""QA525 vNext release classifier (contract docs/qa/QA525-AUTOWEB-CONTRACT-V1.md, v1.2).

PURE and INACTIVE: no I/O, no host, no network. Decides only whether an installed->candidate
interval may be applied by the future `auto-web` path (prebuilt WEB image only) or must stay
`manual`. It grants nothing: a Sponsor policy, an attested envelope and host measurements are
still required. Fail-closed: anything unknown is `manual`.

Entries come from `git diff --raw --no-renames`-style data produced in CI:
  {"path": str, "status": "added"|"modified"|"removed"|"renamed", "mode": "100644"|...,
   "content_kind": sniff(blob bytes) | "none" (removed), ["previous_path": str]}
Renames, symlinks (120000), submodules (160000) and executables (100755) are always manual.

PROFILE F4-A (review 5904808610 P1-4, comment 6878675183 point 4): the first activation only
admits NON-EXECUTABLE content whose bytes CI sniffed by magic number (PNG, JPEG, WebP, WOFF2,
ICO) and whose kind matches the extension. TS/TSX/CSS/SVG stay `manual` until a content/E2E
gate exists: a screen can add `fetch`, import an API client or touch data without hitting any
denylisted path, and SVG/CSS can carry active content. The protocol category stays `auto-web`.
"""
from __future__ import annotations

import re

VERSION = 3  # v1 = #1012 a894991 (flat); v2 = contract v1.2; v3 = contract v1.3 profile F4-A
PROFILE = "F4A_STATIC_NON_EXECUTABLE"
MAX_FILES = 300
AUTO = "auto-web"
MANUAL = "manual"
STATUSES = frozenset({"added", "modified", "removed", "renamed"})
REGULAR_MODE = "100644"
SRC = "frontend/src/"
PUBLIC_BRAND = "frontend/public/brand/"
PUBLIC_TOP = "frontend/public/"
# extension -> content kind that the sniffed bytes must have (F4-A).
SRC_EXT = {".png": "png", ".jpg": "jpeg", ".jpeg": "jpeg", ".webp": "webp", ".woff2": "woff2"}
PUBLIC_EXT = {".png": "png", ".jpg": "jpeg", ".jpeg": "jpeg", ".webp": "webp", ".ico": "ico"}
# Known UI sources that are executable or active content: always manual in F4-A.
ACTIVE_EXT = frozenset({".ts", ".tsx", ".css", ".svg", ".js", ".jsx", ".mjs", ".html", ".json", ".md"})
KINDS = frozenset({"png", "jpeg", "webp", "woff2", "ico", "unknown", "none"})
MAX_ASSET_BYTES = 2 * 1024 * 1024
# Directory names under frontend/src/ that hold auth, API clients, accounts or runtime wiring.
DENY_DIRS = frozenset({"api", "auth", "oidc", "keycloak", "accounts", "config", "runtime", "env"})
# Name tokens (camelCase/kebab split) that mark auth, secrets, env, service worker or build config.
DENY_TOKENS = frozenset({"auth", "oidc", "keycloak", "token", "secret", "password", "credential",
                         "session", "env", "runtime", "sw", "vite", "manifest", "config"})
PATH_OK = re.compile(r"[A-Za-z0-9._/@+-]{1,200}\Z")

R_ALLOW = "ALLOWLIST_ONLY"


def sniff(data):
    """Content kind from magic bytes only (CI side; pure). Anything else is `unknown`."""
    if type(data) is not bytes or not data or len(data) > MAX_ASSET_BYTES:
        return "unknown"
    if data.startswith(b"\x89PNG\r\n\x1a\n"):
        return "png"
    if data.startswith(b"\xff\xd8\xff"):
        return "jpeg"
    if len(data) >= 12 and data[:4] == b"RIFF" and data[8:12] == b"WEBP":
        return "webp"
    if data.startswith(b"wOF2"):
        return "woff2"
    if data.startswith(b"\x00\x00\x01\x00"):
        return "ico"
    return "unknown"


def _tokens(stem):
    spaced = re.sub(r"([a-z0-9])([A-Z])", r"\1 \2", stem)
    return {t for t in re.split(r"[^A-Za-z0-9]+", spaced.lower()) if t}


def _path_reason(path, status, kind):
    if (type(path) is not str or PATH_OK.fullmatch(path) is None or path.startswith(("/", "./"))
            or any(seg in ("", ".", "..") for seg in path.split("/"))):
        return "PATH_INVALID"
    name = path.rsplit("/", 1)[-1]
    stem, dot, ext = name.rpartition(".")
    ext = "." + ext.lower() if dot else ""
    if path.startswith(SRC):
        if any(p.lower() in DENY_DIRS for p in path[len(SRC):].split("/")[:-1]):
            return "DENIED_DIRECTORY"
        base = stem[:-5] if stem.endswith(".test") else stem
        if base.lower() == "main" or name.lower().endswith(".d.ts") or _tokens(base) & DENY_TOKENS:
            return "DENIED_NAME"
        table = SRC_EXT
    elif path.startswith(PUBLIC_BRAND) or (path.startswith(PUBLIC_TOP) and "/" not in path[len(PUBLIC_TOP):]):
        if path.startswith(PUBLIC_BRAND) and status == "removed":
            return "BRAND_REMOVAL"
        if _tokens(stem) & DENY_TOKENS:
            return "DENIED_NAME"
        table = PUBLIC_EXT if path.startswith(PUBLIC_BRAND) else {k: v for k, v in PUBLIC_EXT.items() if k != ".ico"}
    else:
        return "OUTSIDE_AUTO_WEB_ROOTS"
    if ext in ACTIVE_EXT:
        return "F4A_ACTIVE_CONTENT_MANUAL"
    if ext not in table:
        return "EXTENSION_NOT_ALLOWED"
    if status == "removed":
        return "F4A_REMOVAL_MANUAL"
    if kind != table[ext]:
        return "CONTENT_KIND_MISMATCH"
    return None


def _entry_reasons(entry):
    if (type(entry) is not dict or not {"path", "status", "mode", "content_kind"} <= set(entry)
            or not set(entry) <= {"path", "status", "mode", "content_kind", "previous_path"}
            or entry.get("status") not in STATUSES or type(entry.get("mode")) is not str
            or entry.get("content_kind") not in KINDS
            or (entry["status"] == "removed") != (entry["content_kind"] == "none")
            or (entry["status"] == "renamed") != ("previous_path" in entry)):
        return ["ENTRY_INVALID"]
    reasons = []
    if entry["status"] == "renamed":
        reasons.append("RENAME_IS_MANUAL")
    if entry["mode"] != REGULAR_MODE:
        reasons.append("MODE_NOT_REGULAR_FILE")
    for p in (entry["path"], entry.get("previous_path", entry["path"])):
        why = _path_reason(p, entry["status"], entry["content_kind"])
        if why:
            reasons.append(why)
    return reasons


def classify(files, *, complete, total=None):
    """files: sorted list of entries; complete: interval not truncated; total: count from CI."""
    reasons = []
    if complete is not True:
        reasons.append("COMPARISON_INCOMPLETE")
    if type(files) is not list or not files:
        reasons.append("EMPTY_OR_INVALID_INTERVAL")
    elif len(files) >= MAX_FILES:
        reasons.append("TOO_MANY_FILES")
    else:
        if total is not None and (type(total) is not int or total != len(files)):
            reasons.append("TOTAL_MISMATCH")
        paths = [e.get("path") if type(e) is dict else None for e in files]
        if paths != sorted(p for p in paths if type(p) is str) or len(set(paths)) != len(paths):
            reasons.append("FILES_NOT_SORTED_UNIQUE")
        for entry in files:
            reasons.extend(_entry_reasons(entry))
    reasons = sorted(set(reasons))
    return {"version": VERSION, "result": MANUAL if reasons else AUTO,
            "reasons": reasons or [R_ALLOW, PROFILE]}


if __name__ == "__main__":
    raise SystemExit("QA525_RELEASE_CLASSIFIER_LIBRARY_ONLY_NO_EXECUTION")
