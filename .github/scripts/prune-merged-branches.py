#!/usr/bin/env python3
"""Poda conservadora de ramas integradas a dev; dry-run por defecto.

Requiere GITHUB_TOKEN y GITHUB_REPOSITORY únicamente al consultar GitHub.
Nunca infiere integración por la antigüedad ni por ahead/behind (squash).
"""
from __future__ import annotations

import argparse
import collections
import datetime as dt
import json
import os
import sys
import urllib.error
import urllib.parse
import urllib.request

PROTECTED_NAMES = {
    "main", "master", "dev", "develop", "staging", "production",
    "gh-pages", "pages",
}
PROTECTED_PREFIXES = (
    "release/", "uat/", "recovery/", "codex/", "claude/",
    "hotfix/", "backup/", "archive/", "dependabot/",
)
DEFAULT_MIN_DAYS = 7
DEFAULT_MAX_DELETE = 80


class ApiError(Exception):
    def __init__(self, code: int, message: str):
        super().__init__(f"GitHub HTTP {code}: {message}")
        self.code = code


class GitHub:
    def __init__(self, repository: str, token: str):
        if not token or not repository or repository.count("/") != 1:
            raise ValueError("Requiere GITHUB_REPOSITORY y GITHUB_TOKEN")
        self.repository = repository
        self.token = token
        self.owner = repository.partition("/")[0]
        self.root = f"https://api.github.com/repos/{repository}"

    def request(self, method: str, path: str):
        req = urllib.request.Request(
            self.root + "/" + path.lstrip("/"),
            method=method,
            headers={
                "Authorization": f"Bearer {self.token}",
                "Accept": "application/vnd.github+json",
                "X-GitHub-Api-Version": "2022-11-28",
                "User-Agent": "centenario-safe-branch-pruner",
            },
        )
        try:
            with urllib.request.urlopen(req, timeout=30) as response:
                raw = response.read()
                return json.loads(raw) if raw else None
        except urllib.error.HTTPError as exc:
            msg = exc.read().decode("utf-8", errors="replace")[:800]
            raise ApiError(exc.code, msg) from exc

    def pages(self, endpoint: str):
        # Exigir inventario completo antes de borrar cualquier referencia.
        items = []
        sep = "&" if "?" in endpoint else "?"
        for page in range(1, 51):
            data = self.request("GET", f"{endpoint}{sep}per_page=100&page={page}")
            if not isinstance(data, list):
                raise RuntimeError(f"Respuesta paginada inválida: {endpoint}")
            items.extend(data)
            if len(data) < 100:
                return items
        raise RuntimeError(f"Paginación agotada: {endpoint} (no borrar con inventario parcial)")

    def branch(self, name: str):
        return self.request("GET", "branches/" + urllib.parse.quote(name, safe=""))

    def open_prs_for(self, name: str):
        head = urllib.parse.urlencode({"head": f"{self.owner}:{name}"})
        return self.pages(f"pulls?state=open&{head}")

    def delete_branch(self, name: str):
        return self.request(
            "DELETE", "git/refs/heads/" + urllib.parse.quote(name, safe="/")
        )


def protected(name: str, branch: dict) -> bool:
    return (
        name in PROTECTED_NAMES
        or any(name.startswith(prefix) for prefix in PROTECTED_PREFIXES)
        or branch.get("protected") is not False
    )


