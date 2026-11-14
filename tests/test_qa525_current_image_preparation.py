"""Offline QA525 signed API+WEB image-preparation tests, NO Docker or host."""
import contextlib
import importlib.util
import io
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import Mock, patch

SRC = Path(__file__).resolve().parents[1] / "scripts/qa/operators/qa525-current-image-preparation.py"
spec = importlib.util.spec_from_file_location("qa525_dynamic_image_prepare_test", SRC)
mod = importlib.util.module_from_spec(spec)
spec.loader.exec_module(mod)

SHA = "a" * 40
API = "sha256:" + "b" * 64
WEB = "sha256:" + "c" * 64
OLD = "sha256:" + "d" * 64
CID = "e" * 64


class DynamicImagePreparationTests(unittest.TestCase):
    def setUp(self):
        self.t = tempfile.TemporaryDirectory(prefix="qa525-dynamic-test-")
        self.addCleanup(self.t.cleanup)
        self.package = Path(self.t.name) / "signed-source"
        (self.package / "backend").mkdir(parents=True)
        (self.package / "backend" / "Dockerfile").write_text("FROM scratch\n")
        self.events = []

    def run_docker(self, args, **kwargs):
        if args[0] == "build":
            self.events.append("build-api")
            p = Path(args[args.index("--iidfile") + 1])
            p.write_text(API)
            self.assertIn("org.opencontainers.image.revision=" + SHA,
                          args[args.index("--label") + 1])
            self.assertNotIn("up", args)
            return ""
        if args[:3] == ["image", "inspect", "--format"]:
            return API if args[3] == "{{.Id}}" else SHA
        raise AssertionError("unexpected Docker command")

    def verify(self, package, sha):
        self.assertEqual(self.package, package)
        self.assertEqual(SHA, sha)
        self.events.append("verified")
        return True

    def web(self, package, sha, verify):
        self.events.append("build-web")
        self.assertTrue(verify(package, sha))
        return {"status": "LOCAL_WEB_IMAGE_PREPARED_NOT_DEPLOYED",
                "source_sha": sha, "web_image_id": WEB}

    def test_build_signed_api_and_web_without_container_changes(self):
        result = mod.build_images(self.package, SHA, self.verify,
                                  self.run_docker, self.web)
        self.assertEqual({"api_image_id": API, "web_image_id": WEB}, result)
        self.assertEqual(1, self.events.count("build-api"))
        self.assertEqual(1, self.events.count("build-web"))
        self.assertGreaterEqual(self.events.count("verified"), 4)

    def test_signature_missing_prevents_api_build(self):
        runner = Mock()
        with self.assertRaisesRegex(mod.Blocked, "SOURCE_NOT_REVERIFIED"):
            mod.build_images(self.package, SHA, lambda *_: False,
                             runner, self.web)
        runner.assert_not_called()

    def test_unsigned_mutation_after_api_build_blocks(self):
        index = [0]
        def verify(package, sha):
            index[0] += 1
            return index[0] == 1
        with self.assertRaisesRegex(mod.Blocked, "SOURCE_CHANGED_DURING_API_BUILD"):
            mod.build_images(self.package, SHA, verify, self.run_docker, self.web)
        self.assertNotIn("build-web", self.events)

    def test_source_mutation_before_web_build_blocks(self):
        index = [0]
        def verify(package, sha):
            index[0] += 1
            return index[0] <= 2
        with self.assertRaisesRegex(mod.Blocked, "SOURCE_CHANGED_BEFORE_WEB_BUILD"):
            mod.build_images(self.package, SHA, verify, self.run_docker, self.web)

    def test_different_release_sha_in_web_receipt_blocks(self):
        def wrong(pkg, sha, verify):
            return {"status": "LOCAL_WEB_IMAGE_PREPARED_NOT_DEPLOYED",
                    "source_sha": "f" * 40, "web_image_id": WEB}
        with self.assertRaisesRegex(mod.Blocked, "WEB_PREPARATION_INVALID"):
            mod.build_images(self.package, SHA, self.verify, self.run_docker, wrong)

    def test_no_duplicate_image_ids(self):
        def duplicate(pkg, sha, verify):
            return {"status": "LOCAL_WEB_IMAGE_PREPARED_NOT_DEPLOYED",
                    "source_sha": sha, "web_image_id": API}
        with self.assertRaisesRegex(mod.Blocked, "WEB_PREPARATION_INVALID"):
            mod.build_images(self.package, SHA, self.verify,
                             self.run_docker, duplicate)

    def test_invalid_source_sha_stops_before_docker(self):
        runner = Mock()
        with self.assertRaisesRegex(mod.Blocked, "TARGET_NOT_CURRENT_SHA"):
            mod.build_images(self.package, "old", self.verify,
                             runner, self.web)
        runner.assert_not_called()

    def test_invalid_api_image_id_rejected(self):
        def bad(args, **kwargs):
            if args[0] == "build":
                Path(args[args.index("--iidfile") + 1]).write_text("fake-tag")
                return ""
            raise AssertionError("should not inspect")
        with self.assertRaisesRegex(mod.Blocked, "BUILT_API_IMAGE_INVALID"):
            mod.build_images(self.package, SHA, self.verify, bad, self.web)

    def test_api_build_context_must_contain_dockerfile(self):
        (self.package / "backend" / "Dockerfile").unlink()
        runner = Mock()
        with self.assertRaisesRegex(mod.Blocked, "SIGNED_API_BUILD_CONTEXT_INVALID"):
            mod.build_images(self.package, SHA, self.verify, runner, self.web)
        runner.assert_not_called()

    def test_snapshot_only_has_seven_services_and_no_secrets(self):
        def docker(args, **kw):
            name = args[-1].split("-")[-2]
            return json.dumps([{
                "Id": CID, "Image": OLD, "State": {"Status": "running"},
                "Config": {
                    "Env": ["SystemMail__EncryptionKey=very-secret"],
                    "Labels": {
                        "com.docker.compose.project": mod.PROJECT,
                        "com.docker.compose.service": name,
                    },
                },
            }])
        snap = mod.snapshot(docker)
        self.assertEqual(set(mod.SERVICES), set(snap))
        self.assertNotIn("very-secret", json.dumps(snap))
        self.assertTrue(all(v["running"] for v in snap.values()))

    def test_snapshot_stopped_or_wrong_project_is_rejected(self):
        for failed in ("stopped", "project"):
            def docker(args, **kw):
                name = args[-1].split("-")[-2]
                return json.dumps([{
                    "Id": CID, "Image": OLD,
                    "State": {"Status": "stopped" if failed == "stopped" else "running"},
                    "Config": {"Labels": {
                        "com.docker.compose.project": "wrong" if failed == "project" else mod.PROJECT,
                        "com.docker.compose.service": name,
                    }},
                }])
            with self.subTest(failed=failed):
                with self.assertRaisesRegex(mod.Blocked, "DOCKER_BASELINE_INVALID"):
                    mod.snapshot(docker)

    def test_live_gate_blocks_when_identity_risk_or_sha_drift(self):
        with patch.object(mod.stage.signed.staging, "verify_manifest"), \
             patch.object(mod.stage, "validated_host_check", return_value={
                 "status": "BLOCKED_REVIEW_REQUIRED",
                 "source_sha": SHA, "may_apply": False,
             }):
            self.assertFalse(mod.signed_source_guard(self.package, SHA))

    def test_manifest_verification_fails_closed(self):
        with patch.object(mod.stage.signed.staging, "verify_manifest", side_effect=RuntimeError("invalid")):
            self.assertFalse(mod.signed_source_guard(self.package, SHA))

    def test_not_glmdch_blocks_preparation_before_any_file_or_docker_access(self):
        with patch.object(mod.os, "geteuid", return_value=1000), \
             patch.object(mod.socket, "gethostname", return_value="somewhere"), \
             patch.object(mod, "trusted_state") as state, \
             patch.object(mod.stage, "stage_current") as stage:
            with self.assertRaisesRegex(mod.Blocked, "HOST_NOT_AUTHORIZED"):
                mod.prepare()
            state.assert_not_called()
            stage.assert_not_called()

    def test_only_explicit_prepare_cli_allowed_and_no_secret_errors(self):
        out = io.StringIO()
        with patch.object(mod, "prepare", side_effect=RuntimeError("SMTP_SECRET=private")), \
             contextlib.redirect_stdout(out):
            result = mod.main(["--prepare"])
        self.assertEqual(2, result)
        self.assertIn("PREPARATION_NOT_VERIFIED", out.getvalue())
        self.assertNotIn("SMTP_SECRET", out.getvalue())
        with patch.object(mod, "prepare") as prep:
            self.assertEqual(2, mod.main([]))
            prep.assert_not_called()

    def test_root_sticky_lock_directory_is_narrowly_allowed(self):
        from types import SimpleNamespace
        import stat as st
        with patch.object(mod, "trusted") as trusted, \
             patch.object(Path, "lstat", return_value=SimpleNamespace(
                 st_mode=st.S_IFDIR | 0o1777, st_uid=0)):
            mod.trusted_lock_parent(Path("/run/lock"))
            trusted.assert_called_once_with(Path("/run"), directory=True)

    def test_untrusted_lock_directory_is_blocked(self):
        from types import SimpleNamespace
        import stat as st
        for mode, uid in ((0o0777, 0), (0o1777, 1000),
                          (0o0775, 0)):
            with self.subTest(mode=mode, uid=uid), \
                 patch.object(mod, "trusted"), \
                 patch.object(Path, "lstat", return_value=SimpleNamespace(
                     st_mode=st.S_IFDIR | mode, st_uid=uid)):
                with self.assertRaisesRegex(mod.Blocked, "LOCK_DIRECTORY_NOT_ROOT_STICKY"):
                    mod.trusted_lock_parent(Path("/run/lock"))

    def test_code_does_not_contain_apply_operations(self):
        source = SRC.read_text()
        for op in ("--force-recreate", "'up'", '"up"', "systemctl", "pg_dump",
                   "dropdb", "docker compose up", "migration apply"):
            self.assertNotIn(op, source)
        self.assertIn('"may_apply": False', source)
        self.assertLess(source.index("bootstrap_import_trust()"),
                        source.index("stage = load("))


