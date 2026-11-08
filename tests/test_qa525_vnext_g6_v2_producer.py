"""QA525 G6 v2 producer -- hermetic tests: fake Restic, tmp dirs, no host, no secrets."""
import datetime as dt
import gzip
import hashlib
import importlib.util
import io
import json
import os
from pathlib import Path
import shutil
import stat
import tarfile
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
CIPHER = b"\x01cipher:"


class G6V2ProducerFakeRestic:
    """Simulator faithful to REAL Restic hierarchy: absolute paths are preserved
    unless the caller restores ``snapshot:subfolder``. State lives ONLY in the repo."""

    def __init__(self, repo_id=REPO_ID):
        self.calls, self.repo_id_value, self.fail = [], repo_id, None
        self.tamper_restored = False

    def _maybe_fail(self, op):
        if self.fail == op:
            raise prod.Stop("G6P_RESTIC_FAILED")

    def init(self, repo, keyfile):
        self.calls.append(("init", repo)); self._maybe_fail("init")
        os.mkdir(os.path.join(repo, "data"), 0o700)
        os.mkdir(os.path.join(repo, "snapshots"), 0o700)
        Path(repo, "config").write_bytes(b"\x00cipher-config")

    def repo_id(self, repo, keyfile):
        self.calls.append(("repo_id", repo)); return self.repo_id_value

    def backup(self, repo, keyfile, stage):
        self.calls.append(("backup", stage)); self._maybe_fail("backup")
        files = {}
        for base, _dirs, names in os.walk(stage):
            for n in names:
                full = os.path.join(base, n)
                files[os.path.relpath(full, stage)] = Path(full).read_bytes().hex()
        blob = CIPHER + json.dumps({"paths": [stage], "files": files}).encode()
        snap = hashlib.sha256(blob).hexdigest()[:16]
        Path(repo, "data", "blob-" + snap).write_bytes(blob)
        Path(repo, "snapshots", snap).write_text(stage)
        return snap

    def check(self, repo, keyfile):
        self.calls.append(("check", repo)); self._maybe_fail("check")
        for blob in Path(repo, "data").iterdir():
            if not blob.read_bytes().startswith(CIPHER):
                raise prod.Stop("G6P_RESTIC_FAILED")

    def snapshot_paths(self, repo, keyfile, snapshot_id):
        self.calls.append(("snapshot_paths", repo)); self._maybe_fail("snapshot_paths")
        marker = Path(repo, "snapshots", snapshot_id)
        if not marker.is_file():
            raise prod.Stop("G6P_RESTIC_FAILED")
        return [marker.read_text()]

    def restore(self, repo, keyfile, target, snapshot_id, subfolder):
        self.calls.append(("restore", repo)); self._maybe_fail("restore")
        blob = json.loads(Path(repo, "data", "blob-" + snapshot_id).read_bytes()[len(CIPHER):])
        stage = blob["paths"][0]
        root = target if subfolder == stage else os.path.join(target, stage.lstrip("/"))
        for rel, hexdata in blob["files"].items():
            dest = os.path.join(root, rel)
            os.makedirs(os.path.dirname(dest), mode=0o700, exist_ok=True)
            data = bytes.fromhex(hexdata)
            if self.tamper_restored and rel == "pmgm.dump":
                data = b"tampered"
            Path(dest).write_bytes(data); os.chmod(dest, 0o600)


def good_captor(stage):
    for name, data in (("pmgm.dump", b"pg1"), ("pmgm_keycloak.dump", b"pg2"),
                       ("globals.sql", b"roles")):
        p = Path(stage, name); p.write_bytes(data); os.chmod(p, 0o600)
    os.mkdir(os.path.join(stage, "silo"), 0o700)
    p = Path(stage, "silo", "obj1"); p.write_bytes(b"x"); os.chmod(p, 0o600)


def verifier_ok(data_dir):
    return {"pmgm": 12, "pmgm_keycloak": 7}, 3


