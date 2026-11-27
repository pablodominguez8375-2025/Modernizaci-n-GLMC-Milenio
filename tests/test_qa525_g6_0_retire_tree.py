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
        os.chmod(self.t.name, 0o755)
        self.root = self.t.name + "/g6"
        self.ret = self.root + ".retired-20261113"
        os.mkdir(self.root, 0o700)
        os.mkdir(self.root + "/repo-v2", 0o700)
        os.mkdir(self.root + "/state-v2", 0o700)
        with open(self.root + "/key-v2", "w") as fh:
            fh.write("SECRET-KEY-MATERIAL")
        os.chmod(self.root + "/key-v2", 0o600)

    def go(self, mode):
        out = io.StringIO()
        code = g.run(mode, self.root, self.ret, UID, out)
        return code, out.getvalue()

    def test_preflight_is_read_only(self):
        before = snapshot(self.t.name)
        code, text = self.go("preflight")
        self.assertEqual(code, 0, text)
        self.assertIn("G6_0_PREFLIGHT=PASS", text)
        self.assertEqual(snapshot(self.t.name), before)
        self.assertNotIn("SECRET", text)

    def test_apply_moves_without_deleting_and_new_root_is_empty(self):
        before = snapshot(self.root)
        ino = os.lstat(self.root).st_ino
        code, text = self.go("apply")
        self.assertEqual(code, 0, text)
        self.assertEqual(snapshot(self.ret), before)  # byte-for-byte the same tree, same inode
        self.assertEqual(os.lstat(self.ret).st_ino, ino)
        self.assertEqual(os.listdir(self.root), [])
        self.assertEqual(oct(os.lstat(self.root).st_mode & 0o777), "0o700")
        self.assertIn("G6_0_APPLIED=1", text)

    def test_apply_twice_is_stop_and_changes_nothing(self):
        self.assertEqual(self.go("apply")[0], 0)
        snap = snapshot(self.t.name)
        code, text = self.go("apply")
        self.assertEqual((code, text), (1, "G6_0_STOP:ROOT_LAYOUT_UNEXPECTED\n"))
        self.assertEqual(snapshot(self.t.name), snap)

    def test_retired_target_exists_is_stop_before_any_change(self):
        os.mkdir(self.ret)
        snap = snapshot(self.t.name)
        self.assertEqual(self.go("apply"), (1, "G6_0_STOP:RETIRED_TARGET_EXISTS\n"))
        self.assertEqual(snapshot(self.t.name), snap)

    def test_unsafe_root_symlink_missing_layout_wrong_mode(self):
        os.chmod(self.root, 0o755)
        self.assertEqual(self.go("preflight"), (1, "G6_0_STOP:ROOT_NOT_SAFE_DIR\n"))
        os.chmod(self.root, 0o700)
        os.rmdir(self.root + "/repo-v2")
        self.assertEqual(self.go("preflight"), (1, "G6_0_STOP:ROOT_LAYOUT_UNEXPECTED\n"))
        os.mkdir(self.root + "/repo-v2", 0o700)
        os.rename(self.root, self.root + ".real")
        os.symlink(self.root + ".real", self.root)
        self.assertEqual(self.go("preflight"), (1, "G6_0_STOP:ROOT_NOT_SAFE_DIR\n"))

    def test_rollback_restores_exact_tree_only_when_new_root_is_empty(self):
        before = snapshot(self.root)
        self.assertEqual(self.go("apply")[0], 0)
        code, text = self.go("rollback")
        self.assertEqual(code, 0, text)
        self.assertEqual(snapshot(self.root), before)
        self.assertFalse(os.path.lexists(self.ret))

    def test_rollback_refuses_if_new_root_holds_state(self):
        self.assertEqual(self.go("apply")[0], 0)
        with open(self.root + "/key-v2", "w") as fh:
            fh.write("NEW")
        snap = snapshot(self.t.name)
        self.assertEqual(self.go("rollback"), (1, "G6_0_STOP:NEW_ROOT_NOT_EMPTY_OR_UNSAFE\n"))
        self.assertEqual(snapshot(self.t.name), snap)

    def test_failure_after_rename_rolls_back(self):
        real = os.mkdir
        calls = []

        def boom(path, mode=0o777):
            if path == self.root and not calls:
                calls.append(1)
                raise OSError("denied")
            return real(path, mode)
        before = snapshot(self.root)
        os.mkdir = boom
        try:
            code, text = self.go("apply")
        finally:
            os.mkdir = real
        self.assertEqual((code, text), (1, "G6_0_STOP:APPLY_FAILED_ROLLED_BACK\n"))
        self.assertEqual(snapshot(self.root), before)
        self.assertFalse(os.path.lexists(self.ret))

    def test_mode_invalid_and_root_required_for_real_paths(self):
        self.assertEqual(self.go("destroy"), (1, "G6_0_STOP:MODE_INVALID\n"))
        out = io.StringIO()
        if UID != 0:
            self.assertEqual(g.run("apply", g.ROOT, g.RETIRED, UID, out), 1)
            self.assertEqual(out.getvalue(), "G6_0_STOP:ROOT_REQUIRED\n")


if __name__ == "__main__":
    unittest.main()
