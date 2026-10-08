"""Tests aislados del selector y la revalidación antes de borrar ramas."""
import contextlib
import datetime as dt
import importlib.util
import io
from pathlib import Path
import unittest

PATH = Path(__file__).resolve().parents[1] / ".github/scripts/prune-merged-branches.py"
SPEC = importlib.util.spec_from_file_location("pmgm_pruner", PATH)
module = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(module)
REPO = "owner/repo"
NOW = dt.datetime(2026, 10, 8, tzinfo=dt.timezone.utc)
OLD = "2026-09-10T12:00:00Z"
RECENT = "2026-10-06T12:00:00Z"


def branch(name="docs/old", sha="abc", protected=False):
    return {"name": name, "commit": {"sha": sha}, "protected": protected}


def pr(name="docs/old", sha="abc", merged_at=OLD, base="dev",
       state="closed", repo=REPO, number=99):
    return {
        "number": number, "state": state, "merged_at": merged_at,
        "head": {"ref": name, "sha": sha, "repo": {"full_name": repo}},
        "base": {"ref": base, "repo": {"full_name": REPO}},
    }


class PlanningTests(unittest.TestCase):
    def select(self, branches=None, prs=None):
        return module.plan(
            [branch()] if branches is None else branches,
            [pr()] if prs is None else prs, REPO, NOW, 7
        )

    def test_merged_exact_head_is_eligible(self):
        results, _ = self.select()
        self.assertEqual(results[0]["branch"], "docs/old")
        self.assertEqual(results[0]["sha"], "abc")
        self.assertEqual(results[0]["pr"], 99)

    def test_squash_is_not_rejected_by_ahead_behind(self):
        results, _ = self.select([dict(branch(), ahead_by=3, behind_by=400)])
        self.assertEqual(len(results), 1)

    def test_unmerged_or_different_base_is_not_eligible(self):
        for proposal in (pr(merged_at=None), pr(base="main"), pr(sha="before"),
                         pr(repo="external/fork")):
            with self.subTest(proposal=proposal):
                self.assertEqual(self.select(prs=[proposal])[0], [])

    def test_open_pr_blocks_even_with_merged_evidence(self):
        open_pr = pr(state="open", merged_at=None, number=101)
        self.assertEqual(self.select(prs=[pr(), open_pr])[0], [])

    def test_recent_merge_blocked(self):
        self.assertEqual(self.select(prs=[pr(merged_at=RECENT)])[0], [])
        self.assertEqual(self.select(prs=[pr(), pr(merged_at=RECENT, number=102)])[0], [])

    def test_reserved_and_protected_excluded(self):
        reserved = ["main", "dev", "release/v1", "uat/test", "recovery/2026",
                    "codex/test", "claude/test", "hotfix/a", "backup/foo",
                    "dependabot/deps"]
        for name in reserved:
            with self.subTest(name=name):
                self.assertEqual(self.select([branch(name)], [pr(name=name)])[0], [])
        self.assertEqual(self.select([branch(protected=True)])[0], [])
        # Fail closed si GitHub omite el estado de protección.
        self.assertEqual(self.select([{"name": "docs/old", "commit": {"sha": "abc"}}])[0], [])

    def test_never_prune_without_complete_metadata(self):
        self.assertEqual(self.select([{"name": "docs/old"}])[0], [])
        self.assertEqual(self.select([branch()], [pr(name="unrelated")])[0], [])


class FakeAPI:
    repository = REPO

    def __init__(self, sha="abc", open_now=False):
        self.sha = sha
        self.open_now = open_now
        self.removed = []

    def pages(self, path):
        if path == "branches":
            return [branch()]
        if path == "pulls?state=all":
            return [pr()]
        raise AssertionError(path)

    def branch(self, name):
        self.checked = True
        return branch(name, self.sha)

    def open_prs_for(self, name):
        return [pr(state="open")] if self.open_now else []

    def delete_branch(self, name):
        self.removed.append(name)


class ExecutionTests(unittest.TestCase):
    def test_dry_run_never_deletes(self):
        api = FakeAPI()
        with contextlib.redirect_stdout(io.StringIO()):
            module.run(api, apply=False, min_days=7, max_delete=80)
        self.assertEqual(api.removed, [])

    def test_apply_rechecks_head_and_open_pr(self):
        for modified, opened, expected in [
            ("new", False, []),
            ("abc", True, []),
            ("abc", False, ["docs/old"]),
        ]:
            with self.subTest(modified=modified, opened=opened):
                api = FakeAPI(sha=modified, open_now=opened)
                # La fecha de prueba utiliza un merge antiguo y el reloj real actual.
                with contextlib.redirect_stdout(io.StringIO()):
                    module.run(api, apply=True, min_days=7, max_delete=80)
                self.assertEqual(api.removed, expected)


if __name__ == "__main__":
    unittest.main()