class G6V2ProducerBase(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.base = os.path.realpath(self.tmp.name)
        self.root = os.path.join(self.base, "g6")
        os.mkdir(self.root, 0o700)
        self.fake = G6V2ProducerFakeRestic()
        self.epoch = prod.key_epoch(KEY)
        self.pins = dict(expected_repo_id=REPO_ID, expected_key_epoch=self.epoch)

    def tearDown(self):
        self.tmp.cleanup()

    def init(self):
        return prod.init_repo(self.root, runner=self.fake, key_bytes=KEY, uid=UID, now=NOW)

    def backup(self, **over):
        args = dict(runner=self.fake, captor=good_captor, uid=UID, now=NOW, **self.pins)
        args.update(over)
        return prod.backup(self.root, **args)

    def code(self, call, *a, **k):
        with self.assertRaises(prod.Stop) as ctx:
            call(*a, **k)
        return str(ctx.exception)

    def download(self, out, data=None):
        """Simulate the Sponsor's Drive download into downloads-v2 (0600)."""
        d = os.path.join(self.root, "downloads-v2")
        if not os.path.isdir(d):
            os.mkdir(d, 0o700)
        dst = os.path.join(d, out["archive_name"])
        shutil.copyfile(os.path.join(self.root, "exports-v2", out["archive_name"]), dst) \
            if data is None else Path(dst).write_bytes(data)
        os.chmod(dst, 0o600)
        return dst


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
        os.chmod(self.root, 0o700)
        self.assertEqual(self.code(prod.init_repo, "relative", runner=self.fake,
                                   key_bytes=KEY, uid=UID, now=NOW), "G6P_ROOT_INVALID")
        self.assertEqual(self.code(prod.init_repo, self.root, runner=self.fake,
                                   key_bytes=KEY, uid=UID + 1, now=NOW), "G6P_DIR_UNSAFE")

    def test_root_reached_through_a_symlink_is_stop(self):
        link = os.path.join(self.base, "link")
        os.symlink(self.root, link)
        self.assertEqual(self.code(prod.init_repo, link, runner=self.fake, key_bytes=KEY,
                                   uid=UID, now=NOW), "G6P_ROOT_INVALID")


class G6V2ProducerBackupTests(G6V2ProducerBase):
    def setUp(self):
        super().setUp(); self.init(); self.fake.calls.clear()

    def test_backup_never_inits_and_is_repeatable_without_touching_key(self):
        key = os.path.join(self.root, "key-v2")
        before = os.lstat(key)
        first = self.backup()
        second = self.backup(now=NOW + dt.timedelta(hours=1))
        self.assertNotIn("init", [c[0] for c in self.fake.calls])
        self.assertEqual(Path(key).read_bytes(), KEY)
        self.assertEqual(os.lstat(key).st_mtime_ns, before.st_mtime_ns)
        self.assertNotEqual(first["archive_name"], second["archive_name"])

    def test_only_the_verified_stage_is_snapshotted_and_then_removed(self):
        out = self.backup()
        backed = [c[1] for c in self.fake.calls if c[0] == "backup"]
        self.assertEqual(backed, [os.path.join(self.root, "stages-v2", out["stamp"])])
        self.assertEqual(os.listdir(os.path.join(self.root, "stages-v2")), [])
        record = json.loads(Path(self.root, "state-v2",
                                 "capture-%s.json" % out["stamp"]).read_bytes())
        self.assertEqual(record["snapshot_id"], out["snapshot_id"])

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
        self.assertEqual(doc["sha256"], out["archive_sha256"])
        self.assertNotIn(KEY, archive)

    def test_incomplete_or_foreign_capture_is_stop_and_never_snapshotted(self):
        def missing(stage):
            good_captor(stage); os.unlink(os.path.join(stage, "globals.sql"))

        def extra(stage):
            good_captor(stage); Path(stage, "etc-shadow").write_bytes(b"x")

        def empty_dump(stage):
            good_captor(stage); Path(stage, "pmgm.dump").write_bytes(b"")

        def linked(stage):
            good_captor(stage); os.unlink(os.path.join(stage, "globals.sql"))
            os.symlink("/etc/hostname", os.path.join(stage, "globals.sql"))

        def silo_link(stage):
            good_captor(stage); os.symlink("/etc/hostname", os.path.join(stage, "silo", "l"))

        def empty_silo(stage):
            good_captor(stage); os.unlink(os.path.join(stage, "silo", "obj1"))

        def loose_mode(stage):
            good_captor(stage); os.chmod(os.path.join(stage, "pmgm.dump"), 0o644)

        for captor in (missing, extra, empty_dump, linked, silo_link, empty_silo, loose_mode):
            with self.subTest(captor=captor.__name__):
                self.fake.calls.clear()
                self.assertIn(self.code(self.backup, captor=captor),
                              ("G6P_STAGE_INVALID", "G6P_FILE_UNSAFE"))
                self.assertNotIn("backup", [c[0] for c in self.fake.calls])
                self.assertEqual(os.listdir(os.path.join(self.root, "stages-v2")), [])
                self.assertEqual(os.listdir(os.path.join(self.root, "exports-v2")), [])

    def test_captor_must_be_callable(self):
        self.assertEqual(self.code(self.backup, captor=None), "G6P_CAPTOR_INVALID")

    def test_archive_is_deterministic_for_same_repo_state(self):
        paths = {n: os.path.join(self.root, n) for n in ("repo-v2", "key-v2")}
        one, two = (os.path.join(self.base, n) for n in ("a.tgz", "b.tgz"))
        h1 = prod._write_archive(paths["repo-v2"], paths["key-v2"], one)
        h2 = prod._write_archive(paths["repo-v2"], paths["key-v2"], two)
        self.assertEqual(h1, h2)

    def test_archive_is_streamed_to_disk_with_a_running_size_cap(self):
        old = prod.MAX_ARCHIVE_BYTES
        try:
            prod.MAX_ARCHIVE_BYTES = 64
            Path(self.root, "repo-v2", "data", "big").write_bytes(os.urandom(4096))
            out = os.path.join(self.base, "x.tgz")
            self.assertEqual(self.code(prod._write_archive, os.path.join(self.root, "repo-v2"),
                                       os.path.join(self.root, "key-v2"), out),
                             "G6P_ARCHIVE_SIZE")
            self.assertFalse(os.path.lexists(out))
        finally:
            prod.MAX_ARCHIVE_BYTES = old
        src = SOURCE.read_text()
        self.assertNotIn("BytesIO", src)

    def test_same_second_backup_never_overwrites(self):
        self.backup()
        self.assertEqual(self.code(self.backup), "G6P_TARGET_EXISTS")

    def test_wrong_pins_are_stop_before_any_restic_backup(self):
        for key, value in (("expected_repo_id", "cd" * 32),
                           ("expected_key_epoch", "0" * 32)):
            with self.subTest(key):
                self.assertEqual(self.code(self.backup, **{key: value}),
                                 "G6P_GENERATION_MISMATCH")
        self.assertNotIn("backup", [c[0] for c in self.fake.calls])

    def test_runner_reporting_other_repo_is_stop(self):
        self.fake.repo_id_value = "cd" * 32
        self.assertEqual(self.code(self.backup), "G6P_GENERATION_MISMATCH")

    def test_foreign_tampered_or_unsafe_key_is_stop(self):
        key = os.path.join(self.root, "key-v2")
        Path(key).write_bytes(b"z" * 40)
        self.assertEqual(self.code(self.backup), "G6P_GENERATION_MISMATCH")
        os.chmod(key, 0o640)
        self.assertEqual(self.code(self.backup), "G6P_FILE_UNSAFE")
        os.chmod(key, 0o600); os.unlink(key); os.symlink("/etc/hostname", key)
        self.assertEqual(self.code(self.backup), "G6P_FILE_UNSAFE")

    def test_missing_identity_or_repo_is_stop_and_never_creates(self):
        os.unlink(os.path.join(self.root, "state-v2", "identity.json"))
        self.assertEqual(self.code(self.backup), "G6P_FILE_UNSAFE")
        shutil.rmtree(os.path.join(self.root, "repo-v2"))
        self.assertEqual(self.code(self.backup), "G6P_DIR_UNSAFE")
        self.assertFalse(os.path.lexists(os.path.join(self.root, "repo-v2")))

    def test_identity_must_be_canonical_and_equal_the_independent_pin(self):
        ident = os.path.join(self.root, "state-v2", "identity.json")
        doc = json.loads(Path(ident).read_bytes())
        Path(ident).write_bytes(json.dumps(doc, indent=2).encode())
        self.assertEqual(self.code(self.backup), "G6P_IDENTITY_INVALID")
        doc["restic_repo_id"] = "cd" * 32
        Path(ident).write_bytes(prod.canonical(doc))
        self.assertEqual(self.code(self.backup), "G6P_GENERATION_MISMATCH")

    def test_restic_failure_leaves_no_receipt_and_no_stage(self):
        for op in ("backup", "check"):
            with self.subTest(op):
                self.fake.fail = op
                self.assertEqual(self.code(self.backup), "G6P_RESTIC_FAILED")
                self.assertEqual(os.listdir(os.path.join(self.root, "exports-v2")), [])
                self.assertEqual(os.listdir(os.path.join(self.root, "stages-v2")), [])

    def test_special_file_in_repo_is_stop_and_partial_archive_removed(self):
        os.symlink("/etc/hostname", os.path.join(self.root, "repo-v2", "link"))
        self.assertEqual(self.code(self.backup), "G6P_ARCHIVE_SPECIAL_FILE")
        self.assertEqual(os.listdir(os.path.join(self.root, "exports-v2")), [])

    def test_key_inside_repo_is_stop(self):
        self.assertEqual(self.code(prod._write_archive, self.root,
                                   os.path.join(self.root, "key-v2"),
                                   os.path.join(self.base, "x.tgz")),
                         "G6P_KEY_INSIDE_REPO")

    def test_archive_members_are_normalised_and_hold_no_owner_or_time(self):
        out = self.backup()
        with tarfile.open(os.path.join(self.root, "exports-v2", out["archive_name"])) as tar:
            members = tar.getmembers()
        self.assertTrue(members)
        for m in members:
            self.assertEqual((m.mtime, m.uid, m.gid, m.uname, m.gname), (0, 0, 0, "", ""))
            self.assertIn(m.mode, (0o700, 0o600))

    def test_directory_and_file_owner_are_enforced(self):
        self.assertEqual(self.code(prod._safe_dir, self.root, UID + 1), "G6P_DIR_UNSAFE")
        self.assertEqual(self.code(prod._safe_file, os.path.join(self.root, "key-v2"),
                                   UID + 1), "G6P_FILE_UNSAFE")

    def test_file_swapped_after_lstat_is_detected_by_fstat_identity(self):
        key = os.path.join(self.root, "key-v2")
        real_lstat = os.lstat
        calls = {"n": 0}

        def swapped(path, *a, **k):
            result = real_lstat(path, *a, **k)
            if path == key and calls["n"] == 0:
                calls["n"] = 1
                fresh = key + ".new"
                Path(fresh).write_bytes(KEY); os.chmod(fresh, 0o600)
                os.rename(fresh, key)  # new inode while the old one is still allocated
            return result
        os.lstat = swapped
        try:
            self.assertEqual(self.code(prod._safe_file, key, UID), "G6P_FILE_UNSAFE")
        finally:
            os.lstat = real_lstat


class G6V2ProducerRestoreReviewTests(G6V2ProducerBase):
    def setUp(self):
        super().setUp(); self.init()
        self.out = self.backup()
        self.name, self.sha = self.out["archive_name"], self.out["archive_sha256"]
        self.snap = self.out["snapshot_id"]
        self.dl = self.download(self.out)
        self.fake.calls.clear()
        self.forbidden = [os.path.join(self.base, "prod")]
        self.later = NOW + dt.timedelta(hours=2)

    def restore(self, **over):
        args = dict(runner=self.fake, verifier=verifier_ok, archive_name=self.name,
                    archive_sha256=self.sha, snapshot_id=self.snap,
                    expected_manifest_sha256=self.out["manifest_sha256"],
                    forbidden_prefixes=self.forbidden, uid=UID, now=self.later, **self.pins)
        args.update(over)
        return prod.restore(self.root, **args)

    def review(self, **over):
        args = dict(archive_name=self.name, drive_file_id="D" * 20, uid=UID, **self.pins)
        args.update(over)
        return prod.finalize_review(self.root, **args)

    def test_restore_uses_the_extracted_downloaded_repo_never_the_live_one(self):
        report = self.restore()
        live = os.path.join(self.root, "repo-v2")
        used = [c[1] for c in self.fake.calls if c[0] in ("check", "restore")]
        self.assertTrue(used)
        for repo in used:
            self.assertNotEqual(repo, live)
            self.assertTrue(repo.startswith(os.path.join(self.root, "restores-v2")))
        self.assertFalse(report["production_touched"])
        raw = Path(self.root, "state-v2", "isolated-restore-v2-20261108T120000Z.report.json"
                   ).read_bytes()
        self.assertEqual(raw, prod.canonical(json.loads(raw)))
        self.assertEqual(os.listdir(os.path.join(self.root, "restores-v2")), [])  # plaintext gone

    def test_corrupt_download_with_copied_expected_hash_fails_even_with_healthy_live_repo(self):
        # Attacker rewrites the download with a structurally corrupt tar and
        # supplies ITS hash as the "expected" hash: extraction/check must STOP.
        bad = io.BytesIO()
        with gzip.GzipFile(fileobj=bad, mode="wb", mtime=0) as gz:
            with tarfile.open(fileobj=gz, mode="w|") as tar:
                folder = tarfile.TarInfo("data"); folder.type = tarfile.DIRTYPE
                tar.addfile(folder)
                info = tarfile.TarInfo("data/blob-x"); info.size = 4
                tar.addfile(info, io.BytesIO(b"junk"))
        data = bad.getvalue()
        self.download(self.out, data=data)
        fake_sha = hashlib.sha256(data).hexdigest()
        self.assertEqual(self.code(self.restore, archive_sha256=fake_sha),
                         "G6P_RESTIC_FAILED")
        self.assertFalse(os.path.exists(
            os.path.join(self.root, "state-v2", "isolated-restore-v2-20261108T120000Z.report.json")))
        self.assertTrue(all(c[1] != os.path.join(self.root, "repo-v2")
                            for c in self.fake.calls if c[0] == "restore"))

    def test_restore_works_with_live_repo_and_capture_record_completely_gone(self):
        shutil.rmtree(os.path.join(self.root, "repo-v2"))
        os.unlink(os.path.join(self.root, "state-v2", "capture-20261108T120000Z.json"))
        os.unlink(os.path.join(self.root, "state-v2", "identity.json"))
        self.assertEqual(self.restore()["silo_objects_verified"], 3)

    def test_live_repo_corrupted_does_not_affect_restore(self):
        for blob in Path(self.root, "repo-v2", "data").iterdir():
            blob.write_bytes(b"garbage")
        self.assertEqual(self.restore()["silo_objects_verified"], 3)

    def test_restore_needs_the_key_and_extracted_repo_must_equal_the_pinned_id(self):
        self.fake.repo_id_value = "cd" * 32  # extracted repo reports another identity
        self.assertEqual(self.code(self.restore), "G6P_GENERATION_MISMATCH")
        self.fake.repo_id_value = REPO_ID
        self.assertEqual(self.code(self.restore, expected_key_epoch="0" * 32),
                         "G6P_GENERATION_MISMATCH")
        os.unlink(os.path.join(self.root, "key-v2"))
        self.assertEqual(self.code(self.restore), "G6P_FILE_UNSAFE")

    def test_restored_layout_must_be_the_exact_stage_at_the_target_root(self):
        real = self.fake.restore

        def nested(repo, keyfile, target, snapshot_id, subfolder):
            return real(repo, keyfile, target, snapshot_id, "/not/the/stage")  # full hierarchy
        self.fake.restore = nested
        self.assertEqual(self.code(self.restore), "G6P_RESTORE_LAYOUT")

    def test_snapshot_must_record_exactly_one_matching_stage_path(self):
        for bad in ([], ["/a/stages-v2/20261108T120000Z", "/b"], ["/x/other/20261108T120000Z"],
                    ["relative/stages-v2/20261108T120000Z"], ["/a/stages-v2/20990101T000000Z"]):
            with self.subTest(bad=bad):
                self.tearDown(); self.setUp()
                self.fake.snapshot_paths = lambda *a, _b=bad: _b
                self.assertEqual(self.code(self.restore), "G6P_RESTORE_LAYOUT")
                self.assertNotIn("restore", [c[0] for c in self.fake.calls])

    def test_manifest_pin_and_content_are_enforced(self):
        self.assertEqual(self.code(self.restore, expected_manifest_sha256="5" * 64),
                         "G6P_RESTORE_LAYOUT")
        for bad in ("x", None, "A" * 64):
            self.assertEqual(self.code(self.restore, expected_manifest_sha256=bad),
                             "G6P_ARCHIVE_REF_INVALID")
        self.tearDown(); self.setUp()
        self.fake.tamper_restored = True  # restored dump differs from manifest hash
        self.assertEqual(self.code(self.restore), "G6P_RESTORE_LAYOUT")

    def test_invalid_calendar_stamp_is_a_fixed_stop(self):
        bad = "centenario-qa-g6-v2-CIFRADO-20261308T120000Z.tar.gz"
        self.assertEqual(self.code(self.restore, archive_name=bad), "G6P_ARCHIVE_REF_INVALID")
        self.assertEqual(self.code(prod._parse_stamp, "20261308T120000Z"),
                         "G6P_ARCHIVE_REF_INVALID")

    def test_download_must_match_pinned_hash_and_is_never_the_exports_copy(self):
        self.assertEqual(self.code(self.restore, archive_sha256="0" * 64),
                         "G6P_ARCHIVE_HASH_MISMATCH")
        os.unlink(self.dl)
        self.assertEqual(self.code(self.restore), "G6P_FILE_UNSAFE")  # exports copy ignored

    def test_download_swapped_between_hash_and_extract_is_detected(self):
        real = prod._extract_safe

        def swap_then_extract(fd, dest, expected):
            # Same inode rewritten after the first hash: second pass must differ.
            with open(self.dl, "r+b") as writer:
                writer.seek(4); writer.write(b"\x01\x02\x03\x04")  # gzip mtime only
            return real(fd, dest, expected)
        prod._extract_safe = swap_then_extract
        try:
            self.assertIn(self.code(self.restore),
                          ("G6P_EXTRACT_UNSAFE", "G6P_ARCHIVE_HASH_MISMATCH"))
        finally:
            prod._extract_safe = real

    def test_hostile_tar_members_are_refused(self):
        def tar_with(member_builder):
            buf = io.BytesIO()
            with gzip.GzipFile(fileobj=buf, mode="wb", mtime=0) as gz:
                with tarfile.open(fileobj=gz, mode="w|") as tar:
                    member_builder(tar)
            return buf.getvalue()

        def file_entry(name, typ=tarfile.REGTYPE, link=""):
            def build(tar):
                info = tarfile.TarInfo(name); info.type = typ; info.linkname = link
                if typ == tarfile.REGTYPE:
                    info.size = 1
                    tar.addfile(info, io.BytesIO(b"x"))
                else:
                    tar.addfile(info)
            return build
        for label, builder in (
                ("traversal", file_entry("../evil")), ("absolute", file_entry("/etc/evil")),
                ("dotdot-mid", file_entry("a/../../b")), ("symlink", file_entry("l", tarfile.SYMTYPE, "/etc")),
                ("hardlink", file_entry("h", tarfile.LNKTYPE, "x")),
                ("fifo", file_entry("f", tarfile.FIFOTYPE)), ("space", file_entry("a b"))):
            with self.subTest(label):
                self.tearDown(); self.setUp()
                data = tar_with(builder)
                self.download(self.out, data=data)
                self.assertEqual(self.code(self.restore, archive_sha256=hashlib.sha256(data).hexdigest()),
                                 "G6P_EXTRACT_UNSAFE")
                self.assertFalse(os.path.lexists(os.path.join(self.base, "evil")))

    def test_duplicate_member_is_refused(self):
        buf = io.BytesIO()
        with gzip.GzipFile(fileobj=buf, mode="wb", mtime=0) as gz:
            with tarfile.open(fileobj=gz, mode="w|") as tar:
                for _ in range(2):
                    info = tarfile.TarInfo("config"); info.size = 1
                    tar.addfile(info, io.BytesIO(b"x"))
        data = buf.getvalue(); self.download(self.out, data=data)
        self.assertEqual(self.code(self.restore, archive_sha256=hashlib.sha256(data).hexdigest()),
                         "G6P_EXTRACT_UNSAFE")

    def test_extracted_repo_is_checked_before_restore_even_when_snapshot_exists(self):
        # Snapshot marker present, ciphertext blob replaced: only check() catches it.
        out = os.path.join(self.base, "evil")
        with tarfile.open(self.dl) as tar:
            tar.extractall(out, filter="data")
        for blob in Path(out, "data").iterdir():
            blob.write_bytes(b"not-ciphertext")
        buf = io.BytesIO()
        with gzip.GzipFile(fileobj=buf, mode="wb", mtime=0) as gz:
            with tarfile.open(fileobj=gz, mode="w|") as tar:
                for base, dirs, files in os.walk(out):
                    for n in sorted(dirs) + sorted(files):
                        tar.add(os.path.join(base, n), os.path.relpath(os.path.join(base, n), out),
                                recursive=False)
        data = buf.getvalue(); self.download(self.out, data=data)
        self.assertEqual(self.code(self.restore, archive_sha256=hashlib.sha256(data).hexdigest()),
                         "G6P_RESTIC_FAILED")
        self.assertNotIn("restore", [c[0] for c in self.fake.calls])

    def test_pinned_hash_mismatch_creates_no_work_directory(self):
        self.assertEqual(self.code(self.restore, archive_sha256="1" * 64),
                         "G6P_ARCHIVE_HASH_MISMATCH")
        self.assertEqual(os.listdir(os.path.join(self.root, "restores-v2")), [])

    def test_member_count_is_capped(self):
        old = prod.MAX_MEMBERS
        try:
            prod.MAX_MEMBERS = 1
            self.assertEqual(self.code(self.restore), "G6P_EXTRACT_UNSAFE")
        finally:
            prod.MAX_MEMBERS = old

    def test_review_rejects_report_hash_or_generation_tamper(self):
        self.restore()
        rep = os.path.join(self.root, "state-v2", "isolated-restore-v2-20261108T120000Z.report.json")
        rcp = os.path.join(self.root, "state-v2", "receipt-20261108T120000Z.json")
        good_rep, good_rcp = Path(rep).read_bytes(), Path(rcp).read_bytes()
        doc = json.loads(good_rep); doc["archive_sha256"] = "2" * 64
        Path(rep).write_bytes(prod.canonical(doc))
        self.assertEqual(self.code(self.review), "G6P_OFFHOST_HASH_MISMATCH")
        Path(rep).write_bytes(good_rep)
        doc = json.loads(good_rcp); doc["key_epoch"] = "3" * 32
        Path(rcp).write_bytes(prod.canonical(doc))
        self.assertEqual(self.code(self.review), "G6P_GENERATION_MISMATCH")

    def test_snapshot_is_pinned_exactly(self):
        self.assertEqual(self.code(self.restore, snapshot_id="deadbeefdeadbeef"),
                         "G6P_RESTIC_FAILED")
        for bad in ("x", None, "ABCDEF12"):
            self.assertEqual(self.code(self.restore, snapshot_id=bad), "G6P_ARCHIVE_REF_INVALID")
        later_backup = self.backup(now=NOW + dt.timedelta(minutes=5))
        self.assertNotEqual(later_backup["snapshot_id"], self.snap)
        self.restore()
        restored = [c for c in self.fake.calls if c[0] == "restore"]
        self.assertEqual(len(restored), 1)

    def test_restore_window_is_enforced(self):
        for when in (NOW - dt.timedelta(seconds=1), NOW + dt.timedelta(hours=48),
                     NOW + dt.timedelta(days=3)):
            with self.subTest(when=when):
                self.assertEqual(self.code(self.restore, now=when), "G6P_RESTORE_WINDOW")
        self.restore(now=NOW + dt.timedelta(hours=47, minutes=59))

    def test_second_restore_same_cut_is_stop_before_any_restic_work(self):
        self.restore()
        self.fake.calls.clear()
        self.assertEqual(self.code(self.restore), "G6P_TARGET_EXISTS")
        self.assertEqual(self.fake.calls, [])

    def test_failed_restore_leaves_no_workdir_and_can_be_retried(self):
        self.fake.fail = "check"
        self.assertEqual(self.code(self.restore), "G6P_RESTIC_FAILED")
        self.assertEqual(os.listdir(os.path.join(self.root, "restores-v2")), [])
        self.fake.fail = None
        self.assertEqual(self.restore()["silo_objects_verified"], 3)

    def test_nonempty_restore_target_is_refused(self):
        real = self.fake.snapshot_paths

        def dirty(repo, keyfile, snapshot_id):
            Path(os.path.dirname(repo), "data", "leftover").write_bytes(b"x")
            return real(repo, keyfile, snapshot_id)
        self.fake.snapshot_paths = dirty
        self.assertEqual(self.code(self.restore), "G6P_TARGET_EXISTS")
        self.assertNotIn("restore", [c[0] for c in self.fake.calls])

    def test_archive_name_mismatch_is_stop_before_restic(self):
        for bad in ("centenario-qa-g6-CIFRADO-20261108T120000Z.tar.gz", "../x", None):
            with self.subTest(bad=bad):
                self.assertEqual(self.code(self.restore, archive_name=bad),
                                 "G6P_ARCHIVE_REF_INVALID")
        self.assertNotIn("restore", [c[0] for c in self.fake.calls])

    def test_forbidden_target_is_stop(self):
        self.assertEqual(self.code(self.restore, forbidden_prefixes=[self.root]),
                         "G6P_RESTORE_TARGET_FORBIDDEN")
        self.assertEqual(self.code(self.restore, forbidden_prefixes=[]),
                         "G6P_RESTORE_TARGET_FORBIDDEN")
        self.assertNotIn("restore", [c[0] for c in self.fake.calls])

    def test_verifier_results_are_strict(self):
        bad = (({"pmgm": 1}, 0), ({"pmgm": 0, "pmgm_keycloak": 1}, 0),
               ({"pmgm": True, "pmgm_keycloak": 1}, 0),
               ({"pmgm": 1, "pmgm_keycloak": 1, "extra": 1}, 0),
               ({"pmgm": 1, "pmgm_keycloak": 1}, -1), ({"pmgm": 1, "pmgm_keycloak": 1}, True))
        for tables, silo in bad:
            with self.subTest(tables=tables, silo=silo):
                self.tearDown(); self.setUp()
                self.assertEqual(self.code(self.restore, verifier=lambda t: (tables, silo)),
                                 "G6P_RESTORE_VERIFY_FAILED")
                self.assertFalse(os.path.exists(os.path.join(
                    self.root, "state-v2", "isolated-restore-v2-20261108T120000Z.report.json")))

    def test_review_hashes_the_download_itself_and_needs_a_restore_report(self):
        self.assertEqual(self.code(self.review), "G6P_FILE_UNSAFE")  # no restore report yet
        self.restore()
        review = self.review()
        self.assertTrue(review["g6_offhost_sha256_verified"])
        self.assertEqual(review["g6_archive_sha256"], self.sha)
        self.assertEqual(review["g6_restore_report_name"],
                         "isolated-restore-v2-20261108T120000Z.report.json")
        self.assertEqual(self.code(self.review), "G6P_TARGET_EXISTS")

    def test_review_with_missing_or_different_download_is_stop(self):
        self.restore()
        os.unlink(self.dl)
        self.assertEqual(self.code(self.review), "G6P_FILE_UNSAFE")
        self.download(self.out, data=b"different bytes")
        self.assertEqual(self.code(self.review), "G6P_OFFHOST_HASH_MISMATCH")

    def test_review_input_and_report_tamper_failures(self):
        self.restore()
        for key, value in (("drive_file_id", "short"), ("drive_file_id", None),
                           ("expected_key_epoch", "x"), ("expected_repo_id", None),
                           ("archive_name", "v1.tar.gz")):
            with self.subTest(key=key):
                self.assertIn(self.code(self.review, **{key: value}),
                              ("G6P_REVIEW_INPUT_INVALID", "G6P_PIN_INVALID"))
        rep = os.path.join(self.root, "state-v2", "isolated-restore-v2-20261108T120000Z.report.json")
        doc = json.loads(Path(rep).read_bytes()); doc["production_touched"] = True
        Path(rep).write_bytes(prod.canonical(doc))
        self.assertEqual(self.code(self.review), "G6P_RESTORE_REPORT_INVALID")

    def test_end_to_end_documents_are_mutually_consistent(self):
        self.restore(); self.review()
        st = Path(self.root, "state-v2")
        docs = [json.loads(st.joinpath(f).read_bytes()) for f in sorted(os.listdir(st))
                if f.startswith(("review-", "receipt-", "isolated-"))]
        self.assertEqual(len(docs), 3)
        for doc in docs:
            self.assertEqual((doc["restic_repo_id"], doc["key_epoch"]), (REPO_ID, self.epoch))


RESTIC = shutil.which("restic")


class G6V2ProducerRealResticTests(unittest.TestCase):
    """Hermetic integration against the REAL restic binary on a temp synthetic repo.

    CI installs restic and sets QA525_REQUIRE_RESTIC=1, so absence is a FAILURE there;
    elsewhere the class skips. No host paths, no secrets, only synthetic bytes.
    """

    def setUp(self):
        if RESTIC is None:
            if os.environ.get("QA525_REQUIRE_RESTIC") == "1":
                self.fail("restic is required in CI but not installed")
            self.skipTest("restic not installed")
        self.tmp = tempfile.TemporaryDirectory()
        self.base = os.path.realpath(self.tmp.name)
        self.root = os.path.join(self.base, "g6")
        os.mkdir(self.root, 0o700)
        digest = hashlib.sha256(Path(RESTIC).read_bytes()).hexdigest()
        self.cli = prod.ResticCli(RESTIC, binary_sha256=digest, timeout=120)
        self.key = os.urandom(32).hex().encode()
        self.addCleanup(self.tmp.cleanup)

    def flow(self):
        ident = prod.init_repo(self.root, runner=self.cli, key_bytes=self.key, uid=UID, now=NOW)
        pins = dict(expected_repo_id=ident["restic_repo_id"],
                    expected_key_epoch=ident["key_epoch"])
        out = prod.backup(self.root, runner=self.cli, captor=good_captor, uid=UID,
                          now=NOW, **pins)
        d = os.path.join(self.root, "downloads-v2"); os.mkdir(d, 0o700)
        dst = os.path.join(d, out["archive_name"])
        shutil.copyfile(os.path.join(self.root, "exports-v2", out["archive_name"]), dst)
        os.chmod(dst, 0o600)
        return pins, out

    def test_real_restic_round_trip_lands_exact_artifacts_at_the_target_root(self):
        pins, out = self.flow()
        seen = {}

        def verifier(data_dir):
            seen["names"] = sorted(os.listdir(data_dir))
            seen["silo"] = sorted(os.listdir(os.path.join(data_dir, "silo")))
            seen["dump"] = Path(data_dir, "pmgm.dump").read_bytes()
            return {"pmgm": 1, "pmgm_keycloak": 1}, len(seen["silo"])
        # live repo and records are completely gone: recovery depends on the download only
        shutil.rmtree(os.path.join(self.root, "repo-v2"))
        shutil.rmtree(os.path.join(self.root, "state-v2", "cache"), ignore_errors=True)
        report = prod.restore(
            self.root, runner=self.cli, verifier=verifier, archive_name=out["archive_name"],
            archive_sha256=out["archive_sha256"], snapshot_id=out["snapshot_id"],
            expected_manifest_sha256=out["manifest_sha256"],
            forbidden_prefixes=[os.path.join(self.base, "prod")], uid=UID,
            now=NOW + dt.timedelta(hours=1), **pins)
        self.assertEqual(seen["names"], ["globals.sql", "manifest.json", "pmgm.dump",
                                         "pmgm_keycloak.dump", "silo"])
        self.assertEqual((seen["silo"], seen["dump"]), (["obj1"], b"pg1"))
        self.assertEqual(report["silo_objects_verified"], 1)

    def test_real_restic_default_restore_would_nest_the_absolute_stage_path(self):
        pins, out = self.flow()
        repo = os.path.join(self.root, "repo-v2")
        keyfile = os.path.join(self.root, "key-v2")
        target = os.path.join(self.base, "nested"); os.mkdir(target, 0o700)
        # what a naive restore returns: the absolute hierarchy, NOT the stage root
        self.cli._run(["restore", out["snapshot_id"], "--target", target], repo, keyfile)
        self.assertFalse(os.path.exists(os.path.join(target, "pmgm.dump")))
        self.assertTrue(any(True for _ in Path(target).rglob("pmgm.dump")))
        self.assertEqual(len(self.cli.snapshot_paths(repo, keyfile, out["snapshot_id"])), 1)

    def test_real_restic_wrong_key_wrong_snapshot_and_corrupt_repo_stop(self):
        pins, out = self.flow()
        repo = os.path.join(self.root, "repo-v2")
        keyfile = os.path.join(self.root, "key-v2")
        wrong = os.path.join(self.base, "wrong"); Path(wrong).write_bytes(b"w" * 64)
        os.chmod(wrong, 0o600)
        with self.assertRaises(prod.Stop):
            self.cli.check(repo, wrong)
        with self.assertRaises(prod.Stop):
            self.cli.snapshot_paths(repo, keyfile, "deadbeef")
        for blob in Path(repo, "data").rglob("*"):
            if blob.is_file():
                os.chmod(blob, 0o600)  # restic packs are read-only; CI is not root
                blob.write_bytes(b"corrupt"); break
        with self.assertRaises(prod.Stop):
            self.cli.check(repo, keyfile)


class G6V2ProducerCliSurfaceTests(unittest.TestCase):
    def make(self, tmp, body):
        fake = os.path.join(tmp, "restic")
        Path(fake).write_text(body); os.chmod(fake, 0o700)
        return fake, hashlib.sha256(Path(fake).read_bytes()).hexdigest()

    def test_cli_adapter_pins_binary_and_never_puts_secrets_in_argv(self):
        with tempfile.TemporaryDirectory() as tmp:
            log = os.path.join(tmp, "log.json")
            fake, sha = self.make(tmp, (
                "#!/usr/bin/env python3\nimport json,os,sys\n"
                "open(%r,'a').write(json.dumps([sys.argv[1:],sorted(k for k in os.environ if k!='LC_CTYPE')])+'\\n')\n"
                "a=sys.argv[2:]\n"
                "if a[:2]==['cat','config']: print(json.dumps({'id':'%s'}))\n"
                "if a[:1]==['backup']: print(json.dumps({'message_type':'summary','snapshot_id':'abcdef1234'}))\n"
                "if a[:1]==['snapshots']: print(json.dumps([{'id':'abcdef1234'+'0'*54,'paths':['/r/stages-v2/s']}]))\n"
                % (log, REPO_ID)))
            cli = prod.ResticCli(fake, binary_sha256=sha)
            cli.init("/r/repo-v2", "/r/key-v2")
            self.assertEqual(cli.repo_id("/r/repo-v2", "/r/key-v2"), REPO_ID)
            self.assertEqual(cli.backup("/r/repo-v2", "/r/key-v2", "/r/stages-v2/s"), "abcdef1234")
            self.assertEqual(cli.snapshot_paths("/r/repo-v2", "/r/key-v2", "abcdef1234"),
                             ["/r/stages-v2/s"])
            cli.restore("/r/repo-v2", "/r/key-v2", "/t", "abcdef1234", "/r/stages-v2/s")
            lines = [json.loads(x) for x in Path(log).read_text().splitlines()]
            self.assertEqual([l[0][1] for l in lines],
                             ["init", "cat", "backup", "snapshots", "restore"])
            self.assertTrue(all(l[0][0] == "--no-cache" for l in lines))
            for argv, env in lines:
                self.assertFalse(any("key-v2" in a for a in argv))
                self.assertEqual(env, ["HOME", "PATH", "RESTIC_PASSWORD_FILE",
                                       "RESTIC_REPOSITORY"])
            self.assertEqual(lines[2][0], ["--no-cache", "backup", "--json",
                                           "--one-file-system", "--", "/r/stages-v2/s"])
            self.assertEqual(lines[4][0], ["--no-cache", "restore",
                                           "abcdef1234:/r/stages-v2/s", "--target", "/t"])

    def test_swapped_binary_and_bad_snapshot_are_stop(self):
        with tempfile.TemporaryDirectory() as tmp:
            fake, sha = self.make(tmp, "#!/bin/sh\nexit 0\n")
            cli = prod.ResticCli(fake, binary_sha256=sha)
            Path(fake).write_text("#!/bin/sh\necho evil\n")
            with self.assertRaises(prod.Stop) as ctx:
                cli.check("/r", "/k")
            self.assertEqual(str(ctx.exception), "G6P_RESTIC_BINARY_INVALID")
            good = prod.ResticCli(*self.make(tmp, "#!/bin/sh\nexit 0\n")[:1],
                                  binary_sha256=self.make(tmp, "#!/bin/sh\nexit 0\n")[1])
            for snap, sub in (("x", "/s"), (None, "/s"), ("abcdef12", "rel"),
                              ("abcdef12", "/a:b"), ("abcdef12", None), ("../../etc", "/s")):
                with self.subTest(snap=snap, sub=sub), self.assertRaises(prod.Stop) as ctx:
                    good.restore("/r", "/k", "/t", snap, sub)
                self.assertEqual(str(ctx.exception), "G6P_SNAPSHOT_INVALID")
            with self.assertRaises(prod.Stop):
                cli.snapshot_paths("/r", "/k", "x")

    def test_cli_failures_and_binary_validation(self):
        for binary, pin in (("restic", "a" * 64), ("", "a" * 64), (None, "a" * 64),
                            ("/a\0b", "a" * 64), ("/bin/true", "short"), ("/bin/true", None)):
            with self.assertRaises(prod.Stop):
                prod.ResticCli(binary, binary_sha256=pin)
        with tempfile.TemporaryDirectory() as tmp:
            fake, sha = self.make(tmp, "#!/bin/sh\nexit 3\n")
            with self.assertRaises(prod.Stop) as ctx:
                prod.ResticCli(fake, binary_sha256=sha).check("/r", "/k")
            self.assertEqual(str(ctx.exception), "G6P_RESTIC_FAILED")
            fake, sha = self.make(tmp, "#!/bin/sh\necho '{\"id\":\"zz\"}'\n")
            with self.assertRaises(prod.Stop) as ctx:
                prod.ResticCli(fake, binary_sha256=sha).repo_id("/r", "/k")
            self.assertEqual(str(ctx.exception), "G6P_REPO_ID_INVALID")
            fake, sha = self.make(tmp, "#!/bin/sh\necho '{\"message_type\":\"summary\"}'\n")
            with self.assertRaises(prod.Stop) as ctx:
                prod.ResticCli(fake, binary_sha256=sha).backup("/r", "/k", "/s")
            self.assertEqual(str(ctx.exception), "G6P_SNAPSHOT_INVALID")

    def test_module_is_library_only_and_names_no_v1_paths(self):
        text = SOURCE.read_text()
        for forbidden in ("/opt/centenario", "archive.receipt.json", "qa-g6-restic-password",
                          "/etc/pmgm", "os.environ", "shell=True"):
            self.assertNotIn(forbidden, text)
        with self.assertRaises(SystemExit):
            exec(compile(text, "x", "exec"), {"__name__": "__main__"})


if __name__ == "__main__":
    unittest.main()
