"""Offline QA525 offhost restore laboratory tests; no real containers/keys."""
import io
import contextlib
import json
import os
from pathlib import Path
import importlib.util
from tempfile import TemporaryDirectory
import unittest
from unittest.mock import Mock, patch
from types import SimpleNamespace

SRC = (Path(__file__).resolve().parents[1] / "scripts/qa/operators"
       / "qa525-offhost-dual-restore-drill.py")
spec = importlib.util.spec_from_file_location("qa525_restore_drill_test", SRC)
mod = importlib.util.module_from_spec(spec)
spec.loader.exec_module(mod)
BACKUP = "backup-20261029T121500Z-abcdef0123456789"


def fixture_bundle(base):
    bundle = Path(base) / BACKUP
    bundle.mkdir()
    files = []
    for name in ("pmgm.pgdump.age", "pmgm_keycloak.pgdump.age"):
        path = bundle / name
        path.write_bytes(b"age-encryption.org/v1\n" + name.encode() + b"X" * 2048)
        os.chmod(path, 0o600)
        files.append({"file": name, "encrypted_bytes": path.stat().st_size,
                      "sha256": mod.digest_file(path)})
    manifest = {
        "status": "TWO_DATABASES_ENCRYPTED_LOCALLY_UNVERIFIED_OFFHOST",
        "scope": "ENCRYPTED_LOCAL_DUAL_DB_BACKUP_NO_OFFHOST_NO_RESTORE_NO_APPLY",
        "environment": "QA_SYNTHETIC",
        "databases": ["pmgm", "pmgm_keycloak"],
        "archives": files,
        "offhost_custody_verified": False,
        "restoration_drill_verified": False,
        "may_apply": False,
    }
    (bundle / "manifest.json").write_text(json.dumps(manifest))
    os.chmod(bundle / "manifest.json", 0o600)
    os.chmod(bundle, 0o700)
    return bundle, manifest


class FakeProc:
    def __init__(self, stdout=None, rc=0):
        self.stdout = io.BytesIO(b"PGDMPsynthetic") if stdout is True else None
        self.rc = rc
        self.killed = False

    def wait(self, timeout=None):
        return self.rc

    def poll(self):
        return self.rc

    def kill(self):
        self.killed = True


