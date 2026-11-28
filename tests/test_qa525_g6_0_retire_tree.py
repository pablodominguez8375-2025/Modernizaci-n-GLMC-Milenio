import importlib.util
import io
import os
import pathlib
import tempfile
import unittest

SRC = pathlib.Path(__file__).resolve().parent.parent / "scripts/qa/operators/qa525-g6-0-retire-tree.py"
spec = importlib.util.spec_from_file_location("g60", SRC)
g = importlib.util.module_from_spec(spec)
spec.loader.exec_module(g)
UID = os.getuid()


def snapshot(root):
    snap = {}
    for dp, dn, fn in os.walk(root):
        for n in dn + fn:
            p = os.path.join(dp, n)
            st = os.lstat(p)
            snap[os.path.relpath(p, root)] = (st.st_mode, st.st_size, st.st_ino)
    return snap


class Retire(unittest.TestCase):
    def setUp(self):
        self.t = tempfile.TemporaryDirectory()
        self.addCleanup(self.t.cleanup)
        self.base = os.path.realpath(self.t.name)
        os.chmod(self.base, 0o755)
        self.root = self.base + "/g6"
        self.ret = self.root + ".retired-20261113"
        os.mkdir(self.root, 0o700)
        os.mkdir(self.root + "/repo-v2", 0o700)
        for n in ("state-v2", "exports-v2", "restores-v2", "downloads-v2", "stages-v2"):
            os.mkdir(self.root + "/" + n, 0o700)
        with open(self.root + "/key-v2", "w") as fh:
            fh.write("SECRET-KEY-MATERIAL")
        os.chmod(self.root + "/key-v2", 0o600)
        st = os.lstat(self.root)
        self.bind = (st.st_dev, st.st_ino)
        self.addCleanup(setattr, g, "_hook", g._hook)

    def go(self, mode, bind="default"):
        out = io.StringIO()
        b = self.bind if bind == "default" else bind
        code = g.run(mode, self.root, self.ret, UID, out, None if mode == "preflight" and bind == "default" else b)
        return code, out.getvalue()

    def hook(self, stage, fn):
        g._hook = lambda s: fn() if s == stage else None

    def test_preflight_read_only_prints_bind(self):
        before = snapshot(self.base)
        code, text = self.go("preflight")
        self.assertEqual(code, 0, text)
        self.assertIn("G6_0_ROOT_BIND=%d:%d" % self.bind, text)
        self.assertEqual(snapshot(self.base), before)
        self.assertNotIn("SECRET", text)

    def test_apply_moves_without_deleting(self):
        before = snapshot(self.root)
        code, text = self.go("apply")
        self.assertEqual(code, 0, text)
        self.assertEqual(snapshot(self.ret), before)
        self.assertEqual((os.lstat(self.ret).st_dev, os.lstat(self.ret).st_ino), self.bind)
        self.assertEqual(os.listdir(self.root), [])
        self.assertEqual(oct(os.lstat(self.root).st_mode & 0o777), "0o700")

    def test_bind_required_and_wrong_tree_refused(self):
        self.assertEqual(self.go("apply", None), (1, "G6_0_STOP:BIND_REQUIRED\n"))
        snap = snapshot(self.base)
        self.assertEqual(self.go("apply", (self.bind[0], self.bind[1] + 1)), (1, "G6_0_STOP:TREE_NOT_THE_INVENTORIED_ONE\n"))
        self.assertEqual(snapshot(self.base), snap)

    def test_key_and_subdir_metadata_unexpected(self):
        os.chmod(self.root + "/key-v2", 0o644)
        self.assertEqual(self.go("preflight"), (1, "G6_0_STOP:KEY_METADATA_UNEXPECTED\n"))
        os.chmod(self.root + "/key-v2", 0o600)
        os.chmod(self.root + "/repo-v2", 0o755)
        self.assertEqual(self.go("preflight"), (1, "G6_0_STOP:SUBDIR_METADATA_UNEXPECTED\n"))

    def test_key_mode_must_be_exactly_0600_and_no_extra_entries(self):
        os.chmod(self.root + "/key-v2", 0o700)
        self.assertEqual(self.go("preflight"), (1, "G6_0_STOP:KEY_METADATA_UNEXPECTED\n"))
        os.chmod(self.root + "/key-v2", 0o400)
        self.assertEqual(self.go("preflight"), (1, "G6_0_STOP:KEY_METADATA_UNEXPECTED\n"))
        os.chmod(self.root + "/key-v2", 0o600)
        os.mkdir(self.root + "/extra", 0o700)
        snap = snapshot(self.base)
        self.assertEqual(self.go("apply"), (1, "G6_0_STOP:ROOT_LAYOUT_UNEXPECTED\n"))
        self.assertEqual(snapshot(self.base), snap)
        os.rmdir(self.root + "/extra")
        code, text = self.go("preflight")
        self.assertEqual(code, 0, text)
        self.assertIn("entries=7", text)
        self.assertIn("G6_0_KEY_V2=file nlink=1 mode=0600", text)

    def test_rollback_requires_exact_set_in_retired(self):
        self.assertEqual(self.go("apply")[0], 0)
        os.mkdir(self.ret + "/extra", 0o700)
        self.assertEqual(self.go("rollback"), (1, "G6_0_STOP:RETIRED_NOT_RESTORABLE\n"))

    def test_symlink_root_and_unsafe_modes_and_layout(self):
        os.chmod(self.root, 0o755)
        self.assertEqual(self.go("preflight"), (1, "G6_0_STOP:ROOT_NOT_SAFE_DIR\n"))
        os.chmod(self.root, 0o700)
        os.rmdir(self.root + "/repo-v2")
        self.assertEqual(self.go("preflight"), (1, "G6_0_STOP:ROOT_LAYOUT_UNEXPECTED\n"))
        os.mkdir(self.root + "/repo-v2", 0o700)
        os.rename(self.root, self.root + ".real")
        os.symlink(self.root + ".real", self.root)
        self.assertEqual(self.go("preflight"), (1, "G6_0_STOP:ROOT_NOT_SAFE_DIR\n"))

    def test_retired_target_exists_before_and_during_race(self):
        os.mkdir(self.ret)
        self.assertEqual(self.go("apply"), (1, "G6_0_STOP:RETIRED_TARGET_EXISTS\n"))
        os.rmdir(self.ret)
        self.hook("after_preflight", lambda: os.mkdir(self.ret))  # empty dir appears: plain rename would replace it
        before = snapshot(self.root)
        self.assertEqual(self.go("apply"), (1, "G6_0_STOP:RENAME_REFUSED_ERRNO_EEXIST\n"))
        self.assertEqual(snapshot(self.root), before)
        self.assertEqual(os.listdir(self.ret), [])

    def test_swap_root_between_preflight_and_rename(self):
        def swap():
            os.rename(self.root, self.root + ".orig")
            os.mkdir(self.root, 0o700)
            os.mkdir(self.root + "/other")
        self.hook("after_preflight", swap)
        code, text = self.go("apply")
        self.assertEqual((code, text), (1, "G6_0_STOP:MOVED_TREE_NOT_BOUND_ROLLED_BACK\n"))
        self.assertEqual(os.listdir(self.root), ["other"])  # swapped-in tree restored untouched
        self.assertFalse(os.path.lexists(self.ret))
        self.assertTrue(os.path.isdir(self.root + ".orig/repo-v2"))

    def test_root_name_taken_between_rename_and_mkdir(self):
        self.hook("after_rename", lambda: os.mkdir(self.root, 0o700))
        code, text = self.go("apply")
        self.assertEqual((code, text), (1, "G6_0_STOP:APPLY_FAILED_ROLLED_BACK\n"))
        self.assertEqual((os.lstat(self.root).st_dev, os.lstat(self.root).st_ino), self.bind)
        self.assertFalse(os.path.lexists(self.ret))

    def test_root_name_taken_with_content_is_not_removed(self):
        def take():
            os.mkdir(self.root, 0o700)
            open(self.root + "/x", "w").close()
        self.hook("after_rename", take)
        code, text = self.go("apply")
        self.assertEqual((code, text), (1, "G6_0_STOP:APPLY_FAILED_ROLLBACK_FAILED\n"))
        self.assertEqual(os.listdir(self.root), ["x"])
        self.assertTrue(os.path.isdir(self.ret + "/repo-v2"))

    def test_failure_after_rename_rolls_back_by_fd(self):
        real = os.mkdir

        def boom(path, mode=0o777, dir_fd=None):
            raise OSError("denied")
        before = snapshot(self.root)
        os.mkdir = boom
        try:
            code, text = self.go("apply")
        finally:
            os.mkdir = real
        self.assertEqual((code, text), (1, "G6_0_STOP:APPLY_FAILED_ROLLED_BACK\n"))
        self.assertEqual(snapshot(self.root), before)
        self.assertFalse(os.path.lexists(self.ret))

    def test_rollback_restores_exact_tree(self):
        before = snapshot(self.root)
        self.assertEqual(self.go("apply")[0], 0)
        code, text = self.go("rollback")
        self.assertEqual(code, 0, text)
        self.assertEqual(snapshot(self.root), before)
        self.assertFalse(os.path.lexists(self.ret))
        self.assertEqual((os.lstat(self.root).st_dev, os.lstat(self.root).st_ino), self.bind)

    def test_rollback_refuses_new_state_and_wrong_bind(self):
        self.assertEqual(self.go("apply")[0], 0)
        self.assertEqual(self.go("rollback", (self.bind[0], self.bind[1] + 1)), (1, "G6_0_STOP:RETIRED_NOT_THE_BOUND_TREE\n"))
        with open(self.root + "/key-v2", "w") as fh:
            fh.write("NEW")
        snap = snapshot(self.base)
        self.assertEqual(self.go("rollback"), (1, "G6_0_STOP:NEW_ROOT_NOT_EMPTY_OR_UNSAFE\n"))
        self.assertEqual(snapshot(self.base), snap)

    def test_rollback_race_state_appears_before_rmdir(self):
        self.assertEqual(self.go("apply")[0], 0)

        def fill():
            with open(self.root + "/key-v2", "w") as fh:
                fh.write("NEW")
        self.hook("after_rollback_check", fill)
        snap = snapshot(self.base)
        code, text = self.go("rollback")
        self.assertEqual((code, text), (1, "G6_0_STOP:NEW_ROOT_NOT_EMPTY_OR_UNSAFE\n"))
        self.assertEqual(os.listdir(self.root), ["key-v2"])
        self.assertTrue(os.path.isdir(self.ret + "/repo-v2"))

    def test_rollback_race_target_created_after_rmdir(self):
        self.assertEqual(self.go("apply")[0], 0)
        self.hook("after_rmdir", lambda: os.mkdir(self.root, 0o700))
        self.assertEqual(self.go("rollback"), (1, "G6_0_STOP:RENAME_REFUSED_ERRNO_EEXIST\n"))
        self.assertTrue(os.path.isdir(self.ret))

    def test_renameat2_signature_and_error_conversion(self):
        fn = g._load_renameat2()
        self.assertEqual(fn.restype, g.ctypes.c_int)
        self.assertEqual(list(fn.argtypes), [g.ctypes.c_int, g.ctypes.c_char_p, g.ctypes.c_int, g.ctypes.c_char_p, g.ctypes.c_uint])
        pfd = os.open(self.base, g.DIR_FLAGS)
        self.addCleanup(os.close, pfd)
        with self.assertRaises(g.Stop) as c:  # real syscall: source absent -> ENOENT
            g._noreplace_rename(pfd, "nope", "dest")
        self.assertEqual(str(c.exception), "RENAME_REFUSED_ERRNO_ENOENT")

        def fake(*a):
            return -1
        with self.assertRaises(g.Stop) as c:
            g._noreplace_rename(pfd, "a", "b", loader=lambda: fake)
        self.assertTrue(str(c.exception).startswith("RENAME_REFUSED_ERRNO_"))

        def missing():
            raise g.Stop("RENAMEAT2_UNAVAILABLE")
        with self.assertRaises(g.Stop) as c:
            g._noreplace_rename(pfd, "a", "b", loader=missing)
        self.assertEqual(str(c.exception), "RENAMEAT2_UNAVAILABLE")

    def test_parent_symlink_and_cli_grammar(self):
        link = self.base + "-link"
        os.symlink(self.base, link)
        self.addCleanup(os.unlink, link)
        out = io.StringIO()
        self.assertEqual(g.run("preflight", link + "/g6", link + "/g6.retired-20261113", UID, out), 1)
        self.assertEqual(out.getvalue(), "G6_0_STOP:PARENT_NOT_CANONICAL\n")
        for argv in (["x"], ["x", "apply"], ["x", "apply", "1:2:3"], ["x", "preflight", "1:2"], ["x", "destroy"], ["x"]):
            buf = io.StringIO()
            old, g.sys.stdout = g.sys.stdout, buf
            try:
                self.assertEqual(g.main(argv), 1)
            finally:
                g.sys.stdout = old
            self.assertEqual(buf.getvalue(), "G6_0_STOP:MODE_INVALID\n")


if __name__ == "__main__":
    unittest.main()
