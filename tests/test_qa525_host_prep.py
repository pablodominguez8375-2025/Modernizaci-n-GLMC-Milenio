import importlib.util
import io
import os
import pathlib
import re
import tempfile
import unittest

OPS = pathlib.Path(__file__).resolve().parent.parent / "scripts/qa/operators"


def load(path, name):
    spec = importlib.util.spec_from_file_location(name, path)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


hp = load(OPS / "qa525-host-prep.py", "hp")
build = load(OPS / "qa525-host-prep-build.py", "hpb")
driver = load(OPS / "qa525-manual-cut-e5c40d3a.py", "drv")
UID = os.getuid()


def snap(root):
    out = {}
    for dp, dn, fn in os.walk(root):
        for n in dn + fn:
            p = os.path.join(dp, n)
            st = os.lstat(p)
            out[os.path.relpath(p, root)] = (st.st_mode, st.st_size, st.st_ino)
    return out


class Manifest(unittest.TestCase):
    def test_bundle_equals_driver_pins_and_repo_bytes(self):
        want = {**driver.LIB_PINS, **driver.G6_PINS}
        self.assertEqual({n: v[0] for n, v in hp.BUNDLE.items()}, want)
        self.assertEqual(len(hp.BUNDLE), 11)
        self.assertEqual(set(hp.BUNDLE), set(build.NAMES))

    def test_committed_script_is_reproducible_from_template(self):
        tpl = (OPS / "qa525-host-prep.template.py").read_text()
        self.assertEqual(build.build(tpl, OPS.parent), (OPS / "qa525-host-prep.py").read_text())

    def test_bundle_decodes_and_verifies(self):
        files = hp.bundle_files()
        self.assertEqual(len(files), 11)

    def test_tampered_bundle_is_stop(self):
        name = sorted(hp.BUNDLE)[0]
        old = hp.BUNDLE[name]
        try:
            hp.BUNDLE[name] = (old[0], old[1], ("AAAA",) + tuple(old[2][1:]))
            with self.assertRaises(hp.Stop) as c:
                hp.bundle_files()
            self.assertIn(str(c.exception), ("BUNDLE_HASH_MISMATCH", "BUNDLE_DECODE_FAILED"))
        finally:
            hp.BUNDLE[name] = old


