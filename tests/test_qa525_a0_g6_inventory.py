import hashlib
import importlib.util
import io
import json
import os
import pathlib
import tempfile
import unittest

SRC = pathlib.Path(__file__).resolve().parent.parent / "scripts/qa/operators/qa525-a0-g6-inventory.py"
spec = importlib.util.spec_from_file_location("inv", SRC)
inv = importlib.util.module_from_spec(spec)
spec.loader.exec_module(inv)
UID = os.getuid()
SECRET = "SUPER-SECRET-RESTIC-KEY-0123456789abcdef"


def snapshot(root):
    snap = {}
    for dp, dn, fn in os.walk(root):
        for n in dn + fn:
            p = os.path.join(dp, n)
            st = os.lstat(p)
            snap[p] = (st.st_mode, st.st_size, st.st_mtime_ns)
    return snap


class Inventory(unittest.TestCase):
    def setUp(self):
        self.t = tempfile.TemporaryDirectory()
        self.addCleanup(self.t.cleanup)
        b = self.t.name
        self.rs = b + "/root-source"
        sha = "a" * 40
        self.ld = self.rs + "/" + sha
        os.makedirs(self.ld, mode=0o755)
        self.state = b + "/g6"
        os.makedirs(self.state, mode=0o700)
        for d in inv.SUBDIRS:
            os.mkdir(self.state + "/" + d, 0o700)
        with open(self.state + "/key-v2", "w") as fh:
            fh.write(SECRET)
        os.chmod(self.state + "/key-v2", 0o600)
        self.inbox = b + "/inbox"
        os.mkdir(self.inbox, 0o700)
        open(self.inbox + "/go-1.json", "w").close()
        cfg = {k: b + "/p/" + k for k in inv.PATH_KEYS}
        cfg["g6_pins"] = None
        self.cfgpath = self.ld + "/launch-config.json"
        cfg["inbox"] = self.inbox
        self.write_cfg(cfg)
        self.pins = {}
        for f in inv.G6_FILES:
            with open(self.ld + "/" + f, "w") as fh:
                fh.write("x = 1\n" + f)
            self.pins[f] = hashlib.sha256(("x = 1\n" + f).encode()).hexdigest()
        for k in ("temp_root", "journal_root", "lease_dir"):
            os.makedirs(b + "/p/" + k, mode=0o700)
        self.rid, self.ep = "b" * 64, "c" * 32
        with open(self.state + "/state-v2/identity.json", "w") as fh:
            json.dump({"schema": "x", "restic_repo_id": self.rid, "key_epoch": self.ep}, fh)
        open(self.state + "/exports-v2/centenario-qa-g6-v2-CIFRADO-20261126T100000Z.tar.gz", "w").write("data")
        open(self.state + "/exports-v2/unrelated-name.txt", "w").write("data")
        self.lockdir = b + "/locks"
        os.mkdir(self.lockdir)
        self.locks = (self.lockdir + "/a.lock", self.lockdir + "/b.lock")
        open(self.locks[0], "w").close()
        self.markers = (b + "/uat-cred",)

    def write_cfg(self, cfg, mode=0o600):
        with open(self.cfgpath, "w") as fh:
            json.dump(cfg, fh)
        os.chmod(self.cfgpath, mode)

    def run_inv(self):
        out = io.StringIO()
        code = inv.inventory(UID, out, self.cfgpath, self.rs, self.state, self.locks, self.markers, self.pins)
        return code, out.getvalue()

    def test_complete_read_only_and_closed(self):
        before = snapshot(self.t.name)
        code, text = self.run_inv()
        self.assertEqual(code, 0, text)
        self.assertEqual(snapshot(self.t.name), before)  # no writes at all
        self.assertIn("A0G6_INVENTORY_COMPLETE=1", text)
        self.assertIn("A0G6_G6_PINS=ABSENT", text)
        self.assertIn("A0G6_EVIDENCE_identity.json=sha256=", text)
        self.assertIn("restic_repo_id=" + self.rid, text)
        self.assertIn("key_epoch=" + self.ep, text)
        self.assertIn("ARCHIVE_centenario-qa-g6-v2-CIFRADO-20261126T100000Z.tar.gz", text)
        self.assertIn("A0G6_LOCK_%s=file" % self.locks[0], text)
        self.assertIn("A0G6_LOCK_%s=ABSENT" % self.locks[1], text)
        self.assertIn("A0G6_MARKER_%s=ABSENT" % self.markers[0], text)
        self.assertIn("A0G6_PRE4_G6_FILE_qa525-vnext-g6-v2-run.py=file", text)
        self.assertIn("PIN=MATCH", text)

    def test_secrets_and_unlisted_names_never_printed(self):
        _, text = self.run_inv()
        self.assertNotIn(SECRET, text)
        self.assertNotIn("unrelated-name", text)
        self.assertNotIn("key-v2/key", text)
        self.assertIn("A0G6_KEYFILE_key-v2=file uid=%d mode=0600 nlink=1 SAFE" % UID, text)

    def test_stop_cases(self):
        os.chmod(self.cfgpath, 0o666)
        self.assertEqual(self.run_inv(), (1, "A0G6_STOP:FILE_META\n"))
        self.write_cfg({"inbox": "relative"})
        self.assertEqual(self.run_inv()[1], "A0G6_STOP:CONFIG_PATH_INBOX\n")
        with open(self.cfgpath, "w") as fh:
            fh.write("{not json")
        os.chmod(self.cfgpath, 0o600)
        self.assertEqual(self.run_inv()[1], "A0G6_STOP:CONFIG_JSON\n")
        os.unlink(self.cfgpath)
        self.assertEqual(self.run_inv()[1], "A0G6_STOP:OPEN_FAILED\n")

    def test_symlinked_config_refused(self):
        real = self.cfgpath + ".real"
        os.rename(self.cfgpath, real)
        os.symlink(real, self.cfgpath)
        self.assertEqual(self.run_inv()[1], "A0G6_STOP:OPEN_FAILED\n")

    def test_wrong_owner_expected_refused(self):
        out = io.StringIO()
        code = inv.inventory(UID + 12345, out, self.cfgpath, self.rs, self.state, self.locks, self.markers, self.pins)
        self.assertEqual(code, 1)
        self.assertTrue(out.getvalue().startswith("A0G6_STOP:"))

    def test_g6_pins_values_filtered(self):
        cfg = json.load(open(self.cfgpath))
        cfg["g6_pins"] = {"g6_command": "SECRET words here", "g6_operation_sha256": "d" * 64}
        self.write_cfg(cfg)
        _, text = self.run_inv()
        self.assertIn("A0G6_G6PIN_g6_command=NOT_SHOWN", text)
        self.assertIn("A0G6_G6PIN_g6_operation_sha256=" + "d" * 64, text)
        self.assertNotIn("SECRET words", text)

    def test_absent_state_root_is_stop_not_complete(self):
        out = io.StringIO()
        code = inv.inventory(UID, out, self.cfgpath, self.rs, self.t.name + "/nope", self.locks, self.markers, self.pins)
        text = out.getvalue()
        self.assertEqual(code, 1)
        self.assertIn("A0G6_STATE_ROOT=ABSENT", text)
        self.assertTrue(text.endswith("A0G6_UNSAFE_DIRS=root\nA0G6_STOP:STATE_DIR_UNSAFE\n"), text)
        self.assertNotIn("A0G6_INVENTORY_COMPLETE", text)

    def test_absent_mandatory_subdir_is_stop_not_complete(self):
        os.rmdir(self.state + "/stages-v2")
        code, text = self.run_inv()
        self.assertEqual(code, 1)
        self.assertIn("A0G6_DIR_stages-v2=ABSENT", text)
        self.assertTrue(text.endswith("A0G6_UNSAFE_DIRS=stages-v2\nA0G6_STOP:STATE_DIR_UNSAFE\n"), text)
        self.assertNotIn("A0G6_INVENTORY_COMPLETE", text)

    def test_pin_mismatch_absent_g6_file_stop(self):
        self.pins[inv.G6_FILES[0]] = "0" * 64
        self.assertEqual(self.run_inv()[1], "A0G6_STOP:G6_PIN_MISMATCH\n")
        self.pins[inv.G6_FILES[0]] = hashlib.sha256(("x = 1\n" + inv.G6_FILES[0]).encode()).hexdigest()
        os.unlink(self.ld + "/" + inv.G6_FILES[1])
        self.assertEqual(self.run_inv()[1], "A0G6_STOP:G6_FILE_ABSENT\n")

    def test_nlink_stop(self):
        os.link(self.cfgpath, self.cfgpath + ".hl")
        self.assertEqual(self.run_inv()[1], "A0G6_STOP:FILE_META\n")

    def test_short_read_and_growth(self):
        real = os.read
        calls = {"n": 0}

        def short(fd, n):
            calls["n"] += 1
            return b"" if calls["n"] == 1 else real(fd, n)
        os.read = short
        try:
            self.assertEqual(self.run_inv()[1], "A0G6_STOP:TRUNCATED\n")
        finally:
            os.read = real

        def grow(fd, n):
            data = real(fd, n)
            return data if n != 1 else b"x"
        os.read = grow
        try:
            self.assertEqual(self.run_inv()[1], "A0G6_STOP:GREW\n")
        finally:
            os.read = real

    def test_launcher_dir_absent_or_symlink_and_unsafe_state_dir(self):
        cfg = json.load(open(self.cfgpath))
        os.rmdir(cfg["journal_root"])
        self.assertEqual(self.run_inv()[1], "A0G6_STOP:LAUNCHER_DIR_JOURNAL_ROOT\n")
        os.symlink(self.t.name, cfg["journal_root"])
        self.assertEqual(self.run_inv()[1], "A0G6_STOP:LAUNCHER_DIR_JOURNAL_ROOT\n")
        os.unlink(cfg["journal_root"])
        os.mkdir(cfg["journal_root"], 0o700)
        os.chmod(self.state + "/exports-v2", 0o777)
        code, text = self.run_inv()
        self.assertEqual(code, 1)
        self.assertTrue(text.endswith("A0G6_UNSAFE_DIRS=exports-v2\nA0G6_STOP:STATE_DIR_UNSAFE\n"), text)
        self.assertIn("A0G6_DIR_exports-v2=dir uid=%d mode=0777 nlink=" % UID, text)
        self.assertNotIn("A0G6_INVENTORY_COMPLETE", text)
        self.assertNotIn(SECRET, text)

    def test_unsafe_root_reports_only_root_metadata(self):
        os.chmod(self.state, 0o777)
        code, text = self.run_inv()
        self.assertEqual(code, 1)
        self.assertIn("A0G6_UNSAFE_DIRS=root\nA0G6_STOP:STATE_DIR_UNSAFE\n", text)
        self.assertNotIn("A0G6_DIR_", text)
        self.assertNotIn("A0G6_INVENTORY_COMPLETE", text)

    def test_state_root_symlink_is_not_followed(self):
        real = self.t.name + "/real-g6"
        os.rename(self.state, real)
        os.symlink(real, self.state)
        code, text = self.run_inv()
        self.assertEqual(code, 1)
        self.assertIn("A0G6_STATE_ROOT=link", text)
        self.assertIn("A0G6_UNSAFE_DIRS=root\n", text)
        self.assertNotIn("A0G6_DIR_", text)
        self.assertNotIn(self.rid, text)

    def test_state_root_not_a_directory(self):
        shutil_rm = self.state
        import shutil
        shutil.rmtree(shutil_rm)
        open(self.state, "w").close()
        code, text = self.run_inv()
        self.assertEqual(code, 1)
        self.assertIn("A0G6_STATE_ROOT=file", text)
        self.assertIn("A0G6_UNSAFE_DIRS=root\n", text)
        self.assertNotIn("A0G6_DIR_", text)

    def test_symlinked_subdir_reported_without_descending(self):
        elsewhere = self.t.name + "/elsewhere"
        os.mkdir(elsewhere)
        os.rmdir(self.state + "/restores-v2")
        os.symlink(elsewhere, self.state + "/restores-v2")
        code, text = self.run_inv()
        self.assertEqual(code, 1)
        self.assertIn("A0G6_UNSAFE_DIRS=restores-v2\n", text)
        self.assertIn("A0G6_DIR_restores-v2=link", text)
        self.assertNotIn("restores-v2_COUNT", text)

    def test_swap_between_stat_and_open_is_caught(self):
        elsewhere = self.t.name + "/elsewhere2"
        os.mkdir(elsewhere, 0o700)
        real_open = inv._open_dir
        state = self.state

        def swapping(name, dir_fd=None):
            if name == "exports-v2" and dir_fd is not None:
                os.rename(state + "/exports-v2", state + "/exports-v2.orig")
                os.symlink(elsewhere, state + "/exports-v2")
            return real_open(name, dir_fd)
        inv._open_dir = swapping
        try:
            code, text = self.run_inv()
        finally:
            inv._open_dir = real_open
        self.assertEqual(code, 1)
        self.assertIn("A0G6_UNSAFE_DIRS=exports-v2\n", text)
        self.assertNotIn("exports-v2_COUNT", text)
        self.assertNotIn("ARCHIVE_", text)

    def test_determinism(self):
        self.assertEqual(self.run_inv(), self.run_inv())

    def test_keyfile_variants_are_stop_and_never_read(self):
        k = self.state + "/key-v2"
        os.chmod(k, 0o644)
        code, text = self.run_inv()
        self.assertEqual(code, 1)
        self.assertIn("A0G6_KEYFILE_key-v2=file uid=%d mode=0644 nlink=1 UNSAFE" % UID, text)
        self.assertIn("A0G6_UNSAFE_DIRS=key-v2\n", text)
        self.assertNotIn(SECRET, text)
        os.chmod(k, 0o600)
        os.link(k, k + ".hl")
        self.assertIn("nlink=2 UNSAFE", self.run_inv()[1])
        os.unlink(k + ".hl")
        os.unlink(k)
        os.symlink(self.t.name, k)
        code, text = self.run_inv()
        self.assertIn("A0G6_KEYFILE_key-v2=link", text)
        self.assertIn("UNSAFE", text)
        os.unlink(k)
        code, text = self.run_inv()
        self.assertIn("A0G6_KEYFILE_key-v2=ABSENT", text)
        self.assertTrue(text.endswith("A0G6_UNSAFE_DIRS=key-v2\nA0G6_STOP:STATE_DIR_UNSAFE\n"))
        os.mkdir(k)  # a directory where the key file must be is also refused
        self.assertIn("A0G6_KEYFILE_key-v2=dir", self.run_inv()[1])


if __name__ == "__main__":
    unittest.main()
