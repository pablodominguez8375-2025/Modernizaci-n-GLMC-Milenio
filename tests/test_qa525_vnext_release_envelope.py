"""Hermetic tests: auto-web classifier (F4-A), envelope v1.3, CI producer and F1 simulator.

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
ARGS = json.loads((ROOT / "ops/qa525/autoweb/web-build-args.json").read_text())
ARGS_SHA = E.build_args_sha256(ARGS)
LAYERS = [D(c) for c in "bcd"] + [D("e"), D("f")]  # 3 base (nginx + conf) + 2 content (dist, chmod)


def image_config(layers=LAYERS, **cfg):
    config = {"Cmd": ["nginx", "-g", "daemon off;"], "Entrypoint": ["/docker-entrypoint.sh"],
              "Env": ["PATH=/usr/sbin:/usr/bin"], "ExposedPorts": {"80/tcp": {}},
              "Healthcheck": {"Test": ["CMD-SHELL", "wget -qO- http://127.0.0.1/health/web"]},
              "StopSignal": "SIGQUIT", "User": "", "Volumes": None, "WorkingDir": "",
              "Labels": {"maintainer": "NGINX Docker Maintainers"}}
    config.update(cfg)
    return {"Id": D("8"), "Architecture": "amd64", "Os": "linux", "Config": config,
            "RootFS": {"Type": "layers", "Layers": list(layers)}}


BINDING = E.image_binding(image_config(), 2)
NOW = dt.datetime(2026, 11, 14, 12, 0, 0, tzinfo=dt.timezone.utc)
KIND = {".png": "png", ".jpg": "jpeg", ".jpeg": "jpeg", ".webp": "webp", ".woff2": "woff2", ".ico": "ico"}


def F(p, st="modified", **kw):
    kind = "none" if st == "removed" else KIND.get(p[p.rfind("."):], "unknown")
    return dict({"path": p, "status": st, "mode": "100644", "content_kind": kind}, **kw)


UI = sorted([F("frontend/src/assets/lodge-banner.webp"), F("frontend/src/fonts/serif.woff2"),
             F("frontend/public/brand/logo.png", "added")], key=lambda e: e["path"])
EXPECT = {"repository": REPO, "source_repository_id": REPO_ID, "installed": dict(INSTALLED),
          "classifier_sha256": CLS_SHA, "workflow": WF, "workflow_ref": REF, "subject": SUBJECT,
          "allowed_base_image_digests": dict(BASES), "build_args_sha256": ARGS_SHA,
          "oci_shape_sha256": BINDING["oci_shape_sha256"],
          "base_diff_ids_sha256": BINDING["base_diff_ids_sha256"], "content_layer_count": 2,
          "first_sequence": 10, "last_sequence": 14, "last_applied_sequence": 11}


def make(files=UI, *, complete=True, total=None, seq=12, cand=None, installed=None,
         ci=None, times=("2026-11-14T11:00:00Z", "2026-11-14T11:30:00Z", "2026-11-15T11:30:00Z"),
         merge_base=INST, ahead=3, cls_sha=CLS_SHA):
    candidate = {"sha": CAND, "web_image_digest": D("8"), "web_image_archive_sha256": D("9"),
                 "web_sbom_sha256": D("a"), "base_image_digests": dict(BASES),
                 "build_args_sha256": ARGS_SHA, **BINDING}
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

    def test_static_assets_only_are_auto_web_f4a(self):
        self.assertEqual(self.res(UI), {"version": 3, "result": "auto-web",
                                        "reasons": ["ALLOWLIST_ONLY", "F4A_STATIC_NON_EXECUTABLE"]})

    def test_f4a_executable_or_active_content_is_manual(self):
        for p in ("frontend/src/LodgeManagementPage.tsx", "frontend/src/home-consistency.css",
                  "frontend/src/icons/star.svg", "frontend/public/brand/logo.svg", "frontend/src/util.ts"):
            with self.subTest(path=p):
                self.assertIn("F4A_ACTIVE_CONTENT_MANUAL", self.res([F(p)])["reasons"])

    def test_content_kind_must_match_extension(self):
        fake = F("frontend/src/assets/photo.png", content_kind="unknown")
        self.assertIn("CONTENT_KIND_MISMATCH", self.res([fake])["reasons"])
        jpeg_as_png = F("frontend/src/assets/photo.png", content_kind="jpeg")
        self.assertIn("CONTENT_KIND_MISMATCH", self.res([jpeg_as_png])["reasons"])
        self.assertIn("F4A_REMOVAL_MANUAL", self.res([F("frontend/src/assets/old.png", "removed")])["reasons"])

    def test_p2_coverage_denied_directory_jpeg_header_and_top_ico(self):
        """The 3 mutants the independent review found uncovered."""
        for p in ("frontend/src/api/logo.png", "frontend/src/auth/x.png", "frontend/src/Accounts/y.png"):
            with self.subTest(path=p):
                self.assertIn("DENIED_DIRECTORY", self.res([F(p)])["reasons"])
        self.assertEqual(C.sniff(b"\xff\xd8" + b"0" * 10), "unknown")  # needs the 3rd 0xFF byte
        self.assertEqual(C.sniff(b"\xff\xd8\xff"), "jpeg")
        self.assertIn("EXTENSION_NOT_ALLOWED", self.res([F("frontend/public/favicon.ico")])["reasons"])
        self.assertEqual(self.res([F("frontend/public/brand/favicon.ico")])["result"], "auto-web")

    def test_sniff_magic_numbers(self):
        self.assertEqual(C.sniff(b"\x89PNG\r\n\x1a\n" + b"0" * 8), "png")
        self.assertEqual(C.sniff(b"\xff\xd8\xff\xe0" + b"0" * 8), "jpeg")
        self.assertEqual(C.sniff(b"RIFF\x00\x00\x00\x00WEBPVP8 "), "webp")
        self.assertEqual(C.sniff(b"wOF2" + b"0" * 8), "woff2")
        self.assertEqual(C.sniff(b"\x00\x00\x01\x00" + b"0" * 8), "ico")
        for data in (b"<svg onload='x'>", b"GIF89a", b"", "png", b"\x89PNG" + b"0" * (3 * 1024 * 1024)):
            self.assertEqual(C.sniff(data), "unknown")

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
        cases = [F("frontend/src/b.png", "renamed", previous_path="frontend/src/a.png"),
                 F("frontend/src/link.png", mode="120000"), F("frontend/src/vendor", mode="160000"),
                 F("frontend/src/tool.png", mode="100755"), F("frontend/public/lib.wasm", "added")]
        for entry in cases:
            with self.subTest(entry=entry):
                self.assertEqual(self.res([entry])["result"], "manual")

    def test_brand_removal_is_manual_but_addition_is_auto(self):
        self.assertIn("BRAND_REMOVAL", self.res([F("frontend/public/brand/logo.png", "removed")])["reasons"])
        self.assertEqual(self.res([F("frontend/public/brand/logo-v3.png", "added")])["result"], "auto-web")

    def test_truncated_total_mismatch_unsorted_and_limit(self):
        self.assertIn("COMPARISON_INCOMPLETE", self.res(UI, complete=False)["reasons"])
        self.assertIn("TOTAL_MISMATCH", self.res(UI, total=4)["reasons"])
        self.assertIn("FILES_NOT_SORTED_UNIQUE", self.res(list(reversed(UI)))["reasons"])
        many = [F("frontend/src/assets/p%03d.png" % i) for i in range(300)]
        self.assertIn("TOO_MANY_FILES", self.res(many)["reasons"])

    def test_malformed_entries(self):
        for entry in ({"path": "frontend/src/a.png", "status": "modified", "mode": "100644"},
                      F("frontend/src/a.png", extra=1), F("frontend/src/../x.png"), F("/etc/passwd"),
                      F("frontend/src/a.png", content_kind="none"), F("frontend/src/a.png", content_kind="gif")):
            with self.subTest(entry=entry):
                self.assertEqual(self.res([entry])["result"], "manual")


class AutoWebEnvelopeTests(unittest.TestCase):
    def test_valid_auto_web_candidate(self):
        out = check(make())
        self.assertEqual(out["decision"], E.AUTO_WEB_CANDIDATE)
        self.assertEqual(out["rollback_web_image_digest"], INSTALLED["web_image_digest"])
        self.assertEqual(out["contract"], "1.3")

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

    def test_build_args_exact_seven_keys_and_rules(self):
        self.assertEqual(set(ARGS), E.BUILD_ARG_KEYS)
        dockerfile = (ROOT / "frontend/Dockerfile").read_text()
        self.assertEqual({line.split()[1].split("=")[0] for line in dockerfile.splitlines()
                          if line.startswith("ARG ")}, E.BUILD_ARG_KEYS)
        with self.assertRaisesRegex(E.Stop, "BUILD_ARGS_MOCKS_FORBIDDEN"):
            E.build_args_sha256(dict(ARGS, VITE_USE_MOCKS="true"))
        for bad in ({k: v for k, v in ARGS.items() if k != "VITE_OIDC_ALLOW_HTTP_QA"},
                    dict(ARGS, VITE_EXTRA="1"), dict(ARGS, VITE_OIDC_SCOPE=None)):
            with self.assertRaisesRegex(E.Stop, "BUILD_ARGS_INVALID"):
                E.build_args_sha256(bad)
        for key, value in (("VITE_API_BASE_URL", "https://evil"), ("VITE_OIDC_AUTHORITY", "http://x"),
                           ("VITE_OIDC_ALLOW_HTTP_LOCAL", "true"), ("VITE_OIDC_CLIENT_ID", "other")):
            with self.assertRaisesRegex(E.Stop, "BUILD_ARGS_IDENTITY_CHANGED"):
                E.build_args_sha256(dict(ARGS, **{key: value}))
        other = E.build_args_sha256(dict(ARGS, VITE_OIDC_ALLOW_HTTP_QA="true"))
        with self.assertRaisesRegex(E.Stop, "ENVELOPE_BUILD_ARGS"):
            check(make(cand={"build_args_sha256": other}))

    def test_base_images_shape_and_layers(self):
        out = check(make(cand={"base_image_digests": {"build": D("6"), "final": D("c")}}))
        self.assertEqual((out["decision"], out["reasons"]), (E.MANUAL, ["BASE_IMAGE_DRIFT"]))
        shaped = E.image_binding(image_config(User="root"), 2)
        out = check(make(cand=shaped))
        self.assertEqual((out["decision"], out["reasons"]), (E.MANUAL, ["OCI_SHAPE_CHANGED"]))
        rebased = E.image_binding(image_config(layers=[D("9")] + LAYERS[1:]), 2)
        out = check(make(cand=rebased))
        self.assertEqual((out["decision"], out["reasons"]), (E.MANUAL, ["BASE_LAYERS_DRIFT"]))

    def test_content_layers_change_rootfs_but_not_base(self):
        new_content = E.image_binding(image_config(layers=LAYERS[:3] + [D("0"), D("1")]), 2)
        self.assertEqual(new_content["base_diff_ids_sha256"], BINDING["base_diff_ids_sha256"])
        self.assertNotEqual(new_content["rootfs_diff_ids_sha256"], BINDING["rootfs_diff_ids_sha256"])

    def test_verify_loaded_image(self):
        decision = check(make())
        self.assertTrue(E.verify_loaded_image(decision, image_config(), 2))
        for bad in (image_config(layers=LAYERS[:3] + [D("0"), D("1")]), image_config(Cmd=["sh"]),
                    {"Config": {}}, None):
            with self.subTest(bad=str(bad)[:40]):
                with self.assertRaises(E.Stop):
                    E.verify_loaded_image(decision, bad, 2)

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

    def test_p2_shape_includes_labels_shell_onbuild_and_rejects_ambiguous(self):
        for change in ({"Labels": {"traefik.enable": "true"}}, {"Shell": ["/bin/bash", "-c"]},
                       {"OnBuild": ["RUN id"]}):
            with self.subTest(change=change):
                self.assertNotEqual(E.image_binding(image_config(**change), 2)["oci_shape_sha256"],
                                    BINDING["oci_shape_sha256"])
        both = dict(image_config(), config={"Entrypoint": ["/bin/sh"]})
        with self.assertRaisesRegex(E.Stop, "IMAGE_CONFIG_AMBIGUOUS"):
            E.image_binding(both, 2)
        nested = image_config()
        nested["RootFS"]["diff_ids"] = list(LAYERS)
        with self.assertRaisesRegex(E.Stop, "IMAGE_CONFIG_AMBIGUOUS"):
            E.image_binding(nested, 2)

    def test_p2_issued_at_lower_bound(self):
        with self.assertRaisesRegex(E.Stop, "ENVELOPE_WINDOW_INVALID"):
            check(make(times=("2000-01-01T00:00:00Z", "2026-11-14T11:30:00Z", "2026-11-15T11:30:00Z")))
        with self.assertRaisesRegex(E.Stop, "ENVELOPE_WINDOW_INVALID"):
            check(make(times=("2026-11-14T11:00:00Z", "2026-11-14T11:30:00Z", "2026-11-15T11:30:00Z")),
                  now=dt.datetime(2026, 11, 15, 11, 0, 0, tzinfo=dt.timezone.utc) + dt.timedelta(days=7))

    def test_sequence_must_be_exactly_next(self):
        for seq in (13, 14):
            with self.subTest(seq=seq):
                with self.assertRaisesRegex(E.Stop, "ENVELOPE_SEQUENCE_GAP"):
                    check(make(seq=seq))

    def test_bool_is_not_int(self):
        with self.assertRaisesRegex(E.Stop, "ENVELOPE_SEQUENCE_INVALID"):
            check(resign(make(), lambda r: r.update(sequence=True)))


class AutoWebSimulatorF1Tests(unittest.TestCase):
    """The 19 rows of the contract table §6 (14 from #1010 + 5 added in v1.2)."""

    def run_case(self, raw=None, state="IDLE", now=NOW, expect=None, **events):
        exp = copy.deepcopy(EXPECT)
        exp.update(expect or {})
        cfg = events.pop("image_config", image_config())
        return S.tick(state=state, raw=make() if raw is None else raw, expect=exp, now=now,
                      image_config=cfg, events=events)

    def assertOutcome(self, out, outcome, code=None, spent=False, critical=False, state=None):
        self.assertEqual(out["outcome"], outcome, out)
        if code:
            self.assertIn(code, out["code"])
        self.assertEqual(out["spent"], spent, out)
        self.assertEqual(out["critical"], critical, out)
        if state:
            self.assertEqual(out["state"], state, out)

    def test_01_static_assets_only_auto_web(self):
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

    def test_06_sequence_replay_gap_or_outside_policy_reject(self):
        self.assertOutcome(self.run_case(make(seq=11)), "reject", "SEQUENCE_REPLAY")
        self.assertOutcome(self.run_case(make(seq=13)), "reject", "SEQUENCE_GAP")
        self.assertOutcome(self.run_case(make(seq=15), expect={"last_applied_sequence": 14}), "reject",
                           "OUTSIDE_POLICY")

    def test_07_before_not_before_no_op(self):
        self.assertOutcome(self.run_case(now=NOW - dt.timedelta(minutes=45)), "no-op", "NOT_BEFORE")

    def test_08_expired_or_bad_policy_signature_reject(self):
        self.assertOutcome(self.run_case(now=NOW + dt.timedelta(days=2)), "reject", "EXPIRED")
        self.assertOutcome(self.run_case(policy_ok=False), "reject", "POLICY_INVALID")
        self.assertOutcome(self.run_case(attestation_ok=False), "reject", "ATTESTATION_INVALID")

    def test_09_workflow_or_classifier_mismatch_reject(self):
        self.assertOutcome(self.run_case(make(ci={"workflow_ref": "refs/heads/x"})), "reject", "WORKFLOW_PIN")
        self.assertOutcome(self.run_case(make(cls_sha=D("e"))), "reject", "CLASSIFIER_PIN")

    def test_10_image_digest_or_loaded_image_mismatch_reject(self):
        self.assertOutcome(self.run_case(loaded_digest_ok=False), "reject", "LOADED_DIGEST")
        other = image_config(layers=LAYERS[:3] + [D("0"), D("1")])
        self.assertOutcome(self.run_case(image_config=other), "reject", "LOADED_IMAGE_MISMATCH")
        self.assertOutcome(self.run_case(image_config=None), "reject", "IMAGE_CONFIG_INVALID")

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
        shaped = E.image_binding(image_config(User="root"), 2)
        self.assertOutcome(self.run_case(make(cand=shaped)), "manual", "OCI_SHAPE_CHANGED")
        self.assertOutcome(self.run_case(make(cand={"base_image_digests": {"build": D("6"), "final": D("c")}})),
                           "manual", "BASE_IMAGE_DRIFT")
        self.assertOutcome(self.run_case(make([F("frontend/src/Page.tsx")])), "manual", "F4A_ACTIVE_CONTENT")

    def test_nothing_but_apply_paths_spend(self):
        """NO QUEMAR: only rows 01, 12, 13, 14 may spend a release slot."""
        quiet = [self.run_case(make([F("frontend/src/main.tsx")])), self.run_case(make(seq=11)),
                 self.run_case(lock_busy=True), self.run_case(state="BLOCKED"),
                 self.run_case(network_drop=True), self.run_case(invariants_pre_ok=False),
                 self.run_case(loaded_digest_ok=False), self.run_case(make(seq=13)),
                 self.run_case(image_config=image_config(Cmd=["sh"]))]
        self.assertFalse(any(o["spent"] for o in quiet))


if __name__ == "__main__":
    unittest.main()
