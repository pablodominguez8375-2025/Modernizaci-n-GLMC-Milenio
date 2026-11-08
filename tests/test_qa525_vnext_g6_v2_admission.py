"""QA525 G6 v2 admission adapter: real producer output, fake Restic, tmp dirs, no host, no GO."""
import datetime as dt
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import shutil
import tempfile
import unittest
from unittest import mock

ROOT = Path(__file__).resolve().parents[1]


def load(name, rel):
    spec = importlib.util.spec_from_file_location(name, ROOT / rel)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


adm = load("qa525_g6adm_adapter_under_test", "scripts/qa/qa525-vnext-g6-v2-admission.py")
prod = load("qa525_g6adm_adapter_producer", "scripts/qa/qa525-vnext-g6-v2-producer.py")
ptest = load("qa525_g6adm_adapter_ptest", "tests/test_qa525_vnext_g6_v2_producer.py")
SOURCE = (ROOT / "scripts/qa/qa525-vnext-g6-v2-admission.py").read_text()
UID = os.getuid()
NOW = ptest.NOW


def pin(raw):
    return "sha256:" + hashlib.sha256(raw).hexdigest()


class AdmissionBase(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.root = os.path.join(os.path.realpath(self.tmp.name), "g6")
        os.mkdir(self.root, 0o700)
        fake = ptest.G6V2ProducerFakeRestic()
        self.epoch = prod.key_epoch(ptest.KEY)
        pins = dict(expected_repo_id=ptest.REPO_ID, expected_key_epoch=self.epoch)
        prod.init_repo(self.root, runner=fake, key_bytes=ptest.KEY, uid=UID, now=NOW)
        out = prod.backup(self.root, runner=fake, captor=ptest.good_captor, uid=UID, now=NOW,
                          **pins)
        self.name = out["archive_name"]
        self.stamp = self.name.split("-")[-1].split(".")[0]
        os.mkdir(os.path.join(self.root, "downloads-v2"), 0o700)
        dl = os.path.join(self.root, "downloads-v2", self.name)
        shutil.copyfile(os.path.join(self.root, "exports-v2", self.name), dl)
        os.chmod(dl, 0o600)
        prod.restore(self.root, runner=fake, verifier=ptest.verifier_ok, archive_name=self.name,
                     archive_sha256=out["archive_sha256"], snapshot_id=out["snapshot_id"],
                     expected_manifest_sha256=out["manifest_sha256"],
                     forbidden_prefixes=[os.path.join(self.root, "prod")], uid=UID,
                     now=NOW + dt.timedelta(hours=2), **pins)
        prod.finalize_review(self.root, archive_name=self.name, drive_file_id="D" * 20,
                             uid=UID, **pins)
        self.state = os.path.join(self.root, "state-v2")
        self.out = out

    def path(self, kind):
        return os.path.join(self.state, {
            "review": "review-%s.json" % self.stamp, "receipt": "receipt-%s.json" % self.stamp,
            "restore": "isolated-restore-v2-%s.report.json" % self.stamp}[kind])

    def pins(self):
        return {k + "_sha256": pin(Path(self.path(k)).read_bytes())
                for k in ("review", "receipt", "restore")}

    def admit(self, **over):
        args = dict(stamp=self.stamp, expected_restic_repo_id=ptest.REPO_ID,
                    expected_key_epoch=self.epoch, uid=UID,
                    now=NOW + dt.timedelta(hours=3),
                    admission_expires_at=NOW + dt.timedelta(hours=4))
        args.update(over)
        if "pins" not in args:
            args["pins"] = self.pins()
        return adm.admit(self.root, **args)

    def code(self, **over):
        with self.assertRaises(adm.Stop) as err:
            self.admit(**over)
        self.assertIsNone(err.exception.__cause__)
        return str(err.exception)

    def tree(self):
        return sorted((str(p), p.lstat().st_mtime_ns, p.lstat().st_size)
                      for p in Path(self.root).rglob("*"))


class AdmissionTests(AdmissionBase):
    def test_real_producer_cut_is_coherent_and_never_authority(self):
        before = self.tree()
        result = self.admit()
        self.assertEqual(result["admission"], adm.STATUS)
        self.assertIs(result["may_apply"], False)
        self.assertIs(result["may_install"], False)
        self.assertIs(result["sponsor_authorized_here"], False)
        self.assertIs(result["offhost_custody_authenticated_here"], False)
        self.assertEqual(result["archive_sha256"], "sha256:" + self.out["archive_sha256"])
        self.assertEqual(self.tree(), before)             # reading wrote nothing anywhere

    def test_path_traversal_and_malformed_stamps_are_refused_before_any_open(self):
        for bad in ("../20261108T120000Z", "20261108T120000Z/../x", "20261108T120000Z\x00",
                    "2026-11-08", "", None, 7, "20261108T120000Z\n"):
            with mock.patch.object(adm.os, "open", side_effect=AssertionError("opened")):
                self.assertEqual(self.code(stamp=bad), "G6ADM_NAME_INVALID", repr(bad))

    def test_root_must_be_absolute_and_free_of_symlinks(self):
        link = self.root + "-link"
        os.symlink(self.root, link)
        with self.assertRaises(adm.Stop) as err:
            adm.admit(link, stamp=self.stamp, pins=self.pins(),
                      expected_restic_repo_id=ptest.REPO_ID, expected_key_epoch=self.epoch,
                      uid=UID, now=NOW, admission_expires_at=NOW)
        self.assertEqual(str(err.exception), "G6ADM_ROOT_UNTRUSTED")
        with self.assertRaises(adm.Stop):
            adm.admit("relative/g6", stamp=self.stamp, pins=self.pins(),
                      expected_restic_repo_id=ptest.REPO_ID, expected_key_epoch=self.epoch,
                      uid=UID, now=NOW, admission_expires_at=NOW)

    def test_a_symlinked_ancestor_of_the_root_is_refused(self):
        parent = os.path.dirname(self.root)
        alias = parent + "-alias"
        os.symlink(parent, alias)                      # alias/g6 reaches the real root
        with self.assertRaises(adm.Stop) as err:
            adm.admit(os.path.join(alias, "g6"), stamp=self.stamp, pins=self.pins(),
                      expected_restic_repo_id=ptest.REPO_ID, expected_key_epoch=self.epoch,
                      uid=UID, now=NOW + dt.timedelta(hours=3),
                      admission_expires_at=NOW + dt.timedelta(hours=4))
        self.assertEqual(str(err.exception), "G6ADM_ROOT_UNTRUSTED")

    def root_code(self, root=None, **over):
        with self.assertRaises(adm.Stop) as err:
            self.admit_at(root or self.root, **over)
        return str(err.exception)

    def admit_at(self, root, **over):
        args = dict(stamp=self.stamp, pins=self.pins(), expected_restic_repo_id=ptest.REPO_ID,
                    expected_key_epoch=self.epoch, uid=UID, now=NOW + dt.timedelta(hours=3),
                    admission_expires_at=NOW + dt.timedelta(hours=4))
        args.update(over)
        return adm.admit(root, **args)

    def test_the_root_itself_must_be_private_and_owned_by_the_expected_uid(self):
        for mode in (0o755, 0o750, 0o705, 0o770, 0o777, 0o710):
            os.chmod(self.root, mode)
            self.assertEqual(self.root_code(), "G6ADM_ROOT_UNTRUSTED", oct(mode))
        os.chmod(self.root, 0o700)
        self.admit_at(self.root)                         # control: private root is fine
        self.assertEqual(self.root_code(uid=UID + 1), "G6ADM_ROOT_UNTRUSTED")
        self.assertEqual(self.root_code(uid=0 if UID else 1), "G6ADM_ROOT_UNTRUSTED")

    def test_a_group_or_world_writable_ancestor_is_refused(self):
        parent = os.path.dirname(self.root)
        for mode in (0o770, 0o775, 0o702, 0o777):
            os.chmod(parent, mode)
            self.assertEqual(self.root_code(), "G6ADM_ROOT_UNTRUSTED", oct(mode))
        os.chmod(parent, 0o700)
        self.admit_at(self.root)

    def test_a_file_where_the_root_should_be_is_refused(self):
        fake = os.path.join(os.path.dirname(self.root), "plain")
        Path(fake).write_text("x")
        self.assertEqual(self.root_code(fake), "G6ADM_ROOT_UNTRUSTED")

    def test_the_root_is_closed_when_its_check_fails(self):
        before = set(os.listdir("/proc/self/fd"))
        os.chmod(self.root, 0o755)
        for _ in range(3):
            self.root_code()
        self.assertEqual(set(os.listdir("/proc/self/fd")) - before, set())

    def test_the_root_swapped_between_the_check_and_the_open_is_refused(self):
        other = self.root + "-other"
        os.mkdir(other, 0o700)
        real_open = os.open

        def swap(path, flags, *a, **k):
            if path == self.root and not a and not k:
                os.rename(self.root, self.root + "-moved")
                os.rename(other, self.root)             # a different directory takes the name
            return real_open(path, flags, *a, **k)

        with mock.patch.object(adm.os, "open", side_effect=swap):
            code = self.root_code()
        self.assertIn(code, ("G6ADM_DIR_UNTRUSTED", "G6ADM_ROOT_UNTRUSTED"))

    def test_the_root_renamed_right_after_it_was_opened_is_refused(self):
        real_fstat, done = os.fstat, []

        def rename_then_fstat(fd, *a):
            if not done:
                done.append(True)
                os.rename(self.root, self.root + "-moved")   # the opened dir leaves its name
                os.mkdir(self.root, 0o700)                   # and a decoy takes it
            return real_fstat(fd, *a)

        with mock.patch.object(adm.os, "fstat", side_effect=rename_then_fstat):
            self.assertEqual(self.root_code(), "G6ADM_ROOT_UNTRUSTED")
        self.assertTrue(done)

    @unittest.skipUnless(os.geteuid() == 0, "needs root to chown an ancestor")
    def test_an_ancestor_owned_by_a_stranger_is_refused(self):
        parent = os.path.dirname(self.root)
        os.chown(parent, 12345, 12345)
        self.assertEqual(self.root_code(), "G6ADM_ROOT_UNTRUSTED")

    def test_symlinked_evidence_file_dir_or_archive_is_refused(self):
        for kind in ("review", "receipt", "restore"):
            original = self.path(kind)
            moved = original + ".real"
            os.rename(original, moved)
            os.symlink(moved, original)
            self.assertEqual(self.code(pins={k: "sha256:" + "0" * 64 for k in (
                "review_sha256", "receipt_sha256", "restore_sha256")}), "G6ADM_FILE_UNTRUSTED")
            os.unlink(original)
            os.rename(moved, original)
        for sub in ("state-v2", "exports-v2", "downloads-v2"):
            real = os.path.join(self.root, sub)
            os.rename(real, real + ".real")
            os.symlink(real + ".real", real)
            self.assertEqual(self.code(), "G6ADM_DIR_UNTRUSTED", sub)
            os.unlink(real)
            os.rename(real + ".real", real)
        archive = os.path.join(self.root, "downloads-v2", self.name)
        os.rename(archive, archive + ".real")
        os.symlink(archive + ".real", archive)
        self.assertEqual(self.code(), "G6ADM_FILE_UNTRUSTED")

    def test_loose_modes_hardlinks_and_non_regular_files_are_refused(self):
        original = self.path("review")
        os.chmod(original, 0o644)
        self.assertEqual(self.code(), "G6ADM_FILE_UNTRUSTED")
        os.chmod(original, 0o600)
        os.link(original, original + ".hard")
        self.assertEqual(self.code(), "G6ADM_FILE_UNTRUSTED")
        os.unlink(original + ".hard")
        os.chmod(os.path.join(self.root, "state-v2"), 0o755)
        self.assertEqual(self.code(), "G6ADM_DIR_UNTRUSTED")
        os.chmod(os.path.join(self.root, "state-v2"), 0o700)
        fixed = self.pins()                           # read BEFORE the file becomes a FIFO
        os.unlink(original)
        os.mkfifo(original, 0o600)
        self.assertEqual(self.code(pins=fixed), "G6ADM_FILE_UNTRUSTED")  # and it does not block

    def test_swapping_the_path_during_the_read_never_yields_the_swapped_bytes(self):
        review = self.path("review")
        evil = review + ".evil"
        Path(evil).write_bytes(Path(review).read_bytes().replace(b"a", b"b", 1))
        os.chmod(evil, 0o600)
        real_read, done = os.read, []

        def swap_then_read(fd, n):
            if not done:
                done.append(True)
                os.replace(evil, review)               # atomic path swap mid-read
            return real_read(fd, n)

        with mock.patch.object(adm.os, "read", side_effect=swap_then_read):
            # the opened inode lost its link: fail closed (ctime/identity changed), no result
            self.assertEqual(self.code(), "G6ADM_FILE_CHANGED")
        self.assertTrue(done)

    def test_rewriting_a_file_in_place_while_it_is_read_is_detected(self):
        review = self.path("review")
        real_read, done = os.read, []

        def grow_then_read(fd, n):
            if not done:
                done.append(True)
                with open(review, "ab") as stream:
                    stream.write(b" ")
            return real_read(fd, n)

        with mock.patch.object(adm.os, "read", side_effect=grow_then_read):
            self.assertEqual(self.code(), "G6ADM_FILE_CHANGED")

    def test_v1_names_schemas_and_layout_are_not_accepted(self):
        review = json.loads(Path(self.path("review")).read_text())
        review["schema"] = "qa525-g6-review"
        review["g6_archive_name"] = "centenario-qa-g6-CIFRADO-%s.tar.gz" % self.stamp
        Path(self.path("review")).write_bytes(prod.canonical(review))
        self.assertTrue(self.code().startswith("G6_V2_"))
        # a legacy-looking stamp argument or directory is not even looked at
        os.mkdir(os.path.join(self.root, "legacy"), 0o700)
        Path(self.root, "legacy", "archive.receipt.json").write_text("{}")
        self.assertEqual(self.code(stamp="legacy"), "G6ADM_NAME_INVALID")

    def test_each_archive_copy_is_hashed_here_and_must_match(self):
        for sub in ("exports-v2", "downloads-v2"):
            target = os.path.join(self.root, sub, self.name)
            good = Path(target).read_bytes()
            Path(target).write_bytes(good + b"x")
            self.assertEqual(self.code(), "G6_V2_ARCHIVE_OR_OFFHOST_HASH_MISMATCH", sub)
            Path(target).write_bytes(good)
        os.unlink(os.path.join(self.root, "downloads-v2", self.name))
        self.assertEqual(self.code(), "G6ADM_FILE_UNTRUSTED")

    def test_external_pins_generation_and_window_are_enforced(self):
        wrong = dict(self.pins(), review_sha256="sha256:" + "0" * 64)
        self.assertEqual(self.code(pins=wrong), "G6_V2_REVIEW_PIN_MISMATCH")
        self.assertEqual(self.code(expected_restic_repo_id="f" * 64),
                         "G6_V2_GENERATION_MISMATCH")
        self.assertEqual(self.code(expected_key_epoch="f" * 32), "G6_V2_GENERATION_MISMATCH")
        self.assertEqual(self.code(now=NOW + dt.timedelta(hours=49)),
                         "G6_V2_WINDOW_EXPIRED_OR_UNCOVERED")
        self.assertEqual(self.code(uid="0"), "G6ADM_UID_INVALID")

    def test_files_owned_by_someone_else_are_refused(self):
        self.assertEqual(self.code(uid=UID + 1), "G6ADM_ROOT_UNTRUSTED")


class AdmissionStaticTests(unittest.TestCase):
    def test_no_write_no_legacy_path_no_process_no_network_no_clock_in_the_source(self):
        for forbidden in ("/opt/centenario", "/home/soporte", "archive.receipt", "ARCHIVE_RECEIPT",
                          "validate_g6", "O_WRONLY", "O_RDWR", "O_CREAT", "O_TRUNC", "O_APPEND",
                          "open(", "subprocess", "os.system", "socket", "urllib", "shutil",
                          "os.rename", "os.replace", "os.unlink", "os.remove", "os.chmod",
                          "time.time", "datetime", "environ", "getenv", "eval(", "exec("):
            if forbidden == "open(":
                self.assertNotIn("open(", SOURCE.replace("os.open(", ""), forbidden)
            else:
                self.assertNotIn(forbidden, SOURCE, forbidden)

    def test_it_is_a_library_that_never_runs_on_import(self):
        self.assertIn('if __name__ == "__main__":', SOURCE)
        self.assertEqual(SOURCE.count("raise SystemExit("), 1)


if __name__ == "__main__":
    unittest.main()
