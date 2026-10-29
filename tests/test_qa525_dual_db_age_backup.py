"""Hermetic QA525 dual encrypted database backup tests. NO Docker/no age."""
import contextlib
import io
import json
import os
from pathlib import Path
from tempfile import TemporaryDirectory
from types import SimpleNamespace
import importlib.util
import unittest
from unittest.mock import Mock, patch

SRC = (Path(__file__).resolve().parents[1] / "scripts/qa/operators"
       / "qa525-dual-db-age-backup.py")
spec = importlib.util.spec_from_file_location("qa525_dual_age_backup_tests", SRC)
mod = importlib.util.module_from_spec(spec)
spec.loader.exec_module(mod)


def metadata():
    values = {}
    for index, name in enumerate(mod.SERVICES + mod.JOBS):
        values[name] = [
            f"{index+1:064x}", "sha256:" + f"{index+1:064x}",
            "running" if name in mod.SERVICES else "exited",
            "2026-10-29", 0, name, "/root/qa", "/root/compose.json",
            [], {}, {}, 0,
        ]
    return values


def recipient():
    return "age1" + "q" * 58 + "\n"


class FakeProc:
    def __init__(self, *, status=0, content=None):
        self.stdout = io.BytesIO(content) if content is not None else None
        self.status = status
        self.killed = False

    def wait(self, timeout=None):
        return self.status

    def poll(self):
        return self.status if not self.killed else -9

    def kill(self):
        self.killed = True


def ciphertext_runner(*, db_fail=False, age_fail=False):
    calls = []

    def run(args, *, stdin=None, stdout=None):
        calls.append(tuple(args))
        if args[0] == str(mod.DOCKER):
            return FakeProc(status=5 if db_fail else 0,
                            content=b"PGDMP" + b"x" * 4096)
        if args[0] == str(mod.AGE):
            if stdout is not None:
                stdout.write(b"age-encryption.org/v1\n" + b"C" * 4096)
            return FakeProc(status=9 if age_fail else 0)
        raise AssertionError("UNEXPECTED_PROCESS")

    return run, calls


