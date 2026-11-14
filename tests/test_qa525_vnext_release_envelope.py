"""Hermetic tests: release classifier + envelope checker. No signature, host or network."""
import copy
import datetime as dt
import hashlib
import importlib.util
import json
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]


def load(name):
    spec = importlib.util.spec_from_file_location("t_" + name.replace("-", "_"),
                                                  ROOT / "scripts/qa" / (name + ".py"))
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


C = load("qa525-vnext-release-classifier")
E = load("qa525-vnext-release-envelope")
CLS_SHA = "sha256:" + hashlib.sha256((ROOT / "scripts/qa/qa525-vnext-release-classifier.py").read_bytes()).hexdigest()
INST, CAND = "a" * 40, "b" * 40
IMG_NEW, IMG_OLD = "sha256:" + "1" * 64, "sha256:" + "2" * 64
WF = "owner/repo/.github/workflows/qa-release-envelope.yml"
NOW = dt.datetime(2026, 11, 14, 12, 0, 0, tzinfo=dt.timezone.utc)
PINS = {"installed_sha": INST, "classifier_sha256": CLS_SHA, "signer_workflow": WF,
        "source_repository_id": "1356543647"}
UI = [{"path": "frontend/src/LodgeManagementPage.tsx", "status": "modified"},
      {"path": "frontend/src/home-consistency.css", "status": "modified"},
      {"path": "frontend/public/brand/logo.svg", "status": "added"}]


def make(files=UI, **over):
    args = dict(installed_sha=INST, candidate_sha=CAND, merge_base_sha=INST, ahead_by=3,
                comparison_complete=True, files=files, classifier=C, classifier_sha256=CLS_SHA,
                web_image_digest=IMG_NEW, rollback_web_image_digest=IMG_OLD, sequence=5,
                not_before="2026-11-14T11:30:00Z", expires_at="2026-11-14T23:30:00Z",
                signer_workflow=WF, source_repository_id="1356543647")
    args.update(over)
    return E.build(**args)


def chk(raw, **over):
    kw = dict(pins=PINS, classifier=C, now=NOW, last_sequence=4)
    kw.update(over)
    return E.check(raw, **kw)


class ClassifierTests(unittest.TestCase):
    def cat(self, files, complete=True):
        return C.classify(files, complete=complete)

    def test_ui_only_is_auto_web(self):
        self.assertEqual(self.cat(UI)["category"], "auto-web")

    def test_sensitive_paths_force_manual(self):
        for p in ("frontend/src/main.tsx", "frontend/src/api/calendarApi.ts",
                  "frontend/src/accounts/accountLifecycle.ts", "frontend/package-lock.json",
                  "frontend/package.json", "frontend/index.html", "frontend/nginx.conf",
                  "frontend/Dockerfile", "frontend/.env.example", "frontend/src/vite-env.d.ts",
                  "frontend/src/authSession.ts", "frontend/public/sw.js", "frontend/public/manifest.webmanifest",
                  "backend/src/PMGM.Api/Program.cs", ".github/workflows/ci.yml",
                  "scripts/qa/qa525-vnext-release-classifier.py", "scripts/qa/qa525-vnext-root-launcher.py",
                  "backend/src/PMGM.Api/Migrations/2026_x.cs", "docs/x.md"):
            r = self.cat(UI + [{"path": p, "status": "modified"}])
            self.assertEqual(r["category"], "manual", p)

    def test_interval_with_migration_among_ui_is_manual(self):
        r = self.cat(UI + [{"path": "backend/src/PMGM.Infrastructure/Migrations/1.cs", "status": "added"}])
        self.assertEqual(r["category"], "manual")

    def test_incomplete_empty_toomany_are_manual(self):
        self.assertEqual(self.cat(UI, complete=False)["reasons"], ["COMPARISON_INCOMPLETE"])
        self.assertEqual(self.cat([])["category"], "manual")
        many = [{"path": "frontend/src/a%d.tsx" % i, "status": "added"} for i in range(301)]
        self.assertEqual(self.cat(many)["reasons"], ["TOO_MANY_FILES"])

    def test_path_tricks_and_unknown_status(self):
        for p in ("frontend/src/../../backend/x.tsx", "/frontend/src/a.tsx", "frontend/src//a.tsx",
                  "frontend/src/a b.tsx", "frontend/src/é.tsx", "frontend/src/a.tsx\n"):
            self.assertEqual(self.cat([{"path": p, "status": "modified"}])["category"], "manual", repr(p))
        self.assertEqual(self.cat([{"path": "frontend/src/a.tsx", "status": "copied"}])["category"], "manual")

    def test_rename_checks_both_names(self):
        ok = {"path": "frontend/src/B.tsx", "status": "renamed", "previous_filename": "frontend/src/A.tsx"}
        bad = {"path": "frontend/src/B.tsx", "status": "renamed", "previous_filename": "frontend/src/api/A.ts"}
        self.assertEqual(self.cat([ok])["category"], "auto-web")
        self.assertEqual(self.cat([bad])["category"], "manual")
        self.assertEqual(self.cat([{"path": "frontend/src/B.tsx", "status": "modified",
                                    "previous_filename": "frontend/src/A.tsx"}])["category"], "manual")

    def test_normal_names_with_main_or_config_words_are_not_denied(self):
        for p in ("frontend/src/MainMenuHelp.tsx", "frontend/src/SystemConfigurationPage.tsx",
                  "frontend/src/SystemConfigurationPage.test.ts"):
            self.assertEqual(self.cat([{"path": p, "status": "modified"}])["category"], "auto-web", p)