class OffhostRestoreTests(unittest.TestCase):
    def test_both_encrypted_files_and_hashes_are_required(self):
        with TemporaryDirectory() as temp:
            bundle, _ = fixture_bundle(temp)
            with patch.object(mod, "trusted"):
                result = mod.read_manifest(bundle)
            self.assertEqual(
                {"pmgm.pgdump.age", "pmgm_keycloak.pgdump.age"}, set(result))

    def test_sha_mismatch_blocks_before_decryption(self):
        with TemporaryDirectory() as temp:
            bundle, _ = fixture_bundle(temp)
            (bundle / "pmgm.pgdump.age").write_bytes(b"tampered")
            with patch.object(mod, "trusted"):
                with self.assertRaisesRegex(mod.Blocked, "OFFHOST_CIPHERTEXT_DIGEST_MISMATCH"):
                    mod.read_manifest(bundle)

    def test_missing_or_extra_file_blocks(self):
        with TemporaryDirectory() as temp:
            bundle, _ = fixture_bundle(temp)
            (bundle / "pmgm_keycloak.pgdump.age").unlink()
            with patch.object(mod, "trusted"):
                with self.assertRaisesRegex(mod.Blocked, "INCOMPLETE_OR_EXTRA"):
                    mod.read_manifest(bundle)

    def test_manifest_claim_cannot_forge_approval(self):
        with TemporaryDirectory() as temp:
            bundle, receipt = fixture_bundle(temp)
            receipt["may_apply"] = True
            (bundle / "manifest.json").write_text(json.dumps(receipt))
            with patch.object(mod, "trusted"):
                with self.assertRaisesRegex(mod.Blocked, "UNTRUSTED_BACKUP_RECEIPT"):
                    mod.read_manifest(bundle)

    def test_duplicate_database_file_entry_blocks(self):
        with TemporaryDirectory() as temp:
            bundle, receipt = fixture_bundle(temp)
            receipt["archives"][1] = receipt["archives"][0]
            (bundle / "manifest.json").write_text(json.dumps(receipt))
            with patch.object(mod, "trusted"):
                with self.assertRaisesRegex(mod.Blocked, "INVALID_BACKUP_FILE_ENTRY"):
                    mod.read_manifest(bundle)

    def test_only_fixed_restore_database_names(self):
        self.assertEqual(
            (("pmgm", "qa525_verify_pmgm"),
             ("pmgm_keycloak", "qa525_verify_pmgm_keycloak")),
            mod.RESTORE_DATABASES)

    def test_decrypted_bytes_go_only_to_pg_restore_pipe(self):
        commands = []
        def fake_start(args, *, stdin=None, stdout=None, **kw):
            commands.append((list(args), stdin, stdout, kw))
            return FakeProc(stdout=(stdout == mod.subprocess.PIPE))
        mod.decrypt_to_restore(Path("/fake/encrypted.age"),
                               "qa525_verify_pmgm", start=fake_start)
        self.assertEqual(2, len(commands))
        self.assertEqual(str(mod.AGE), commands[0][0][0])
        self.assertIn("--decrypt", commands[0][0])
        self.assertIn("pg_restore", commands[1][0])
        self.assertEqual(mod.subprocess.DEVNULL, commands[1][2])
        self.assertNotIn("qa525_verify_pmgm", commands[0][0])

    def test_catalog_inspection_uses_list_not_modify(self):
        commands = []
        def start(args, *, stdin=None, stdout=None, **kw):
            commands.append(args)
            return FakeProc(stdout=(stdout == mod.subprocess.PIPE))
        mod.decrypt_to_restore(Path("/fake/pmgm.age"),
                               "qa525_verify_pmgm", list_only=True, start=start)
        self.assertIn("--list", commands[1])
        self.assertNotIn("--dbname", commands[1])

    def test_forged_restore_database_rejected(self):
        run = Mock()
        with self.assertRaisesRegex(mod.Blocked, "RESTORE_DATABASE_INVALID"):
            mod.decrypt_to_restore(Path("/fake"), "postgres", start=run)
        run.assert_not_called()

    def test_lab_check_rejects_external_network(self):
        replies = ["qa525-restore-lab", "postgres", "running", "bridge"]
        with patch.object(mod, "command", side_effect=replies):
            with self.assertRaisesRegex(mod.Blocked, "RESTORE_CONTAINER_NOT_ISOLATED"):
                mod.check_lab()

    def safe_lab_replies(self):
        return [
            "qa525-restore-lab", "postgres", "running", "none",
            "false", "", "private", "[]", "null", "{}",
            "postgres:17@sha256:" + "a" * 64,
            "sha256:" + "b" * 64,
            '[{"Type":"volume","Name":"qa525-restore-lab_pgdata",'
            '"Destination":"/var/lib/postgresql/data","RW":true}]',
            '{"com.docker.compose.project":"qa525-restore-lab",'
            '"com.docker.compose.volume":"pgdata"}',
            "qa525-restore-lab-postgres-1",
        ]

    def test_lab_check_rejects_bind_mount(self):
        replies = self.safe_lab_replies()
        replies[12] = '[{"Type":"bind","Source":"/var/lib/postgresql/data"}]'
        with patch.object(mod, "command", side_effect=replies):
            with self.assertRaisesRegex(mod.Blocked, "RESTORE_VOLUME_NOT_ISOLATED"):
                mod.check_lab()

    def test_lab_check_accepts_only_named_test_volume(self):
        with patch.object(mod, "command", side_effect=self.safe_lab_replies()):
            mod.check_lab()

    def test_privileged_or_host_pid_mode_blocks(self):
        for index, value in ((4, "true"), (5, "host"), (6, "host"),
                             (7, '[{"PathOnHost":"/dev/sda"}]'),
                             (8, '["/etc:/etc"]'),
                             (9, '{"5432/tcp":[{"HostPort":"5432"}]}')):
            with self.subTest(index=index):
                data = self.safe_lab_replies()
                data[index] = value
                with patch.object(mod, "command", side_effect=data):
                    with self.assertRaisesRegex(mod.Blocked, "RESTORE_LAB_HOST_ESCAPE_RISK"):
                        mod.check_lab()

    def test_restore_container_image_must_be_postgres17_oci_digest(self):
        for index, value in ((10, "postgres:17"),
                             (10, "postgres:latest@sha256:" + "a" * 64),
                             (11, "sha256:unknown")):
            with self.subTest(index=index, value=value):
                data = self.safe_lab_replies()
                data[index] = value
                with patch.object(mod, "command", side_effect=data):
                    with self.assertRaisesRegex(mod.Blocked, "RESTORE_LAB_POSTGRES_IMAGE_UNPINNED"):
                        mod.check_lab()

    def test_foreign_volume_labels_or_shared_volume_blocks(self):
        for index, value, code in (
            (13, '{"com.docker.compose.project":"centenario-qa-lab2"}',
             "RESTORE_VOLUME_PROVENANCE_INVALID"),
            (14, "qa525-restore-lab-postgres-1\\nother-container",
             "RESTORE_VOLUME_SHARED_OR_UNVERIFIED"),
        ):
            with self.subTest(index=index):
                data = self.safe_lab_replies()
                data[index] = value
                with patch.object(mod, "command", side_effect=data):
                    with self.assertRaisesRegex(mod.Blocked, code):
                        mod.check_lab()

    def test_drill_fails_if_any_target_database_already_used(self):
        with patch.object(mod.os, "geteuid", return_value=0), \
             patch.object(mod.socket, "gethostname", return_value="isolated-lab"), \
             patch.object(mod, "trusted"), \
             patch.object(mod, "read_manifest", return_value={
                 "pmgm.pgdump.age": Path("/a"),
                 "pmgm_keycloak.pgdump.age": Path("/b")}), \
             patch.object(mod, "check_lab"), \
             patch.object(mod, "lab_exclusive", return_value=contextlib.nullcontext()), \
             patch.object(mod, "lab_table_count", side_effect=[0, 1]), \
             patch.object(mod, "decrypt_to_restore") as rest:
            with self.assertRaisesRegex(mod.Blocked, "RESTORE_LAB_DATABASE_NOT_EMPTY"):
                mod.drill(BACKUP)
            rest.assert_not_called()

    def test_restore_requires_root_private_lock_before_any_files(self):
        with TemporaryDirectory() as temp:
            lock = Path(temp) / "restore.lock"
            lock.write_bytes(b"")
            os.chmod(lock, 0o644)
            with patch.object(mod, "LOCK", lock), patch.object(mod, "trusted"):
                with self.assertRaisesRegex(mod.Blocked, "RESTORE_LOCK_UNTRUSTED"):
                    with mod.lab_exclusive():
                        pass

    def test_restore_lock_denies_overlap(self):
        import fcntl
        with TemporaryDirectory() as temp:
            lock = Path(temp) / "restore.lock"
            lock.write_bytes(b"")
            os.chmod(lock, 0o600)
            # Kernel lock behavior is tested without bypassing file-open checks:
            # fake only root UID in fstat since GitHub runners are not root.
            old = mod.os.fstat
            def root_stat(fd):
                st = old(fd)
                return SimpleNamespace(st_mode=st.st_mode, st_uid=0,
                                       st_nlink=st.st_nlink)
            with patch.object(mod, "LOCK", lock), \
                 patch.object(mod, "trusted"), \
                 patch.object(mod.os, "fstat", side_effect=root_stat):
                with mod.lab_exclusive():
                    with self.assertRaisesRegex(mod.Blocked, "CONCURRENT_RESTORE_LAB_OPERATION"):
                        with mod.lab_exclusive():
                            pass

    def test_real_glmdch_is_explicitly_forbidden(self):
        with patch.object(mod.os, "geteuid", return_value=0), \
             patch.object(mod.socket, "gethostname", return_value="glmdch"), \
             patch.object(mod, "read_manifest") as read:
            with self.assertRaisesRegex(mod.Blocked, "OFFHOST_ROOT_ONLY"):
                mod.drill(BACKUP)
            read.assert_not_called()

    def test_two_full_restores_only_from_empty_lab_return_no_apply(self):
        calls = []
        with patch.object(mod.os, "geteuid", return_value=0), \
             patch.object(mod.socket, "gethostname", return_value="isolated-lab"), \
             patch.object(mod, "trusted"), \
             patch.object(mod, "read_manifest", return_value={
                 "pmgm.pgdump.age": Path("/a"),
                 "pmgm_keycloak.pgdump.age": Path("/b")}), \
             patch.object(mod, "check_lab"), \
             patch.object(mod, "lab_exclusive", return_value=contextlib.nullcontext()), \
             patch.object(mod, "lab_table_count", side_effect=[0, 0, 42, 35]), \
             patch.object(mod, "decrypt_to_restore",
                          side_effect=lambda p, db, list_only=False: calls.append((db, list_only))):
            result = mod.drill(BACKUP)
        self.assertEqual(
            [("qa525_verify_pmgm", True), ("qa525_verify_pmgm_keycloak", True),
             ("qa525_verify_pmgm", False), ("qa525_verify_pmgm_keycloak", False)],
            calls)
        self.assertFalse(result["may_apply"])
        self.assertFalse(result["independent_offhost_custody_attested"])
        self.assertFalse(result["repository_or_host_signed_evidence"])

    def test_invalid_backup_name_and_unknown_cli_block_without_mutations(self):
        for wrong in ("../../etc/passwd", "backup-today", "", "pmgm"):
            with patch.object(mod, "trusted") as trust, \
                 patch.object(mod.os, "geteuid", return_value=0), \
                 patch.object(mod.socket, "gethostname", return_value="isolated-lab"):
                with self.assertRaisesRegex(mod.Blocked, "BACKUP_IDENTIFIER_INVALID"):
                    mod.drill(wrong)
                trust.assert_not_called()
        for argv in (["--apply", BACKUP], ["--restore", BACKUP], []):
            with patch.object(mod, "drill") as method, patch("sys.stdout", io.StringIO()):
                self.assertEqual(2, mod.main(argv))
                method.assert_not_called()

    def test_sensitive_exception_text_never_reaches_stdout(self):
        out = io.StringIO()
        with patch.object(mod, "drill", side_effect=RuntimeError("AGE_PRIVATE_KEY_TEXT")), \
             patch("sys.stdout", out):
            self.assertEqual(2, mod.main(["--restore-drill", BACKUP]))
        self.assertNotIn("AGE_PRIVATE_KEY_TEXT", out.getvalue())
        self.assertIn('"may_apply": false', out.getvalue())

    def test_no_glmdch_or_production_commands(self):
        code = SRC.read_text()
        for phrase in ("docker compose up", "DROP DATABASE",
                       "ALTER ROLE", '"--force-recreate"', "systemctl",
                       "psql --password", "os.system("):
            self.assertNotIn(phrase, code)
        self.assertIn('"may_apply": False', code)


if __name__ == "__main__":
    unittest.main()
