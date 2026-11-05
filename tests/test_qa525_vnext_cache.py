import argparse
import datetime as dt
from email.message import Message
import hashlib
from io import BytesIO
import importlib.util
import json
import os
from pathlib import Path
import shutil
import socket
import stat
import tempfile
import threading
from types import SimpleNamespace
import unittest
from unittest import mock

ROOT = Path(__file__).resolve().parents[1]
SCRIPT = ROOT / "scripts" / "qa" / "qa525-vnext-cache.py"
STAGING = ROOT / "scripts" / "qa" / "qa-signed-staging.py"
READINESS = ROOT / "scripts" / "qa" / "qa-agent-readiness.py"
SPEC = importlib.util.spec_from_file_location("qa525_vnext_cache", SCRIPT)
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)
STAGING_SPEC = importlib.util.spec_from_file_location("qa525_staging_test", STAGING)
STAGING_MODULE = importlib.util.module_from_spec(STAGING_SPEC)
STAGING_SPEC.loader.exec_module(STAGING_MODULE)

APP = "6eed4f00b7917da3cca36bbda70e16d16328648d"
OPS = "2e9d40a0be8a39e65e786673b23ab721c63e325b"
NONCE = "qa525-vnext-admission-0001"
NOW = dt.datetime(2026, 11, 5, 12, 0, tzinfo=dt.timezone.utc)


def write_private(path, value):
    path.parent.mkdir(mode=0o700, parents=True, exist_ok=True)
    payload = value if isinstance(value, bytes) else (json.dumps(value, sort_keys=True) + "\n").encode()
    path.write_bytes(payload)
    path.chmod(0o600)


def digest(path):
    return "sha256:" + hashlib.sha256(path.read_bytes()).hexdigest()


def identity(path):
    info = path.stat()
    return {"device": info.st_dev, "inode": info.st_ino, "size": info.st_size,
            "mtime_ns": info.st_mtime_ns}


