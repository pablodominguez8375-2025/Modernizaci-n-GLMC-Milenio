#!/usr/bin/env python3
"""QA525 auto-web F4-B SHADOW profile (INACTIVE). Contract: docs/qa/QA525-AUTOWEB-F4B-SHADOW.md

F4-B decides whether a frontend interval WOULD be eligible for automatic publication if screens
were admitted. It runs only in shadow: its verdict is recorded next to the real F4-A verdict and
never changes the envelope, the policy or what the host does. The pinned F4-A classifier
(`qa525-vnext-release-classifier.py`) is not modified, so its policy hash stays valid.

Rules (all must hold for `F4B_WOULD_AUTO`):
* every entry is `modified` (no additions, removals, renames), mode 100644;
* every path passes the F4-A denylist (directories/names) and is .ts/.tsx/.css under
  frontend/src, or a static asset accepted by F4-A;
* per file (AST facts from `qa525-vnext-autoweb-f4b-analyze.cjs`): parses, identical import set,
  no new network/storage/eval/HTML sinks, no new references to names imported from API/auth
  modules, no new calls to mutating callback props (onSave, onDelete, ...);
* total added lines <= 400, at most 15 files;
* the CI E2E gate (all showcase views, 0 console errors) passed for the candidate.
"""
from __future__ import annotations

import importlib.util
import json
from pathlib import Path
import subprocess

HERE = Path(__file__).resolve().parent
PROFILE = "F4B_SHADOW_SCREENS"
SCREEN_EXT = (".ts", ".tsx", ".css")
MAX_FILES = 15
MAX_ADDED_LINES = 400
WOULD_AUTO = "F4B_WOULD_AUTO"
WOULD_MANUAL = "F4B_WOULD_MANUAL"
FACT_KEYS = frozenset({"path", "parse_ok", "imports_equal", "imports_added", "imports_removed",
                       "new_sinks", "new_api_refs", "new_mutating_prop_calls", "added_lines"})


def _load(name, filename):
    spec = importlib.util.spec_from_file_location(name, HERE / filename)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


CLASSIFIER = _load("f4b_shadow_classifier", "qa525-vnext-release-classifier.py")


def _screen_path_reason(path):
    """F4-A path rules (denylist dirs/names, roots) but with screen extensions admitted."""
    if not path.startswith(CLASSIFIER.SRC) or not path.endswith(SCREEN_EXT):
        return "F4B_PATH_NOT_SCREEN"
    rel = path[len(CLASSIFIER.SRC):].split("/")
    if any(p.lower() in CLASSIFIER.DENY_DIRS for p in rel[:-1]):
        return "DENIED_DIRECTORY"
    name = rel[-1]
    stem = name.rsplit(".", 1)[0]
    base = stem[:-5] if stem.endswith(".test") else stem
    if (base.lower() == "main" or name.lower().endswith(".d.ts")
            or CLASSIFIER._tokens(base) & CLASSIFIER.DENY_TOKENS):
        return "DENIED_NAME"
    if CLASSIFIER.PATH_OK.fullmatch(path) is None or ".." in rel:
        return "PATH_INVALID"
    return None


def shadow_verdict(files, facts, *, e2e_passed, complete=True):
    """Pure. `files`: classifier entries; `facts`: analyzer output for the screen files."""
    reasons = set()
    if complete is not True:
        reasons.add("COMPARISON_INCOMPLETE")
    if type(files) is not list or not files or len(files) > MAX_FILES:
        reasons.add("F4B_FILE_COUNT")
        files = files if type(files) is list else []
    by_path = {}
    for fact in facts if type(facts) is list else []:
        if type(fact) is not dict or set(fact) != FACT_KEYS or fact["path"] in by_path:
            reasons.add("F4B_FACTS_INVALID")
            continue
        by_path[fact["path"]] = fact
    total_added = 0
    screens = 0
    for entry in files:
        if type(entry) is not dict or entry.get("status") != "modified" or entry.get("mode") != "100644":
            reasons.add("F4B_ONLY_MODIFIED_REGULAR_FILES")
            continue
        path = entry.get("path")
        if type(path) is str and path.endswith(SCREEN_EXT):
            why = _screen_path_reason(path)
            if why:
                reasons.add(why)
                continue
            fact = by_path.get(path)
            if fact is None:
                reasons.add("F4B_FACTS_MISSING")
                continue
            screens += 1
            if fact["parse_ok"] is not True:
                reasons.add("F4B_PARSE_FAILED")
            if fact["imports_equal"] is not True:
                reasons.add("F4B_IMPORTS_CHANGED")
            if fact["new_sinks"]:
                reasons.add("F4B_NEW_SINK")
            if fact["new_api_refs"]:
                reasons.add("F4B_NEW_API_REFERENCE")
            if fact["new_mutating_prop_calls"]:
                reasons.add("F4B_NEW_MUTATING_CALLBACK")
            if type(fact["added_lines"]) is not int or fact["added_lines"] < 0:
                reasons.add("F4B_FACTS_INVALID")
            else:
                total_added += fact["added_lines"]
        else:
            single = CLASSIFIER.classify([entry], complete=True, total=1)
            if single["result"] != CLASSIFIER.AUTO:
                reasons.update(single["reasons"])
    if total_added > MAX_ADDED_LINES:
        reasons.add("F4B_TOO_LARGE")
    if screens and e2e_passed is not True:
        reasons.add("F4B_E2E_NOT_PASSED")
    reasons = sorted(reasons)
    return {"profile": PROFILE, "shadow": True, "may_apply": False,
            "result": WOULD_MANUAL if reasons else WOULD_AUTO,
            "reasons": reasons or ["F4B_ALL_GATES_PASSED"], "added_lines": total_added}


def collect_facts(repo, base_sha, head_sha, files):
    """CI side: run the AST analyzer on old/new contents of the screen files (git + node)."""
    payload = []
    for entry in files:
        path = entry.get("path", "")
        if entry.get("status") == "modified" and path.endswith(SCREEN_EXT):
            old = subprocess.run(["git", "-C", str(repo), "show", f"{base_sha}:{path}"],
                                 check=True, capture_output=True).stdout.decode("utf-8")
            new = subprocess.run(["git", "-C", str(repo), "show", f"{head_sha}:{path}"],
                                 check=True, capture_output=True).stdout.decode("utf-8")
            payload.append({"path": path, "old": old, "new": new})
    if not payload:
        return []
    out = subprocess.run(["node", str(HERE / "qa525-vnext-autoweb-f4b-analyze.cjs")],
                         input=json.dumps(payload).encode(), check=True, capture_output=True)
    return json.loads(out.stdout)


if __name__ == "__main__":
    raise SystemExit("QA525_AUTOWEB_F4B_LIBRARY_ONLY_NO_EXECUTION")
