"""Hermetic metadata-gate tests. No GitHub traffic, Docker, host or secrets."""
import copy
import importlib.util
from pathlib import Path
import unittest
from unittest.mock import patch

FILE = Path(__file__).resolve().parents[1] / "scripts/qa/qa525-current-release-gate.py"
spec = importlib.util.spec_from_file_location("qa525_current_release_gate", FILE)
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)

SHA = "a" * 40
DIGEST = "sha256:" + "b" * 64
TAG = "qa-signed-" + SHA
RUN_PATH = "/actions/runs?head_sha=" + SHA + "&per_page=100"


def fixture():
    runs = [{
        "name": name, "id": 1000 + n, "run_attempt": 1,
        "head_sha": SHA, "head_branch": "dev", "event": "push",
        "status": "completed", "conclusion": "success",
    } for n, name in enumerate(module.readiness.REQUIRED)]
    publisher = {
        "name": module.PUBLISH_WORKFLOW, "id": 2010, "run_attempt": 1,
        "head_sha": SHA, "head_branch": "dev", "event": "push",
        "status": "completed", "conclusion": "success",
    }
    silo = next(r for r in runs if r["name"] == "QA Silo supply and recovery")
    release = {
        "tag_name": TAG, "target_commitish": SHA,
        "draft": False, "prerelease": True,
        "assets": [{"name": TAG + ".zip", "digest": DIGEST,
                    "size": 2048, "id": 501}],
    }
    db = {
        "/branches/dev": {"commit": {"sha": SHA}},
        RUN_PATH: {"workflow_runs": runs + [publisher]},
        "/actions/runs/" + str(silo["id"]) + "/artifacts?per_page=100": {
            "artifacts": [{
                "name": module.readiness.ARTIFACT_PREFIX + SHA, "id": 400,
                "expired": False, "size_in_bytes": 2048, "digest": DIGEST,
            }]},
        "/releases/tags/" + TAG: release,
    }
    return db, release, publisher


def fetch_from(db):
    def fetch(path):
        return copy.deepcopy(db[path])
    return fetch


