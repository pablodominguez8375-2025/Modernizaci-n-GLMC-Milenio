"""QA525 G6 v2 producer -- hermetic tests: fake Restic, tmp dirs, no host, no secrets."""
import datetime as dt
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import stat
import tempfile
import unittest

SOURCE = Path(__file__).resolve().parents[1] / "scripts/qa/qa525-vnext-g6-v2-producer.py"
spec = importlib.util.spec_from_file_location("qa525_vnext_g6_v2_producer_test", SOURCE)
prod = importlib.util.module_from_spec(spec)
spec.loader.exec_module(prod)
UID = os.getuid()
NOW = dt.datetime(2026, 11, 8, 12, 0, 0, tzinfo=dt.timezone.utc)
KEY = b"k" * 40
REPO_ID = "ab" * 32


class G6V2ProducerFakeRestic:
    """Safe simulator: records calls, writes ciphertext-looking blobs only."""

    def __init__(self, repo_id=REPO_ID):
        self.calls, self.repo_id_value, self.fail = [], repo_id, None

    def _maybe_fail(self, op):
        if self.fail == op:
            raise prod.Stop("G6P_RESTIC_FAILED")

    def init(self, repo, keyfile):
        self.calls.append("init"); self._maybe_fail("init")
        os.mkdir(os.path.join(repo, "data"), 0o700)
        Path(repo, "config").write_bytes(b"\x00cipher-config")

    def repo_id(self, repo, keyfile):
        self.calls.append("repo_id"); return self.repo_id_value

    def backup(self, repo, keyfile, sources):
        self.calls.append("backup"); self._maybe_fail("backup")
        Path(repo, "data", "blob-%d" % len(self.calls)).write_bytes(b"\x01cipher")

    def check(self, repo, keyfile):
        self.calls.append("check"); self._maybe_fail("check")

    def restore(self, repo, keyfile, target):
        self.calls.append("restore"); self._maybe_fail("restore")
        Path(target, "restored.txt").write_text("synthetic")


def verifier_ok(target):
    return {"pmgm": 12, "pmgm_keycloak": 7}, 3