class AdmissionFixture:
    def __init__(self, root):
        self.root = Path(root)
        self.cache_root = self.root / "cache"
        self.cache = self.cache_root / APP
        self.state = self.root / "state"
        self.g6 = self.root / "qa525-g6-reviewed-20261104T130000Z.json"
        self.archive_root = self.root / "archives"
        self.report_root = self.root / "reports"
        self.archive_receipt = self.report_root / "archive.receipt.json"
        self.approval = self.root / "approval.json"
        artifact = b"signed artifact bytes\n"
        write_private(self.cache / "artifact.zip", artifact)
        self.package = self.cache / "package"
        files = {
            "BUILD-INFO.txt": ("SOURCE_SHA=" + APP + "\n" +
                               "STORAGE=SIGNED_REBUILT_SILO_ARCHIVE_SYNTHETIC_QA\n").encode(),
            "storage-bundle/project-provenance.sigstore.json": b"{}\n",
            "storage-bundle/rebuild-report.json": b"{}\n",
            "storage-bundle/server-fixed.openvex.json": b"{}\n",
            "storage-bundle/server-cve-triage.json": b"{}\n",
            "storage-bundle/silo-qa-review-only.tar": b"synthetic\n",
        }
        for name, payload in files.items():
            write_private(self.package / name, payload)
        manifest = "".join(hashlib.sha256(payload).hexdigest() + "  ./" + name + "\n"
                           for name, payload in sorted(files.items()))
        write_private(self.package / "MANIFEST.sha256", manifest.encode())
        write_private(self.cache / "installable-provenance.sigstore.json", b'{"bundle":"verified"}\n')
        for directory in (self.cache_root, self.cache, self.package,
                          self.package / "storage-bundle"):
            directory.chmod(0o700)
        artifact_path = self.cache / "artifact.zip"
        manifest_path = self.package / "MANIFEST.sha256"
        bundle_path = self.cache / "installable-provenance.sigstore.json"
        self.artifact_digest = digest(artifact_path)
        write_private(self.cache / "cache.json", {
            "schema": "qa525-cache-v2", "status": "SYNCED_ATTESTED",
            "ops_source_sha": OPS, "policy_hash": MODULE.POLICY_HASH,
            "app_source_sha": APP, "attestation_source_sha": APP,
            "attestation_signer_workflow": STAGING_MODULE.WORKFLOW,
            "artifact_id": 525001, "artifact_sha256": self.artifact_digest,
            "artifact_identity": identity(artifact_path),
            "manifest_sha256": digest(manifest_path), "manifest_identity": identity(manifest_path),
            "attestation_bundle_sha256": digest(bundle_path),
            "attestation_bundle_identity": identity(bundle_path),
            "network_allowed_after_sync": False, "synced_at": "2026-11-05T11:55:00Z",
            "cache_expires_at": "2026-11-05T18:00:00Z",
        })
        archive_name = "centenario-qa-g6-CIFRADO-20261104T130000Z.tar.gz"
        report_name = "isolated-restore-20261104T130000Z.report.json"
        archive = self.archive_root / archive_name
        write_private(archive, b"synthetic encrypted local backup")
        sha = hashlib.sha256(archive.read_bytes()).hexdigest()
        write_private(self.g6, {
            "g6_archive_name": archive_name, "g6_archive_sha256": sha,
            "g6_drive_file_id": "drive_file_id_1234567890",
            "g6_offhost_sha256_verified": True,
            "g6_restore_report_name": report_name})
        write_private(self.archive_receipt, {
            "status": "ENCRYPTED_LOCAL_ARCHIVE_READY_OFFHOST_PENDING",
            "archive_name": archive_name, "sha256": sha})
        write_private(self.report_root / report_name, {
            "result": "G6_SAME_HOST_ISOLATED_RESTORE_PASS_NOT_DISASTER_RECOVERY",
            "archive_filename": archive_name, "archive_sha256": sha,
            "postgres_tables_by_database": {"pmgm": 2, "pmgm_keycloak": 2},
            "silo_objects_verified": 1, "production_touched": False})
        write_private(self.approval, {
            "status": "APPROVED", "nonce": NONCE, "ops_source_sha": OPS,
            "app_source_sha": APP, "artifact_id": 525001,
            "artifact_sha256": self.artifact_digest, "scope": "API_WEB_QA_ONLY",
            "environment": "QA_SYNTHETIC", "action": "ADMIT_OFFLINE_APPLY",
            "host": "glmdch", "issued_at": "2026-11-05T11:55:00Z",
            "expires_at": "2026-11-05T12:25:00Z",
        })

    def args(self):
        return type("Args", (), {"app_sha": APP, "nonce": NONCE,
            "cache_root": self.cache_root, "g6_receipt": self.g6,
            "approval": self.approval, "state_dir": self.state,
            "g6_margin_seconds": 1800})()


