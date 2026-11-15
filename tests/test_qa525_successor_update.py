"""QA525 successor operator wiring: real entrypoint/policy/gate modules, fake host. No side effects."""
import importlib.util
import sys
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]
OPS = ROOT / "scripts/qa/operators"


def load(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    mod = importlib.util.module_from_spec(spec)
    sys.modules[name] = mod
    spec.loader.exec_module(mod)
    return mod


policy = load("t_succ_policy", OPS / "qa525-current-change-policy.py")
gate = load("t_succ_gate", OPS / "qa525-support00-schema-gate.py")
succ_update = load("t_succ_update", OPS / "qa525-successor-update.py")
A, B = "a" * 40, "b" * 40
D = "sha256:" + "b" * 64
IMG = {"api": "sha256:" + "1" * 64, "web": "sha256:" + "2" * 64}


def host_state(sha=A):
    prepared = {"source_sha": sha, "status": "PREPARED_API_WEB_SIGNATURE_VERIFIED_NO_DEPLOYMENT",
                "api_image_id": IMG["api"], "web_image_id": IMG["web"]}
    journals = {sha: {"source_sha": sha, "status": "APPLIED", "new_images": dict(IMG)}}
    live = {n: ["cid-" + n, IMG[n], "running"] for n in ("api", "web")}
    return prepared, journals, live


class FakeSigned:
    class gate:  # noqa: N801
        class readiness:  # noqa: N801
            pass


def signed_with(files, blobs=None, status="ahead", merge=A, ahead=3):
    blobs = blobs if blobs is not None else succ_update.pinned_blobs(gate)
    diff = {"status": status, "merge_base_commit": {"sha": merge}, "behind_by": 0,
            "ahead_by": ahead, "total_commits": ahead,
            "files": [{"filename": f, "status": "modified"} for f in files]}

    def fetch(path):
        if path.startswith("/compare/"):
            assert path == "/compare/%s...%s" % (A, B)
            return diff
        for p, sha in blobs.items():
            if path == "/contents/%s?ref=%s" % (p, B):
                return {"sha": sha}
        raise AssertionError(path)
    s = FakeSigned()
    s.gate.readiness.fetch_public = staticmethod(fetch)
    return s


SOURCE = {"status": "READY_FOR_SIGNATURE_VERIFICATION_ONLY", "scope": policy.gate.RESULT_SCOPE,
          "source_sha": B, "release_tag": "qa-signed-" + B, "release_asset_sha256": D}


class SuccessorUpdateTests(unittest.TestCase):
    def setUp(self):
        self.entry = load("t_succ_entry", OPS / "qa525-supervised-dual-entrypoint.py")
        self.original_approved = self.entry.approved_review
        self.orig_baseline = self.entry.BASELINE

    def install(self, state=None, healthy=True):
        return succ_update.install(self.entry, None, state=state or host_state(),
                                   health=lambda s: healthy and s == A)

    def test_install_sets_baseline_from_applied_state_only(self):
        self.assertEqual(A, self.install())
        self.assertEqual(A, self.entry.BASELINE)
        self.assertNotEqual(self.orig_baseline, self.entry.BASELINE)

    def test_install_refuses_unhealthy_unapplied_or_not_live(self):
        for mutate, code in (("health", "BASELINE_HEALTH_UNVERIFIED"),
                             ("pending", "BASELINE_JOURNAL_NOT_APPLIED"),
                             ("live", "BASELINE_NOT_LIVE")):
            prepared, journals, live = host_state()
            if mutate == "pending":
                journals[A]["status"] = "PENDING"
            if mutate == "live":
                live["web"][2] = "exited"
            with self.assertRaises(Exception) as ctx:
                self.install((prepared, journals, live), healthy=mutate != "health")
            self.assertEqual(code, str(ctx.exception))
            self.assertEqual(self.orig_baseline, self.entry.BASELINE)  # nothing patched on failure

    def review(self, now=1000):
        return {"source_sha": B, "release_asset_sha256": D, "baseline_sha": A,
                "reviewed_sections": sorted(self.entry.REVIEWED), "sensitive_reasons": [],
                "no_new_ef_migrations": False, "runtime_migration_flags_disabled": True,
                "legacy_master_and_smtp_preserved": True, "reviewed_at": now,
                "g6_archive_name": "centenario-qa-g6-CIFRADO-20261114T120000Z.tar.gz",
                "g6_archive_sha256": "c" * 64, "g6_drive_file_id": "d" * 30,
                "g6_offhost_sha256_verified": True,
                "g6_restore_report_name": "isolated-restore-20261114T120500Z.report.json"}

    def test_frontend_only_successor_is_eligible_and_approved(self):
        self.install()
        s = signed_with(["frontend/src/App.tsx", "frontend/src/pmgmApi.ts"])
        decision, state = self.entry.current_change_review(policy, s, SOURCE, gate)
        self.assertEqual("ELIGIBLE_FOR_HOST_PREFLIGHT_ONLY", decision["status"])
        self.assertEqual(gate.SCHEMA_REVIEW_STATE, state)
        self.entry.approved_review(self.review(), SOURCE, decision, 1010, schema_approved=True)

    def test_historical_function_still_rejects_eligible_without_successor(self):
        decision = {"status": "ELIGIBLE_FOR_HOST_PREFLIGHT_ONLY", "reasons": []}
        with self.assertRaises(self.entry.Blocked):
            self.original_approved(self.review(), SOURCE, decision, 1010, schema_approved=True)

    def test_approved_review_negatives(self):
        self.install()
        eligible = {"status": "ELIGIBLE_FOR_HOST_PREFLIGHT_ONLY", "reasons": []}
        cases = []
        r = self.review(); r["baseline_sha"] = "e" * 40; cases.append(r)          # wrong baseline
        r = self.review(); r["sensitive_reasons"] = ["X"]; cases.append(r)        # claims reasons
        r = self.review(); r["g6_offhost_sha256_verified"] = False; cases.append(r)
        r = self.review(); r["release_asset_sha256"] = "sha256:" + "f" * 64; cases.append(r)
        for review in cases:
            with self.assertRaises(Exception):
                self.entry.approved_review(review, SOURCE, eligible, 1010, schema_approved=True)
        with self.assertRaises(Exception):  # stale review
            self.entry.approved_review(self.review(), SOURCE, eligible, 1000 + 1801, schema_approved=True)
        with self.assertRaises(Exception):  # eligible policy but non-empty reasons
            self.entry.approved_review(self.review(), SOURCE,
                                       {"status": "ELIGIBLE_FOR_HOST_PREFLIGHT_ONLY", "reasons": ["X"]},
                                       1010, schema_approved=True)

    def test_sensitive_change_keeps_stop_or_review(self):
        self.install()
        d, _ = self.entry.current_change_review(
            policy, signed_with(["backend/src/PMGM.Api/Program.cs"]), SOURCE, gate)
        self.assertEqual("BLOCKED_REVIEW_REQUIRED", d["status"])
        self.assertTrue(d["reasons"])

    def test_schema_changes_are_rejected(self):
        self.install()
        for path in ("backend/src/PMGM.Api/Migrations/20261201_X.cs", "backend/src/X/AppDbContext.cs"):
            with self.assertRaises(Exception) as ctx:
                self.entry.current_change_review(policy, signed_with([path]), SOURCE, gate)
            self.assertEqual("SUCCESSOR_SCHEMA_CHANGE_NOT_ALLOWED", str(ctx.exception))

    def test_changed_pinned_blob_rejected(self):
        self.install()
        blobs = dict(succ_update.pinned_blobs(gate))
        blobs[gate.MIGRATION_PATH] = "0" * 40
        with self.assertRaises(Exception) as ctx:
            self.entry.current_change_review(policy, signed_with(["frontend/src/a.ts"], blobs), SOURCE, gate)
        self.assertEqual("SUCCESSOR_SCHEMA_BLOBS_CHANGED", str(ctx.exception))

    def test_truncated_or_non_ancestral_diff_rejected(self):
        self.install()
        many = ["frontend/src/f%d.ts" % i for i in range(300)]
        for s in (signed_with(many), signed_with(["frontend/src/a.ts"], merge="e" * 40),
                  signed_with(["frontend/src/a.ts"], status="diverged")):
            with self.assertRaises(Exception):
                self.entry.current_change_review(policy, s, SOURCE, gate)

    def test_live_ef_history_check(self):
        self.install()
        pin = {"migrations": ["M1", "M2", "M3"]}
        check = succ_update.make_live_ef_check(self.entry, lambda: pin)
        self.assertTrue(check(lambda args: "M1\nM2\nM3"))
        for raw in ("M1\nM2", "M1\nM2\nM3\nM4", "M1\nM2\nMX", "M1\nM1\nM3"):
            with self.assertRaises(Exception):
                check(lambda args, raw=raw: raw)
        with self.assertRaises(Exception):
            succ_update.make_live_ef_check(self.entry, lambda: {"migrations": []})(lambda a: "")

    def test_successor_flag_only_with_run_reviewed(self):
        update = load("t_succ_main", OPS / "qa525-update.py")
        for argv in (["--successor", "--apply-prepared"], ["--successor", "--status"]):
            with self.assertRaises(Exception) as ctx:
                update.main(argv)
            self.assertEqual("UPDATE_SUCCESSOR_ONLY_WITH_RUN_REVIEWED", str(ctx.exception))


if __name__ == "__main__":
    unittest.main()