class Gate(unittest.TestCase):
    def setUp(self):
        self.t = tempfile.TemporaryDirectory()
        self.addCleanup(self.t.cleanup)
        b = os.path.realpath(self.t.name)
        self.opt, self.var = b + "/opt", b + "/var"
        for d in (self.opt, self.var):
            os.mkdir(d, 0o755)
        self.base = b
        self.addCleanup(setattr, hp, "_hook", hp._hook)

    def bind(self):
        o, v = os.lstat(self.opt), os.lstat(self.var)
        return ((o.st_dev, o.st_ino), (v.st_dev, v.st_ino))

    def go(self, mode, bind="default"):
        out = io.StringIO()
        b = self.bind() if bind == "default" and mode != "preflight" else (None if bind == "default" else bind)
        return hp.run(mode, self.opt, self.var, UID, out, b), out.getvalue()

    def hook(self, stage, fn):
        hp._hook = lambda s: fn() if s == stage else None

    def lib(self):
        return self.opt + "/" + hp.BASE + "/" + hp.LIB

    def jr(self):
        return self.var + "/" + hp.JBASE + "/" + hp.JOURNAL

    def test_preflight_read_only(self):
        before = snap(self.base)
        code, text = self.go("preflight")
        self.assertEqual(code, 0, text)
        self.assertIn("G6HP_PREFLIGHT=PASS", text)
        self.assertRegex(text, r"G6HP_BIND=\d+:\d+,\d+:\d+")
        self.assertEqual(snap(self.base), before)

    def test_apply_creates_exact_set_with_modes(self):
        code, text = self.go("apply")
        self.assertEqual(code, 0, text)
        self.assertIn("G6HP_APPLIED=1", text)
        self.assertEqual(sorted(os.listdir(self.lib())), sorted(hp.BUNDLE))
        for n in hp.BUNDLE:
            st = os.lstat(self.lib() + "/" + n)
            self.assertEqual(oct(st.st_mode & 0o777), "0o644")
            self.assertEqual(st.st_nlink, 1)
        self.assertEqual(oct(os.lstat(self.lib()).st_mode & 0o777), "0o755")
        self.assertEqual(oct(os.lstat(self.jr()).st_mode & 0o777), "0o700")
        self.assertEqual(os.listdir(self.jr()), [])
        for n, data in hp.bundle_files().items():
            self.assertEqual(open(self.lib() + "/" + n, "rb").read(), data)

    def test_apply_requires_bind_and_matching_identity(self):
        self.assertEqual(self.go("apply", None), (1, "G6HP_STOP:BIND_REQUIRED\n"))
        o, v = self.bind()
        before = snap(self.base)
        self.assertEqual(self.go("apply", ((o[0], o[1] + 1), v)), (1, "G6HP_STOP:PARENT_NOT_THE_BOUND_ONE\n"))
        self.assertEqual(snap(self.base), before)

    def test_second_apply_and_collisions_are_stop_without_changes(self):
        self.assertEqual(self.go("apply")[0], 0)
        before = snap(self.base)
        self.assertEqual(self.go("apply"), (1, "G6HP_STOP:TARGET_EXISTS\n"))
        self.assertEqual(snap(self.base), before)

    def test_each_target_collision_blocks_preflight(self):
        for path in (self.opt + "/" + hp.BASE, self.var + "/" + hp.JBASE,
                     self.opt + "/" + hp.BASE + hp.SUFFIX_FAIL, self.var + "/" + hp.JBASE + hp.SUFFIX_RB):
            os.mkdir(path)
            self.assertEqual(self.go("preflight"), (1, "G6HP_STOP:TARGET_EXISTS\n"), path)
            os.rmdir(path)

    def test_symlink_target_and_parent_symlink(self):
        os.symlink("/tmp", self.opt + "/" + hp.BASE)
        self.assertEqual(self.go("preflight"), (1, "G6HP_STOP:TARGET_EXISTS\n"))
        os.unlink(self.opt + "/" + hp.BASE)
        link = self.base + "/link"
        os.symlink(self.opt, link)
        out = io.StringIO()
        self.assertEqual(hp.run("preflight", link, self.var, UID, out), 1)
        self.assertEqual(out.getvalue(), "G6HP_STOP:PARENT_NOT_CANONICAL\n")

    def test_unsafe_parent_modes(self):
        os.chmod(self.opt, 0o777)
        self.assertEqual(self.go("preflight"), (1, "G6HP_STOP:PARENT_NOT_SAFE\n"))

    def test_parent_swap_during_apply_does_not_follow(self):
        # parent path replaced by a symlink after the FDs were opened: operations stay on the original inode
        moved = self.base + "/opt.moved"

        def swap():
            os.rename(self.opt, moved)
            os.symlink(self.var, self.opt)
        self.hook("before", swap)
        code, text = self.go("apply")
        self.assertEqual(code, 0, text)
        self.assertTrue(os.path.isdir(moved + "/" + hp.BASE + "/" + hp.LIB))
        self.assertFalse(os.path.exists(self.var + "/" + hp.BASE))

    def test_failure_mid_install_renames_created_tops_nothing_deleted(self):
        def boom():
            raise OSError("disk")
        self.hook("file:5", boom)
        code, text = self.go("apply")
        self.assertEqual((code, text), (1, "G6HP_STOP:APPLY_FAILED_ROLLED_BACK\n"))
        self.assertFalse(os.path.lexists(self.opt + "/" + hp.BASE))
        failed = self.opt + "/" + hp.BASE + hp.SUFFIX_FAIL + "/" + hp.LIB
        self.assertEqual(len(os.listdir(failed)), 5)  # evidence kept, not deleted

    def test_failure_after_journal_created_renames_both(self):
        def boom():
            raise OSError("x")
        self.hook("verify", boom)
        self.assertEqual(self.go("apply"), (1, "G6HP_STOP:APPLY_FAILED_ROLLED_BACK\n"))
        self.assertTrue(os.path.isdir(self.var + "/" + hp.JBASE + hp.SUFFIX_FAIL))
        self.assertTrue(os.path.isdir(self.opt + "/" + hp.BASE + hp.SUFFIX_FAIL))

    def test_failure_with_failed_name_taken_is_rollback_failed(self):
        os.mkdir(self.opt + "/" + hp.BASE + hp.SUFFIX_FAIL) if False else None
        def boom():
            os.mkdir(self.opt + "/" + hp.BASE + hp.SUFFIX_FAIL)
            raise OSError("x")
        self.hook("file:3", boom)
        code, text = self.go("apply")
        self.assertEqual((code, text), (1, "G6HP_STOP:APPLY_FAILED_ROLLBACK_FAILED\n"))

    def test_rollback_renames_exact_installed_set(self):
        self.assertEqual(self.go("apply")[0], 0)
        code, text = self.go("rollback")
        self.assertEqual((code, text), (0, "G6HP_ROLLED_BACK=1\n"))
        self.assertFalse(os.path.lexists(self.opt + "/" + hp.BASE))
        self.assertEqual(len(os.listdir(self.opt + "/" + hp.BASE + hp.SUFFIX_RB + "/" + hp.LIB)), 11)
        self.assertTrue(os.path.isdir(self.var + "/" + hp.JBASE + hp.SUFFIX_RB + "/" + hp.JOURNAL))

    def test_rollback_refuses_used_journal_extra_file_modified_lib(self):
        self.assertEqual(self.go("apply")[0], 0)
        open(self.jr() + "/entry", "w").close()
        before = snap(self.base)
        self.assertEqual(self.go("rollback"), (1, "G6HP_STOP:JOURNAL_NOT_EMPTY\n"))
        self.assertEqual(snap(self.base), before)
        os.unlink(self.jr() + "/entry")
        open(self.lib() + "/extra.py", "w").close()
        self.assertEqual(self.go("rollback"), (1, "G6HP_STOP:INSTALLED_STATE_UNEXPECTED\n"))
        os.unlink(self.lib() + "/extra.py")
        name = sorted(hp.BUNDLE)[0]
        with open(self.lib() + "/" + name, "ab") as fh:
            fh.write(b"#")
        self.assertEqual(self.go("rollback"), (1, "G6HP_STOP:INSTALLED_STATE_UNEXPECTED\n"))

    def test_rollback_without_install_and_name_taken(self):
        self.assertEqual(self.go("rollback")[0], 1)
        self.assertEqual(self.go("apply")[0], 0)
        os.mkdir(self.opt + "/" + hp.BASE + hp.SUFFIX_RB)
        self.assertEqual(self.go("rollback"), (1, "G6HP_STOP:TARGET_EXISTS\n"))

    def test_renameat2_signature_and_error_conversion(self):
        fn = hp._load_renameat2()
        self.assertEqual(fn.restype, hp.ctypes.c_int)
        self.assertEqual(list(fn.argtypes), [hp.ctypes.c_int, hp.ctypes.c_char_p, hp.ctypes.c_int, hp.ctypes.c_char_p, hp.ctypes.c_uint])
        pfd = os.open(self.base, hp.DIR_FLAGS)
        self.addCleanup(os.close, pfd)
        with self.assertRaises(hp.Stop) as c:
            hp._noreplace_rename(pfd, "nope", "dest")
        self.assertEqual(str(c.exception), "RENAME_REFUSED_ERRNO_ENOENT")
        with self.assertRaises(hp.Stop) as c:
            hp._noreplace_rename(pfd, "a", "b", loader=lambda: (lambda *a: -1))
        self.assertTrue(str(c.exception).startswith("RENAME_REFUSED_ERRNO_"))

    def test_mode_and_cli_grammar_and_root_required(self):
        self.assertEqual(self.go("destroy"), (1, "G6HP_STOP:MODE_INVALID\n"))
        if UID != 0:
            out = io.StringIO()
            self.assertEqual(hp.run("apply", hp.OPT_PARENT, hp.VAR_PARENT, UID, out), 1)
            self.assertEqual(out.getvalue(), "G6HP_STOP:ROOT_REQUIRED\n")
        for argv in (["x"], ["x", "apply"], ["x", "apply", "1:2"], ["x", "preflight", "1:2,3:4"], ["x", "destroy"],
                     ["x", "apply", "1:2,3:4;ls"]):
            buf = io.StringIO()
            old, hp.sys.stdout = hp.sys.stdout, buf
            try:
                self.assertEqual(hp.main(argv), 1)
            finally:
                hp.sys.stdout = old
            self.assertEqual(buf.getvalue(), "G6HP_STOP:MODE_INVALID\n")

    def test_source_imports_and_calls_are_closed(self):
        import ast
        tree = ast.parse((OPS / "qa525-host-prep.template.py").read_text())
        imports = {a.name for n in ast.walk(tree) if isinstance(n, ast.Import) for a in n.names}
        self.assertEqual(imports, {"base64", "ctypes", "errno", "hashlib", "io", "os", "re", "stat", "sys"})
        self.assertFalse([n for n in ast.walk(tree) if isinstance(n, ast.ImportFrom)])
        banned = {"system", "popen", "remove", "unlink", "rmdir", "rmtree", "exec", "eval", "spawn", "fork", "execv", "kill"}
        used = {n.attr for n in ast.walk(tree) if isinstance(n, ast.Attribute)} | {n.id for n in ast.walk(tree) if isinstance(n, ast.Name)}
        self.assertFalse(banned & used, banned & used)


if __name__ == "__main__":
    unittest.main()
