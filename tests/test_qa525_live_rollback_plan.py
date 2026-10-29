"""Offline tests for QA525 physically grounded rollback plan; no Docker."""
import copy
import importlib.util
import io
import json
from pathlib import Path
import unittest
from unittest.mock import Mock, patch

ROOT = Path(__file__).resolve().parents[1]
SRC = ROOT / "scripts/qa/operators/qa525-live-rollback-plan.py"
spec = importlib.util.spec_from_file_location("qa525_live_rollback_plan_test", SRC)
mod = importlib.util.module_from_spec(spec)
spec.loader.exec_module(mod)
WORK = "/opt/centenario/qa-lab-firstboot/recovery-v2"
BASE_FILE = WORK + "/compose.recovery.json"
API_LAYER = "/opt/centenario/qa-api-refresh/api-" + "a" * 40 + ".json"
WEB_LAYER = "/opt/centenario/qa-auto-lite/web-" + "c" * 64 + ".json"
IDENTITY = "/etc/pmgm/qa754-identity-overlay.stage.json"
SHA = "a" * 40
ASSET = "sha256:" + "f" * 64
API_OLD = "sha256:" + "b" * 64
WEB_OLD = "sha256:" + "c" * 64
API_NEW = "sha256:" + "d" * 64
WEB_NEW = "sha256:" + "e" * 64


def fixture():
    rows = {}
    baseline = {}
    for i, name in enumerate(mod.SERVICES, start=1):
        cid = f"{i:064x}"
        image = API_OLD if name == "api" else WEB_OLD if name == "web" else "sha256:" + f"{i+9:064x}"
        rows[name] = {
            "container_id": cid, "image_id": image, "status": "running",
            "compose_files_count": 3 if name == "api" else 2 if name == "web" else 1,
        }
        baseline[name] = {
            "container_fingerprint": cid, "image_id": image, "running": True,
        }
    return {
        "inventory": {
            "status": "PHYSICAL_METADATA_CAPTURED_NO_CHANGE_AUTHORITY",
            "scope": "HOST_INVENTORY_READONLY_NO_SECRETS_NO_INSTALL_NO_APPLY",
            "project": "centenario-qa-lab2",
            "working_dir": WORK,
            "services": rows,
            "compose_file_digests": [
                {"path": p, "sha256": str(i) * 64}
                for i, p in enumerate([BASE_FILE, API_LAYER, WEB_LAYER, IDENTITY], 1)
            ],
            "current_git_release_identified": False,
            "identity_verified": False, "may_prepare": False, "may_apply": False,
        },
        "prepared": {
            "status": "PREPARED_API_WEB_SIGNATURE_VERIFIED_NO_DEPLOYMENT",
            "source_sha": SHA, "source_asset_sha256": ASSET,
            "api_image_id": API_NEW, "web_image_id": WEB_NEW,
            "baseline": baseline,
            "images_built": True, "services_unchanged": True,
            "may_apply": False, "fresh_backup_created": False,
        },
        "release": {
            "status": "READY_FOR_SIGNATURE_VERIFICATION_ONLY",
            "source_sha": SHA, "release_tag": "qa-signed-" + SHA,
            "release_asset_sha256": ASSET,
        },
    }