class EnvelopeTests(unittest.TestCase):
    def test_ok(self):
        r = chk(make())
        self.assertEqual((r["status"], r["category"], r["sequence"]), (E.STATUS, "auto-web", 5))

    def test_manual_envelope_is_accepted_as_manual_only(self):
        r = chk(make(UI + [{"path": "backend/a.cs", "status": "modified"}]))
        self.assertEqual(r["category"], "manual")

    def stop(self, raw, code, **over):
        with self.assertRaises(E.Stop) as ctx:
            chk(raw, **over)
        self.assertEqual(str(ctx.exception), code)

    def test_replay_and_rollback(self):
        self.stop(make(sequence=4), "ENVELOPE_SEQUENCE_REPLAY")
        self.stop(make(sequence=3), "ENVELOPE_SEQUENCE_REPLAY")
        self.stop(make(merge_base_sha="c" * 40), "ENVELOPE_NOT_DESCENDANT")
        self.stop(make(candidate_sha=INST), "ENVELOPE_NOT_DESCENDANT")
        self.stop(make(ahead_by=0), "ENVELOPE_COMPARISON_INCOMPLETE")
        self.stop(make(), "ENVELOPE_NOT_BASED_ON_INSTALLED", pins=dict(PINS, installed_sha="d" * 40))

    def test_pins(self):
        self.stop(make(), "ENVELOPE_CLASSIFIER_PIN", pins=dict(PINS, classifier_sha256="sha256:" + "9" * 64))
        self.stop(make(), "ENVELOPE_WORKFLOW_PIN", pins=dict(PINS, signer_workflow="o/r/.github/workflows/x.yml"))
        self.stop(make(), "ENVELOPE_REPOSITORY_PIN", pins=dict(PINS, source_repository_id="1"))

    def test_window(self):
        self.stop(make(not_before="2026-11-14T12:00:01Z"), "ENVELOPE_NOT_IN_WINDOW")
        self.stop(make(expires_at="2026-11-14T12:00:00Z"), "ENVELOPE_NOT_IN_WINDOW")
        self.stop(make(expires_at="2026-11-16T12:00:00Z"), "ENVELOPE_NOT_IN_WINDOW")
        self.stop(make(not_before="2026-11-14 11:30:00"), "ENVELOPE_TIME_INVALID")

    def test_host_reclassifies_and_ignores_ci_claim(self):
        env = json.loads(make(UI + [{"path": "backend/a.cs", "status": "modified"}]))
        env["category"], env["reasons"] = "auto-web", []
        self.stop(E.canonical(env), "ENVELOPE_CLASSIFICATION_MISMATCH")

    def test_incomplete_comparison_cannot_be_forged_complete(self):
        env = json.loads(make())
        env["comparison_complete"] = False
        self.stop(E.canonical(env), "ENVELOPE_COMPARISON_INCOMPLETE")

    def test_same_image_is_refused_for_auto(self):
        self.stop(make(web_image_digest=IMG_OLD), "ENVELOPE_IMAGE_NOT_NEW")

    def test_canonical_and_fields(self):
        env = json.loads(make())
        extra = dict(env, extra=1)
        self.stop(E.canonical(extra), "ENVELOPE_NOT_CANONICAL_OR_FIELDS")
        self.stop(json.dumps(env, indent=1).encode() + b"\n", "ENVELOPE_NOT_CANONICAL_OR_FIELDS")
        dup = make().replace(b'"ahead_by":3', b'"ahead_by":3,"ahead_by":3')
        self.stop(dup, "ENVELOPE_DUPLICATE_KEY")
        self.stop(b"", "ENVELOPE_BYTES_INVALID")
        self.stop(b"\xff", "ENVELOPE_JSON_INVALID")
        self.stop(E.canonical(dict(env, host="other")), "ENVELOPE_SCOPE")
        self.stop(E.canonical(dict(env, installed_sha="XYZ")), "ENVELOPE_SHA_INVALID")
        self.stop(E.canonical(dict(env, web_image_digest="sha256:zz")), "ENVELOPE_DIGEST_INVALID")

    def test_bool_is_not_int(self):
        env = json.loads(make())
        self.stop(E.canonical(dict(env, ahead_by=True)), "ENVELOPE_COMPARISON_INCOMPLETE")
        self.stop(E.canonical(dict(env, sequence=True)), "ENVELOPE_SEQUENCE_REPLAY", last_sequence=0)


if __name__ == "__main__":
    unittest.main()