class DualAgeBackupTests(unittest.TestCase):
    def test_database_names_are_fixed_and_distinct(self):
        self.assertEqual(("pmgm", "pmgm_keycloak"), mod.DBS)
        self.assertNotIn("master", mod.DBS)

    def test_streaming_to_age_produces_ciphertext_only(self):
        with TemporaryDirectory() as temp:
            target = Path(temp) / "pmgm.pgdump.age"
            runner, calls = ciphertext_runner()
            result = mod.dump_age("pmgm", target, run=runner)
            self.assertEqual("pmgm.pgdump.age", result["file"])
            self.assertEqual(mod.sha256_file(target), result["sha256"])
            self.assertGreater(result["encrypted_bytes"], 1024)
            self.assertEqual(0o600, target.stat().st_mode & 0o777)
            self.assertNotIn(b"PGDMP", target.read_bytes())
            self.assertEqual(2, len(calls))
            self.assertEqual("pg_dump", calls[0][-6])
            self.assertEqual("--dbname", calls[0][-2])
            self.assertEqual("pmgm", calls[0][-1])
            self.assertEqual(str(mod.AGE), calls[1][0])
            self.assertEqual("-R", calls[1][1])

    def test_no_unlisted_or_malicious_database(self):
        with TemporaryDirectory() as temp:
            runner, calls = ciphertext_runner()
            with self.assertRaisesRegex(mod.Blocked, "INVALID_BACKUP_TARGET"):
                mod.dump_age("postgres", Path(temp) / "other.age", run=runner)
            self.assertEqual([], calls)

    def test_pg_dump_failure_rejects_even_if_age_exits_cleanly(self):
        with TemporaryDirectory() as temp:
            run, _ = ciphertext_runner(db_fail=True)
            with self.assertRaisesRegex(mod.Blocked, "ENCRYPTED_PG_DUMP_FAILED"):
                mod.dump_age("pmgm", Path(temp) / "pmgm.pgdump.age", run=run)

    def test_age_failure_rejects_even_when_pg_dump_exits_cleanly(self):
        with TemporaryDirectory() as temp:
            run, _ = ciphertext_runner(age_fail=True)
            with self.assertRaisesRegex(mod.Blocked, "ENCRYPTED_PG_DUMP_FAILED"):
                mod.dump_age("pmgm", Path(temp) / "pmgm.pgdump.age", run=run)

    def test_snapshot_verifies_7_active_and_3_completed(self):
        m = metadata()
        inv = SimpleNamespace(adapter=SimpleNamespace(metadata=lambda: m))
        got = mod.snapshot(inv)
        self.assertEqual(10, len(got))
        self.assertEqual("running", got["api"]["status"])
        self.assertEqual("exited", got["migrator"]["status"])
        self.assertNotIn("Config", json.dumps(got))

    def test_missing_one_shot_or_incomplete_postgres_blocks(self):
        for name in ("db-grants", "postgres"):
            m = metadata()
            m.pop(name)
            with self.assertRaisesRegex(mod.Blocked, "PROJECT_CONTAINER_SET_CHANGED"):
                mod.snapshot(SimpleNamespace(adapter=SimpleNamespace(metadata=lambda: m)))

    def test_one_shot_exit_failure_blocks(self):
        m = metadata()
        m["migrator"][11] = 1
        with self.assertRaisesRegex(mod.Blocked, "PREVIOUS_ONESHOT_JOB_UNEXPECTED"):
            mod.snapshot(SimpleNamespace(adapter=SimpleNamespace(metadata=lambda: m)))

    def test_active_postgres_stopped_blocks(self):
        m = metadata()
        m["postgres"][2] = "exited"
        with self.assertRaisesRegex(mod.Blocked, "ACTIVE_SERVICE_NOT_RUNNING"):
            mod.snapshot(SimpleNamespace(adapter=SimpleNamespace(metadata=lambda: m)))

    def test_recipient_is_exact_one_public_age_key_and_not_echoed(self):
        with TemporaryDirectory() as temp:
            path = Path(temp) / "recipient"
            path.write_text(recipient())
            os.chmod(path, 0o600)
            with patch.object(mod, "RECIPIENT", path), patch.object(mod, "trusted"):
                h = mod.public_recipient()
                self.assertEqual(64, len(h))
                self.assertNotIn("age1", h)
                path.write_text("ssh-ed25519 AAAA\n")
                with self.assertRaisesRegex(mod.Blocked, "RECIPIENT_FILE_INVALID"):
                    mod.public_recipient()

    def test_binary_or_multiple_age_recipients_block(self):
        with TemporaryDirectory() as temp:
            path = Path(temp) / "recipient"
            for content in (b"\xff", recipient().encode() * 2,
                            (recipient() + "\n").encode()):
                path.write_bytes(content)
                with patch.object(mod, "RECIPIENT", path), patch.object(mod, "trusted"):
                    with self.assertRaisesRegex(mod.Blocked, "RECIPIENT_FILE_INVALID"):
                        mod.public_recipient()

    def test_exclusive_lock_fails_closed_on_bad_mode(self):
        with TemporaryDirectory() as temp:
            lock = Path(temp) / "lock"
            lock.write_text("")
            os.chmod(lock, 0o644)
            with patch.object(mod, "LOCKS", (lock,)), patch.object(mod, "lock_parent"):
                with self.assertRaisesRegex(mod.Blocked, "OPERATOR_LOCK_UNTRUSTED"):
                    with mod.exclusive():
                        pass

    def test_success_creates_two_encrypted_files_and_manifest_no_apply(self):
        with TemporaryDirectory() as temp:
            state = Path(temp) / "qa525"
            state.mkdir()
            os.chmod(state, 0o700)
            recipient_file = Path(temp) / "public-key"
            recipient_file.write_text(recipient())
            os.chmod(recipient_file, 0o600)
            calls = []
            def fake_dump(database, destination):
                self.assertEqual(".age", destination.suffix)
                self.assertFalse(destination.exists())
                destination.write_bytes(b"age-encryption.org/v1\n" + b"D" * 4096)
                os.chmod(destination, 0o600)
                calls.append(database)
                return {"file": destination.name, "encrypted_bytes": destination.stat().st_size,
                        "sha256": mod.sha256_file(destination)}
            with patch.object(mod, "STATE", state), \
                 patch.object(mod, "RECIPIENT", recipient_file), \
                 patch.object(mod.os, "geteuid", return_value=0), \
                 patch.object(mod.socket, "gethostname", return_value="glmdch"), \
                 patch.object(mod, "trusted"), \
                 patch.object(mod, "exclusive", return_value=contextlib.nullcontext()), \
                 patch.object(mod, "load_inventory",
                              return_value=SimpleNamespace(
                                  adapter=SimpleNamespace(metadata=metadata))), \
                 patch.object(mod, "check_postgres"), \
                 patch.object(mod, "dump_age", side_effect=fake_dump):
                result = mod.backup()
            self.assertEqual(list(mod.DBS), calls)
            self.assertFalse(result["offhost_custody_verified"])
            self.assertFalse(result["restoration_drill_verified"])
            self.assertFalse(result["may_apply"])
            completed = [p for p in state.iterdir() if p.is_dir()]
            self.assertEqual(1, len(completed))
            files = sorted(p.name for p in completed[0].iterdir())
            self.assertEqual(["manifest.json", "pmgm.pgdump.age",
                              "pmgm_keycloak.pgdump.age"], files)
            manifest = json.loads((completed[0] / "manifest.json").read_text())
            self.assertEqual(list(mod.DBS), manifest["databases"])
            self.assertFalse(manifest["restoration_drill_verified"])
            self.assertFalse(manifest["may_apply"])
            self.assertNotIn("age1", json.dumps(manifest))
            self.assertNotIn("PGDMP", json.dumps(manifest))

    def test_failed_second_database_does_not_finalize_partial_pair(self):
        with TemporaryDirectory() as temp:
            state = Path(temp) / "qa525"
            state.mkdir()
            os.chmod(state, 0o700)
            def fail_second(database, target):
                if database == "pmgm_keycloak":
                    raise mod.Blocked("SECOND_DATABASE_FAILED")
                target.write_bytes(b"encrypted")
                return {"file": target.name, "sha256": "a" * 64}
            with patch.object(mod, "STATE", state), \
                 patch.object(mod.os, "geteuid", return_value=0), \
                 patch.object(mod.socket, "gethostname", return_value="glmdch"), \
                 patch.object(mod, "trusted"), \
                 patch.object(mod, "public_recipient", return_value="a" * 64), \
                 patch.object(mod, "exclusive", return_value=contextlib.nullcontext()), \
                 patch.object(mod, "load_inventory",
                              return_value=SimpleNamespace(
                                  adapter=SimpleNamespace(metadata=metadata))), \
                 patch.object(mod, "check_postgres"), \
                 patch.object(mod, "dump_age", side_effect=fail_second):
                with self.assertRaisesRegex(mod.Blocked, "SECOND_DATABASE_FAILED"):
                    mod.backup()
            self.assertEqual([], list(state.iterdir()))

    def test_forged_cli_option_never_triggers_backup(self):
        for argv in (["--apply"], ["--prepare"], ["--restore"], []):
            with patch.object(mod, "backup") as operation, \
                 patch("sys.stdout", io.StringIO()):
                self.assertEqual(2, mod.main(argv))
                operation.assert_not_called()

    def test_secret_exception_never_leaks_to_stdout(self):
        out = io.StringIO()
        with patch.object(mod, "backup",
                          side_effect=RuntimeError("PASSWORD_REAL_SECRET")), \
             patch("sys.stdout", out):
            self.assertEqual(2, mod.main(["--backup"]))
        self.assertNotIn("PASSWORD_REAL_SECRET", out.getvalue())
        self.assertIn('"may_apply": false', out.getvalue())

    def test_read_only_db_dump_no_service_mutating_commands(self):
        code = SRC.read_text()
        for x in ("docker compose", '"up"', '"down"', '"restart"',
                  "pg_restore", "psql", "DROP DATABASE", "TRUNCATE",
                  "subprocess.run(['sh'", "os.system("):
            self.assertNotIn(x, code)
        self.assertIn('"may_apply": False', code)
        self.assertIn("pg_dump", code)


if __name__ == "__main__":
    unittest.main()