def plan(branches: list[dict], prs: list[dict], repository: str,
         now: dt.datetime, min_days: int):
    """No propone borrar si falta evidencia de PR fusionada al head exacto."""
    cutoff = now - dt.timedelta(days=min_days)
    active = set()
    merged = collections.defaultdict(list)
    for pr in prs:
        head, base = pr.get("head") or {}, pr.get("base") or {}
        head_repo, base_repo = head.get("repo") or {}, base.get("repo") or {}
        if head_repo.get("full_name") != repository:
            continue
        branch = head.get("ref")
        if not branch:
            continue
        if pr.get("state") == "open":
            active.add(branch)
        merged_at = pr.get("merged_at")
        if (not merged_at or base.get("ref") != "dev"
                or base_repo.get("full_name") != repository
                or not head.get("sha")):
            continue
        when = dt.datetime.fromisoformat(merged_at.replace("Z", "+00:00"))
        merged[(branch, head["sha"])].append((when, pr.get("number")))

    proposed = []
    reasons = collections.Counter()
    for b in branches:
        name = b.get("name") or ""
        sha = (b.get("commit") or {}).get("sha")
        if not name or not sha:
            reasons["sin_datos"] += 1
        elif protected(name, b):
            reasons["protegida_o_reservada"] += 1
        elif name in active:
            reasons["pr_abierto"] += 1
        else:
            evidence = merged.get((name, sha), [])
            if not evidence:
                reasons["sin_merge_exacto_a_dev"] += 1
            elif max(when for when, _ in evidence) > cutoff:
                reasons["periodo_de_gracia"] += 1
            else:
                # Usar el merge más reciente evita borrar una rama recién reutilizada.
                when, number = max(evidence, key=lambda entry: entry[0])
                proposed.append({"branch": name, "sha": sha,
                                 "pr": number, "merged_at": when.isoformat()})
    return sorted(proposed, key=lambda item: (item["merged_at"], item["branch"])), reasons


def run(api: GitHub, *, apply: bool, min_days: int, max_delete: int):
    branches = api.pages("branches")
    prs = api.pages("pulls?state=all")
    candidates, skipped = plan(
        branches, prs, api.repository, dt.datetime.now(dt.timezone.utc), min_days
    )
    print(json.dumps({
        "evento": "inventario", "ramas": len(branches), "prs": len(prs),
        "candidatas": len(candidates), "exclusiones": dict(skipped),
        "modo": "apply" if apply else "dry-run",
    }, ensure_ascii=False))
    for candidate in candidates:
        print(json.dumps({"evento": "candidata", **candidate}, ensure_ascii=False))

    if not apply:
        print("DRY-RUN: sin operaciones DELETE")
        return

    deleted = 0
    for item in candidates[:max_delete]:
        name = item["branch"]
        try:
            current = api.branch(name)
        except ApiError as exc:
            if exc.code == 404:
                print(f"OMITIDA ya inexistente: {name}")
                continue
            raise
        if (protected(name, current)
                or (current.get("commit") or {}).get("sha") != item["sha"]):
            print(f"OMITIDA protección o HEAD modificado: {name}")
            continue
        # Una PR abierta creada después del inventario inicial bloquea el borrado.
        if api.open_prs_for(name):
            print(f"OMITIDA PR abierta antes del borrado: {name}")
            continue
        try:
            api.delete_branch(name)
        except ApiError as exc:
            if exc.code in (404, 422):
                print(f"OMITIDA desaparición/protección concurrente ({exc.code}): {name}")
                continue
            raise
        deleted += 1
        print(json.dumps({"evento": "eliminada", **item}, ensure_ascii=False))
    print(json.dumps({
        "evento": "resultado", "eliminadas": deleted,
        "candidatas_restantes_maximo": max(0, len(candidates) - max_delete),
    }, ensure_ascii=False))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--apply", action="store_true",
                        help="Ejecutar DELETE (por defecto solo simula).")
    parser.add_argument("--min-days", type=int, default=DEFAULT_MIN_DAYS)
    parser.add_argument("--max-delete", type=int, default=DEFAULT_MAX_DELETE)
    args = parser.parse_args()
    if args.min_days < DEFAULT_MIN_DAYS or not 1 <= args.max_delete <= 80:
        parser.error("min-days >= 7 y max-delete entre 1 y 80")
    token = os.getenv("GITHUB_TOKEN", "")
    if not token:
        sys.exit("ERROR: falta GITHUB_TOKEN, no se ejecuta ninguna operación")
    api = GitHub(os.getenv("GITHUB_REPOSITORY", ""), token)
    run(api, apply=args.apply, min_days=args.min_days,
        max_delete=args.max_delete)


if __name__ == "__main__":
    main()
