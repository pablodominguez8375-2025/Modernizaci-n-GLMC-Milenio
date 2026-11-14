"""Hermetic tests: auto-web classifier, envelope v1.2 and F1 simulator (19 cases).

No signature, host, docker or network. Vectors mirror docs/qa/QA525-AUTOWEB-CONTRACT-V1.md §6.
"""
import copy
import datetime as dt
import hashlib
import importlib.util
import json
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]


def _load(name):
    spec = importlib.util.spec_from_file_location("aw_" + name.replace("-", "_"),
                                                  ROOT / "scripts/qa" / (name + ".py"))
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


C = _load("qa525-vnext-release-classifier")
E = _load("qa525-vnext-release-envelope")
S = _load("qa525-vnext-autoweb-simulator")

CLS_SHA = "sha256:" + hashlib.sha256((ROOT / "scripts/qa/qa525-vnext-release-classifier.py").read_bytes()).hexdigest()
REPO, REPO_ID = "pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio", 1356543647
WF, REF = ".github/workflows/qa-release-envelope.yml", "refs/heads/dev"
SUBJECT = "repo:" + REPO + ":ref:" + REF
D = lambda c: "sha256:" + c * 64  # noqa: E731
INST, CAND = "a" * 40, "b" * 40
INSTALLED = {"sha": INST, "web_image_digest": D("1"), "api_image_digest": D("2"),
             "compose_bytes_sha256": D("3"), "identity_digest": D("4"), "silo_image_digest": D("5")}
BASES = {"build": D("6"), "final": D("7")}
ARGS = {"VITE_API_BASE_URL": "", "VITE_USE_MOCKS": "false",
        "VITE_OIDC_AUTHORITY": "https://sistema.example/identity/realms/pmgm",
        "VITE_OIDC_CLIENT_ID": "pmgm-web", "VITE_OIDC_SCOPE": "openid profile"}
ARGS_SHA = E.build_args_sha256(ARGS)
NOW = dt.datetime(2026, 11, 14, 12, 0, 0, tzinfo=dt.timezone.utc)
F = lambda p, st="modified", **kw: dict({"path": p, "status": st, "mode": "100644"}, **kw)  # noqa: E731
UI = sorted([F("frontend/src/LodgeManagementPage.tsx"), F("frontend/src/home-consistency.css"),
             F("frontend/public/brand/logo.svg", "added")], key=lambda e: e["path"])
EXPECT = {"repository": REPO, "source_repository_id": REPO_ID, "installed": dict(INSTALLED),
          "classifier_sha256": CLS_SHA, "workflow": WF, "workflow_ref": REF, "subject": SUBJECT,
          "allowed_base_image_digests": dict(BASES), "build_args_sha256": ARGS_SHA,
          "first_sequence": 10, "last_sequence": 14, "last_applied_sequence": 11}


def make(files=UI, *, complete=True, total=None, seq=12, cand=None, installed=None,
         ci=None, times=("2026-11-14T11:00:00Z", "2026-11-14T11:30:00Z", "2026-11-15T11:30:00Z"),
         merge_base=INST, ahead=3, cls_sha=CLS_SHA):
    candidate = {"sha": CAND, "web_image_digest": D("8"), "web_image_archive_sha256": D("9"),
                 "web_sbom_sha256": D("a"), "base_image_digests": dict(BASES),
                 "build_args_sha256": ARGS_SHA}
    candidate.update(cand or {})
    ci_rec = {"workflow": WF, "workflow_ref": REF, "run_id": 77, "run_attempt": 1,
              "issuer": E.ISSUER, "subject": SUBJECT, "source_sha": CAND}
    ci_rec.update(ci or {})
    return E.build(repository=REPO, source_repository_id=REPO_ID, sequence=seq,
                   installed=installed or INSTALLED, candidate=candidate,
                   lineage={"merge_base": merge_base, "ahead_by": ahead,
                            "total_changed_files": len(files) if total is None else total,
                            "changed_files_complete": complete, "changed_files": files},
                   classifier_module=C, classifier_sha256=cls_sha, ci=ci_rec,
                   issued_at=times[0], not_before=times[1], expires_at=times[2])


def check(raw, **over):
    exp = copy.deepcopy(EXPECT)
    exp.update(over.pop("expect", {}))
    return E.check(raw, expect=exp, classifier_module=C, now=over.pop("now", NOW))


def resign(raw, mutate):
    rec = json.loads(raw)
    mutate(rec)
    rec["release_id"] = E.release_id(rec)
    return E.canonical(rec)