class G6V2ProducerBase(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.root = os.path.join(self.tmp.name, "g6")
        os.mkdir(self.root, 0o700)
        self.fake = G6V2ProducerFakeRestic()
        self.epoch = prod.key_epoch(KEY)
        self.pins = dict(expected_repo_id=REPO_ID, expected_key_epoch=self.epoch)

    def tearDown(self):
        self.tmp.cleanup()

    def init(self):
        return prod.init_repo(self.root, runner=self.fake, key_bytes=KEY, uid=UID, now=NOW)

    def backup(self, **over):
        args = dict(runner=self.fake, sources=["/synthetic/src"], uid=UID, now=NOW,
                    **self.pins)
        args.update(over)
        return prod.backup(self.root, **args)

    def code(self, call, *a, **k):
        with self.assertRaises(prod.Stop) as ctx:
            call(*a, **k)
        return str(ctx.exception)


class G6V2ProducerInitTests(G6V2ProducerBase):
    def test_init_creates_everything_once_with_safe_modes(self):
        ident = self.init()
        self.assertEqual(ident, {"restic_repo_id": REPO_ID, "key_epoch": self.epoch})
        key = os.path.join(self.root, "key-v2")
        self.assertEqual(stat.S_IMODE(os.lstat(key).st_mode), 0o600)
        self.assertEqual(Path(key).read_bytes(), KEY)
        raw = Path(self.root, "state-v2", "identity.json").read_bytes()
        self.assertEqual(raw, prod.canonical(json.loads(raw)))
        self.assertNotIn(KEY, raw)
        self.assertEqual(self.fake.calls, ["init", "repo_id"])

    def test_second_init_is_stop_and_never_touches_restic(self):
        self.init(); self.fake.calls.clear()
        self.assertEqual(self.code(self.init), "G6P_INIT_TARGET_EXISTS")
        self.assertEqual(self.fake.calls, [])

    def test_init_refuses_each_preexisting_target(self):
        for name, maker in (("key-v2", lambda p: Path(p).write_bytes(b"x")),
                            ("repo-v2", os.mkdir), ("state-v2", os.mkdir)):
            with self.subTest(name):
                self.tearDown(); self.setUp()
                maker(os.path.join(self.root, name))
                self.assertEqual(self.code(self.init), "G6P_INIT_TARGET_EXISTS")
                self.assertEqual(self.fake.calls, [])

    def test_init_refuses_dangling_symlink_target(self):
        os.symlink("/nonexistent", os.path.join(self.root, "key-v2"))
        self.assertEqual(self.code(self.init), "G6P_INIT_TARGET_EXISTS")

    def test_weak_or_bad_keys_and_clock_are_stop(self):
        for key in (b"short", "x" * 40, b"a" * 40 + b"\0"):
            with self.subTest(key=type(key)):
                self.assertEqual(self.code(prod.init_repo, self.root, runner=self.fake,
                                           key_bytes=key, uid=UID, now=NOW),
                                 "G6P_KEY_INVALID")
        self.assertEqual(self.code(prod.init_repo, self.root, runner=self.fake,
                                   key_bytes=KEY, uid=UID, now=dt.datetime(2026, 1, 1)),
                         "G6P_CLOCK_INVALID")
        self.assertFalse(os.path.lexists(os.path.join(self.root, "key-v2")))

    def test_bad_repo_id_from_runner_is_stop(self):
        self.fake.repo_id_value = "nothex"
        self.assertEqual(self.code(self.init), "G6P_REPO_ID_INVALID")

    def test_unsafe_root_is_stop(self):
        os.chmod(self.root, 0o755)
        self.assertEqual(self.code(self.init), "G6P_DIR_UNSAFE")
        self.assertEqual(self.code(prod.init_repo, "relative", runner=self.fake,
                                   key_bytes=KEY, uid=UID, now=NOW), "G6P_ROOT_INVALID")
        self.assertEqual(self.code(prod.init_repo, self.root, runner=self.fake,
                                   key_bytes=KEY, uid=UID + 1, now=NOW), "G6P_DIR_UNSAFE")


class G6V2ProducerBackupTests(G6V2ProducerBase):
    def setUp(self):
        super().setUp(); self.init(); self.fake.calls.clear()

    def test_backup_never_inits_and_is_repeatable_without_touching_key(self):
        key = os.path.join(self.root, "key-v2")
        before = os.lstat(key)
        first = self.backup()
        later = NOW + dt.timedelta(hours=1)
        second = self.backup(now=later)
        self.assertNotIn("init", self.fake.calls)
        self.assertEqual(Path(key).read_bytes(), KEY)
        self.assertEqual(os.lstat(key).st_mtime_ns, before.st_mtime_ns)
        self.assertNotEqual(first["archive_name"], second["archive_name"])

    def test_receipt_is_canonical_v2_and_matches_archive(self):
        out = self.backup()
        name = out["archive_name"]
        self.assertRegex(name, r"^centenario-qa-g6-v2-CIFRADO-20261108T120000Z\.tar\.gz$")
        archive = Path(self.root, "exports-v2", name).read_bytes()
        self.assertEqual(hashlib.sha256(archive).hexdigest(), out["archive_sha256"])
        raw = Path(self.root, "state-v2", "receipt-20261108T120000Z.json").read_bytes()
        doc = json.loads(raw)
        self.assertEqual(raw, prod.canonical(doc))
        self.assertEqual(set(doc), {"schema", "archive_name", "sha256", "status",
                                    "restic_repo_id", "key_epoch"})
        self.assertEqual(doc["schema"], "qa525-g6-v2-receipt")
        self.assertEqual(doc["sha256"], out["archive_sha256"])
        self.assertNotIn(KEY, archive)

    def test_archive_is_deterministic_for_same_repo_state(self):
        paths = {n: os.path.join(self.root, n) for n in ("repo-v2", "key-v2")}
        one, two = (os.path.join(self.tmp.name, n) for n in ("a.tgz", "b.tgz"))
        h1 = prod._write_archive(paths["repo-v2"], paths["key-v2"], one)
        h2 = prod._write_archive(paths["repo-v2"], paths["key-v2"], two)
        self.assertEqual(h1, h2)

    def test_same_second_backup_never_overwrites(self):
        self.backup()
        self.assertEqual(self.code(self.backup), "G6P_TARGET_EXISTS")

    def test_wrong_pins_are_stop_before_any_restic_backup(self):
        for key, value in (("expected_repo_id", "cd" * 32),
                           ("expected_key_epoch", "0" * 32)):
            with self.subTest(key):
                self.assertEqual(self.code(self.backup, **{key: value}),
                                 "G6P_GENERATION_MISMATCH")
        self.assertNotIn("backup", self.fake.calls)

    def test_runner_reporting_other_repo_is_stop(self):
        self.fake.repo_id_value = "cd" * 32
        self.assertEqual(self.code(self.backup), "G6P_GENERATION_MISMATCH")
        self.assertNotIn("backup", self.fake.calls)

    def test_foreign_or_tampered_key_is_stop(self):
        key = os.path.join(self.root, "key-v2")
        os.chmod(key, 0o600)
        Path(key).write_bytes(b"z" * 40)
        self.assertEqual(self.code(self.backup), "G6P_GENERATION_MISMATCH")

    def test_unsafe_key_file_variants_are_stop(self):
        key = os.path.join(self.root, "key-v2")
        os.chmod(key, 0o640)
        self.assertEqual(self.code(self.backup), "G6P_FILE_UNSAFE")
        os.chmod(key, 0o600); os.unlink(key); os.symlink("/etc/hostname", key)
        self.assertEqual(self.code(self.backup), "G6P_FILE_UNSAFE")

    def test_missing_identity_or_repo_is_stop_and_never_creates(self):
        os.unlink(os.path.join(self.root, "state-v2", "identity.json"))
        self.assertEqual(self.code(self.backup), "G6P_FILE_UNSAFE")
        import shutil
        shutil.rmtree(os.path.join(self.root, "repo-v2"))
        self.assertEqual(self.code(self.backup), "G6P_DIR_UNSAFE")
        self.assertFalse(os.path.lexists(os.path.join(self.root, "repo-v2")))

    def test_noncanonical_identity_is_stop(self):
        ident = os.path.join(self.root, "state-v2", "identity.json")
        doc = json.loads(Path(ident).read_bytes())
        Path(ident).write_bytes(json.dumps(doc, indent=2).encode())
        self.assertEqual(self.code(self.backup), "G6P_IDENTITY_INVALID")

    def test_restic_failure_leaves_no_receipt(self):
        for op in ("backup", "check"):
            with self.subTest(op):
                self.fake.fail = op
                self.assertEqual(self.code(self.backup), "G6P_RESTIC_FAILED")
                self.assertEqual(os.listdir(os.path.join(self.root, "exports-v2")), [])

    def test_bad_sources_are_stop(self):
        for src in ([], ["relative"], "/abs", [b"/abs"], ["/a\0b"]):
            with self.subTest(src=src):
                self.assertEqual(self.code(self.backup, sources=src), "G6P_SOURCES_INVALID")

    def test_special_file_in_repo_is_stop(self):
        os.symlink("/etc/hostname", os.path.join(self.root, "repo-v2", "link"))
        self.assertEqual(self.code(self.backup), "G6P_ARCHIVE_SPECIAL_FILE")

    def test_identity_file_claim_must_equal_the_independent_pin(self):
        ident = os.path.join(self.root, "state-v2", "identity.json")
        doc = json.loads(Path(ident).read_bytes())
        doc["restic_repo_id"] = "cd" * 32
        Path(ident).write_bytes(prod.canonical(doc))
        self.assertEqual(self.code(self.backup), "G6P_GENERATION_MISMATCH")

    def test_archive_members_are_normalised_and_hold_no_owner_or_time(self):
        import tarfile
        out = self.backup()
        with tarfile.open(os.path.join(self.root, "exports-v2", out["archive_name"])) as tar:
            members = tar.getmembers()
        self.assertTrue(members)
        for m in members:
            self.assertEqual((m.mtime, m.uid, m.gid, m.uname, m.gname), (0, 0, 0, "", ""))
            self.assertIn(m.mode, (0o700, 0o600))

    def test_directory_owner_is_enforced(self):
        self.assertEqual(self.code(prod._safe_dir, self.root, UID + 1), "G6P_DIR_UNSAFE")
        self.assertEqual(self.code(prod._safe_file, os.path.join(self.root, "key-v2"),
                                   UID + 1), "G6P_FILE_UNSAFE")

    def test_key_inside_repo_is_stop(self):
        self.assertEqual(self.code(prod._write_archive, self.root,
                                   os.path.join(self.root, "key-v2"),
                                   os.path.join(self.tmp.name, "x.tgz")),
                         "G6P_KEY_INSIDE_REPO")


class G6V2ProducerRestoreReviewTests(G6V2ProducerBase):
    def setUp(self):
        super().setUp(); self.init()
        out = self.backup()
        self.name, self.sha = out["archive_name"], out["archive_sha256"]
        self.fake.calls.clear()
        self.forbidden = [os.path.join(self.tmp.name, "prod")]

    def restore(self, **over):
        args = dict(runner=self.fake, verifier=verifier_ok, archive_name=self.name,
                    archive_sha256=self.sha, forbidden_prefixes=self.forbidden, uid=UID,
                    now=NOW, **self.pins)
        args.update(over)
        return prod.restore(self.root, **args)

    def test_restore_writes_canonical_v2_report_inside_new_dir(self):
        report = self.restore()
        self.assertFalse(report["production_touched"])
        raw = Path(self.root, "state-v2", "isolated-restore-v2-20261108T120000Z.report.json"
                   ).read_bytes()
        self.assertEqual(raw, prod.canonical(json.loads(raw)))
        self.assertTrue(Path(self.root, "restores-v2", "20261108T120000Z",
                             "restored.txt").exists())

    def test_second_restore_same_cut_is_stop(self):
        self.restore()
        self.assertEqual(self.code(self.restore), "G6P_TARGET_EXISTS")

    def test_archive_hash_or_name_mismatch_is_stop(self):
        self.assertEqual(self.code(self.restore, archive_sha256="0" * 64),
                         "G6P_ARCHIVE_HASH_MISMATCH")
        for bad in ("centenario-qa-g6-CIFRADO-20261108T120000Z.tar.gz", "../x", None):
            with self.subTest(bad=bad):
                self.assertEqual(self.code(self.restore, archive_name=bad),
                                 "G6P_ARCHIVE_REF_INVALID")
        self.assertNotIn("restore", self.fake.calls)

    def test_forbidden_target_is_stop(self):
        self.assertEqual(self.code(self.restore, forbidden_prefixes=[self.root]),
                         "G6P_RESTORE_TARGET_FORBIDDEN")
        self.assertEqual(self.code(self.restore, forbidden_prefixes=[]),
                         "G6P_RESTORE_TARGET_FORBIDDEN")
        self.assertNotIn("restore", self.fake.calls)

    def test_verifier_results_are_strict(self):
        bad = ({"pmgm": 1}, 0), ({"pmgm": 0, "pmgm_keycloak": 1}, 0), \
            ({"pmgm": True, "pmgm_keycloak": 1}, 0), \
            ({"pmgm": 1, "pmgm_keycloak": 1, "extra": 1}, 0), \
            ({"pmgm": 1, "pmgm_keycloak": 1}, -1), ({"pmgm": 1, "pmgm_keycloak": 1}, True)
        for tables, silo in bad:
            with self.subTest(tables=tables, silo=silo):
                self.tearDown(); self.setUp()
                self.assertEqual(self.code(self.restore,
                                           verifier=lambda t: (tables, silo)),
                                 "G6P_RESTORE_VERIFY_FAILED")
                self.assertEqual(os.listdir(os.path.join(self.root, "state-v2")).count(
                    "isolated-restore-v2-20261108T120000Z.report.json"), 0)

    def test_review_requires_downloaded_hash_equal_and_drive_id(self):
        kw = dict(archive_name=self.name, archive_sha256=self.sha,
                  downloaded_sha256=self.sha, drive_file_id="D" * 20, uid=UID, **self.pins)
        review = prod.finalize_review(self.root, **kw)
        self.assertTrue(review["g6_offhost_sha256_verified"])
        self.assertEqual(review["g6_restore_report_name"],
                         "isolated-restore-v2-20261108T120000Z.report.json")
        self.assertEqual(self.code(prod.finalize_review, self.root, **kw), "G6P_TARGET_EXISTS")

    def test_review_input_failures(self):
        base = dict(archive_name=self.name, archive_sha256=self.sha,
                    downloaded_sha256=self.sha, drive_file_id="D" * 20, uid=UID, **self.pins)
        self.assertEqual(self.code(prod.finalize_review, self.root,
                                   **{**base, "downloaded_sha256": "0" * 64}),
                         "G6P_OFFHOST_HASH_MISMATCH")
        for key, value in (("drive_file_id", "short"), ("drive_file_id", None),
                           ("archive_sha256", "x"), ("expected_key_epoch", "x"),
                           ("expected_repo_id", None), ("archive_name", "v1.tar.gz")):
            with self.subTest(key=key):
                self.assertEqual(self.code(prod.finalize_review, self.root,
                                           **{**base, key: value}),
                                 "G6P_REVIEW_INPUT_INVALID")

    def test_end_to_end_documents_are_mutually_consistent(self):
        self.restore()
        kw = dict(archive_name=self.name, archive_sha256=self.sha,
                  downloaded_sha256=self.sha, drive_file_id="D" * 20, uid=UID, **self.pins)
        prod.finalize_review(self.root, **kw)
        st = Path(self.root, "state-v2")
        docs = [json.loads(st.joinpath(f).read_bytes()) for f in sorted(os.listdir(st))
                if f.startswith(("review-", "receipt-", "isolated-"))]
        self.assertEqual(len(docs), 3)
        for doc in docs:
            self.assertEqual((doc["restic_repo_id"], doc["key_epoch"]),
                             (REPO_ID, self.epoch))


class G6V2ProducerCliSurfaceTests(unittest.TestCase):
    def test_cli_adapter_never_puts_secrets_in_argv_and_scrubs_env(self):
        with tempfile.TemporaryDirectory() as tmp:
            log = os.path.join(tmp, "log.json")
            fake = os.path.join(tmp, "restic")
            Path(fake).write_text(
                "#!/usr/bin/env python3\nimport json,os,sys\n"
                "open(%r,'a').write(json.dumps([sys.argv[1:],sorted(k for k in os.environ if k!='LC_CTYPE')])+'\\n')\n"
                "if sys.argv[1:3]==['cat','config']: print(json.dumps({'id':'%s'}))\n"
                % (log, REPO_ID))
            os.chmod(fake, 0o700)
            cli = prod.ResticCli(fake)
            cli.init("/r/repo-v2", "/r/key-v2")
            self.assertEqual(cli.repo_id("/r/repo-v2", "/r/key-v2"), REPO_ID)
            cli.backup("/r/repo-v2", "/r/key-v2", ["/a", "/b"])
            cli.restore("/r/repo-v2", "/r/key-v2", "/t")
            lines = [json.loads(x) for x in Path(log).read_text().splitlines()]
            self.assertEqual([l[0][0] for l in lines], ["init", "cat", "backup", "restore"])
            for argv, env in lines:
                self.assertFalse(any("key-v2" in a for a in argv))
                self.assertEqual(env, ["HOME", "PATH", "RESTIC_CACHE_DIR",
                                       "RESTIC_PASSWORD_FILE", "RESTIC_REPOSITORY"])
            self.assertEqual(lines[2][0], ["backup", "--quiet", "--one-file-system",
                                           "--", "/a", "/b"])

    def test_cli_failures_and_binary_validation(self):
        for binary in ("restic", "", None, "/a\0b"):
            with self.assertRaises(prod.Stop):
                prod.ResticCli(binary)
        with tempfile.TemporaryDirectory() as tmp:
            bad = os.path.join(tmp, "restic")
            Path(bad).write_text("#!/bin/sh\nexit 3\n"); os.chmod(bad, 0o700)
            with self.assertRaises(prod.Stop) as ctx:
                prod.ResticCli(bad).check("/r", "/k")
            self.assertEqual(str(ctx.exception), "G6P_RESTIC_FAILED")
            Path(bad).write_text("#!/bin/sh\necho '{\"id\":\"zz\"}'\n")
            with self.assertRaises(prod.Stop) as ctx:
                prod.ResticCli(bad).repo_id("/r", "/k")
            self.assertEqual(str(ctx.exception), "G6P_REPO_ID_INVALID")

    def test_module_is_library_only_and_names_no_v1_paths(self):
        text = SOURCE.read_text()
        for forbidden in ("/opt/centenario", "archive.receipt.json", "qa-g6-restic-password",
                          "/etc/pmgm", "os.environ"):
            self.assertNotIn(forbidden, text)
        with self.assertRaises(SystemExit):
            exec(compile(text, "x", "exec"), {"__name__": "__main__"})


if __name__ == "__main__":
    unittest.main()