UPDATE_SRC = SRC.parent / "qa525-update.py"
uspec = importlib.util.spec_from_file_location("qa525_update_for_prepare_test", UPDATE_SRC)
update_mod = importlib.util.module_from_spec(uspec)
uspec.loader.exec_module(update_mod)

OLD_SHA = "1" * 40
OLD_API = "sha256:" + "2" * 64
OLD_WEB = "sha256:" + "3" * 64


class FakeEntry:
    @staticmethod
    def private_json(path):
        return json.loads(Path(path).read_text())


class PreparedSuccessorTests(unittest.TestCase):
    def setUp(self):
        self.t = tempfile.TemporaryDirectory(prefix="qa525-successor-")
        self.addCleanup(self.t.cleanup)
        root = Path(self.t.name)
        self.state = root / "state"
        self.etc = root / "etc"
        self.state.mkdir()
        self.etc.mkdir()
        self.actas = tuple(self.etc / n for n in ("review.json", "post.json", "cut.json"))
        for i, p in enumerate(self.actas):
            p.write_text('{"acta": %d}' % i)
        self.previous = {"status": "PREPARED_API_WEB_SIGNATURE_VERIFIED_NO_DEPLOYMENT",
                         "source_sha": OLD_SHA, "source_asset_sha256": "f" * 64,
                         "api_image_id": OLD_API, "web_image_id": OLD_WEB}
        (self.state / "prepared.json").write_text(json.dumps(self.previous))
        self.journal = {"status": "APPLIED", "source_sha": OLD_SHA,
                        "new_images": {"api": OLD_API, "web": OLD_WEB}}
        (self.state / ("dual-" + OLD_SHA + ".json")).write_text(json.dumps(self.journal))
        self.live = {"api": ["c1", OLD_API, "running"], "web": ["c2", OLD_WEB, "running"]}
        self.new = {"status": "PREPARED_API_WEB_SIGNATURE_VERIFIED_NO_DEPLOYMENT",
                    "source_sha": SHA, "source_asset_sha256": "e" * 64,
                    "api_image_id": API, "web_image_id": WEB}
        self.actas[2].write_text(json.dumps({"target_sha": OLD_SHA}))
        self.snap_files = {p.name: p.read_bytes() for p in (self.state / "prepared.json", *self.actas)}

    def check(self, **kw):
        health = kw.pop("health", lambda sha: True)
        return mod.applied_predecessor(self.state, update_mod, FakeEntry, kw.pop("live", self.live), health, None)

    def install(self, **kw):
        return mod.install_prepared_archiving_predecessor(self.state, self.previous, self.new, actas=self.actas, now=1, **kw)

    def test_no_prepared_returns_none(self):
        (self.state / "prepared.json").unlink()
        self.assertIsNone(self.check())

    def test_applied_live_healthy_predecessor_is_accepted(self):
        self.assertEqual(OLD_SHA, self.check()["source_sha"])

    def test_adversarial_predecessor_is_blocked(self):
        cases = {
            "not live": lambda: self.check(live={"api": ["c1", API, "running"], "web": self.live["web"]}),
            "unhealthy": lambda: self.check(health=lambda sha: False),
            "stopped": lambda: self.check(live={"api": ["c1", OLD_API, "stopped"], "web": self.live["web"]}),
        }
        for name, call in cases.items():
            with self.subTest(name), self.assertRaises(mod.Blocked):
                call()
        (self.state / ("dual-" + OLD_SHA + ".json")).write_text(json.dumps(dict(self.journal, status="ROLLED_BACK")))
        with self.assertRaises(mod.Blocked):
            self.check()

    def test_other_pending_journal_or_marker_blocks(self):
        (self.state / ("dual-" + "9" * 40 + ".json")).write_text(json.dumps(dict(self.journal, status="STARTED")))
        with self.assertRaisesRegex(mod.Blocked, "PREPARE_JOURNAL_REQUIRES_RECONCILIATION"):
            self.check()
        (self.state / ("dual-" + "9" * 40 + ".json")).unlink()
        (self.state / mod.PENDING).write_text("{}")
        with self.assertRaisesRegex(mod.Blocked, "PREPARE_ARCHIVE_RECONCILIATION_REQUIRED"):
            self.check()

    def test_install_archives_actas_byte_for_byte_and_publishes_new_receipt(self):
        self.install()
        self.assertEqual(SHA, json.loads((self.state / "prepared.json").read_text())["source_sha"])
        self.assertFalse((self.state / mod.PENDING).exists())
        self.assertFalse((self.state / "prepared.json.next").exists())
        self.assertTrue(all(not p.exists() for p in self.actas))
        archives = list(self.state.glob("review-archive-*"))
        self.assertEqual(1, len(archives))
        for name, data in self.snap_files.items():
            self.assertEqual(data, (archives[0] / name).read_bytes())
        self.assertTrue((self.state / ("dual-" + OLD_SHA + ".json")).exists())  # journal preserved
        self.assertEqual(0o600, (self.state / "prepared.json").stat().st_mode & 0o777)

    def test_failure_midway_restores_everything_and_removes_marker(self):
        original = Path.rename
        calls = []

        def flaky(self_, target):
            calls.append(self_.name)
            if self_.name == "post.json":
                raise OSError("disk")
            return original(self_, target)
        with patch.object(Path, "rename", flaky), self.assertRaises(OSError):
            self.install()
        self.assertEqual(self.snap_files, {p.name: p.read_bytes() for p in (self.state / "prepared.json", *self.actas)})
        self.assertFalse((self.state / mod.PENDING).exists())
        self.assertFalse((self.state / "prepared.json.next").exists())
        self.assertEqual([], list(self.state.glob("review-archive-*")))

    def state_files(self):
        return {p.name: p.read_bytes() for p in (self.state / "prepared.json", *self.actas) if p.exists()}

    def test_hard_crash_at_every_transition_is_recoverable_and_preserves_both_receipts(self):
        steps = ["marker", "aside", "moved-0", "moved-1", "moved-2", "moved-3", "result", "revalidated",
                 "published", "synced"]
        new_bytes = json.dumps(self.new, sort_keys=True).encode()
        for step in steps:
            with self.subTest(step=step):
                self.tearDown() if False else None
                self.setUp()

                class Crash(BaseException):
                    pass

                def hook(label, step=step):
                    if label == step:
                        raise Crash()
                with self.assertRaises(Crash):
                    self.install(hook=hook)
                self.assertTrue((self.state / mod.PENDING).exists())  # blocker stays after a hard crash
                with self.assertRaisesRegex(mod.Blocked, "PREPARE_ARCHIVE_RECONCILIATION_REQUIRED"):
                    self.check()
                status = mod.recover_prepare_archive(self.state)
                self.assertFalse((self.state / mod.PENDING).exists())
                self.assertFalse((self.state / "prepared.json.next").exists())
                if step in ("published", "synced"):
                    self.assertEqual("ROLLED_FORWARD", status)
                    self.assertEqual(new_bytes, (self.state / "prepared.json").read_bytes())
                    archived = list(self.state.glob("review-archive-*"))[0]
                    for name, data in self.snap_files.items():
                        self.assertEqual(data, (archived / name).read_bytes())
                else:
                    self.assertEqual("ROLLED_BACK", status)
                    self.assertEqual(self.snap_files, self.state_files())
                    self.assertEqual([], list(self.state.glob("review-archive-*")))

    def test_failure_after_publication_does_not_roll_back_and_is_recovered_forward(self):
        calls = {"n": 0}

        def fsync_fail(directory):
            calls["n"] += 1
            if calls["n"] > 1:  # first call is the marker fsync; the next is after the publication rename
                raise OSError("fsync")
        with self.assertRaisesRegex(mod.Blocked, "PREPARE_ARCHIVE_PUBLISHED_RECOVERY_REQUIRED"):
            self.install(fsync_dir=fsync_fail)
        self.assertEqual(SHA, json.loads((self.state / "prepared.json").read_text())["source_sha"])
        self.assertTrue((self.state / mod.PENDING).exists())
        self.assertEqual("ROLLED_FORWARD", mod.recover_prepare_archive(self.state))
        archived = list(self.state.glob("review-archive-*"))[0]
        for name, data in self.snap_files.items():
            self.assertEqual(data, (archived / name).read_bytes())

    def test_recovery_with_ambiguous_state_changes_nothing(self):
        steps_crash = {}

        class Crash(BaseException):
            pass

        def hook(label):
            if label == "moved-1":
                raise Crash()
        with self.assertRaises(Crash):
            self.install(hook=hook)
        (self.state / "prepared.json").write_text('{"tampered": true}')  # origin reoccupied by something else
        before = sorted(str(p) for p in self.state.rglob("*"))
        with self.assertRaisesRegex(mod.Blocked, "PREPARE_ARCHIVE_RECOVERY_UNSAFE"):
            mod.recover_prepare_archive(self.state)
        self.assertEqual(before, sorted(str(p) for p in self.state.rglob("*")))
        self.assertTrue((self.state / mod.PENDING).exists())

    def test_cross_device_is_refused_before_any_change(self):
        real = Path.stat

        class Fake:
            def __init__(self, st, dev):
                self._st, self.st_dev = st, dev

            def __getattr__(self, name):
                return getattr(self._st, name)

        def fake_stat(self_, *a, **k):
            st = real(self_, *a, **k)
            return Fake(st, 99) if self_ == self.etc else st
        with patch.object(Path, "stat", fake_stat), self.assertRaisesRegex(mod.Blocked, "PREPARE_ARCHIVE_CROSS_DEVICE"):
            self.install()
        self.assertEqual(self.snap_files, self.state_files())
        self.assertEqual([], list(self.state.glob("review-archive-*")))
        self.assertFalse((self.state / mod.PENDING).exists())

    def test_drift_after_build_before_commit_rolls_back_everything(self):
        def drift():
            raise mod.Blocked("PREPARE_RUNTIME_DRIFT_BEFORE_COMMIT")
        with self.assertRaisesRegex(mod.Blocked, "PREPARE_RUNTIME_DRIFT_BEFORE_COMMIT"):
            self.install(revalidate=drift)
        self.assertEqual(self.snap_files, self.state_files())
        self.assertFalse((self.state / mod.PENDING).exists())
        self.assertEqual([], list(self.state.glob("review-archive-*")))

    def test_acta_changed_between_hash_and_rename_rolls_back(self):
        original = Path.rename

        def tamper(self_, target):
            if self_.name == "post.json":
                self_.write_text('{"swapped": true}')  # TOCTOU window
            return original(self_, target)
        with patch.object(Path, "rename", tamper), self.assertRaisesRegex(mod.Blocked, "PREPARE_ACTA_CHANGED_DURING_ARCHIVE"):
            self.install()
        # fail-closed: a swapped acta is NEVER silently restored or deleted; marker blocks until supervised recovery,
        # which also refuses (digest mismatch) and changes nothing
        self.assertTrue((self.state / mod.PENDING).exists())
        archived = list(self.state.glob("review-archive-*"))[0]
        self.assertTrue((archived / "post.json").exists())
        with self.assertRaisesRegex(mod.Blocked, "PREPARE_ARCHIVE_RECOVERY_UNSAFE"):
            mod.recover_prepare_archive(self.state)
        self.assertTrue((self.state / mod.PENDING).exists())

    def test_acta_of_another_target_is_never_archived(self):
        self.actas[2].write_text(json.dumps({"target_sha": "9" * 40}))
        with self.assertRaisesRegex(mod.Blocked, "PREPARE_ACTA_NOT_PREDECESSOR"):
            self.install()
        self.assertTrue(self.actas[2].exists())
        self.assertFalse((self.state / mod.PENDING).exists())

    def test_cli_recover_flag_and_legacy_updater_blocks_on_our_marker(self):
        with patch.object(mod, "recover", return_value={"status": "x"}) as rec, contextlib.redirect_stdout(io.StringIO()):
            self.assertEqual(0, mod.main(["--recover-prepare-archive"]))
            rec.assert_called_once()
        self.assertIn("prepared-successor-archive-pending.json", UPDATE_SRC.read_text())

    def test_symlink_acta_is_refused_before_any_move(self):
        self.actas[0].unlink()
        self.actas[0].symlink_to(self.actas[1])
        with self.assertRaisesRegex(mod.Blocked, "PREPARE_ACTA_UNTRUSTED"):
            self.install()
        self.assertTrue((self.state / "prepared.json").exists())
        self.assertFalse((self.state / mod.PENDING).exists())

    def test_source_wires_release_verification_before_archive(self):
        src = SRC.read_text()
        self.assertLess(src.index("signed_source_guard(package, sha)"),
                        src.index("install_prepared_archiving_predecessor(STATE"))
        self.assertIn("qa525-update.py", src[src.index("SOURCE_CHAIN"):src.index("class Blocked")])


if __name__ == "__main__":
    unittest.main()