class AutoWebClassifierTests(unittest.TestCase):
    def res(self, files, **kw):
        return C.classify(files, complete=kw.get("complete", True), total=kw.get("total"))

    def test_ui_only_is_auto_web(self):
        self.assertEqual(self.res(UI), {"version": 2, "result": "auto-web", "reasons": ["ALLOWLIST_ONLY"]})

    def test_denylist_paths_are_manual(self):
        for p in ("frontend/src/main.tsx", "frontend/src/api/calendarApi.ts", "frontend/src/auth/config.ts",
                  "frontend/src/accounts/accountLifecycle.ts", "frontend/src/vite-env.d.ts",
                  "frontend/src/SessionPanel.tsx", "frontend/src/runtimeFlags.ts", "frontend/index.html",
                  "frontend/nginx.conf", "frontend/Dockerfile", "frontend/package-lock.json",
                  "frontend/vite.config.ts", "frontend/.env.production", "frontend/public/sw.js",
                  "frontend/public/manifest.webmanifest", ".github/workflows/ci.yml",
                  "scripts/qa/qa525-vnext-release-classifier.py", "backend/src/Program.cs",
                  "frontend/src/api/decree-1759.json", "frontend/src/help/README.md"):
            with self.subTest(path=p):
                self.assertEqual(self.res([F(p)])["result"], "manual")

    def test_rename_symlink_exec_binary_are_manual(self):
        cases = [F("frontend/src/B.tsx", "renamed", previous_path="frontend/src/A.tsx"),
                 F("frontend/src/link.ts", mode="120000"), F("frontend/src/vendor", mode="160000"),
                 F("frontend/src/tool.ts", mode="100755"), F("frontend/public/lib.wasm", "added")]
        for entry in cases:
            with self.subTest(entry=entry):
                self.assertEqual(self.res([entry])["result"], "manual")

    def test_brand_removal_is_manual_but_addition_is_auto(self):
        self.assertIn("BRAND_REMOVAL", self.res([F("frontend/public/brand/logo.svg", "removed")])["reasons"])
        self.assertEqual(self.res([F("frontend/public/brand/logo-v3.svg", "added")])["result"], "auto-web")

    def test_truncated_total_mismatch_unsorted_and_limit(self):
        self.assertIn("COMPARISON_INCOMPLETE", self.res(UI, complete=False)["reasons"])
        self.assertIn("TOTAL_MISMATCH", self.res(UI, total=4)["reasons"])
        self.assertIn("FILES_NOT_SORTED_UNIQUE", self.res(list(reversed(UI)))["reasons"])
        many = [F("frontend/src/P%03d.tsx" % i) for i in range(300)]
        self.assertIn("TOO_MANY_FILES", self.res(many)["reasons"])

    def test_malformed_entries(self):
        for entry in ({"path": "frontend/src/A.tsx", "status": "modified"},
                      F("frontend/src/A.tsx", extra=1), F("frontend/src/../x.tsx"), F("/etc/passwd")):
            with self.subTest(entry=entry):
                self.assertEqual(self.res([entry])["result"], "manual")