class OfflineAdmission(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.fx = AdmissionFixture(self.tmp.name)
        self.patches = [
            mock.patch.object(MODULE, "now_utc", return_value=NOW),
            mock.patch.object(MODULE, "require_root", return_value=None),
            mock.patch.object(MODULE, "require_trusted_ancestors", return_value=None),
            mock.patch.object(MODULE, "load_verified_ops",
                              return_value=({"ops_source_sha": OPS}, object(), STAGING_MODULE)),
            mock.patch.object(MODULE, "G6_REVIEW_ROOT", self.fx.root),
            mock.patch.object(MODULE, "G6_ARCHIVE_ROOT", self.fx.archive_root),
            mock.patch.object(MODULE, "G6_RECEIPT_PATH", self.fx.archive_receipt),
            mock.patch.object(MODULE, "G6_REPORT_ROOT", self.fx.report_root),
        ]
        for patch in self.patches:
            patch.start()

    def tearDown(self):
        for patch in reversed(self.patches):
            patch.stop()
        self.tmp.cleanup()

    def admit_fixture(self, fx):
        with mock.patch.object(MODULE, "G6_REVIEW_ROOT", fx.root), \
             mock.patch.object(MODULE, "G6_ARCHIVE_ROOT", fx.archive_root), \
             mock.patch.object(MODULE, "G6_RECEIPT_PATH", fx.archive_receipt), \
             mock.patch.object(MODULE, "G6_REPORT_ROOT", fx.report_root):
            return MODULE.admit(fx.args())

    def test_happy_path_is_fully_bound_but_nonce_not_consumed(self):
        result = MODULE.admit(self.fx.args())
        self.assertEqual(result["status"], "ADMITTED")
        self.assertEqual(result["artifact_id"], 525001)
        self.assertEqual(result["policy_hash"], MODULE.POLICY_HASH)
        self.assertEqual(result["valid_until"], "2026-11-05T12:25:00Z")
        self.assertFalse(result["nonce_consumed"])
        self.assertEqual(result["nonce_consume_transition"], "APPLYING")
        output = self.fx.state / ("admission-" + NONCE + ".json")
        self.assertEqual(json.loads(output.read_text()), result)
        self.assertFalse(any(self.fx.state.glob("nonce-consumed-*")))
        self.assertEqual(stat.S_IMODE(output.stat().st_mode), 0o600)

    def test_admission_performs_zero_network_including_staging_import(self):
        with mock.patch.object(socket, "create_connection", side_effect=AssertionError("network")), \
             mock.patch.object(socket.socket, "connect", side_effect=AssertionError("network")):
            self.assertEqual(MODULE.admit(self.fx.args())["status"], "ADMITTED")

    def test_concurrent_same_nonce_has_one_exclusive_winner(self):
        barrier = threading.Barrier(2)
        outcomes = []

        def run():
            barrier.wait()
            try:
                outcomes.append(MODULE.admit(self.fx.args())["status"])
            except MODULE.Stop as exc:
                outcomes.append(str(exc))

        threads = [threading.Thread(target=run) for _ in range(2)]
        for thread in threads:
            thread.start()
        for thread in threads:
            thread.join()
        self.assertCountEqual(outcomes, ["ADMITTED", "NONCE_ALREADY_ADMITTED"])

    def test_second_admission_fails_closed(self):
        MODULE.admit(self.fx.args())
        with self.assertRaisesRegex(MODULE.Stop, "NONCE_ALREADY_ADMITTED"):
            MODULE.admit(self.fx.args())

    def test_artifact_or_bundle_change_fails_closed(self):
        for relative in ("artifact.zip", "installable-provenance.sigstore.json"):
            with self.subTest(relative=relative), tempfile.TemporaryDirectory() as directory:
                fx = AdmissionFixture(directory)
                write_private(fx.cache / relative, b"changed")
                with self.assertRaisesRegex(MODULE.Stop, "CACHE_ATTESTATION_BINDING_CHANGED"):
                    self.admit_fixture(fx)

    def test_staged_manifest_change_fails_closed(self):
        write_private(self.fx.package / "storage-bundle/rebuild-report.json", b'{"tampered":true}\n')
        with self.assertRaisesRegex(MODULE.Stop, "CACHE_PACKAGE_INVALID"):
            MODULE.admit(self.fx.args())

    def test_expired_cache_fails_closed(self):
        receipt = json.loads(self.fx.cache.joinpath("cache.json").read_text())
        receipt["cache_expires_at"] = "2026-11-05T11:59:59Z"
        write_private(self.fx.cache / "cache.json", receipt)
        with self.assertRaisesRegex(MODULE.Stop, "CACHE_EXPIRED"):
            MODULE.admit(self.fx.args())

    def test_real_g6_acta_has_no_expires_at_and_derives_48h_expiry(self):
        result = MODULE.admit(self.fx.args())
        self.assertEqual(result["g6_expires_at"], "2026-11-06T13:00:00Z")
        self.assertNotIn("expires_at", json.loads(self.fx.g6.read_text()))

    def test_stale_or_low_margin_g6_fails_before_state_mutation(self):
        for archive, code in (
            ("centenario-qa-g6-CIFRADO-20261103T120000Z.tar.gz", "G6_CHECKPOINT_TOO_OLD"),
            ("centenario-qa-g6-CIFRADO-20261103T122959Z.tar.gz", "G6_MARGIN_LOW")):
            with self.subTest(code=code), tempfile.TemporaryDirectory() as directory:
                fx = AdmissionFixture(directory)
                record = json.loads(fx.g6.read_text())
                record["g6_archive_name"] = archive
                write_private(fx.g6, record)
                with self.assertRaisesRegex(MODULE.Stop, code):
                    self.admit_fixture(fx)
                self.assertFalse(fx.state.exists())

    def test_arbitrary_root_g6_acta_location_is_rejected(self):
        for path in (self.fx.root / "g6.json",
                     self.fx.root / "subdir" / self.fx.g6.name,
                     Path("/var/tmp") / self.fx.g6.name):
            with self.subTest(path=str(path)):
                args = self.fx.args()
                args.g6_receipt = path
                with self.assertRaisesRegex(MODULE.Stop, "G6_REVIEW_PATH_UNTRUSTED"):
                    MODULE.admit(args)
        self.assertEqual(MODULE.admit(self.fx.args())["status"], "ADMITTED")

    def test_g6_review_is_optional_and_not_the_backup_age(self):
        record = json.loads(self.fx.g6.read_text())
        self.assertNotIn("reviewed_at", record)
        record["reviewed_at"] = int(NOW.timestamp()) - 8 * 3600
        write_private(self.fx.g6, record)
        self.assertEqual(MODULE.admit(self.fx.args())["status"], "ADMITTED")

    def test_g6_local_evidence_fail_closed(self):
        for kind, error in (("offhost", "G6_NOT_VERIFIED"),
                            ("missing", "G6_LOCAL_ARCHIVE_MISSING"),
                            ("changed", "G6_LOCAL_ARCHIVE_HASH_CHANGED"),
                            ("receipt", "G6_LOCAL_RECEIPT_MISMATCH"),
                            ("restore", "G6_RESTORATION_UNVERIFIED")):
            with self.subTest(kind=kind), tempfile.TemporaryDirectory() as directory:
                fx = AdmissionFixture(directory)
                g6 = json.loads(fx.g6.read_text())
                if kind == "offhost":
                    g6["g6_offhost_sha256_verified"] = False
                    write_private(fx.g6, g6)
                elif kind == "missing":
                    (fx.archive_root / g6["g6_archive_name"]).unlink()
                elif kind == "changed":
                    write_private(fx.archive_root / g6["g6_archive_name"], b"modified")
                elif kind == "receipt":
                    write_private(fx.archive_receipt, {"status": "FAILED"})
                else:
                    write_private(fx.report_root / g6["g6_restore_report_name"], {"result": "FAILED"})
                with self.assertRaisesRegex(MODULE.Stop, error):
                    self.admit_fixture(fx)

    def test_approval_binds_every_security_dimension(self):
        cases = (("ops_source_sha", "0" * 40), ("app_source_sha", "1" * 40),
                 ("nonce", "other-nonce-000000000000000"), ("artifact_id", 99),
                 ("artifact_sha256", "sha256:" + "f" * 64), ("scope", "WRONG"),
                 ("environment", "PRODUCTION"), ("action", "APPLY"), ("host", "other"))
        for field, value in cases:
            with self.subTest(field=field), tempfile.TemporaryDirectory() as directory:
                fx = AdmissionFixture(directory)
                approval = json.loads(fx.approval.read_text())
                approval[field] = value
                write_private(fx.approval, approval)
                with self.assertRaisesRegex(MODULE.Stop, "APPROVAL_BINDING_INVALID"):
                    self.admit_fixture(fx)

    def test_approval_lifetime_is_capped_at_30_minutes(self):
        approval = json.loads(self.fx.approval.read_text())
        approval["issued_at"] = "2026-11-05T11:54:59Z"
        write_private(self.fx.approval, approval)
        with self.assertRaisesRegex(MODULE.Stop, "APPROVAL_LIFETIME_INVALID"):
            MODULE.admit(self.fx.args())

    def test_expired_approval_fails_before_state(self):
        approval = json.loads(self.fx.approval.read_text())
        approval["issued_at"] = "2026-11-05T11:30:00Z"
        approval["expires_at"] = "2026-11-05T12:00:00Z"
        write_private(self.fx.approval, approval)
        with self.assertRaisesRegex(MODULE.Stop, "APPROVAL_EXPIRED"):
            MODULE.admit(self.fx.args())
        self.assertFalse(self.fx.state.exists())

    def test_symlink_hardlink_and_loose_mode_rejected(self):
        for case in ("symlink", "hardlink", "mode"):
            with self.subTest(case=case), tempfile.TemporaryDirectory() as directory:
                fx = AdmissionFixture(directory)
                target = fx.g6
                if case == "symlink":
                    real = target.with_suffix(".real")
                    target.rename(real)
                    target.symlink_to(real)
                elif case == "hardlink":
                    os.link(target, target.with_suffix(".link"))
                else:
                    target.chmod(0o644)
                with self.assertRaisesRegex(MODULE.Stop, "FILE_(UNTRUSTED|UNAVAILABLE)"):
                    self.admit_fixture(fx)

    def test_cache_root_symlink_or_loose_directory_is_rejected(self):
        for case in ("symlink", "mode"):
            with self.subTest(case=case), tempfile.TemporaryDirectory() as directory:
                fx = AdmissionFixture(directory)
                if case == "symlink":
                    real = fx.cache_root.with_name("cache-real")
                    fx.cache_root.rename(real)
                    fx.cache_root.symlink_to(real, target_is_directory=True)
                else:
                    fx.cache_root.chmod(0o755)
                with self.assertRaisesRegex(MODULE.Stop, "CACHE_DIRECTORY_UNTRUSTED"):
                    self.admit_fixture(fx)


class TrustedAncestorContract(unittest.TestCase):
    def test_all_parents_are_checked_and_writable_or_symlink_rejected(self):
        path = Path("/var/lib/pmgm/qa525-cache/packet.json")
        folders = {"/": 0o755, "/var": 0o755, "/var/lib": 0o755,
                   "/var/lib/pmgm": 0o700, "/var/lib/pmgm/qa525-cache": 0o700}
        original_lstat = Path.lstat

        def fake_lstat(p, *args, **kwargs):
            key = str(p)
            if key not in folders:
                return original_lstat(p, *args, **kwargs)
            return SimpleNamespace(st_uid=0, st_mode=stat.S_IFDIR | folders[key])

        with mock.patch.object(Path, "lstat", autospec=True, side_effect=fake_lstat):
            MODULE.require_trusted_ancestors(path)
            folders["/var/lib"] = 0o777
            with self.assertRaisesRegex(MODULE.Stop, "UNTRUSTED_ANCESTOR"):
                MODULE.require_trusted_ancestors(path)
            folders["/var/lib"] = 0o755
            folders["/var/lib/pmgm"] = stat.S_IFLNK | 0o777
            with self.assertRaisesRegex(MODULE.Stop, "UNTRUSTED_ANCESTOR"):
                MODULE.require_trusted_ancestors(path)

    def test_dotdot_and_relative_path_are_rejected(self):
        for p in (Path("relative/file.json"), Path("/var/lib/../etc/pmgm/a")):
            with self.subTest(path=str(p)), self.assertRaisesRegex(MODULE.Stop, "UNTRUSTED_PATH"):
                MODULE.require_trusted_ancestors(p)


class TrustedOpsAndCache(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.root = Path(self.tmp.name)
        self.chain_patch = mock.patch.object(MODULE, "require_trusted_ancestors", return_value=None)
        self.chain_patch.start()

    def tearDown(self):
        self.chain_patch.stop()
        self.tmp.cleanup()

    def test_fixed_ops_receipt_pins_program_and_both_modules(self):
        program, readiness, staging = (self.root / "program.py", self.root / "readiness.py",
                                       self.root / "staging.py")
        for source, target, mode in ((SCRIPT, program, 0o755), (READINESS, readiness, 0o644),
                                     (STAGING, staging, 0o644)):
            shutil.copyfile(source, target)
            target.chmod(mode)
        receipt = self.root / "ops.json"
        write_private(receipt, {"schema": "qa525-ops-v2", "status": "OPS_SIGNED_VERIFIED",
            "ops_source_sha": OPS, "program_sha256": digest(program),
            "readiness_sha256": digest(readiness), "staging_sha256": digest(staging),
            "policy_hash": MODULE.POLICY_HASH})
        with mock.patch.object(MODULE, "__file__", str(program)), \
             mock.patch.object(MODULE, "READINESS_MODULE", readiness), \
             mock.patch.object(MODULE, "STAGING_MODULE", staging), \
             mock.patch.object(MODULE, "OPS_RECEIPT", receipt):
            loaded, _, _ = MODULE.load_verified_ops()
            self.assertEqual(loaded["ops_source_sha"], OPS)
            readiness.write_text("tampered\n")
            readiness.chmod(0o644)
            with self.assertRaisesRegex(MODULE.Stop, "OPS_SOURCE_HASH_MISMATCH"):
                MODULE.load_verified_ops()

    def test_ops_module_runs_verified_bytes_after_path_replacement(self):
        program, readiness, staging = (self.root / "program.py", self.root / "readiness.py",
                                       self.root / "staging.py")
        for source, target, mode in ((SCRIPT, program, 0o755), (READINESS, readiness, 0o644),
                                     (STAGING, staging, 0o644)):
            shutil.copyfile(source, target)
            target.chmod(mode)
        receipt = self.root / "ops.json"
        write_private(receipt, {"schema": "qa525-ops-v2", "status": "OPS_SIGNED_VERIFIED",
            "ops_source_sha": OPS, "program_sha256": digest(program),
            "readiness_sha256": digest(readiness), "staging_sha256": digest(staging),
            "policy_hash": MODULE.POLICY_HASH})
        original = MODULE.trusted_file
        changed = []

        def mutate_after_read(path, **kw):
            payload = original(path, **kw)
            if Path(path) == readiness:
                # Simulate a file replacement immediately after verified read.
                readiness.write_text('raise RuntimeError("TOCTOU executed")\\n')
                readiness.chmod(0o644)
                changed.append(True)
            return payload

        with mock.patch.object(MODULE, "__file__", str(program)), \
             mock.patch.object(MODULE, "READINESS_MODULE", readiness), \
             mock.patch.object(MODULE, "STAGING_MODULE", staging), \
             mock.patch.object(MODULE, "OPS_RECEIPT", receipt):
            with mock.patch.object(MODULE, "trusted_file", side_effect=mutate_after_read):
                ops, verified_readiness, verified_staging = MODULE.load_verified_ops()
            self.assertTrue(changed)
            self.assertEqual(ops["ops_source_sha"], OPS)
            self.assertTrue(hasattr(verified_readiness, "evaluate_latest_validated"))
            self.assertTrue(hasattr(verified_staging, "verify_manifest"))
            with self.assertRaisesRegex(MODULE.Stop, "OPS_SOURCE_HASH_MISMATCH"):
                MODULE.load_verified_ops()

    def test_g6_margin_type_enforces_bounds_without_large_choices(self):
        self.assertEqual(MODULE.g6_margin_seconds("1800"), 1800)
        self.assertEqual(MODULE.g6_margin_seconds(str(MODULE.G6_LIFETIME - 1)),
                         MODULE.G6_LIFETIME - 1)
        for bad in ("0", "1799", str(MODULE.G6_LIFETIME), "not-a-number"):
            with self.subTest(bad=bad), self.assertRaises(argparse.ArgumentTypeError):
                MODULE.g6_margin_seconds(bad)
        admission_parser = next(
            action for action in MODULE.parser()._subparsers._group_actions
            if action.dest == "command").choices["admit"]
        margin_option = next(action for action in admission_parser._actions
                             if action.dest == "g6_margin_seconds")
        self.assertIsNone(margin_option.choices)

    def test_expired_cache_is_rotated_and_preserved(self):
        cache_root = self.root / "cache"
        target = cache_root / APP
        write_private(target / "cache.json", {"cache_expires_at": "2026-11-05T11:00:00Z"})
        cache_root.chmod(0o700)
        target.chmod(0o700)
        MODULE._rotate_expired(target, NOW)
        rotated = cache_root / ("expired-" + APP + "-20261105T120000Z")
        self.assertFalse(target.exists())
        self.assertTrue(rotated.joinpath("cache.json").exists())

    def test_valid_cache_is_not_replaced(self):
        cache_root = self.root / "cache"
        target = cache_root / APP
        write_private(target / "cache.json", {"cache_expires_at": "2026-11-05T13:00:00Z"})
        cache_root.chmod(0o700)
        target.chmod(0o700)
        with self.assertRaisesRegex(MODULE.Stop, "CACHE_ALREADY_VALID"):
            MODULE._rotate_expired(target, NOW)

    def test_streaming_digest_never_requests_unbounded_read(self):
        before = os.stat(SCRIPT)

        class Fake:
            chunks = 4
            def __enter__(self): return self
            def __exit__(self, *args): return False
            def fileno(self): return 77
            def read(self, size):
                self.assert_size(size)
                if self.chunks:
                    self.chunks -= 1
                    return b"x" * 1024
                return b""
            def assert_size(self, size):
                if size != 1024 * 1024:
                    raise AssertionError("unbounded read")

        fake = Fake()
        with mock.patch.object(MODULE, "_trusted_fd", return_value=(77, before)), \
             mock.patch.object(MODULE.os, "fdopen", return_value=fake), \
             mock.patch.object(MODULE.os, "fstat", return_value=before):
            result, _ = MODULE.trusted_digest(Path("ignored"), max_size=MODULE.MAX_ARTIFACT)
        self.assertTrue(result.startswith("sha256:"))


class StaticContract(unittest.TestCase):
    def test_script_compiles_and_has_no_apply_command(self):
        source = SCRIPT.read_text()
        compile(source, str(SCRIPT), "exec")
        for command in ('sub.add_parser("sync")', 'sub.add_parser("admit")',
                        'sub.add_parser("postcheck")'):
            self.assertIn(command, source)
        self.assertNotIn('sub.add_parser("apply")', source)

    def test_no_self_asserted_ops_or_arbitrary_code_paths(self):
        source = SCRIPT.read_text()
        for option in ("--ops-sha", "--staging", "--readiness", "--output"):
            self.assertNotIn(option, source)
        self.assertIn("PAT_REQUIRED_NO_ANONYMOUS_FALLBACK", source)
        self.assertIn('"network_allowed": False', source)
        self.assertNotRegex(source, r"--review-and-run|docker\s+(compose|run|stop|restart)|pmgm-qa-update")

    def test_root_gate_remains_fail_closed(self):
        with mock.patch.object(MODULE.os, "geteuid", return_value=1000), \
             self.assertRaisesRegex(MODULE.Stop, "ROOT_REQUIRED"):
            MODULE.require_root("ROOT_REQUIRED")


class SyncDownload(unittest.TestCase):
    class Response:
        def __init__(self, url, payload):
            self.url, self.stream = url, BytesIO(payload)
            self.headers = {"Content-Length": str(len(payload))}
        def geturl(self): return self.url
        def read(self, size=-1): return self.stream.read(size)

    class RedirectOpener:
        def __init__(self, location): self.location, self.request = location, None
        def open(self, request, timeout=None):
            self.request = request
            headers = Message()
            headers["Location"] = self.location
            raise MODULE.HTTPError(request.full_url, 302, "Found", headers, None)

    class BlobOpener:
        def __init__(self, location, payload):
            self.location, self.payload, self.request = location, payload, None
        def open(self, request, timeout=None):
            self.request = request
            return SyncDownload.Response(self.location, self.payload)

    def test_off_origin_api_url_is_rejected_before_network(self):
        with tempfile.TemporaryDirectory() as directory, \
             mock.patch.object(MODULE, "build_opener", side_effect=AssertionError("network")):
            with self.assertRaisesRegex(MODULE.Stop, "ARTIFACT_URL_INVALID"):
                MODULE.download_artifact("https://example.invalid/a.zip", "secret", Path(directory) / "a")

    def test_untrusted_redirect_is_rejected(self):
        url = "https://api.github.com/repos/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/actions/artifacts/1/zip"
        api = self.RedirectOpener("https://evil.invalid/a.zip")
        with tempfile.TemporaryDirectory() as directory, \
             mock.patch.object(MODULE, "build_opener", return_value=api):
            with self.assertRaisesRegex(MODULE.Stop, "ARTIFACT_REDIRECT_UNTRUSTED"):
                MODULE.download_artifact(url, "secret", Path(directory) / "a")

    def test_authorization_is_not_forwarded_to_blob(self):
        url = "https://api.github.com/repos/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/actions/artifacts/1/zip"
        location = "https://safe.blob.core.windows.net/container/object?sig=abc"
        api, blob = self.RedirectOpener(location), self.BlobOpener(location, b"artifact")
        with tempfile.TemporaryDirectory() as directory, \
             mock.patch.object(MODULE, "build_opener", side_effect=[api, blob]):
            destination = Path(directory) / "artifact.zip"
            MODULE.download_artifact(url, "secret", destination)
            self.assertEqual(destination.read_bytes(), b"artifact")
        self.assertEqual(api.request.get_header("Authorization"), "Bearer secret")
        self.assertIsNone(blob.request.get_header("Authorization"))


class AppliedPostcheck(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.root = Path(self.tmp.name)
        self.prepared, self.journal = self.root / "prepared.json", self.root / ("dual-" + APP + ".json")
        self.launcher, self.runtime = self.root / "launcher.json", self.root / "runtime.json"
        self.api, self.web = "sha256:" + "a" * 64, "sha256:" + "b" * 64
        self.images = {"api": self.api, "web": self.web}
        self.root_gate = mock.patch.object(MODULE, "require_root", return_value=None)
        self.root_gate.start()
        self.chain_patch = mock.patch.object(MODULE, "require_trusted_ancestors", return_value=None)
        self.chain_patch.start()
        write_private(self.prepared, {"source_sha": APP,
            "status": "PREPARED_API_WEB_SIGNATURE_VERIFIED_NO_DEPLOYMENT",
            "may_apply": False, "services_unchanged": True,
            "api_image_id": self.api, "web_image_id": self.web})
        write_private(self.journal, {"source_sha": APP, "status": "APPLIED", "new_images": self.images})
        write_private(self.launcher, {"status": "QA525_UPDATE_STATUS_READ_ONLY",
            "services_running": True, "prepared_exists": True,
            "archive_reconciliation_required": False, "may_apply": False})
        write_private(self.runtime, {"services": {
            "api": {"status": "running", "revision": APP, "image": self.api},
            "web": {"status": "running", "revision": APP, "image": self.web}}})

    def tearDown(self):
        self.root_gate.stop()
        self.chain_patch.stop()
        self.tmp.cleanup()

    def args(self):
        return type("Args", (), {"app_sha": APP, "prepared": self.prepared,
            "journal": self.journal, "launcher_status": self.launcher, "runtime": self.runtime})()

    def test_retained_prepared_is_valid_when_bound_and_closed(self):
        result = MODULE.postcheck(self.args())
        self.assertEqual(result["status"], "PASS_APPLIED")
        self.assertTrue(result["prepared_retained"])
        self.assertFalse(result["may_apply"])

    def test_retained_prepared_requires_real_launcher_schema(self):
        original = json.loads(self.prepared.read_text())
        for field, value in (("source_sha", "0" * 40), ("status", "PREPARED"),
                             ("api_image_id", "sha256:" + "c" * 64),
                             ("web_image_id", "sha256:" + "d" * 64),
                             ("services_unchanged", False), ("may_apply", True)):
            with self.subTest(field=field):
                record = dict(original)
                record[field] = value
                write_private(self.prepared, record)
                with self.assertRaisesRegex(MODULE.Stop, "PREPARED_NOT_BOUND_TO_APPLIED"):
                    MODULE.postcheck(self.args())
        write_private(self.prepared, original)

    def test_fabricated_old_fixture_is_rejected(self):
        write_private(self.prepared, {"source_sha": APP, "status": "PREPARED",
            "may_apply": False, "new_images": self.images})
        with self.assertRaisesRegex(MODULE.Stop, "PREPARED_NOT_BOUND_TO_APPLIED"):
            MODULE.postcheck(self.args())

    def test_runtime_and_next_drift_are_rejected(self):
        runtime = json.loads(self.runtime.read_text())
        runtime["services"]["api"]["image"] = "sha256:" + "c" * 64
        write_private(self.runtime, runtime)
        with self.assertRaisesRegex(MODULE.Stop, "RUNTIME_MISMATCH_API"):
            MODULE.postcheck(self.args())
        runtime["services"]["api"]["image"] = self.api
        write_private(self.runtime, runtime)
        write_private(Path(str(self.journal) + ".next"), {})
        with self.assertRaisesRegex(MODULE.Stop, "JOURNAL_NEXT_PRESENT"):
            MODULE.postcheck(self.args())


if __name__ == "__main__":
    unittest.main()