class CurrentReleaseGateTests(unittest.TestCase):
    def test_good_release_is_only_ready_to_verify_signature(self):
        db, _, _ = fixture()
        status = module.evaluate(fetch_from(db))
        self.assertEqual("READY_FOR_SIGNATURE_VERIFICATION_ONLY", status["status"])
        self.assertEqual(module.RESULT_SCOPE, status["scope"])
        self.assertEqual(SHA, status["source_sha"])
        self.assertEqual(DIGEST, status["release_asset_sha256"])
        self.assertEqual(501, status["release_asset_id"])
        self.assertNotIn("secret", str(status).lower())

    def test_rejects_publisher_in_progress_or_failed(self):
        for state, conclusion in (("in_progress", None), ("completed", "failure"),
                                  ("completed", "cancelled")):
            with self.subTest(state=state, conclusion=conclusion):
                db, _, publisher = fixture()
                db[RUN_PATH]["workflow_runs"][-1].update(
                    status=state, conclusion=conclusion)
                with self.assertRaisesRegex(module.Blocked, "PUBLISH_WORKFLOW_NOT_SUCCESS"):
                    module.evaluate(fetch_from(db))

    def test_rejects_wrong_source_tag_or_release_mode(self):
        for field, value in (("tag_name", "qa-signed-" + "c"*40),
                             ("target_commitish", "c"*40),
                             ("prerelease", False), ("draft", True)):
            with self.subTest(field=field):
                db, release, _ = fixture()
                release[field] = value
                with self.assertRaisesRegex(module.Blocked, "RELEASE_SOURCE_OR_STATUS_MISMATCH"):
                    module.evaluate(fetch_from(db))

    def test_rejects_tampered_digest_size_id_and_extra_assets(self):
        for field, value in (("digest", "sha256:" + "c"*64),
                             ("size", 0), ("id", "untrusted")):
            with self.subTest(field=field):
                db, release, _ = fixture()
                release["assets"][0][field] = value
                with self.assertRaisesRegex(module.Blocked, "RELEASE_ASSET_DIGEST_OR_SIZE_MISMATCH"):
                    module.evaluate(fetch_from(db))
        db, release, _ = fixture()
        release["assets"].append(copy.deepcopy(release["assets"][0]))
        with self.assertRaisesRegex(module.Blocked, "RELEASE_ASSET_NOT_UNIQUE"):
            module.evaluate(fetch_from(db))
        db, release, _ = fixture()
        release["assets"].append({"name": "unexpected.zip", "size": 1})
        with self.assertRaisesRegex(module.Blocked, "RELEASE_ASSET_NOT_UNIQUE"):
            module.evaluate(fetch_from(db))

    def test_rejects_old_or_missing_required_gate(self):
        db, _, _ = fixture()
        db[RUN_PATH]["workflow_runs"][0]["conclusion"] = "failure"
        with self.assertRaises(module.readiness.GateFailure):
            module.evaluate(fetch_from(db))
        db, _, _ = fixture()
        db[RUN_PATH]["workflow_runs"][0]["head_branch"] = "main"
        with self.assertRaises(module.readiness.GateFailure):
            module.evaluate(fetch_from(db))

    def test_rejects_duplicate_wrong_or_missing_silo_artifact(self):
        db, _, _ = fixture()
        path = next(k for k in db if k.endswith("/artifacts?per_page=100"))
        db[path]["artifacts"].append(copy.deepcopy(db[path]["artifacts"][0]))
        with self.assertRaises(module.readiness.GateFailure):
            module.evaluate(fetch_from(db))
        db, _, _ = fixture()
        db[path]["artifacts"][0]["expired"] = True
        with self.assertRaises(module.readiness.GateFailure):
            module.evaluate(fetch_from(db))

    def test_later_failed_publish_attempt_overrides_earlier_success(self):
        db, _, publisher = fixture()
        newer = dict(publisher, id=2020, conclusion="failure")
        db[RUN_PATH]["workflow_runs"].append(newer)
        with self.assertRaisesRegex(module.Blocked, "PUBLISH_WORKFLOW_NOT_SUCCESS"):
            module.evaluate(fetch_from(db))

    def test_branch_changed_on_final_read_fails_closed(self):
        db, _, _ = fixture()
        count = 0
        def fetch(path):
            nonlocal count
            if path == "/branches/dev":
                count += 1
                return {"commit": {"sha": SHA if count == 1 else "c"*40}}
            return copy.deepcopy(db[path])
        with self.assertRaisesRegex(module.Blocked, "DEV_CHANGED_DURING_GATE"):
            module.evaluate(fetch)

    def test_no_args_mode_does_not_invoke_network(self):
        with patch.object(module.readiness, "fetch_public", side_effect=AssertionError("network")):
            self.assertEqual(2, module.main(["--unsafe-apply"]))

    def test_one_coherent_workflow_inventory_and_fresh_branch_end(self):
        db, _, _ = fixture()
        calls = []
        def fetch(path):
            calls.append(path)
            return copy.deepcopy(db[path])
        module.evaluate(fetch)
        self.assertEqual(calls.count(RUN_PATH), 1)
        self.assertEqual(calls.count("/branches/dev"), 2)
        self.assertEqual(len(calls), 5)
        self.assertEqual(calls[-1], "/branches/dev")

    def test_expected_pin_reduces_reads_but_cannot_select_old_dev(self):
        db, _, _ = fixture()
        calls = []
        def fetch(path):
            calls.append(path)
            return copy.deepcopy(db[path])
        self.assertEqual(module.evaluate(fetch, expected_sha=SHA)["source_sha"], SHA)
        self.assertEqual(len(calls), 4)
        self.assertEqual(calls, [RUN_PATH, next(p for p in db if "/artifacts?" in p),
                                 "/releases/tags/" + TAG, "/branches/dev"])
        db["/branches/dev"] = {"commit": {"sha": "c" * 40}}
        with self.assertRaisesRegex(module.Blocked, "DEV_CHANGED_DURING_GATE"):
            module.evaluate(fetch, expected_sha=SHA)

    def test_cache_lifetime_is_one_gate_not_one_process_or_sha(self):
        db, _, publisher = fixture()
        module.evaluate(fetch_from(db), expected_sha=SHA)
        publisher["conclusion"] = "failure"
        with self.assertRaisesRegex(module.Blocked, "PUBLISH_WORKFLOW_NOT_SUCCESS"):
            module.evaluate(fetch_from(db), expected_sha=SHA)

    def test_bad_pin_is_blocked_without_fetch(self):
        with self.assertRaisesRegex(module.Blocked, "EXPECTED_SOURCE_SHA_INVALID"):
            module.evaluate(lambda _: self.fail("must not fetch"), expected_sha="../old")

    def test_no_shell_docker_token_or_staging_side_effects(self):
        source = FILE.read_text()
        for forbidden in ("subprocess", "docker", "os.system", "open(", "unlink(", "chmod(",
                          "download_workflow_artifact", "GH_TOKEN"):
            self.assertNotIn(forbidden, source)


if __name__ == "__main__":
    unittest.main()