class AutoWebEnvelopeTests(unittest.TestCase):
    def test_valid_auto_web_candidate(self):
        out = check(make())
        self.assertEqual(out["decision"], E.AUTO_WEB_CANDIDATE)
        self.assertEqual(out["rollback_web_image_digest"], INSTALLED["web_image_digest"])
        self.assertEqual(out["contract"], "1.2")

    def test_canonical_release_id_and_strict_json(self):
        raw = make()
        rec = json.loads(raw)
        rec["release_id"] = D("0")
        with self.assertRaisesRegex(E.Stop, "ENVELOPE_RELEASE_ID"):
            check(E.canonical(rec))
        with self.assertRaisesRegex(E.Stop, "ENVELOPE_NOT_CANONICAL"):
            check(json.dumps(json.loads(raw), indent=1).encode() + b"\n")
        with self.assertRaisesRegex(E.Stop, "ENVELOPE_DUPLICATE_KEY"):
            check(b'{"schema":"x","schema":"y"}\n')
        with self.assertRaisesRegex(E.Stop, "ENVELOPE_FLOAT"):
            check(raw.replace(b'"sequence":12', b'"sequence":12.0'))
        with self.assertRaisesRegex(E.Stop, "ENVELOPE_FIELDS"):
            check(resign(raw, lambda r: r.update(extra=1)))

    def test_installed_is_measured_by_host_not_trusted(self):
        drift = dict(INSTALLED, api_image_digest=D("f"))
        with self.assertRaisesRegex(E.Stop, "ENVELOPE_INSTALLED_DRIFT"):
            check(make(), expect={"installed": drift})

    def test_build_args_and_base_images(self):
        with self.assertRaisesRegex(E.Stop, "BUILD_ARGS_MOCKS_FORBIDDEN"):
            E.build_args_sha256(dict(ARGS, VITE_USE_MOCKS="true"))
        with self.assertRaisesRegex(E.Stop, "ENVELOPE_BUILD_ARGS"):
            check(make(cand={"build_args_sha256": E.build_args_sha256(dict(ARGS, VITE_API_BASE_URL="https://evil"))}))
        out = check(make(cand={"base_image_digests": {"build": D("6"), "final": D("c")}}))
        self.assertEqual((out["decision"], out["reasons"]), (E.MANUAL, ["BASE_IMAGE_DRIFT"]))

    def test_workflow_and_repository_pins(self):
        for ci in ({"workflow": ".github/workflows/other.yml"}, {"workflow_ref": "refs/heads/main"},
                   {"issuer": "https://evil"}, {"subject": "repo:x:ref:y"}):
            with self.subTest(ci=ci):
                with self.assertRaisesRegex(E.Stop, "ENVELOPE_WORKFLOW_PIN"):
                    check(make(ci=ci))
        with self.assertRaisesRegex(E.Stop, "ENVELOPE_SOURCE_SHA"):
            check(make(ci={"source_sha": "c" * 40}))
        with self.assertRaisesRegex(E.Stop, "ENVELOPE_REPOSITORY_PIN"):
            check(make(), expect={"source_repository_id": 1})

    def test_image_must_be_new(self):
        with self.assertRaisesRegex(E.Stop, "ENVELOPE_IMAGE_NOT_NEW"):
            check(make(cand={"web_image_digest": INSTALLED["web_image_digest"]}))

    def test_expired_and_window(self):
        with self.assertRaisesRegex(E.Stop, "ENVELOPE_EXPIRED"):
            check(make(), now=NOW + dt.timedelta(days=2))
        with self.assertRaisesRegex(E.Stop, "ENVELOPE_WINDOW_INVALID"):
            check(make(times=("2026-11-14T11:00:00Z", "2026-11-14T11:30:00Z", "2026-11-18T11:30:00Z")))
        with self.assertRaisesRegex(E.Stop, "ENVELOPE_TIME_INVALID"):
            check(make(times=("2026-11-14T11:00:00Z", "2026-13-14T11:30:00Z", "2026-11-15T11:30:00Z")))

    def test_bool_is_not_int(self):
        with self.assertRaisesRegex(E.Stop, "ENVELOPE_SEQUENCE_INVALID"):
            check(resign(make(), lambda r: r.update(sequence=True)))