class LiveRollbackPlanTests(unittest.TestCase):
    def assert_blocked(self, data, code):
        with self.assertRaises(mod.Blocked) as ex:
            mod.plan(**data)
        self.assertEqual(code, str(ex.exception))

    def test_exact_synthetic_runtime_reconciles_but_never_authorizes_apply(self):
        a = fixture()
        original = copy.deepcopy(a)
        r = mod.plan(**a)
        self.assertEqual("ROLLBACK_PLAN_STRUCTURALLY_COHERENT_HOST_ATTESTATION_REQUIRED", r["status"])
        self.assertEqual({"api": API_OLD, "web": WEB_OLD}, r["rollback_images"])
        self.assertEqual({"api": API_NEW, "web": WEB_NEW}, r["candidate_images"])
        self.assertEqual(4, len(r["compose_layer_sha256"]))
        self.assertEqual(list(mod.SERVICES[2:]), r["must_preserve_services"])
        self.assertEqual(["db-grants", "migrator", "minio-init"], r["must_preserve_completed_jobs"])
        self.assertEqual(a, original)
        for key in ("may_apply", "host_runtime_rechecked_by_this_tool",
                    "backup_offhost_verified", "restore_drill_verified",
                    "sigstore_download_verified", "migration_review_approved",
                    "single_use_supervisor_approval_verified"):
            self.assertIs(r[key], False)

    def test_does_not_trust_claims_of_identity_or_apply_authorization(self):
        for field in ("identity_verified", "may_apply", "may_prepare"):
            a = fixture()
            a["inventory"][field] = True
            self.assert_blocked(a, "HOST_INVENTORY_CONTRACT_INVALID")

    def test_requires_seven_services_and_unique_container_ids(self):
        a = fixture()
        a["inventory"]["services"].pop("keycloak")
        self.assert_blocked(a, "SEVEN_SERVICE_INVENTORY_INCOMPLETE")
        a = fixture()
        a["inventory"]["services"]["keycloak"]["container_id"] = a["inventory"]["services"]["postgres"]["container_id"]
        self.assert_blocked(a, "DUPLICATE_DOCKER_CONTAINER_ID")

    def test_does_not_treat_finished_one_shots_as_active_api_web(self):
        a = fixture()
        a["inventory"]["services"]["db-grants"] = {
            "container_id": "9" * 64,
            "image_id": "sha256:" + "9" * 64,
            "status": "exited", "compose_files_count": 1,
        }
        self.assert_blocked(a, "SEVEN_SERVICE_INVENTORY_INCOMPLETE")

    def test_stopped_services_block(self):
        a = fixture()
        a["inventory"]["services"]["postgres"]["status"] = "exited"
        self.assert_blocked(a, "CONTAINER_METADATA_INVALID")

    def test_legacy_api_identity_layer_is_preserved_without_claiming_git_sha(self):
        r = mod.plan(**fixture())
        self.assertFalse(r["host_runtime_rechecked_by_this_tool"])
        self.assertEqual("2" * 64, r["compose_layer_sha256"]["api"])
        self.assertEqual("4" * 64, r["compose_layer_sha256"]["identity"])

    def test_wrong_or_extra_identity_layer_blocks(self):
        for name in ("/etc/pmgm/master.env",
                     "/opt/centenario/qa754-identity-overlay.stage.json"):
            a = fixture()
            a["inventory"]["compose_file_digests"][-1]["path"] = name
            self.assert_blocked(a, "REQUIRED_API_WEB_IDENTITY_LAYERS_MISSING" if "master" in name else "IDENTITY_OVERLAY_CHANGED")

    def test_other_api_overlay_directory_blocks(self):
        a = fixture()
        a["inventory"]["compose_file_digests"][1]["path"] = WORK + "/api-" + SHA + ".json"
        self.assert_blocked(a, "UNEXPECTED_API_OVERRIDE_PARENT")

    def test_web_override_must_match_live_web_image(self):
        a = fixture()
        a["inventory"]["services"]["web"]["image_id"] = "sha256:" + "9" * 64
        a["prepared"]["baseline"]["web"]["image_id"] = "sha256:" + "9" * 64
        self.assert_blocked(a, "WEB_OVERRIDE_DOES_NOT_MATCH_IMAGE")

    def test_missing_or_unverified_compose_digest_fails_closed(self):
        a = fixture()
        a["inventory"]["compose_file_digests"][0]["sha256"] = "not-sha"
        self.assert_blocked(a, "COMPOSE_DIGEST_INVALID")

    def test_compose_paths_can_never_include_traversal(self):
        a = fixture()
        a["inventory"]["compose_file_digests"][1]["path"] = "/opt/centenario/../evil/api-" + SHA + ".json"
        self.assert_blocked(a, "UNTRUSTED_COMPOSE_PATH")

    def test_sensitive_source_sha_or_asset_mismatch_blocks(self):
        for target, field in (("release", "source_sha"), ("prepared", "source_sha"),
                              ("prepared", "source_asset_sha256")):
            a = fixture()
            a[target][field] = "0" * 40 if field == "source_sha" else "sha256:" + "0" * 64
            code = "SIGNED_RELEASE_METADATA_INVALID" if target == "release" else "PREPARED_CUT_NOT_COHERENT"
            self.assert_blocked(a, code)

    def test_container_drift_between_prepare_and_inventory_blocks(self):
        for name in mod.SERVICES:
            a = fixture()
            a["inventory"]["services"][name]["container_id"] = "8" * 64
            self.assert_blocked(a, "PHYSICAL_BASELINE_DRIFT")

    def test_new_image_must_differ_from_baseline(self):
        for key, old in (("api_image_id", API_OLD), ("web_image_id", WEB_OLD)):
            a = fixture()
            a["prepared"][key] = old
            self.assert_blocked(a, "NEW_IMAGES_NOT_DISTINCT")

    def test_prepared_staged_receipt_cannot_claim_fresh_backup(self):
        a = fixture()
        a["prepared"]["fresh_backup_created"] = True
        self.assert_blocked(a, "PREPARED_CUT_NOT_COHERENT")

    def test_errors_never_echo_untrusted_input_or_secrets(self):
        stdin = Mock()
        stdin.buffer.read.return_value = b'{"SMTP_SECRET":"DONOTLEAK"}'
        out = io.StringIO()
        with patch.object(mod.sys, "stdin", stdin), patch("sys.stdout", out):
            self.assertEqual(2, mod.main([]))
        self.assertIn("BLOCKED_QA525_ROLLBACK_PLAN", out.getvalue())
        self.assertNotIn("DONOTLEAK", out.getvalue())

    def test_no_shell_or_host_mutation_in_code(self):
        source = SRC.read_text()
        for banned in ("subprocess", "os.system(", "docker", "systemctl",
                       "pg_dump", "exec(", "importlib.util"):
            self.assertNotIn(banned, source)
        self.assertIn('"may_apply": False', source)


if __name__ == "__main__":
    unittest.main()