class AutoWebSimulatorF1Tests(unittest.TestCase):
    """The 19 rows of the contract table §6 (14 from #1010 + 5 added in v1.2)."""

    def run_case(self, raw=None, state="IDLE", now=NOW, expect=None, **events):
        exp = copy.deepcopy(EXPECT)
        exp.update(expect or {})
        return S.tick(state=state, raw=make() if raw is None else raw, expect=exp, now=now, events=events)

    def assertOutcome(self, out, outcome, code=None, spent=False, critical=False, state=None):
        self.assertEqual(out["outcome"], outcome, out)
        if code:
            self.assertIn(code, out["code"])
        self.assertEqual(out["spent"], spent, out)
        self.assertEqual(out["critical"], critical, out)
        if state:
            self.assertEqual(out["state"], state, out)

    def test_01_ui_only_auto_web(self):
        self.assertOutcome(self.run_case(), "auto-web", "ACTIVE", spent=True, state="IDLE")

    def test_02_migration_api_compose_identity_silo_secrets_manual(self):
        for p in ("backend/src/Migrations/20261114_X.cs", "frontend/src/api/lodgeApi.ts",
                  "deploy/qa/docker-compose.yml", "deploy/keycloak/realm.json", "deploy/minio/policy.json",
                  "frontend/.env.qa"):
            with self.subTest(path=p):
                self.assertOutcome(self.run_case(make([F(p)])), "manual")

    def test_03_denylist_or_unknown_binary_manual(self):
        self.assertOutcome(self.run_case(make([F("frontend/src/main.tsx")])), "manual")
        self.assertOutcome(self.run_case(make([F("frontend/public/x.wasm", "added")])), "manual")

    def test_04_not_fast_forward_reject(self):
        self.assertOutcome(self.run_case(make(merge_base="c" * 40)), "reject", "NOT_DESCENDANT")
        self.assertOutcome(self.run_case(make(ahead=0)), "reject", "NOT_DESCENDANT")

    def test_05_truncated_or_limit_manual(self):
        self.assertOutcome(self.run_case(make(complete=False)), "manual", "COMPARISON_INCOMPLETE")
        self.assertOutcome(self.run_case(make(total=9)), "manual", "TOTAL_MISMATCH")

    def test_06_sequence_replay_or_outside_policy_reject(self):
        self.assertOutcome(self.run_case(make(seq=11)), "reject", "SEQUENCE_REPLAY")
        self.assertOutcome(self.run_case(make(seq=15)), "reject", "OUTSIDE_POLICY")

    def test_07_before_not_before_no_op(self):
        self.assertOutcome(self.run_case(now=NOW - dt.timedelta(minutes=45)), "no-op", "NOT_BEFORE")

    def test_08_expired_or_bad_policy_signature_reject(self):
        self.assertOutcome(self.run_case(now=NOW + dt.timedelta(days=2)), "reject", "EXPIRED")
        self.assertOutcome(self.run_case(policy_ok=False), "reject", "POLICY_INVALID")
        self.assertOutcome(self.run_case(attestation_ok=False), "reject", "ATTESTATION_INVALID")

    def test_09_workflow_or_classifier_mismatch_reject(self):
        self.assertOutcome(self.run_case(make(ci={"workflow_ref": "refs/heads/x"})), "reject", "WORKFLOW_PIN")
        self.assertOutcome(self.run_case(make(cls_sha=D("e"))), "reject", "CLASSIFIER_PIN")

    def test_10_image_digest_mismatch_reject(self):
        self.assertOutcome(self.run_case(loaded_digest_ok=False), "reject", "LOADED_DIGEST")

    def test_11_api_compose_identity_silo_drift_reject(self):
        drift = dict(INSTALLED, compose_bytes_sha256=D("f"))
        self.assertOutcome(self.run_case(expect={"installed": drift}), "reject", "INSTALLED_DRIFT")
        self.assertOutcome(self.run_case(invariants_pre_ok=False), "reject", "INVARIANT_DRIFT_PRE")

    def test_12_health_failure_single_rollback_blocked(self):
        self.assertOutcome(self.run_case(health_ok=False), "BLOCKED", "ROLLED_BACK_ONCE", spent=True, state="BLOCKED")
        self.assertOutcome(self.run_case(invariants_post_ok=False), "BLOCKED", "ROLLED_BACK_ONCE", spent=True)

    def test_13_rollback_failure_critical(self):
        self.assertOutcome(self.run_case(health_ok=False, rollback_ok=False), "BLOCKED", "ROLLBACK_FAILED",
                           spent=True, critical=True)

    def test_14_crash_never_retried(self):
        for phase in ("APPLYING", "VERIFYING"):
            self.assertOutcome(self.run_case(crash=phase), "BLOCKED", "RECOVERY_REQUIRED", spent=True)

    def test_15_concurrent_tick_no_op(self):
        self.assertOutcome(self.run_case(lock_busy=True), "no-op", "LOCK_BUSY")

    def test_16_blocked_is_sticky(self):
        self.assertOutcome(self.run_case(state="BLOCKED"), "no-op", "BLOCKED_STICKY", state="BLOCKED")

    def test_17_network_drop_spends_nothing(self):
        self.assertOutcome(self.run_case(network_drop=True), "no-op", "DOWNLOAD_INTERRUPTED")

    def test_18_asset_count_or_size_reject(self):
        self.assertOutcome(self.run_case(asset_count=2), "reject", "ASSET_INVALID")
        self.assertOutcome(self.run_case(asset_oversize=True), "reject", "ASSET_INVALID")

    def test_19_candidate_equals_installed_reject(self):
        out = self.run_case(make(cand={"sha": INST}, ci={"source_sha": INST}))
        self.assertOutcome(out, "reject", "NOT_DESCENDANT")

    def test_image_shape_and_base_drift_are_manual_without_spending(self):
        self.assertOutcome(self.run_case(image_shape_ok=False), "manual", "IMAGE_SHAPE_CHANGED")
        self.assertOutcome(self.run_case(make(cand={"base_image_digests": {"build": D("6"), "final": D("c")}})),
                           "manual", "BASE_IMAGE_DRIFT")

    def test_nothing_but_apply_paths_spend(self):
        """NO QUEMAR: only rows 01, 12, 13, 14 may spend a release slot."""
        quiet = [self.run_case(make([F("frontend/src/main.tsx")])), self.run_case(make(seq=11)),
                 self.run_case(lock_busy=True), self.run_case(state="BLOCKED"),
                 self.run_case(network_drop=True), self.run_case(invariants_pre_ok=False),
                 self.run_case(loaded_digest_ok=False), self.run_case(image_shape_ok=False)]
        self.assertFalse(any(o["spent"] for o in quiet))


if __name__ == "__main__":
    unittest.main()
